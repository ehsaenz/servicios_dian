using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DIAN.Tenants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pos.Pruebas;

// Carpeta de clientes ficticios: dos tenants con certificado propio (autofirmado), uno inactivo y uno con software "en producción".
public sealed class CarpetaClientes : IDisposable
{
    public const string ClaveA = "clave-del-cliente-a";
    public const string ClaveB = "clave-del-cliente-b";
    public const string ClaveInactivo = "clave-del-cliente-inactivo";
    public const string ClaveProduccion = "clave-del-cliente-produccion";
    public const string ClaveCert = "x";

    public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "dian-pos-pruebas-" + Guid.NewGuid().ToString("N"));

    public CarpetaClientes()
    {
        Directory.CreateDirectory(Ruta);
        CrearCliente("a.json", "A.p12", "CN=Cliente A", "900000001", "CLIENTE A SAS", produccion: false, conNomina: true);
        CrearCliente("b.json", "B.p12", "CN=Cliente B", "900000002", "CLIENTE B SAS", produccion: true);
        CrearCliente("p.json", "P.p12", "CN=Cliente P", "900000003", "CLIENTE P SAS", produccion: true, conNomina: true);

        var indice = new[]
        {
            new { Id = "A", HashClave = ClaveApi.Hash(ClaveA), ArchivoConfig = "a.json", Activo = true, PermiteProduccion = false },
            new { Id = "B", HashClave = ClaveApi.Hash(ClaveB), ArchivoConfig = "b.json", Activo = true, PermiteProduccion = false },
            new { Id = "P", HashClave = ClaveApi.Hash(ClaveProduccion), ArchivoConfig = "p.json", Activo = true, PermiteProduccion = true },
            new { Id = "I", HashClave = ClaveApi.Hash(ClaveInactivo), ArchivoConfig = "a.json", Activo = false, PermiteProduccion = false }
        };
        File.WriteAllText(Path.Combine(Ruta, "tenants.json"), JsonSerializer.Serialize(indice));
    }

    private void CrearCliente(string archivo, string p12, string sujeto, string nit, string razon, bool produccion, bool conNomina = false)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(sujeto, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllBytes(Path.Combine(Ruta, p12), cert.Export(X509ContentType.Pfx, ClaveCert));

        object Modulo(int tipo, string testSet) => new { TipoSoftware = tipo, Identificador = "id-" + nit, PIN = "12345", AmbientePruebas = !produccion, TestSetID = testSet };
        var config = new
        {
            Software = new { Identificador = "id-" + nit, PIN = "12345", AmbientePruebas = !produccion, TestSetID = "set-" + nit },
            // Con "Modulos" cada tipo de documento tiene su software; sin ellos, el software suelto vale para documento soporte y POS.
            Modulos = conNomina
                ? new[] { Modulo(1, "setfe-" + nit), Modulo(4, "set-" + nit), Modulo(5, "set-" + nit), Modulo(2, "setnom-" + nit), Modulo(3, "setradian-" + nit) }
                : Array.Empty<object>(),
            Emisor = new { Nit = nit, DV = "1", RazonSocial = razon },
            Certificado = new { FileName = p12, Clave = ClaveCert }
        };
        File.WriteAllText(Path.Combine(Ruta, archivo), JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        try { Directory.Delete(Ruta, true); } catch (IOException) { }
    }
}

public class ProveedorJsonTests : IClassFixture<CarpetaClientes>
{
    private readonly CarpetaClientes _carpeta;

    public ProveedorJsonTests(CarpetaClientes carpeta) => _carpeta = carpeta;

    private JsonTenantConfigProvider Proveedor() => new(_carpeta.Ruta);

    [Fact]
    public async Task Una_clave_valida_devuelve_su_cliente()
    {
        var tenant = await Proveedor().BuscarPorClaveAsync(CarpetaClientes.ClaveA);

        Assert.NotNull(tenant);
        Assert.Equal("A", tenant!.Id);
        Assert.Equal("900000001", tenant.Emisor.Nit);
        Assert.NotNull(tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte));
        Assert.NotNull(tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente));
    }

    [Theory]
    [InlineData("")]
    [InlineData("clave-que-no-existe")]
    [InlineData(CarpetaClientes.ClaveInactivo)]
    public async Task Una_clave_ausente_desconocida_o_inactiva_no_devuelve_cliente(string clave)
    {
        Assert.Null(await Proveedor().BuscarPorClaveAsync(clave));
    }

    [Fact]
    public async Task Sin_autorizacion_de_produccion_todo_va_a_habilitacion_aunque_el_software_diga_produccion()
    {
        var tenant = (await Proveedor().BuscarPorClaveAsync(CarpetaClientes.ClaveB))!;

        Assert.False(tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)!.AmbientePruebas);
        Assert.True(tenant.EsHabilitacion(TipoSoftware.DocumentoSoporte));

        tenant.PermiteProduccion = true;
        Assert.False(tenant.EsHabilitacion(TipoSoftware.DocumentoSoporte));
    }

    [Fact]
    public void Las_claves_se_guardan_como_hash_y_no_se_pueden_confundir()
    {
        var hash = ClaveApi.Hash("abc");

        Assert.Equal(64, hash.Length);
        Assert.NotEqual(hash, ClaveApi.Hash("abd"));
        Assert.NotEqual(ClaveApi.Generar(), ClaveApi.Generar());
    }

    [Fact]
    public async Task Cada_cliente_firma_con_su_propio_certificado_y_se_guarda_en_cache()
    {
        var proveedor = Proveedor();
        var a = (await proveedor.BuscarPorClaveAsync(CarpetaClientes.ClaveA))!;
        var b = (await proveedor.BuscarPorClaveAsync(CarpetaClientes.ClaveB))!;
        var cache = new CertificadoCache();

        var certA = cache.Obtener(a);
        var certB = cache.Obtener(b);

        Assert.Equal("CN=Cliente A", certA.Subject);
        Assert.Equal("CN=Cliente B", certB.Subject);
        Assert.Same(certA, cache.Obtener(a));
    }
}

public class ApiClienteTests : IClassFixture<CarpetaClientes>
{
    private readonly WebApplicationFactory<Program> _fabrica;

    public ApiClienteTests(CarpetaClientes carpeta)
    {
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Tenants:Carpeta"] = carpeta.Ruta })));
    }

    private async Task<HttpResponseMessage> Llamar(string ruta, string? clave)
    {
        var cliente = _fabrica.CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        if (clave != null)
        {
            peticion.Headers.Add("x-api-key", clave);
        }

        return await cliente.SendAsync(peticion);
    }

    [Fact]
    public async Task La_ruta_de_salud_es_publica()
    {
        Assert.Equal(HttpStatusCode.OK, (await Llamar("/salud", null)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("clave-que-no-existe")]
    [InlineData(CarpetaClientes.ClaveInactivo)]
    public async Task Sin_clave_valida_responde_401(string? clave)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Llamar("/api/Servicio/Quien", clave)).StatusCode);
    }

    [Fact]
    public async Task Cada_clave_ve_solo_a_su_cliente()
    {
        var a = await (await Llamar("/api/Servicio/Quien", CarpetaClientes.ClaveA)).Content.ReadAsStringAsync();
        var b = await (await Llamar("/api/Servicio/Quien", CarpetaClientes.ClaveB)).Content.ReadAsStringAsync();

        Assert.Contains("900000001", a);
        Assert.DoesNotContain("900000002", a);
        Assert.Contains("900000002", b);
        Assert.DoesNotContain("900000001", b);
    }
}
