using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DIAN.Admin.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace Admin.Pruebas;

/// <summary>Canal falso: no sale a la red, guarda el último mensaje y puede simular un gateway caído.</summary>
public sealed class CanalFalso : ICanalWhatsApp
{
    public bool Acepta { get; set; } = true;
    public string? Telefono { get; private set; }
    public string? Mensaje { get; private set; }
    public int Envios { get; private set; }

    /// <summary>El código de seis dígitos del último mensaje.</summary>
    public string Codigo => Regex.Match(Mensaje ?? string.Empty, @"\b(\d{6})\b").Groups[1].Value;

    public Task<bool> EnviarAsync(string telefono, string mensaje, CancellationToken cancelar = default)
    {
        Envios++;
        Telefono = telefono;
        Mensaje = mensaje;
        return Task.FromResult(Acepta);
    }
}

public class SegundoFactorUnitarioTests
{
    [Theory]
    [InlineData("3183837137", "573183837137")]
    [InlineData("318 383 7137", "573183837137")]
    [InlineData("+57 318 383 7137", "573183837137")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normaliza_el_celular_anteponiendo_57_solo_a_los_de_diez_digitos(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, AdminUsuarios.NormalizarTelefono(entrada));
    }

    [Fact]
    public void Enmascarar_solo_deja_ver_los_ultimos_cuatro_digitos()
    {
        var enmascarado = AdminUsuarios.Enmascarar("573183837137");

        Assert.EndsWith("7137", enmascarado);
        Assert.DoesNotContain("573183", enmascarado);
    }

    [Fact]
    public async Task El_codigo_correcto_sirve_una_sola_vez()
    {
        var canal = new CanalFalso();
        var factor = new SegundoFactor(canal);

        var ticket = await factor.IniciarAsync("ana", "573183837137");

        Assert.NotNull(ticket);
        Assert.Equal("ana", factor.UsuarioDe(ticket));
        Assert.Equal(ResultadoSegundoFactor.Correcto, factor.Verificar(ticket, canal.Codigo));
        // Reusar el mismo código (por ejemplo, con el botón «atrás») ya no abre nada.
        Assert.Equal(ResultadoSegundoFactor.Caducado, factor.Verificar(ticket, canal.Codigo));
    }

    [Fact]
    public async Task Tres_codigos_errados_agotan_el_intento()
    {
        var canal = new CanalFalso();
        var factor = new SegundoFactor(canal);
        var ticket = await factor.IniciarAsync("ana", "573183837137");

        Assert.Equal(ResultadoSegundoFactor.Incorrecto, factor.Verificar(ticket, "000000"));
        Assert.Equal(ResultadoSegundoFactor.Incorrecto, factor.Verificar(ticket, "000001"));
        Assert.Equal(ResultadoSegundoFactor.Agotado, factor.Verificar(ticket, "000002"));
        // Agotado el intento, ni siquiera el código bueno vale.
        Assert.Equal(ResultadoSegundoFactor.Caducado, factor.Verificar(ticket, canal.Codigo));
    }

    [Fact]
    public async Task Un_intento_nuevo_invalida_el_codigo_anterior_del_mismo_usuario()
    {
        var canal = new CanalFalso();
        var factor = new SegundoFactor(canal);
        var primero = await factor.IniciarAsync("ana", "573183837137");
        var codigoViejo = canal.Codigo;

        var segundo = await factor.IniciarAsync("ana", "573183837137");

        Assert.Null(factor.UsuarioDe(primero));
        Assert.Equal(ResultadoSegundoFactor.Caducado, factor.Verificar(primero, codigoViejo));
        Assert.Equal(ResultadoSegundoFactor.Correcto, factor.Verificar(segundo, canal.Codigo));
    }

    [Fact]
    public async Task Si_el_gateway_no_acepta_el_mensaje_no_hay_ticket()
    {
        var factor = new SegundoFactor(new CanalFalso { Acepta = false });

        Assert.Null(await factor.IniciarAsync("ana", "573183837137"));
    }

    [Fact]
    public void Sin_clave_de_api_el_canal_se_considera_no_configurado()
    {
        Assert.False(new OpcionesWhatsApp { ClaveApi = string.Empty }.Configurado);
        Assert.True(new OpcionesWhatsApp { ClaveApi = "algo" }.Configurado);
    }

    [Theory]
    [InlineData("clave-con-eñe")]
    [InlineData("clave con espacio")]
    [InlineData("clave-con-tabulador	")]
    public void Una_clave_que_no_cabe_en_una_cabecera_http_cuenta_como_no_configurada(string clave)
    {
        // Pasó en el servidor: el archivo de entorno quedó con otra codificación y HttpClient fallaba con
        // «Request headers must contain only ASCII characters», sin decir qué cabecera.
        Assert.False(new OpcionesWhatsApp { ClaveApi = clave }.Configurado);
    }
}

[Collection("admin-web")]
public class SegundoFactorWebTests : IDisposable
{
    private const string Operador = "operadora";
    private const string Clave = "clave-de-prueba-123";
    private const string Celular = "3183837137";

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-admin-2fa-" + Guid.NewGuid().ToString("N"));
    private readonly CanalFalso _canal = new CanalFalso();
    private readonly WebApplicationFactory<Program> _app;

