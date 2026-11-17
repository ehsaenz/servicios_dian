using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Pos.Servicios;
using DIAN.Tenants;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Pruebas;

namespace Pos.Pruebas;

public sealed class EntornoAdmin : IDisposable
{
    public string Raiz { get; } = Path.Combine(Path.GetTempPath(), "dian-admin-" + Guid.NewGuid().ToString("N"));
    public string Llaves => Path.Combine(Raiz, "llaves");
    public FileTenantStore Almacen { get; }
    public ProtectorSecretos Protector { get; }
    public Auditoria Auditoria { get; }
    public ClienteAdminServicio Admin { get; }

    public EntornoAdmin()
    {
        Directory.CreateDirectory(Raiz);
        Almacen = new FileTenantStore(Path.Combine(Raiz, "almacen"));
        Protector = new ProtectorSecretos(Llaves);
        Auditoria = new Auditoria(Path.Combine(Raiz, "almacen"));
        Admin = new ClienteAdminServicio(Almacen, Protector, Auditoria);
    }

    public void Dispose()
    {
        try { Directory.Delete(Raiz, true); } catch (IOException) { }
    }

    public static byte[] CrearP12(string sujeto, string clave, int diasDesde = -1, int diasHasta = 365, bool conLlave = true)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest(sujeto, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(diasDesde), DateTimeOffset.UtcNow.AddDays(diasHasta));
        return cert.Export(X509ContentType.Pfx, clave);
    }

    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
}

public class AdminServicioTests : IDisposable
{
    private readonly EntornoAdmin _e = new();
    private const string Usuario = "admin";
    private const string IdSoftware = "00000000-0000-0000-0000-00000000aaaa";

    public void Dispose() => _e.Dispose();

    private static EmisorConfig Emisor(string nit = "900000001", string razon = "CLIENTE A SAS") => new() { Nit = nit, DV = "1", RazonSocial = razon };

    private async Task<ClienteRegistro> ClienteCompleto(string id = "A", string nit = "900000001")
    {
        await _e.Admin.CrearAsync(Usuario, id, Emisor(nit));
        await _e.Admin.GuardarSoftwareAsync(Usuario, id, TipoSoftware.DocumentoSoporte, IdSoftware, "pin-secreto-1", true, "set-1");
        await _e.Admin.GuardarSoftwareAsync(Usuario, id, TipoSoftware.DocumentoEquivalente, IdSoftware, "pin-secreto-2", true, null);
        await _e.Admin.SubirCertificadoAsync(Usuario, id, EntornoAdmin.CrearP12("CN=Cliente " + nit, "clave-cert"), "clave-cert");
        return (await _e.Admin.ObtenerAsync(id))!;
    }

