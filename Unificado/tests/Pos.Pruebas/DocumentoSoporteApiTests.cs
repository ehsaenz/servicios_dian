using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Pos.Servicios;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Pruebas;

namespace Pos.Pruebas;

// Enviador falso: no llama a la DIAN; guarda con qué certificado y en qué ambiente se pidió enviar.
public sealed class FabricaFalsa : IFabricaEnviadorDian
{
    public List<(AmbienteServicio Ambiente, string Sujeto)> Llamadas { get; } = new();
    public List<byte[]> Enviados { get; } = new();
    public List<string> SetsUsados { get; } = new();
    public Dictionary<string, string> Facturas { get; } = new();   // UUID -> XML que "devuelve la DIAN"
    public List<byte[]> Eventos { get; } = new();
    /// <summary>Simula un set de pruebas ya aceptado: por ZipKey la DIAN solo dice que el set está aceptado (código 2).</summary>
    public bool SetYaAceptado { get; set; }

    /// <summary>Código con el que responde a EnviarDocumento (00 aprobado, 99 rechazado...).</summary>
    public string CodigoRespuesta { get; set; } = "00";

    /// <summary>Si es true, la respuesta de EnviarDocumento incluye un ApplicationResponse 02 del documento recibido.</summary>
    public bool ConApplicationResponse { get; set; }

    public IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado)
    {
        Llamadas.Add((ambiente, certificado.Subject));
        return new Falso(this);
    }

    private sealed class Falso : IEnviadorDian
    {
        private readonly FabricaFalsa _padre;
        public Falso(FabricaFalsa padre) => _padre = padre;

        public DianResponse EnviarDocumento(byte[] xmlFirmado)
        {
            _padre.Enviados.Add(xmlFirmado);
            var respuesta = new DianResponse { StatusCode = _padre.CodigoRespuesta, StatusMessage = "Procesado Correctamente", XmlDocumentKey = "uuid-falso" };
            if (_padre.ConApplicationResponse)
            {
                // Como la DIAN: ApplicationResponse con código 02 y el número y CUFE del documento recibido.
                var recibido = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(xmlFirmado));
                System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
                var ar = "<ApplicationResponse xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2\"" +
                         " xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"" +
                         " xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
                         "<cbc:IssueDate>2026-11-08</cbc:IssueDate><cbc:IssueTime>10:15:32-05:00</cbc:IssueTime>" +
                         "<cac:DocumentResponse><cac:Response><cbc:ResponseCode>02</cbc:ResponseCode></cac:Response>" +
                         "<cac:DocumentReference><cbc:ID>" + recibido.Root!.Element(cbc + "ID")!.Value + "</cbc:ID><cbc:UUID>" +
                         recibido.Root.Element(cbc + "UUID")!.Value + "</cbc:UUID></cac:DocumentReference></cac:DocumentResponse></ApplicationResponse>";
                respuesta.XmlBase64Bytes = System.Text.Encoding.UTF8.GetBytes(ar);
            }

            return respuesta;
        }

        public DianResponse EnviarNomina(byte[] xmlFirmado)
        {
            _padre.Enviados.Add(xmlFirmado);
            return new DianResponse { StatusCode = "00", StatusMessage = "Procesado Correctamente", XmlDocumentKey = "uuid-falso" };
        }

        public DianResponse EnviarEvento(byte[] xmlFirmado)
        {
            _padre.Eventos.Add(xmlFirmado);
            return new DianResponse { StatusCode = "00", StatusMessage = "Procesado Correctamente" };
        }

        public EventResponse ObtenerDocumentoXml(string uuid) =>
            _padre.Facturas.TryGetValue(uuid, out var xml)
                ? new EventResponse { XmlBytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xml)) }
                : new EventResponse();

        public UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas)
        {
            _padre.Enviados.AddRange(xmlFirmados);
            _padre.SetsUsados.Add(idSetPruebas);
            return new UploadDocumentResponse { ZipKey = "zip-falso" };
        }

        public DianResponse[] ConsultarDocumentos(string zipKey) => _padre.SetYaAceptado
            ? new[] { new DianResponse { StatusCode = "2", StatusMessage = "Set de prueba con identificador x se encuentra Aceptado." } }
            : new[] { new DianResponse { StatusCode = "00", StatusMessage = "ok " + zipKey } };

        public DianResponse ConsultarDocumento(string uuid) => new DianResponse { StatusCode = "00", StatusMessage = "ok " + uuid };
    }
}

