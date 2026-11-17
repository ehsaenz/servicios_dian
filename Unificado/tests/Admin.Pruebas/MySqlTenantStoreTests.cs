using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DIAN.Admin.Web;
using DIAN.Tenants;
using MySql.Data.MySqlClient;

namespace Admin.Pruebas;

/// <summary>Corre solo con la variable DIAN_MYSQL (cadena de conexión a una base DESECHABLE: las pruebas vacían las tablas dian_*).</summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DIAN_MYSQL")))
        {
            Skip = "Manual: defina DIAN_MYSQL con la cadena de conexión de una base de pruebas (se vacían sus tablas dian_*).";
        }
    }
}

public class MySqlTenantStoreTests : IDisposable
{
    private const string Usuario = "admin";
    private static readonly string Cadena = Environment.GetEnvironmentVariable("DIAN_MYSQL") ?? "";
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-mysql-" + Guid.NewGuid().ToString("N"));
    private readonly ProtectorSecretos _protector;

    public MySqlTenantStoreTests()
    {
        Directory.CreateDirectory(_raiz);
        _protector = new ProtectorSecretos(Path.Combine(_raiz, "llaves"));
        if (!string.IsNullOrEmpty(Cadena))
        {
            MySqlTenantStore.CrearEsquemaAsync(Cadena).GetAwaiter().GetResult();
            foreach (var tabla in new[] { "dian_claves", "dian_software", "dian_sucursales", "dian_webhooks", "dian_archivos", "dian_clientes" })
            {
                using var conexion = new MySqlConnection(Cadena);
                conexion.Open();
                using var comando = new MySqlCommand("DELETE FROM " + tabla, conexion);
                comando.ExecuteNonQuery();
            }
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    private MySqlTenantStore Almacen(string nombre = "cache", TimeSpan? comprobarCada = null) =>
        new(Cadena, Path.Combine(_raiz, nombre), comprobarCada ?? TimeSpan.Zero);

    private ClienteAdminServicio Admin(ITenantStore almacen) => new(almacen, _protector, new Auditoria(Path.Combine(_raiz, "auditoria")));

    private static EmisorConfig Emisor(string nit = "900000001", string razon = "CLIENTE A SAS") => new() { Nit = nit, DV = "1", RazonSocial = razon };

    private static byte[] P12(string sujeto, string clave)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(sujeto, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        return cert.Export(X509ContentType.Pfx, clave);
    }

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    private const string IdSoftware = "00000000-0000-0000-0000-00000000aaaa";

    private static async Task Completo(ClienteAdminServicio admin, string id, string nit)
    {
        await admin.CrearAsync(Usuario, id, Emisor(nit));
        await admin.GuardarSoftwareAsync(Usuario, id, TipoSoftware.DocumentoSoporte, IdSoftware, "pin-ds", true, "set-1");
        await admin.GuardarSoftwareAsync(Usuario, id, TipoSoftware.DocumentoEquivalente, IdSoftware, "pin-pos", true, null);
        await admin.SubirCertificadoAsync(Usuario, id, P12("CN=Cliente " + nit, "clave-cert"), "clave-cert");
    }

    [MySqlFact]
    public async Task El_esquema_se_puede_crear_varias_veces()
    {
        await MySqlTenantStore.CrearEsquemaAsync(Cadena);
        await MySqlTenantStore.CrearEsquemaAsync(Cadena);
    }

    [MySqlFact]
    public async Task Un_cliente_completo_se_guarda_y_la_api_lo_resuelve_por_su_clave_con_secretos_descifrados()
    {
        var almacen = Almacen();
        var admin = Admin(almacen);
        await Completo(admin, "A", "900000001");
        var clave = await admin.GenerarClaveAsync(Usuario, "A", "ERP");

        var tenant = await new StoreTenantConfigProvider(Almacen("cache2"), _protector).BuscarPorClaveAsync(clave);

        Assert.NotNull(tenant);
        Assert.Equal("900000001", tenant!.Emisor.Nit);
        Assert.Equal("pin-ds", tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)!.PIN);
        Assert.Equal("set-1", tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)!.TestSetId);
        Assert.Equal("clave-cert", tenant.Certificado.Clave);
        Assert.True(File.Exists(tenant.Certificado.RutaArchivo));
        Assert.Equal("CN=Cliente 900000001", new CertificadoCache().Obtener(tenant).Subject);
    }