    [Fact]
    public async Task Crear_valida_el_id_el_nit_y_los_duplicados()
    {
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CrearAsync(Usuario, "a b", Emisor()));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CrearAsync(Usuario, "AB", Emisor(nit: "90000-1")));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CrearAsync(Usuario, "AB", Emisor(razon: " ")));

        await _e.Admin.CrearAsync(Usuario, "AB", Emisor());
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CrearAsync(Usuario, "ab", Emisor()));
    }

    [Fact]
    public async Task El_pin_y_la_clave_del_certificado_no_quedan_en_claro()
    {
        await ClienteCompleto();

        var json = File.ReadAllText(_e.Almacen.RutaArchivo("A", "cliente.json"));
        Assert.DoesNotContain("pin-secreto-1", json);
        Assert.DoesNotContain("pin-secreto-2", json);
        Assert.DoesNotContain("clave-cert", json);
    }

    [Fact]
    public async Task Un_certificado_con_clave_incorrecta_o_vencido_no_se_guarda()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        var mala = await Assert.ThrowsAsync<ErrorAdministracion>(() =>
            _e.Admin.SubirCertificadoAsync(Usuario, "A", EntornoAdmin.CrearP12("CN=A", "buena"), "mala"));
        Assert.Contains("clave es incorrecta", mala.Message);

        var vencido = await Assert.ThrowsAsync<ErrorAdministracion>(() =>
            _e.Admin.SubirCertificadoAsync(Usuario, "A", EntornoAdmin.CrearP12("CN=A", "x", diasDesde: -400, diasHasta: -10), "x"));
        Assert.Contains("vencido", vencido.Message);

        Assert.Null((await _e.Admin.ObtenerAsync("A"))!.ArchivoCertificado);
        Assert.False(File.Exists(_e.Almacen.RutaArchivo("A", "certificado.p12")));
    }

    [Fact]
    public async Task Un_certificado_valido_se_guarda_y_avisa_si_el_nit_no_aparece()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor(nit: "900000001"));

        var resultado = await _e.Admin.SubirCertificadoAsync(Usuario, "A", EntornoAdmin.CrearP12("CN=Otra empresa", "x"), "x");

        Assert.Equal("CN=Otra empresa", resultado.Sujeto);
        Assert.Contains(resultado.Advertencias, a => a.Contains("900000001"));
        var cliente = (await _e.Admin.ObtenerAsync("A"))!;
        Assert.Equal("certificado.p12", cliente.ArchivoCertificado);
        Assert.True(File.Exists(_e.Almacen.RutaArchivo("A", "certificado.p12")));
    }

    [Fact]
    public async Task El_software_valida_el_guid_y_conserva_el_pin_si_no_se_envia_uno_nuevo()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSoftwareAsync(Usuario, "A", TipoSoftware.DocumentoSoporte, "no-es-guid", "1", true, null));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSoftwareAsync(Usuario, "A", TipoSoftware.DocumentoSoporte, IdSoftware, "", true, null));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSoftwareAsync(Usuario, "A", (TipoSoftware)99, IdSoftware, "1", true, null));

        await _e.Admin.GuardarSoftwareAsync(Usuario, "A", TipoSoftware.DocumentoSoporte, IdSoftware, "pin-1", true, null);
        var primero = (await _e.Admin.ObtenerAsync("A"))!.Software.Single(s => s.TipoSoftware == TipoSoftware.DocumentoSoporte).PinProtegido;
        await _e.Admin.GuardarSoftwareAsync(Usuario, "A", TipoSoftware.DocumentoSoporte, IdSoftware, null, false, "set-2");

        var cliente = (await _e.Admin.ObtenerAsync("A"))!;
        Assert.Single(cliente.Software);
        Assert.Equal(primero, cliente.Software[0].PinProtegido);
        Assert.False(cliente.Software[0].AmbientePruebas);
        Assert.Equal("set-2", cliente.Software[0].TestSetId);

        // La facturación electrónica (factura y notas) sí se acepta: la atiende api/documento.
        await _e.Admin.GuardarSoftwareAsync(Usuario, "A", TipoSoftware.FacturacionElectronica, IdSoftware, "pin-fe", true, null);
        Assert.Contains((await _e.Admin.ObtenerAsync("A"))!.Software, s => s.TipoSoftware == TipoSoftware.FacturacionElectronica);
    }

    [Fact]
    public async Task La_clave_de_api_se_muestra_una_vez_se_guarda_como_hash_y_se_puede_revocar()
    {
        await ClienteCompleto();
        var proveedor = new StoreTenantConfigProvider(_e.Almacen, _e.Protector);

        var clave = await _e.Admin.GenerarClaveAsync(Usuario, "A", "ERP principal");

        var json = File.ReadAllText(_e.Almacen.RutaArchivo("A", "cliente.json"));
        Assert.DoesNotContain(clave, json);
        Assert.Contains(ClaveApi.Hash(clave), json);

        var tenant = await proveedor.BuscarPorClaveAsync(clave);
        Assert.NotNull(tenant);
        Assert.Equal("900000001", tenant!.Emisor.Nit);
        Assert.Equal("pin-secreto-1", tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)!.PIN);
        Assert.Equal("clave-cert", tenant.Certificado.Clave);
        Assert.True(File.Exists(tenant.Certificado.RutaArchivo));

        var id = (await _e.Admin.ObtenerAsync("A"))!.Claves.Single().Id;
        await _e.Admin.RevocarClaveAsync(Usuario, "A", id);
        Assert.Null(await proveedor.BuscarPorClaveAsync(clave));
    }

    [Fact]
    public async Task Un_cliente_desactivado_no_se_resuelve_aunque_su_clave_sea_valida()
    {
        await ClienteCompleto();
        var proveedor = new StoreTenantConfigProvider(_e.Almacen, _e.Protector);
        var clave = await _e.Admin.GenerarClaveAsync(Usuario, "A", "");

        await _e.Admin.CambiarEstadoAsync(Usuario, "A", false);
        Assert.Null(await proveedor.BuscarPorClaveAsync(clave));

        await _e.Admin.CambiarEstadoAsync(Usuario, "A", true);
        Assert.NotNull(await proveedor.BuscarPorClaveAsync(clave));
    }

    [Fact]
    public async Task La_clave_de_un_cliente_no_abre_a_otro()
    {
        await ClienteCompleto("A", "900000001");
        await ClienteCompleto("B", "900000002");
        var proveedor = new StoreTenantConfigProvider(_e.Almacen, _e.Protector);

        var claveA = await _e.Admin.GenerarClaveAsync(Usuario, "A", "");
        var claveB = await _e.Admin.GenerarClaveAsync(Usuario, "B", "");

        Assert.Equal("A", (await proveedor.BuscarPorClaveAsync(claveA))!.Id);
        Assert.Equal("B", (await proveedor.BuscarPorClaveAsync(claveB))!.Id);
    }

    [Fact]
    public async Task Autorizar_produccion_exige_el_texto_exacto_certificado_y_software()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CambiarProduccionAsync(Usuario, "A", true, ClienteAdminServicio.TextoAutorizacionProduccion));

        await ClienteCompleto("B", "900000002");
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.CambiarProduccionAsync(Usuario, "B", true, "si"));
        Assert.False((await _e.Admin.ObtenerAsync("B"))!.PermiteProduccion);

        await _e.Admin.CambiarProduccionAsync(Usuario, "B", true, ClienteAdminServicio.TextoAutorizacionProduccion);
        Assert.True((await _e.Admin.ObtenerAsync("B"))!.PermiteProduccion);

        await _e.Admin.CambiarProduccionAsync(Usuario, "B", false, null);
        Assert.False((await _e.Admin.ObtenerAsync("B"))!.PermiteProduccion);
    }

    [Fact]
    public async Task El_logo_solo_acepta_png_o_jpg_reales_de_hasta_1_MB()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoAsync(Usuario, "A", "logo.gif", EntornoAdmin.Png));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoAsync(Usuario, "A", "logo.png", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoAsync(Usuario, "A", "logo.png", new byte[2 * 1024 * 1024]));

        await _e.Admin.SubirLogoAsync(Usuario, "A", "Mi Logo.PNG", EntornoAdmin.Png);
        Assert.Equal("logo.png", (await _e.Admin.ObtenerAsync("A"))!.ArchivoLogo);
        Assert.True(File.Exists(_e.Almacen.RutaArchivo("A", "logo.png")));
    }

    [Fact]
    public async Task La_sucursal_guarda_su_perfil_y_su_logo_y_conserva_el_logo_al_actualizar()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        await _e.Admin.GuardarSucursalAsync(Usuario, "A", "MAT-001", "Sucursal Norte", "Responsable de IVA");
        await _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-001", "norte.PNG", EntornoAdmin.Png);

        var sucursal = Assert.Single((await _e.Admin.ObtenerAsync("A"))!.Sucursales);
        Assert.Equal("MAT-001", sucursal.MatriculaMercantil);
        Assert.Equal("Sucursal Norte", sucursal.Nombre);
        Assert.Equal("logo-MAT-001.png", sucursal.ArchivoLogo);
        Assert.True(File.Exists(_e.Almacen.RutaArchivo("A", "logo-MAT-001.png")));

        // Actualizar nombre y régimen no pierde el logo, y la matrícula no distingue mayúsculas.
        await _e.Admin.GuardarSucursalAsync(Usuario, "A", "mat-001", "Sucursal Norte 2", "No responsable");
        sucursal = Assert.Single((await _e.Admin.ObtenerAsync("A"))!.Sucursales);
        Assert.Equal("Sucursal Norte 2", sucursal.Nombre);
        Assert.Equal("logo-MAT-001.png", sucursal.ArchivoLogo);

        // Cambiar el formato del logo reemplaza el archivo anterior.
        await _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-001", "norte.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 });
        Assert.Equal("logo-MAT-001.jpg", (await _e.Admin.ObtenerAsync("A"))!.Sucursales[0].ArchivoLogo);
        Assert.False(File.Exists(_e.Almacen.RutaArchivo("A", "logo-MAT-001.png")));

        // El logo del cliente es independiente del de las sucursales.
        Assert.Null((await _e.Admin.ObtenerAsync("A"))!.ArchivoLogo);
    }

    [Fact]
    public async Task El_webhook_guarda_su_secreto_protegido_y_solo_lo_muestra_cuando_es_nuevo()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        var secreto = await _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "https://erp.ejemplo.com/dian/aviso", regenerarSecreto: false);
        Assert.False(string.IsNullOrEmpty(secreto));

        var cliente = (await _e.Admin.ObtenerAsync("A"))!;
        Assert.Equal("https://erp.ejemplo.com/dian/aviso", cliente.WebhookUrl);
        Assert.NotEqual(secreto, cliente.WebhookSecretoProtegido);          // nunca en claro
        Assert.Equal(secreto, _e.Protector.Desproteger(cliente.WebhookSecretoProtegido!));

        // Cambiar solo la URL conserva el secreto; regenerar lo cambia.
        Assert.Null(await _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "https://erp.ejemplo.com/otro", regenerarSecreto: false));
        Assert.Equal(secreto, _e.Protector.Desproteger((await _e.Admin.ObtenerAsync("A"))!.WebhookSecretoProtegido!));
        var nuevo = await _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "https://erp.ejemplo.com/otro", regenerarSecreto: true);
        Assert.NotEqual(secreto, nuevo);

        // Rechaza URL con usuario y clave o de otro esquema; con la URL vacía se quita el webhook.
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "https://u:p@erp.ejemplo.com/", false));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "ftp://erp.ejemplo.com/", false));
        await _e.Admin.ConfigurarWebhookAsync(Usuario, "A", "", false);
        cliente = (await _e.Admin.ObtenerAsync("A"))!;
        Assert.Null(cliente.WebhookUrl);
        Assert.Null(cliente.WebhookSecretoProtegido);
    }

    [Fact]
    public async Task El_perfil_de_impresion_se_busca_por_matricula_y_cae_al_logo_del_cliente()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor(razon: "CLIENTE A SAS"));
        var logoCliente = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 1, 1, 1 };
        await _e.Admin.SubirLogoAsync(Usuario, "A", "logo.png", logoCliente);
        await _e.Admin.GuardarSucursalAsync(Usuario, "A", "MAT-001", "Sucursal Norte", "Responsable de IVA");
        await _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-001", "norte.png", EntornoAdmin.Png);
        await _e.Admin.GuardarSucursalAsync(Usuario, "A", "MAT-002", "Sucursal Sin Logo", null);
        var cliente = (await _e.Admin.ObtenerAsync("A"))!;

        var norte = PerfilImpresionResolver.Resolver(cliente, "mat-001", _e.Almacen);
        Assert.Equal("Sucursal Norte", norte.Establecimiento);
        Assert.Equal("Responsable de IVA", norte.RegimenFiscal);
        Assert.Equal(EntornoAdmin.Png, norte.Logo);

        // Sin logo propio se usa el del cliente; con matrícula desconocida o vacía, el perfil general.
        Assert.Equal(logoCliente, PerfilImpresionResolver.Resolver(cliente, "MAT-002", _e.Almacen).Logo);
        var general = PerfilImpresionResolver.Resolver(cliente, "NO-EXISTE", _e.Almacen);
        Assert.Equal("CLIENTE A SAS", general.Establecimiento);
        Assert.Equal(string.Empty, general.Matricula);
        Assert.Equal(logoCliente, PerfilImpresionResolver.Resolver(cliente, null, _e.Almacen).Logo);
    }

    [Fact]
    public async Task La_sucursal_valida_matricula_nombre_y_logo_y_se_puede_eliminar_con_su_logo()
    {
        await _e.Admin.CrearAsync(Usuario, "A", Emisor());

        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSucursalAsync(Usuario, "A", "../secreto", "Nombre", null));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSucursalAsync(Usuario, "A", "con espacio", "Nombre", null));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.GuardarSucursalAsync(Usuario, "A", "MAT-002", "  ", null));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "NO-EXISTE", "l.png", EntornoAdmin.Png));

        await _e.Admin.GuardarSucursalAsync(Usuario, "A", "MAT-002", "Sucursal Sur", null);
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-002", "l.gif", EntornoAdmin.Png));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-002", "l.png", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));

        await _e.Admin.SubirLogoSucursalAsync(Usuario, "A", "MAT-002", "l.png", EntornoAdmin.Png);
        Assert.True(File.Exists(_e.Almacen.RutaArchivo("A", "logo-MAT-002.png")));

        await _e.Admin.EliminarSucursalAsync(Usuario, "A", "MAT-002");
        Assert.Empty((await _e.Admin.ObtenerAsync("A"))!.Sucursales);
        Assert.False(File.Exists(_e.Almacen.RutaArchivo("A", "logo-MAT-002.png")));
        await Assert.ThrowsAsync<ErrorAdministracion>(() => _e.Admin.EliminarSucursalAsync(Usuario, "A", "MAT-002"));
    }

    [Fact]
    public async Task La_auditoria_registra_los_cambios_y_nunca_los_secretos()
    {
        await ClienteCompleto();
        var clave = await _e.Admin.GenerarClaveAsync(Usuario, "A", "");

        var texto = string.Join("\n", _e.Auditoria.Ultimas().Select(a => $"{a.Accion} {a.Cliente} {a.Detalle}"));

        Assert.Contains("crear A", texto);
        Assert.Contains("certificado A", texto);
        Assert.Contains("clave-crear A", texto);
        foreach (var secreto in new[] { "pin-secreto-1", "pin-secreto-2", "clave-cert", clave })
        {
            Assert.DoesNotContain(secreto, texto);
        }
    }

    [Fact]
    public void Los_secretos_protegidos_por_una_instancia_los_lee_otra_con_la_misma_carpeta_de_llaves()
    {
        var protegido = _e.Protector.Proteger("secreto");

        Assert.NotEqual("secreto", protegido);
        Assert.Equal("secreto", new ProtectorSecretos(_e.Llaves).Desproteger(protegido));
    }

    [Fact]
    public async Task Un_cliente_dado_de_alta_desde_el_admin_envia_documentos_por_la_api_con_su_clave()
    {
        // MARLIT-como-ejemplo: alta completa y luego un tiquete por la API que lee del mismo almacén.
        await ClienteCompleto("A", "900000001");
        var clave = await _e.Admin.GenerarClaveAsync(Usuario, "A", "ERP");

        var fabrica = new FabricaFalsa();
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Almacen:Carpeta"] = Path.Combine(_e.Raiz, "almacen"),
                ["Secretos:Llaves"] = _e.Llaves,
                ["Datos:Carpeta"] = Path.Combine(_e.Raiz, "datos"),
                ["Dian:EsperaSetSegundos"] = "0"
            }));
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(fabrica)));
        });

        var json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            Converters = { new JsonStringEnumConverter() },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoElectronico/EnviarDE")
        {
            Content = JsonContent.Create(DatosPos.Tiquete("900000001", "CLIENTE A SAS", "EPOS1"), options: json)
        };
        peticion.Headers.Add("x-api-key", clave);
        var respuesta = await app.CreateClient().SendAsync(peticion);

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("CN=Cliente 900000001", fabrica.Llamadas.Single().Sujeto);
        Assert.Single(new[] { "set-1" });

        // Revocada la clave, la API la rechaza.
        await _e.Admin.RevocarClaveAsync(Usuario, "A", (await _e.Admin.ObtenerAsync("A"))!.Claves.Single().Id);
        var rechazada = new HttpRequestMessage(HttpMethod.Get, "/api/Servicio/Quien");
        rechazada.Headers.Add("x-api-key", clave);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().SendAsync(rechazada)).StatusCode);
    }
}
