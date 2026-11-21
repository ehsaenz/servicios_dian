using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DIAN.Nomina.Servicios;
using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NominaElectronica.Enums;
using Shared.Pruebas;
using Xunit;

namespace Pos.Pruebas;

public class ResultadoSetTests
{
    [Fact]
    public void Codigo_2_con_se_encuentra_Aceptado_es_aceptado()
    {
        var r = ResultadoSet.Desde("2", "Set de prueba con identificador 1234 se encuentra Aceptado.", "zip1", "FV1");
        Assert.Equal("aceptado", r.Estado);
        Assert.Equal("zip1", r.ZipKey);
    }

    [Fact]
    public void Codigo_99_es_rechazado()
    {
        Assert.Equal("rechazado", ResultadoSet.Desde("99", "Regla NSAJ25a", "zip2", "FV2").Estado);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "Batch en proceso")]
    [InlineData("2", "Batch en proceso")]
    [InlineData("00", "Procesado correctamente")]
    public void Cualquier_otra_respuesta_queda_en_proceso(string? codigo, string? mensaje)
    {
        Assert.Equal("en_proceso", ResultadoSet.Desde(codigo, mensaje, null, "FV3").Estado);
    }
}

public class EnvioSetModulosApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FabricaConfigurable _fabrica = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-set-modulos-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public EnvioSetModulosApiTests(CarpetaClientes carpeta)
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:Carpeta"] = carpeta.Ruta,
                ["Datos:Carpeta"] = _datos,
                ["Dian:EsperaSetSegundos"] = "0"
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(_fabrica)));
        });
    }

    public void Dispose()
    {
        _app.Dispose();
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private sealed class FabricaConfigurable : IFabricaEnviadorDian
    {
        public List<string> SetsUsados { get; } = new();
        public Dictionary<string, string> Facturas { get; } = new();
        public int Consultas { get; private set; }
        public string CodigoZip { get; set; } = "2";
        public string MensajeZip { get; set; } = "Set de prueba con identificador x se encuentra Aceptado.";
        public string[]? ErroresZip { get; set; }
        public string? ZipKeyOverride { get; set; } = "zip-set";

        public IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado) => new Enviador(this);

        private sealed class Enviador : IEnviadorDian
        {
            private readonly FabricaConfigurable _p;
            public Enviador(FabricaConfigurable p) => _p = p;

            public DianResponse EnviarDocumento(byte[] xmlFirmado) => throw new InvalidOperationException("No debe llamarse EnviarDocumento");
            public DianResponse EnviarNomina(byte[] xmlFirmado) => throw new InvalidOperationException("No debe llamarse EnviarNomina");
            public DianResponse EnviarEvento(byte[] xmlFirmado) => throw new InvalidOperationException("No debe llamarse EnviarEvento");
            public DianResponse ConsultarDocumento(string uuid) => new();
            public EventResponse ObtenerDocumentoXml(string uuid) =>
                _p.Facturas.TryGetValue(uuid, out var xml)
                    ? new EventResponse { XmlBytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xml)) }
                    : new EventResponse();

            public UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas)
            {
                _p.SetsUsados.Add(idSetPruebas);
                return new UploadDocumentResponse { ZipKey = _p.ZipKeyOverride };
            }

            public DianResponse[] ConsultarDocumentos(string zipKey)
            {
                _p.Consultas++;
                return new[]
                {
                    new DianResponse
                    {
                        StatusCode = _p.CodigoZip,
                        StatusMessage = _p.MensajeZip,
                        ErrorMessage = _p.ErroresZip
                    }
                };
            }
        }
    }

    private async Task<HttpResponseMessage> PostConSet(string ruta, object cuerpo, string clave = CarpetaClientes.ClaveA)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        peticion.Headers.Add("X-Envio-Set", "true");
        return await _app.CreateClient().SendAsync(peticion);
    }

    private static async Task<ResultadoSet> LeerSet(HttpResponseMessage r)
    {
        var txt = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, txt);
        return JsonSerializer.Deserialize<ResultadoSet>(txt, Json)!;
    }

    private static FacturacionColombia_Metadatos.MetaDatosDE DocSoporteA()
    {
        var ds = Datos.MetaDocumentoSoporte();
        ds.Adquiriente.Identificacion = "900000001";
        ds.Adquiriente.RazonSocial = "CLIENTE A SAS";
        return ds;
    }

    private static string FacturaParaRadian(string cufe)
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XElement Parte(string rol, string nit, string nombre) =>
            new(cac + rol, new XElement(cac + "Party", new XElement(cac + "PartyTaxScheme",
                new XElement(cbc + "RegistrationName", nombre),
                new XElement(cbc + "CompanyID", new XAttribute("schemeName", "31"), nit))));
        return new XDocument(new XElement(XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2") + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", cac.NamespaceName), new XAttribute(XNamespace.Xmlns + "cbc", cbc.NamespaceName),
            new XElement(cbc + "ID", "SETP990000001"),
            new XElement(cbc + "UUID", new XAttribute("schemeName", "CUFE-SHA384"), cufe),
            new XElement(cbc + "IssueDate", "2026-11-07"),
            new XElement(cbc + "InvoiceTypeCode", "01"),
            Parte("AccountingSupplierParty", "800000001", "PROVEEDOR SAS"),
            Parte("AccountingCustomerParty", "900000001", "CLIENTE A SAS"))).ToString();
    }

    [Fact]
    public async Task Pos_tiquete_y_nota_de_ajuste_con_X_Envio_Set_escriben_set_pos_y_no_guardan_xml()
    {
        var tiquete = await LeerSet(await PostConSet("/api/DocumentoElectronico/EnviarDE", DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS901")));
        Assert.Equal("aceptado", tiquete.Estado);
        Assert.Equal("EPOS901", tiquete.Numero);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "set-pos.json")));
        Assert.False(File.Exists(Path.Combine(_datos, "A", "XML", "DETP-EPOS901.xml")));

        var cude = new string('a', 96);
        var nota = await LeerSet(await PostConSet("/api/DocumentoElectronico/EnviarDE", DatosPos.NotaAjuste("900000001", "CLIENTE A SAS", "NA901", "EPOS901", cude)));
        Assert.Equal("aceptado", nota.Estado);
        Assert.Equal("NA901", nota.Numero);
        Assert.False(File.Exists(Path.Combine(_datos, "A", "XML", "NATP-NA901.xml")));
    }

    [Fact]
    public async Task Documento_soporte_con_X_Envio_Set_por_ambas_rutas_escribe_set_ds_sin_guardar_xml()
    {
        var r1 = await LeerSet(await PostConSet("/api/DocumentoSoporte/EnviarDSNO", DocSoporteA()));
        Assert.Equal("aceptado", r1.Estado);
        var archivoSet = Path.Combine(_datos, "A", "set-ds.json");
        Assert.True(File.Exists(archivoSet));
        Assert.DoesNotContain("DSNO-", string.Join("|", Directory.GetFiles(Path.Combine(_datos, "A"))));

        _fabrica.CodigoZip = "00";
        _fabrica.MensajeZip = "Procesado Correctamente";
        var ds2 = DocSoporteA();
        ds2.Numero = "SEDS984000902";
        var r2 = await LeerSet(await PostConSet("/api/DocumentoElectronico/EnviarDE", ds2));
        Assert.Equal("en_proceso", r2.Estado);
        Assert.Contains("\"Estado\": \"en_proceso\"", File.ReadAllText(archivoSet));
    }

    [Fact]
    public async Task Nomina_individual_y_ajuste_con_X_Envio_Set_escriben_set_nomina_sin_almacenar()
    {
        var r1 = await LeerSet(await PostConSet("/api/v1/Nomina/EnviarNE", DatosNomina.Individual("900000001", "CLIENTE A SAS", "NOM", 901)));
        Assert.Equal("aceptado", r1.Estado);
        Assert.Equal("NOM901", r1.Numero);
        Assert.Contains("setnom-900000001", _fabrica.SetsUsados);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "set-nomina.json")));
        Assert.False(Directory.Exists(Path.Combine(_datos, "A", "nomina", "docs")));

        _fabrica.CodigoZip = "99";
        _fabrica.MensajeZip = "Validación con errores";
        _fabrica.ErroresZip = new[] { "Regla NIE001, Rechazo de prueba" };
        var ajuste = DatosNomina.Ajuste("900000001", "CLIENTE A SAS", "NOM", 902, enmTipoEnvio.Reemplazar, new string('b', 96), "NOM901");
        var r2 = await LeerSet(await PostConSet("/api/v1/Nomina/EnviarNE", ajuste));
        Assert.Equal("rechazado", r2.Estado);
        Assert.Contains("NIE001", r2.Mensaje);
    }

    [Fact]
    public async Task Eventos_RADIAN_con_X_Envio_Set_escriben_set_radian_sin_guardar_evento()
    {
        var cufe = Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
        _fabrica.Facturas[cufe] = FacturaParaRadian(cufe);

        var r = await LeerSet(await PostConSet("/api/v1/Eventos/AcuseFactura",
            new EventoRadian { Codigo = "030", UUID = cufe, Numero = "EVT901" }));

        Assert.Equal("aceptado", r.Estado);
        Assert.Equal("EVT901", r.Numero);
        Assert.Contains("setradian-900000001", _fabrica.SetsUsados);
        Assert.True(File.Exists(Path.Combine(_datos, "A", "set-radian.json")));
        Assert.False(Directory.Exists(Path.Combine(_datos, "A", "radian", "eventos")));
    }

    [Fact]
    public async Task En_produccion_o_sin_TestSetId_responde_400_y_no_llama_a_la_DIAN()
    {
        var rPos = await PostConSet("/api/DocumentoElectronico/EnviarDE",
            DatosPos.Tiquete("900000003", "CLIENTE P SAS", "EPOS99"), CarpetaClientes.ClaveProduccion);
        Assert.Equal(HttpStatusCode.BadRequest, rPos.StatusCode);

        var rNom = await PostConSet("/api/v1/Nomina/EnviarNE",
            DatosNomina.Individual("900000003", "CLIENTE P SAS", "NOM", 99), CarpetaClientes.ClaveProduccion);
        Assert.Equal(HttpStatusCode.BadRequest, rNom.StatusCode);

        var dsProd = Datos.MetaDocumentoSoporte();
        dsProd.Adquiriente.Identificacion = "900000003";
        dsProd.Adquiriente.RazonSocial = "CLIENTE P SAS";
        var rDs = await PostConSet("/api/DocumentoSoporte/EnviarDSNO", dsProd, CarpetaClientes.ClaveProduccion);
        Assert.Equal(HttpStatusCode.BadRequest, rDs.StatusCode);

        var cufe = Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
        var rEvt = await PostConSet("/api/v1/Eventos/AcuseFactura",
            new EventoRadian { Codigo = "030", UUID = cufe, Numero = "EVT99" }, CarpetaClientes.ClaveProduccion);
        Assert.Equal(HttpStatusCode.BadRequest, rEvt.StatusCode);

        Assert.Empty(_fabrica.SetsUsados);
    }

    [Fact]
    public async Task Si_la_DIAN_no_devuelve_ZipKey_queda_rechazado_sin_consultar_lote()
    {
        _fabrica.ZipKeyOverride = null;
        var r = await LeerSet(await PostConSet("/api/DocumentoElectronico/EnviarDE",
            DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS905")));

        Assert.Equal("rechazado", r.Estado);
        Assert.Contains("ZipKey", r.Mensaje);
        Assert.Equal(0, _fabrica.Consultas);
    }

    [Fact]
    public void El_resultado_lleva_el_codigo_de_la_DIAN_y_el_uuid_del_documento()
    {
        var r = ResultadoSet.Desde("2", "Set de prueba con identificador 1 se encuentra Aceptado.", "zip9", "FV9", "abc-uuid");
        Assert.Equal("2", r.CodigoDian);
        Assert.Equal("abc-uuid", r.Uuid);
    }
}
