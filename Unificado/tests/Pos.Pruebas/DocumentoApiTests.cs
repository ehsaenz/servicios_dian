using System.Net;
using System.Text;
using DIAN.Pos.Servicios;
using DIAN.Impresion;
using DIAN.Pos.Servicios.Documentos;
using eFacturacionColombia_NETCore.ServiciosWeb;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Pruebas;

namespace Pos.Pruebas;

// Documentos del ERP (factura, nota crédito, nota débito) por api/documento. La DIAN es el enviador falso.
public class DocumentoApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private readonly FabricaFalsa _fabrica = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-doc-datos-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public DocumentoApiTests(CarpetaClientes carpeta)
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = carpeta.Ruta,
                ["Datos:Carpeta"] = _datos,
                ["Documentos:EnvioEnSegundoPlano"] = "false"
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(_fabrica)));
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private static string Json(string numero, string tipoDian = "01", string tipoErp = "FV")
    {
        var doc = ModeloDianTests.Factura();
        doc.Numero = numero;
        doc.TipoDocDian = tipoDian;
        doc.TipoDocErp = tipoErp;
        if (tipoDian != "01")
        {
            doc.DocumentoRef = new FE.Models.Doc.documento_ref { Numero = "SETP990000001", CUFE = new string('a', 96), Fecha = doc.FechaHoraDoc.AddDays(-1) };
        }

        return JsonConvert.SerializeObject(doc);
    }

    private Task<HttpResponseMessage> Llamar(HttpMethod metodo, string ruta, string clave, string? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, ruta);
        if (cuerpo != null) peticion.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        peticion.Headers.Add("x-api-key", clave);
        return _app.CreateClient().SendAsync(peticion);
    }

    private static async Task<JObject> Cuerpo(HttpResponseMessage r) => JObject.Parse(await r.Content.ReadAsStringAsync());

    [Fact]
    public async Task Una_factura_se_firma_se_envia_y_queda_aprobada_con_su_CUFE()
    {
        var r = await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000001"));

        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var c = await Cuerpo(r);
        Assert.Equal("Aprobado", c["Estado"]!.ToString());
        Assert.Equal(96, c["CUFE"]!.ToString().Length);
        Assert.True((bool)c["Habilitacion"]!);

        var xml = Encoding.UTF8.GetString(_fabrica.Enviados.Single());
        Assert.Contains("Signature", xml);
        Assert.Contains("<Invoice", xml);
        Assert.Equal("CN=Cliente A", _fabrica.Llamadas.Single().Sujeto);
        Assert.Equal(AmbienteServicio.PRUEBAS, _fabrica.Llamadas.Single().Ambiente);
    }

    [Fact]
    public async Task Reenviar_el_mismo_numero_no_vuelve_a_enviar_a_la_DIAN()
    {
        await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000002"));
        var segundo = await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000002"));

        Assert.Equal(HttpStatusCode.Accepted, segundo.StatusCode);
        Assert.Equal("Aprobado", (await Cuerpo(segundo))["Estado"]!.ToString());
        Assert.Single(_fabrica.Enviados);
    }

    [Fact]
    public async Task Notas_credito_y_debito_se_aceptan_con_su_documento_de_referencia()
    {
        var nc = await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000003", "91", "NC"));
        var nd = await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000004", "92", "ND"));

        Assert.Equal("Aprobado", (await Cuerpo(nc))["Estado"]!.ToString());
        Assert.Equal("Aprobado", (await Cuerpo(nd))["Estado"]!.ToString());
        Assert.Contains("<CreditNote", Encoding.UTF8.GetString(_fabrica.Enviados[0]));
        Assert.Contains("<DebitNote", Encoding.UTF8.GetString(_fabrica.Enviados[1]));
    }

    [Fact]
    public async Task Un_documento_no_traducible_se_rechaza_al_instante_y_no_se_guarda()
    {
        var sinLineas = JObject.Parse(Json("SETP990000005"));
        sinLineas["Items"] = new JArray();
        var r = await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, sinLineas.ToString());

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Llamar(HttpMethod.Get, "/api/documento/estado/SETP990000005", CarpetaClientes.ClaveA)).StatusCode);
        Assert.Empty(_fabrica.Enviados);
    }

    [Fact]
    public async Task Cada_cliente_solo_ve_sus_documentos()
    {
        await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000006"));

        Assert.Equal(HttpStatusCode.OK, (await Llamar(HttpMethod.Get, "/api/documento/estado/SETP990000006", CarpetaClientes.ClaveA)).StatusCode);
        // El cliente P también tiene software de FE, pero ese número no es suyo.
        Assert.Equal(HttpStatusCode.NotFound, (await Llamar(HttpMethod.Get, "/api/documento/estado/SETP990000006", CarpetaClientes.ClaveProduccion)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/api/documento/estado/SETP990000006")).StatusCode);
    }

    [Fact]
    public async Task El_ERP_pide_los_cambios_desde_su_cursor_y_baja_el_XML_y_la_respuesta()
    {
        await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000007"));
        await Llamar(HttpMethod.Post, "/api/documento/agregar", CarpetaClientes.ClaveA, Json("SETP990000008"));

        var todos = await Cuerpo(await Llamar(HttpMethod.Get, "/api/documento/cambios?desde=0", CarpetaClientes.ClaveA));
        Assert.Equal(2, ((JArray)todos["Documentos"]!).Count);
        var cursor = (long)todos["Cursor"]!;

        var ninguno = await Cuerpo(await Llamar(HttpMethod.Get, "/api/documento/cambios?desde=" + cursor, CarpetaClientes.ClaveA));
        Assert.Empty((JArray)ninguno["Documentos"]!);

        var xml = await Llamar(HttpMethod.Get, "/api/documento/xml/SETP990000007", CarpetaClientes.ClaveA);
        Assert.Equal(HttpStatusCode.OK, xml.StatusCode);
        Assert.Contains("Signature", await xml.Content.ReadAsStringAsync());
    }
}

// PDF oficial y correo: solo para documentos aprobados por la DIAN.
public class DocumentoPdfApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private sealed class PdfFalso : IGeneradorPdf
    {
        public List<DIAN.Impresion.DocumentoImpresion> Recibidos { get; } = new();
        public bool Falla { get; set; }

        public byte[] Generar(DIAN.Impresion.DocumentoImpresion documento, byte[]? logo)
        {
            if (Falla) throw new InvalidOperationException("motor de PDF caído");
            Recibidos.Add(documento);
            return Encoding.ASCII.GetBytes("%PDF-falso " + documento.Numero);
        }
    }

    private sealed class CorreoFalso : IEnviadorCorreo
    {
        public List<(string Para, string Asunto, IReadOnlyList<AdjuntoCorreo> Adjuntos)> Enviados { get; } = new();
        public void Enviar(string destinatario, string asunto, string cuerpo, IReadOnlyList<AdjuntoCorreo> adjuntos) => Enviados.Add((destinatario, asunto, adjuntos));
    }

    private readonly FabricaFalsa _fabrica = new() { ConApplicationResponse = true };
    private readonly PdfFalso _pdf = new();
    private readonly CorreoFalso _correo = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-doc-pdf-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public DocumentoPdfApiTests(CarpetaClientes carpeta)
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = carpeta.Ruta,
                ["Datos:Carpeta"] = _datos,
                ["Documentos:EnvioEnSegundoPlano"] = "false"
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(_fabrica)));
        });
        var salidas = _app.Services.GetRequiredService<ServiciosPos>().Salidas;
        salidas.Pdf = _pdf;
        salidas.Correo = _correo;
    }

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private async Task<JObject> Agregar(string numero)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/documento/agregar")
        {
            Content = new StringContent(JsonConvert.SerializeObject(Factura(numero)), Encoding.UTF8, "application/json")
        };
        peticion.Headers.Add("x-api-key", CarpetaClientes.ClaveA);
        return JObject.Parse(await (await _app.CreateClient().SendAsync(peticion)).Content.ReadAsStringAsync());
    }

    private static FE.Models.Doc.DocumentoDian Factura(string numero)
    {
        var doc = ModeloDianTests.Factura();
        doc.Numero = numero;
        return doc;
    }

    private Task<HttpResponseMessage> Pdf(string numero)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/documento/pdf/" + numero);
        peticion.Headers.Add("x-api-key", CarpetaClientes.ClaveA);
        return _app.CreateClient().SendAsync(peticion);
    }

    [Fact]
    public async Task Al_aprobarse_se_genera_el_PDF_oficial_y_se_envia_por_correo()
    {
        var registro = await Agregar("SETP990000021");

        Assert.Equal("Aprobado", registro["Estado"]!.ToString());
        Assert.True((bool)registro["Pdf"]!);
        Assert.True((bool)registro["CorreoEnviado"]!);

        // El generador solo recibe un modelo aprobado por la DIAN.
        var modelo = Assert.Single(_pdf.Recibidos);
        Assert.True(modelo.Aprobado);
        Assert.Equal("SETP990000021", modelo.Numero);

        var correo = Assert.Single(_correo.Enviados);
        Assert.Equal(registro["CorreoDestino"]!.ToString(), correo.Para);
        Assert.Contains(correo.Adjuntos, a => a.Nombre == "SETP990000021.pdf");
        Assert.Contains(correo.Adjuntos, a => a.Nombre == "SETP990000021.xml");

        var descarga = await Pdf("SETP990000021");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", await descarga.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_documento_rechazado_no_tiene_PDF_ni_correo()
    {
        _fabrica.CodigoRespuesta = "99";
        var registro = await Agregar("SETP990000022");

        Assert.Equal("Rechazado", registro["Estado"]!.ToString());
        Assert.False((bool)registro["Pdf"]!);
        Assert.Empty(_pdf.Recibidos);
        Assert.Empty(_correo.Enviados);
        Assert.Equal(HttpStatusCode.Conflict, (await Pdf("SETP990000022")).StatusCode);
    }

    [Fact]
    public async Task Si_el_generador_falla_el_documento_sigue_aprobado_y_queda_anotado()
    {
        _pdf.Falla = true;
        var registro = await Agregar("SETP990000023");

        Assert.Equal("Aprobado", registro["Estado"]!.ToString());
        Assert.False((bool)registro["Pdf"]!);
        Assert.Contains("motor de PDF caído", registro["PdfMensaje"]!.ToString());
        Assert.Empty(_correo.Enviados);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Pdf("SETP990000023")).StatusCode);

        // Al recuperarse el motor, el PDF se genera cuando el ERP lo pide.
        _pdf.Falla = false;
        Assert.Equal(HttpStatusCode.OK, (await Pdf("SETP990000023")).StatusCode);
        Assert.Single(_correo.Enviados);
    }
}
