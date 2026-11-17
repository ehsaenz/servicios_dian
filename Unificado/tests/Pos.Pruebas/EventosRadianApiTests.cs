using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DIAN.Nomina.Servicios;
using DIAN.Pos.Servicios;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Pos.Pruebas;

public class EventosRadianApiTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FabricaFalsa _fabrica = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-radian-datos-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public EventosRadianApiTests(CarpetaClientes carpeta)
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

    // Factura mínima: lo único que el evento necesita leer (número, CUFE, tipo, fecha, emisor y receptor).
    private static string Factura(string cufe, string nitReceptor)
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
            Parte("AccountingSupplierParty", "800000001", "PROVEEDOR EJEMPLO SAS"),
            Parte("AccountingCustomerParty", nitReceptor, "RECEPTOR EJEMPLO SAS"))).ToString();
    }

    private static string NuevoCufe() => Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();

    private async Task<(HttpStatusCode Codigo, ResponseDian Respuesta)> Enviar(string clave, string accion, EventoRadian evento)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Eventos/" + accion) { Content = JsonContent.Create(evento, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        var respuesta = await _app.CreateClient().SendAsync(peticion);
        return (respuesta.StatusCode, JsonSerializer.Deserialize<ResponseDian>(await respuesta.Content.ReadAsStringAsync(), Json) ?? new ResponseDian());
    }

    private string FacturaRecibidaPorA()
    {
        var cufe = NuevoCufe();
        _fabrica.Facturas[cufe] = Factura(cufe, "900000001");
        return cufe;
    }

    [Fact]
    public async Task El_acuse_de_una_factura_recibida_se_firma_con_el_certificado_del_cliente_y_usa_su_consecutivo()
    {
        var cufe = FacturaRecibidaPorA();

        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = cufe });

        Assert.Equal(HttpStatusCode.OK, codigo);
        Assert.Equal("00", r.statusCode);
        Assert.Equal(cufe, r.UUID);
        Assert.Equal("CN=Cliente A", _fabrica.Llamadas.Single().Sujeto);
        Assert.Contains("Signature", System.Text.Encoding.UTF8.GetString(_fabrica.Eventos.Single()));
        Assert.True(File.Exists(Path.Combine(_datos, "A", "radian", "xml", "030_EVT1.xml")));
        Assert.True(File.Exists(Path.Combine(_datos, "A", "radian", "facturas", cufe + ".xml")));
    }

    [Fact]
    public async Task El_consecutivo_avanza_por_tipo_de_evento_y_se_respeta_el_numero_que_mande_el_erp()
    {
        var cufe = FacturaRecibidaPorA();

        await Enviar(CarpetaClientes.ClaveA, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = cufe });
        await Enviar(CarpetaClientes.ClaveA, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = cufe });
        await Enviar(CarpetaClientes.ClaveA, "ReciboBienesServicios", new EventoRadian { Codigo = "032", UUID = cufe });
        await Enviar(CarpetaClientes.ClaveA, "AceptacionExpresa", new EventoRadian { Codigo = "033", UUID = cufe, Numero = "EVT900" });

        var xml = Path.Combine(_datos, "A", "radian", "xml");
        Assert.True(File.Exists(Path.Combine(xml, "030_EVT1.xml")));   // primer acuse
        Assert.True(File.Exists(Path.Combine(xml, "030_EVT2.xml")));   // segundo acuse
        Assert.True(File.Exists(Path.Combine(xml, "032_EVT1.xml")));   // el recibo lleva su propio consecutivo, que también empieza en 1
        Assert.True(File.Exists(Path.Combine(xml, "033_EVT900.xml"))); // número que mandó el ERP
        Assert.Equal(4, _fabrica.Eventos.Count);
        Assert.Equal(4, Directory.GetFiles(xml, "*.xml").Length);      // ninguno pisó a otro
    }

    [Fact]
    public async Task Un_evento_sobre_una_factura_de_otro_receptor_se_rechaza_sin_enviar()
    {
        var cufe = NuevoCufe();
        _fabrica.Facturas[cufe] = Factura(cufe, "900000002"); // el receptor es el cliente B

        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = cufe });

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("101", r.statusCode);
        Assert.Empty(_fabrica.Eventos);
    }

    [Fact]
    public async Task Una_factura_que_la_DIAN_no_devuelve_responde_199()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = NuevoCufe() });

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("199", r.statusCode);
    }

    [Fact]
    public async Task El_codigo_debe_corresponder_a_la_opcion_y_el_reclamo_exige_su_causa()
    {
        var cufe = FacturaRecibidaPorA();

        var (c1, r1) = await Enviar(CarpetaClientes.ClaveA, "ReclamoFactura", new EventoRadian { Codigo = "030", UUID = cufe });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Contains("no corresponde", r1.statusMessage);

        var (c2, r2) = await Enviar(CarpetaClientes.ClaveA, "ReclamoFactura", new EventoRadian { Codigo = "031", UUID = cufe });
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("código de la causa", r2.statusMessage);

        var (c3, r3) = await Enviar(CarpetaClientes.ClaveA, "ReclamoFactura",
            new EventoRadian { Codigo = "031", UUID = cufe, Causa = new CausaReclamo { Codigo = "01", Significado = "Documento con inconsistencias" } });
        Assert.Equal(HttpStatusCode.OK, c3);
        Assert.Equal("00", r3.statusCode);
    }

    [Fact]
    public async Task La_aceptacion_tacita_aun_no_esta_disponible()
    {
        var (codigo, r) = await Enviar(CarpetaClientes.ClaveA, "AceptacionTacita", new EventoRadian { Codigo = "034", UUID = FacturaRecibidaPorA() });

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("109", r.statusCode);
        Assert.Empty(_fabrica.Eventos);
    }

    [Fact]
    public async Task Un_cliente_sin_software_de_eventos_recibe_un_mensaje_claro()
    {
        var cufe = NuevoCufe();
        _fabrica.Facturas[cufe] = Factura(cufe, "900000002");

        var (codigo, r) = await Enviar(CarpetaClientes.ClaveB, "AcuseFactura", new EventoRadian { Codigo = "030", UUID = cufe });

        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal("108", r.statusCode);
        Assert.Contains("RADIAN", r.statusMessage);
    }

    [Fact]
    public async Task Sin_clave_de_api_no_se_envia_nada()
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/api/v1/Eventos/AcuseFactura", new EventoRadian { Codigo = "030", UUID = NuevoCufe() }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(_fabrica.Eventos);
    }
}
