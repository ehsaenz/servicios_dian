using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DIAN.Pos.Servicios;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Pruebas;

namespace Pos.Pruebas;

// Recuperar el XML y el acuse por CUFE cuando el almacén local no los tiene (p. ej. factura enviada desde otra instancia).
public class DocumentoRecuperacionCufeTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private readonly string _datosA = Path.Combine(Path.GetTempPath(), "dian-rec-a-" + Guid.NewGuid().ToString("N"));
    private readonly string _datosB = Path.Combine(Path.GetTempPath(), "dian-rec-b-" + Guid.NewGuid().ToString("N"));
    private readonly CarpetaClientes _carpeta;

    public DocumentoRecuperacionCufeTests(CarpetaClientes carpeta) => _carpeta = carpeta;

    public void Dispose()
    {
        try { Directory.Delete(_datosA, true); } catch (IOException) { }
        try { Directory.Delete(_datosB, true); } catch (IOException) { }
    }

    private WebApplicationFactory<Program> Crear(string datos, IFabricaEnviadorDian fabrica) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = _carpeta.Ruta,
                ["Datos:Carpeta"] = datos,
                ["Documentos:EnvioEnSegundoPlano"] = "false"
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(fabrica)));
        });

    private static string Json(string numero)
    {
        var doc = ModeloDianTests.Factura();
        doc.Numero = numero;
        doc.TipoDocDian = "01";
        doc.TipoDocErp = "FV";
        return JsonConvert.SerializeObject(doc);
    }

    private static Task<HttpResponseMessage> Llamar(WebApplicationFactory<Program> app, HttpMethod metodo, string ruta, string? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, ruta);
        if (cuerpo != null) peticion.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        peticion.Headers.Add("x-api-key", CarpetaClientes.ClaveA);
        return app.CreateClient().SendAsync(peticion);
    }

    [Fact]
    public async Task El_XML_de_un_documento_ajeno_se_trae_de_la_DIAN_por_CUFE_y_se_guarda_en_local()
    {
        var envio = new FabricaFalsa();
        using var appA = Crear(_datosA, envio);
        var alta = await Llamar(appA, HttpMethod.Post, "/api/documento/agregar", Json("SETP990000101"));
        var cufe = JObject.Parse(await alta.Content.ReadAsStringAsync())["CUFE"]!.ToString();
        var firmado = envio.Enviados.Single();

        var dian = new FabricaDianRecuperacion(firmado, cufe, "02");
        using var appB = Crear(_datosB, dian);
        var r = await Llamar(appB, HttpMethod.Get, $"/api/documento/xml/SETP990000101?cufe={cufe}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(firmado, await r.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, dian.ConsultasXml);

        // Segunda petición: ya está en local, no vuelve a consultar la DIAN.
        var otra = await Llamar(appB, HttpMethod.Get, $"/api/documento/xml/SETP990000101?cufe={cufe}");
        Assert.Equal(HttpStatusCode.OK, otra.StatusCode);
        Assert.Equal(1, dian.ConsultasXml);
    }

    [Fact]
    public async Task Sin_CUFE_no_se_consulta_la_DIAN_y_el_documento_ajeno_responde_404()
    {
        var envio = new FabricaFalsa();
        using var appA = Crear(_datosA, envio);
        await Llamar(appA, HttpMethod.Post, "/api/documento/agregar", Json("SETP990000102"));

        var dian = new FabricaDianRecuperacion(Array.Empty<byte>(), "x", "02");
        using var appB = Crear(_datosB, dian);
        var r = await Llamar(appB, HttpMethod.Get, "/api/documento/xml/SETP990000102");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal(0, dian.ConsultasXml);
    }

    [Fact]
    public async Task Si_la_DIAN_no_aprueba_el_documento_no_se_guarda_y_responde_502()
    {
        var envio = new FabricaFalsa();
        using var appA = Crear(_datosA, envio);
        var alta = await Llamar(appA, HttpMethod.Post, "/api/documento/agregar", Json("SETP990000103"));
        var cufe = JObject.Parse(await alta.Content.ReadAsStringAsync())["CUFE"]!.ToString();

        var dian = new FabricaDianRecuperacion(envio.Enviados.Single(), cufe, "04");
        using var appB = Crear(_datosB, dian);
        var r = await Llamar(appB, HttpMethod.Get, $"/api/documento/pdf/SETP990000103?cufe={cufe}");

        Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Llamar(appB, HttpMethod.Get, "/api/documento/xml/SETP990000103")).StatusCode);
    }

    // DIAN falsa para la recuperación: devuelve el XML del documento y un acuse (ApplicationResponse) con el código indicado.
    private sealed class FabricaDianRecuperacion : IFabricaEnviadorDian
    {
        private readonly byte[] _documento;
        private readonly string _cufe;
        private readonly string _codigo;

        public FabricaDianRecuperacion(byte[] documento, string cufe, string codigo)
        {
            _documento = documento;
            _cufe = cufe;
            _codigo = codigo;
        }

        public int ConsultasXml { get; private set; }

        public IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado) => new Enviador(this);

        private static string Acuse(string cufe, string codigo) =>
            "<ApplicationResponse xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2\" " +
            "xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" " +
            "xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
            "<cbc:IssueDate>2026-11-23</cbc:IssueDate><cbc:IssueTime>10:00:00-05:00</cbc:IssueTime>" +
            "<cac:DocumentResponse><cac:Response><cbc:ResponseCode>" + codigo + "</cbc:ResponseCode></cac:Response>" +
            "<cac:DocumentReference><cbc:UUID>" + cufe + "</cbc:UUID></cac:DocumentReference></cac:DocumentResponse>" +
            "</ApplicationResponse>";

        private sealed class Enviador : IEnviadorDian
        {
            private readonly FabricaDianRecuperacion _f;

            public Enviador(FabricaDianRecuperacion f) => _f = f;

            public EventResponse ObtenerDocumentoXml(string uuid)
            {
                _f.ConsultasXml++;
                return uuid == _f._cufe ? new EventResponse { XmlBytesBase64 = Convert.ToBase64String(_f._documento) } : null!;
            }

            public DianResponse ConsultarDocumento(string uuid) =>
                new DianResponse { XmlBase64Bytes = Encoding.UTF8.GetBytes(Acuse(uuid, _f._codigo)) };

            public DianResponse EnviarDocumento(byte[] xmlFirmado) => throw new NotSupportedException();
            public DianResponse EnviarNomina(byte[] xmlFirmado) => throw new NotSupportedException();
            public DianResponse EnviarEvento(byte[] xmlFirmado) => throw new NotSupportedException();
            public UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas) => throw new NotSupportedException();
            public DianResponse[] ConsultarDocumentos(string zipKey) => throw new NotSupportedException();
        }
    }
}
