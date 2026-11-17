using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DIAN.Admin.Web;
using DIAN.Tenants;

namespace Admin.Pruebas;

public class ImportadorTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-import-" + Guid.NewGuid().ToString("N"));
    private readonly ClienteAdminServicio _admin;
    private readonly FileTenantStore _almacen;
    private readonly ProtectorSecretos _protector;

    public ImportadorTests()
    {
        Directory.CreateDirectory(_raiz);
        _almacen = new FileTenantStore(Path.Combine(_raiz, "almacen"));
        _protector = new ProtectorSecretos(Path.Combine(_raiz, "llaves"));
        _admin = new ClienteAdminServicio(_almacen, _protector, new Auditoria(Path.Combine(_raiz, "almacen")));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CLAVE_PRUEBA_P12", null);
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    private string Config(object contenido, string clave = "clave-cert")
    {
        var carpeta = Path.Combine(_raiz, "origen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest("CN=Cliente 900000001", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        File.WriteAllBytes(Path.Combine(carpeta, "cert.p12"), cert.Export(X509ContentType.Pfx, clave));
        var archivo = Path.Combine(carpeta, "config.json");
        File.WriteAllText(archivo, JsonSerializer.Serialize(contenido));
        return archivo;
    }

    private static object Software(int tipo, string pin) => new { TipoSoftware = tipo, Identificador = "00000000-0000-0000-0000-00000000aaaa", PIN = pin, AmbientePruebas = true, TestSetID = "set-" + tipo };

    [Fact]
    public async Task Importa_un_cliente_con_dos_softwares_y_certificado_y_genera_su_clave()
    {
        var archivo = Config(new
        {
            Emisor = new { Nit = "900000001", DV = "1", RazonSocial = "CLIENTE A SAS" },
            Certificado = new { FileName = "cert.p12" },
            Modulos = new[] { Software(4, "pin-ds"), Software(5, "pin-pos") }
        });
        Environment.SetEnvironmentVariable("CLAVE_PRUEBA_P12", "clave-cert");

        var clave = await ImportadorCliente.ImportarAsync(_admin, "consola", "CA", archivo, "CLAVE_PRUEBA_P12", true);

        var tenant = await new StoreTenantConfigProvider(_almacen, _protector).BuscarPorClaveAsync(clave!);
        Assert.NotNull(tenant);
        Assert.Equal("900000001", tenant!.Emisor.Nit);
        Assert.Equal("pin-ds", tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)!.PIN);
        Assert.Equal("pin-pos", tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente)!.PIN);
        Assert.Equal("clave-cert", tenant.Certificado.Clave);
        Assert.DoesNotContain("pin-ds", File.ReadAllText(_almacen.RutaArchivo("CA", "cliente.json")));
    }

    [Fact]
    public async Task Un_software_suelto_del_formato_antiguo_vale_para_los_dos_tipos_y_no_se_genera_clave_si_no_se_pide()
    {
        var archivo = Config(new
        {
            Emisor = new { Nit = "900000001", DV = "1", RazonSocial = "CLIENTE A SAS" },
            Certificado = new { FileName = "cert.p12", Clave = "clave-cert" },
            Software = new { Identificador = "00000000-0000-0000-0000-00000000aaaa", PIN = "pin-unico", AmbientePruebas = true, TestSetID = "s" }
        });

        var clave = await ImportadorCliente.ImportarAsync(_admin, "consola", "CA", archivo, null, false);

        Assert.Null(clave);
        var cliente = (await _admin.ObtenerAsync("CA"))!;
        Assert.Equal(2, cliente.Software.Count);
        Assert.Empty(cliente.Claves);
    }

    [Fact]
    public async Task No_sobrescribe_un_cliente_existente_y_exige_la_clave_del_certificado()
    {
        var archivo = Config(new
        {
            Emisor = new { Nit = "900000001", DV = "1", RazonSocial = "CLIENTE A SAS" },
            Certificado = new { FileName = "cert.p12" },
            Modulos = new[] { Software(4, "pin-ds") }
        });

        var sinClave = await Assert.ThrowsAsync<ErrorAdministracion>(() => ImportadorCliente.ImportarAsync(_admin, "consola", "CA", archivo, "VARIABLE_QUE_NO_EXISTE", false));
        Assert.Contains("clave del certificado", sinClave.Message);
        Assert.Null(await _admin.ObtenerAsync("CA")); // no queda un cliente a medias

        Environment.SetEnvironmentVariable("CLAVE_PRUEBA_P12", "clave-cert");
        await ImportadorCliente.ImportarAsync(_admin, "consola", "CA", archivo, "CLAVE_PRUEBA_P12", false);
        await Assert.ThrowsAsync<ErrorAdministracion>(() => ImportadorCliente.ImportarAsync(_admin, "consola", "CA", archivo, "CLAVE_PRUEBA_P12", false));
    }
}
