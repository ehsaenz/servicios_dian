using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Pos.Servicios;
using eFacturacionColombia_NETCore.ServiciosWeb;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Pruebas;

namespace Pos.Pruebas;

public class PosApiTests : IClassFixture<CarpetaClientes>, IDisposable
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

    public PosApiTests(CarpetaClientes carpeta)
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
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private Task<HttpResponseMessage> EnviarDE(string clave, MetaDatosDE meta)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoElectronico/EnviarDE") { Content = JsonContent.Create(meta, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        return _app.CreateClient().SendAsync(peticion);
    }

    private static async Task<ResponseDian> Leer(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, texto);
        return JsonSerializer.Deserialize<ResponseDian>(texto, Json)!;
    }

    [Fact]
    public async Task Cada_cliente_envia_su_tiquete_con_su_certificado_y_a_su_set_de_pruebas()
    {
        var a = await Leer(await EnviarDE(CarpetaClientes.ClaveA, DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS1")));
        var b = await Leer(await EnviarDE(CarpetaClientes.ClaveB, DatosPos.Tiquete("900000002", "CLIENTE B SAS", "EPOS1")));

        Assert.Equal("00", a.statusCode);
        Assert.Equal(96, a.UUID.Length);
        Assert.False(string.IsNullOrEmpty(a.StrQR));
        Assert.NotEqual(a.UUID, b.UUID);

        Assert.Equal(new[] { "CN=Cliente A", "CN=Cliente B" }, _fabrica.Llamadas.Select(l => l.Sujeto).ToArray());
        Assert.Equal(new[] { "set-900000001", "set-900000002" }, _fabrica.SetsUsados.ToArray());
        Assert.All(_fabrica.Llamadas, l => Assert.Equal(AmbienteServicio.PRUEBAS, l.Ambiente));

        // El mismo número de dos clientes distintos no se pisa: cada uno tiene su carpeta.
        Assert.True(File.Exists(Path.Combine(_datos, "A", "XML", "DETP-EPOS1.xml")));
        Assert.True(File.Exists(Path.Combine(_datos, "B", "XML", "DETP-EPOS1.xml")));
    }

    [Fact]
    public async Task Reenviar_un_tiquete_ya_enviado_consulta_y_no_lo_duplica()
    {
        var meta = DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS2");

        await Leer(await EnviarDE(CarpetaClientes.ClaveA, meta));
        var segunda = await Leer(await EnviarDE(CarpetaClientes.ClaveA, DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS2")));

        Assert.Equal("00", segunda.statusCode);
        Assert.Single(_fabrica.Enviados);
    }

    [Fact]
    public async Task No_envia_un_tiquete_de_otro_emisor()
    {
        var respuesta = await EnviarDE(CarpetaClientes.ClaveA, DatosPos.Tiquete("900000002", "CLIENTE B SAS", "EPOS3"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("900000002", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_fabrica.Enviados);
    }

    [Fact]
    public async Task La_nota_de_ajuste_se_firma_con_el_certificado_del_cliente_y_no_se_duplica()
    {
        var cude = new string('a', 96);
        var primera = await Leer(await EnviarDE(CarpetaClientes.ClaveB, DatosPos.NotaAjuste("900000002", "CLIENTE B SAS", "NA1", "EPOS1", cude)));

        Assert.Equal("00", primera.statusCode);
        Assert.Equal(96, primera.UUID.Length);
        Assert.Equal("CN=Cliente B", _fabrica.Llamadas.First().Sujeto);
        Assert.True(File.Exists(Path.Combine(_datos, "B", "XML", "NATP-NA1.xml")));

        var segunda = await Leer(await EnviarDE(CarpetaClientes.ClaveB, DatosPos.NotaAjuste("900000002", "CLIENTE B SAS", "NA1", "EPOS1", cude)));

        Assert.Equal(primera.UUID, segunda.UUID);
        Assert.Single(_fabrica.Enviados);
    }

    [Fact]
    public async Task Una_nota_de_ajuste_invalida_responde_400_y_no_envia_nada()
    {
        var nota = DatosPos.NotaAjuste("900000002", "CLIENTE B SAS", "NA2", "EPOS1", new string('a', 96));
        nota.ConceptoAjuste = null!;

        var respuesta = await EnviarDE(CarpetaClientes.ClaveB, nota);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(_fabrica.Enviados);
    }

    [Fact]
    public async Task Una_nota_de_ajuste_de_otro_emisor_responde_400()
    {
        var nota = DatosPos.NotaAjuste("900000001", "CLIENTE A SAS", "NA3", "EPOS1", new string('a', 96));

        var respuesta = await EnviarDE(CarpetaClientes.ClaveB, nota);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(_fabrica.Enviados);
    }

    // ---- Producción: el cliente P tiene software en producción Y producción autorizada ----

    [Fact]
    public async Task En_produccion_el_tiquete_exige_codigo_y_nombre_del_comprador()
    {
        var sin = await EnviarDE(CarpetaClientes.ClaveProduccion, DatosPos.Tiquete("900000003", "CLIENTE P SAS", "EPOS10"));

        Assert.Equal(HttpStatusCode.BadRequest, sin.StatusCode);
        Assert.Contains("obligatorios en producción", await sin.Content.ReadAsStringAsync());
        Assert.Empty(_fabrica.Enviados);

        var completo = DatosPos.Tiquete("900000003", "CLIENTE P SAS", "EPOS10");
        completo.Adquiriente.CodigoCliente = "CLI-1";
        completo.Adquiriente.NombreComercial = "Consumidor Final";
        var respuesta = await Leer(await EnviarDE(CarpetaClientes.ClaveProduccion, completo));

        Assert.Equal("00", respuesta.statusCode);
        Assert.Equal(AmbienteServicio.PRODUCCION, _fabrica.Llamadas.Single().Ambiente);
        Assert.Empty(_fabrica.SetsUsados);
    }

    [Fact]
    public async Task En_produccion_la_nota_exige_prefijo_e_identificador_de_pago()
    {
        var cude = new string('a', 96);
        var sin = await EnviarDE(CarpetaClientes.ClaveProduccion, DatosPos.NotaAjuste("900000003", "CLIENTE P SAS", "NATP1", "EPOS1", cude));

        Assert.Equal(HttpStatusCode.BadRequest, sin.StatusCode);
        Assert.Contains("obligatorios en producción", await sin.Content.ReadAsStringAsync());
        Assert.Empty(_fabrica.Enviados);

        var completa = DatosPos.NotaAjuste("900000003", "CLIENTE P SAS", "NATP1", "EPOS1", cude);
        completa.Emisor.PrefijoRangoNumeracion = "NATP";
        completa.DetallePago.Identificador = "PAGO-123";
        var respuesta = await Leer(await EnviarDE(CarpetaClientes.ClaveProduccion, completa));

        Assert.Equal("00", respuesta.statusCode);
        Assert.Equal(AmbienteServicio.PRODUCCION, _fabrica.Llamadas.First().Ambiente);
    }

    [Fact]
    public async Task En_habilitacion_esos_datos_se_completan_solos()
    {
        // Cliente A: sin código/nombre del comprador, sin prefijo y sin identificador de pago.
        var tiquete = await Leer(await EnviarDE(CarpetaClientes.ClaveA, DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS11")));
        var nota = await Leer(await EnviarDE(CarpetaClientes.ClaveA, DatosPos.NotaAjuste("900000001", "CLIENTE A SAS", "NATP7", "EPOS11", tiquete.UUID)));

        Assert.Equal("00", tiquete.statusCode);
        Assert.Equal("00", nota.statusCode);
    }
}