public class DocumentoSoporteApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FabricaFalsa _fabrica = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-pos-datos-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public DocumentoSoporteApiTests(CarpetaClientes carpeta)
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = carpeta.Ruta,
                ["Datos:Carpeta"] = _datos
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(_fabrica)));
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    // Documento soporte cuyo adquiriente es el emisor del cliente de prueba.
    private static MetaDatosDE Documento(string nit, string razon)
    {
        var meta = Datos.MetaDocumentoSoporte();
        meta.Adquiriente.Identificacion = nit;
        meta.Adquiriente.RazonSocial = razon;
        return meta;
    }

    private Task<HttpResponseMessage> Enviar(string clave, MetaDatosDE meta, string ruta = "/api/DocumentoSoporte/EnviarDSNO")
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(meta, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        return _app.CreateClient().SendAsync(peticion);
    }

    [Fact]
    public async Task Un_documento_soporte_sin_periodo_se_envia_igual()
    {
        // El periodo de facturación es opcional: un ERP que no lo use no debe romper el envío.
        var meta = Documento("900000001", "CLIENTE A SAS");
        meta.Periodo = null;

        var respuesta = await Enviar(CarpetaClientes.ClaveA, meta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("00", (await respuesta.Content.ReadFromJsonAsync<ResponseDian>(Json))!.statusCode);
    }

    [Fact]
    public async Task Cada_cliente_firma_y_envia_con_su_propio_certificado_y_siempre_a_habilitacion()
    {
        var a = await Enviar(CarpetaClientes.ClaveA, Documento("900000001", "CLIENTE A SAS"));
        var b = await Enviar(CarpetaClientes.ClaveB, Documento("900000002", "CLIENTE B SAS"));

        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal("00", (await a.Content.ReadFromJsonAsync<ResponseDian>(Json))!.statusCode);

        Assert.Equal(2, _fabrica.Llamadas.Count);
        Assert.Equal("CN=Cliente A", _fabrica.Llamadas[0].Sujeto);
        Assert.Equal("CN=Cliente B", _fabrica.Llamadas[1].Sujeto);

        // B tiene su software marcado como producción, pero no tiene producción autorizada: va a habilitación.
        Assert.All(_fabrica.Llamadas, l => Assert.Equal(AmbienteServicio.PRUEBAS, l.Ambiente));
    }

    [Fact]
    public async Task El_xml_enviado_esta_firmado_y_los_archivos_quedan_en_la_carpeta_del_cliente()
    {
        await Enviar(CarpetaClientes.ClaveA, Documento("900000001", "CLIENTE A SAS"));

        var xml = System.Text.Encoding.UTF8.GetString(_fabrica.Enviados.Single());
        Assert.Contains("Signature", xml);
        Assert.Contains("DocumentoSoporte".Length > 0 ? "<Invoice" : "", xml);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "XML", "DSNO-SEDS1.xml")));
        Assert.False(Directory.Exists(Path.Combine(_datos, "B")));
    }

    [Fact]
    public async Task No_envia_documentos_de_un_adquiriente_distinto_al_cliente()
    {
        // El cliente A intenta enviar un documento a nombre del cliente B.
        var respuesta = await Enviar(CarpetaClientes.ClaveA, Documento("900000002", "CLIENTE B SAS"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("900000002", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_fabrica.Llamadas);
    }

    [Fact]
    public async Task EnviarDE_acepta_el_documento_soporte()
    {
        var soporte = Documento("900000001", "CLIENTE A SAS");
        var ok = await Enviar(CarpetaClientes.ClaveA, soporte, "/api/DocumentoElectronico/EnviarDE");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task EnviarDE_rechaza_un_tipo_de_documento_que_no_atiende()
    {
        var otro = Documento("900000001", "CLIENTE A SAS");
        otro.TipoDocumento = new TipoDocumentoElectronico { Codigo = "01" };
        var respuesta = await Enviar(CarpetaClientes.ClaveA, otro, "/api/DocumentoElectronico/EnviarDE");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Consultar_usa_el_certificado_del_cliente()
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoSoporte/ConsultarDSNO?track_id=abc123");
        peticion.Headers.Add("x-api-key", CarpetaClientes.ClaveB);
        var respuesta = await _app.CreateClient().SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("abc123", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("CN=Cliente B", _fabrica.Llamadas.Single().Sujeto);
    }

    [Fact]
    public async Task Sin_clave_no_se_envia_nada()
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/api/DocumentoSoporte/EnviarDSNO", Documento("900000001", "CLIENTE A SAS"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(_fabrica.Llamadas);
    }
}