    [MySqlFact]
    public async Task Los_secretos_no_quedan_en_claro_en_la_base()
    {
        var admin = Admin(Almacen());
        await Completo(admin, "A", "900000001");
        var clave = await admin.GenerarClaveAsync(Usuario, "A", "");

        var texto = "";
        foreach (var consulta in new[] { "SELECT PinProtegido FROM dian_software", "SELECT ClaveCertificadoProtegida FROM dian_clientes", "SELECT HashClave FROM dian_claves" })
        {
            using var conexion = new MySqlConnection(Cadena);
            await conexion.OpenAsync();
            using var lector = await new MySqlCommand(consulta, conexion).ExecuteReaderAsync();
            while (await lector.ReadAsync()) texto += lector.GetString(0) + "\n";
        }

        Assert.DoesNotContain("pin-ds", texto);
        Assert.DoesNotContain("pin-pos", texto);
        Assert.DoesNotContain("clave-cert", texto);
        Assert.DoesNotContain(clave, texto);
        Assert.Contains(ClaveApi.Hash(clave), texto);
    }

    [MySqlFact]
    public async Task Un_id_repetido_se_rechaza_con_un_mensaje_claro()
    {
        var admin = Admin(Almacen());
        await admin.CrearAsync(Usuario, "A", Emisor());

        var error = await Assert.ThrowsAsync<ErrorAdministracion>(() => admin.CrearAsync(Usuario, "a", Emisor()));

        Assert.Contains("Ya existe", error.Message);
    }

    [MySqlFact]
    public async Task Dos_ediciones_a_la_vez_no_se_pisan_la_segunda_se_rechaza()
    {
        var almacen = Almacen();
        await Admin(almacen).CrearAsync(Usuario, "A", Emisor());

        var primera = (await almacen.ObtenerAsync("A"))!;
        var segunda = (await almacen.ObtenerAsync("A"))!;
        primera.Emisor.RazonSocial = "CAMBIO UNO";
        segunda.Emisor.RazonSocial = "CAMBIO DOS";

        await almacen.GuardarAsync(primera);
        var error = await Assert.ThrowsAsync<ErrorAdministracion>(() => almacen.GuardarAsync(segunda));

        Assert.Contains("cambió", error.Message);
        Assert.Equal("CAMBIO UNO", (await almacen.ObtenerAsync("A"))!.Emisor.RazonSocial);
    }

    [MySqlFact]
    public async Task Un_cambio_en_una_instancia_lo_ve_la_otra_incluidas_las_claves_revocadas()
    {
        var instanciaA = Almacen("cacheA");
        var instanciaB = Almacen("cacheB");
        var admin = Admin(instanciaA);
        var proveedorB = new StoreTenantConfigProvider(instanciaB, _protector);
        await Completo(admin, "A", "900000001");
        var clave = await admin.GenerarClaveAsync(Usuario, "A", "");

        Assert.NotNull(await proveedorB.BuscarPorClaveAsync(clave));

        await admin.RevocarClaveAsync(Usuario, "A", (await instanciaA.ObtenerAsync("A"))!.Claves.Single().Id);
        Assert.Null(await proveedorB.BuscarPorClaveAsync(clave));

        // También el certificado reemplazado llega a la caché de la otra instancia.
        await admin.SubirCertificadoAsync(Usuario, "A", P12("CN=Certificado nuevo 900000001", "otra-clave"), "otra-clave");
        var nueva = await admin.GenerarClaveAsync(Usuario, "A", "");
        var tenantB = (await proveedorB.BuscarPorClaveAsync(nueva))!;
        Assert.Equal("CN=Certificado nuevo 900000001", new CertificadoCache().Obtener(tenantB).Subject);
    }