    public SegundoFactorWebTests()
    {
        Environment.SetEnvironmentVariable("Almacen__Carpeta", Path.Combine(_raiz, "almacen"));
        Environment.SetEnvironmentVariable("Secretos__Llaves", Path.Combine(_raiz, "llaves"));
        Environment.SetEnvironmentVariable("Datos__Carpeta", Path.Combine(_raiz, "datos"));
        // Con clave, el canal cuenta como configurado; el envío real lo sustituye CanalFalso.
        Environment.SetEnvironmentVariable("Whatsapp__ClaveApi", "clave-de-prueba");

        var usuarios = new AdminUsuarios(Path.Combine(_raiz, "almacen"));
        usuarios.Crear(Operador, Clave);
        usuarios.FijarTelefono(Operador, Celular);
        usuarios.Crear("sincelular", Clave);

        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<ICanalWhatsApp>(_canal)));
    }

    public void Dispose()
    {
        _app.Dispose();
        Environment.SetEnvironmentVariable("Almacen__Carpeta", null);
        Environment.SetEnvironmentVariable("Secretos__Llaves", null);
        Environment.SetEnvironmentVariable("Datos__Carpeta", null);
        Environment.SetEnvironmentVariable("Whatsapp__ClaveApi", null);
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    /// <param name="publica">true imita la entrada por internet, que es la que nginx marca.</param>
    private HttpClient Cliente(bool publica)
    {
        var cliente = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (publica)
        {
            cliente.DefaultRequestHeaders.Add("X-Dian-Entrada", "publica");
        }

        return cliente;
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, string ruta, Dictionary<string, string> campos)
    {
        var html = await cliente.GetStringAsync(ruta);
        campos["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return await cliente.PostAsync(ruta, new FormUrlEncodedContent(campos));
    }

    /// <summary>Cuerpo de la respuesta con las entidades HTML resueltas: Razor escapa las tildes.</summary>
    private static async Task<string> Texto(HttpResponseMessage respuesta) =>
        WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

    private static Task<HttpResponseMessage> Ingresar(HttpClient cliente, string usuario = Operador) =>
        Enviar(cliente, "/Login", new() { ["usuario"] = usuario, ["clave"] = Clave });

    [Fact]
    public async Task Por_internet_la_clave_correcta_lleva_al_codigo_y_no_abre_sesion()
    {
        var cliente = Cliente(publica: true);

        var respuesta = await Ingresar(cliente);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Codigo", respuesta.Headers.Location!.OriginalString);
        Assert.Equal(1, _canal.Envios);
        Assert.Equal("57" + Celular, _canal.Telefono);
        // La contraseña nunca viaja en el mensaje y todavía no hay sesión.
        Assert.DoesNotContain(Clave, _canal.Mensaje);
        var inicio = await cliente.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, inicio.StatusCode);
        Assert.Equal("/Login", inicio.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task El_codigo_correcto_abre_la_sesion_y_deja_usar_el_panel_por_internet()
    {
        var cliente = Cliente(publica: true);
        await Ingresar(cliente);

        var respuesta = await Enviar(cliente, "/Codigo", new() { ["codigo"] = _canal.Codigo });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var inicio = await cliente.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, inicio.StatusCode);
    }

    [Fact]
    public async Task El_codigo_errado_no_abre_sesion()
    {
        var cliente = Cliente(publica: true);
        await Ingresar(cliente);

        var respuesta = await Enviar(cliente, "/Codigo", new() { ["codigo"] = "000000" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("incorrecto", await Texto(respuesta));
        var inicio = await cliente.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, inicio.StatusCode);
    }

    [Fact]
    public async Task Sin_pasar_por_el_login_la_pagina_del_codigo_devuelve_al_ingreso()
    {
        var respuesta = await Cliente(publica: true).GetAsync("/Codigo");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Login", respuesta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Un_operador_sin_celular_no_entra_por_internet_y_no_se_le_dice_por_que()
    {
        var cliente = Cliente(publica: true);

        var respuesta = await Ingresar(cliente, "sincelular");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Usuario o contraseña incorrectos", await Texto(respuesta));
        Assert.Equal(0, _canal.Envios);
    }

    [Fact]
    public async Task Si_el_gateway_falla_se_avisa_y_no_se_entra()
    {
        _canal.Acepta = false;
        var cliente = Cliente(publica: true);

        var respuesta = await Ingresar(cliente);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No se pudo enviar el código", await Texto(respuesta));
    }

    [Fact]
    public async Task Volver_al_ingreso_anula_el_codigo_pendiente()
    {
        var cliente = Cliente(publica: true);
        await Ingresar(cliente);
        var codigo = _canal.Codigo;

        await cliente.GetAsync("/Login");

        // El intento de antes ya no existe: la pagina del segundo factor devuelve al ingreso
        // en vez de aceptar el codigo que se envio.
        var respuesta = await cliente.GetAsync("/Codigo");
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Login", respuesta.Headers.Location!.OriginalString);
        Assert.NotEmpty(codigo);
    }

    [Fact]
    public async Task Por_la_red_interna_se_entra_sin_codigo()
    {
        var cliente = Cliente(publica: false);

        var respuesta = await Ingresar(cliente);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(0, _canal.Envios);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Una_sesion_abierta_por_la_red_interna_no_sirve_por_internet()
    {
        // Misma cookie, otra entrada: es el caso que el segundo factor tiene que cerrar.
        var interno = Cliente(publica: false);
        await Ingresar(interno);
        interno.DefaultRequestHeaders.Add("X-Dian-Entrada", "publica");

        var respuesta = await interno.GetAsync("/");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