    [MySqlFact]
    public async Task Cambiar_el_logo_borra_el_anterior_de_la_base_y_de_la_cache()
    {
        var almacen = Almacen();
        var admin = Admin(almacen);
        await admin.CrearAsync(Usuario, "A", Emisor());

        await admin.SubirLogoAsync(Usuario, "A", "logo.png", Png);
        Assert.True(File.Exists(almacen.RutaArchivo("A", "logo.png")));

        await admin.SubirLogoAsync(Usuario, "A", "logo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 });
        Assert.False(File.Exists(almacen.RutaArchivo("A", "logo.png")));
        Assert.True(File.Exists(almacen.RutaArchivo("A", "logo.jpg")));

        using var conexion = new MySqlConnection(Cadena);
        await conexion.OpenAsync();
        var filas = Convert.ToInt32(await new MySqlCommand("SELECT COUNT(*) FROM dian_archivos WHERE Nombre = 'logo.png'", conexion).ExecuteScalarAsync());
        Assert.Equal(0, filas);
    }

    [MySqlFact]
    public async Task Las_sucursales_con_su_logo_se_guardan_y_las_ve_otra_instancia()
    {
        var almacen = Almacen();
        var admin = Admin(almacen);
        await admin.CrearAsync(Usuario, "A", Emisor());
        await admin.GuardarSucursalAsync(Usuario, "A", "MAT-001", "Sucursal Norte", "Responsable de IVA");
        await admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-001", "norte.png", Png);
        await admin.GuardarSucursalAsync(Usuario, "A", "MAT-002", "Sucursal Sur", null);

        var otra = Almacen("cache-otra");
        var cliente = (await otra.ObtenerAsync("A"))!;
        Assert.Equal(2, cliente.Sucursales.Count);
        var norte = cliente.Sucursales.Single(s => s.MatriculaMercantil == "MAT-001");
        Assert.Equal("Sucursal Norte", norte.Nombre);
        Assert.Equal("logo-MAT-001.png", norte.ArchivoLogo);
        Assert.True(File.Exists(otra.RutaArchivo("A", "logo-MAT-001.png")));
        Assert.Null(cliente.Sucursales.Single(s => s.MatriculaMercantil == "MAT-002").ArchivoLogo);

        await admin.EliminarSucursalAsync(Usuario, "A", "MAT-001");
        Assert.Single((await Almacen("cache-otra2").ObtenerAsync("A"))!.Sucursales);
    }

    [MySqlFact]
    public async Task Los_caracteres_especiales_del_nombre_se_conservan()
    {
        var almacen = Almacen();
        const string razon = "ÑANDÚ & HIJOS S.A.S. – Cali’s “Café”";
        await Admin(almacen).CrearAsync(Usuario, "A", Emisor(razon: razon));

        Assert.Equal(razon, (await Almacen("otra").ObtenerAsync("A"))!.Emisor.RazonSocial);
    }

    [MySqlFact]
    public async Task Editar_una_copia_sin_guardar_no_altera_lo_que_ven_los_demas()
    {
        var almacen = Almacen();
        await Admin(almacen).CrearAsync(Usuario, "A", Emisor());

        var copia = (await almacen.ObtenerAsync("A"))!;
        copia.Emisor.RazonSocial = "SIN GUARDAR";
        copia.Activo = false;

        var real = (await almacen.ListarAsync()).Single();
        Assert.Equal("CLIENTE A SAS", real.Emisor.RazonSocial);
        Assert.True(real.Activo);
    }

    [MySqlFact]
    public async Task Migrar_del_almacen_de_archivos_a_MySQL_copia_clientes_y_certificados_sin_duplicar()
    {
        var archivos = new FileTenantStore(Path.Combine(_raiz, "almacen-archivos"));
        var adminArchivos = Admin(archivos);
        await Completo(adminArchivos, "A", "900000001");
        await Completo(adminArchivos, "B", "900000002");
        var claveA = await adminArchivos.GenerarClaveAsync(Usuario, "A", "ERP");
        await adminArchivos.SubirLogoAsync(Usuario, "B", "logo.png", Png);

        var mysql = Almacen();
        Assert.Equal(2, await MigradorMySql.MigrarAsync(archivos, mysql));
        Assert.Equal(0, await MigradorMySql.MigrarAsync(archivos, mysql));

        var tenant = await new StoreTenantConfigProvider(Almacen("cache-nueva"), _protector).BuscarPorClaveAsync(claveA);
        Assert.Equal("900000001", tenant!.Emisor.Nit);
        Assert.Equal("pin-pos", tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente)!.PIN);
        Assert.Equal("CN=Cliente 900000001", new CertificadoCache().Obtener(tenant).Subject);
        Assert.Equal("logo.png", (await mysql.ObtenerAsync("B"))!.ArchivoLogo);
        Assert.True(File.Exists(mysql.RutaArchivo("B", "logo.png")));
    }
}
