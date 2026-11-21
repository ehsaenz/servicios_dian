using System.Net;
using System.Text.RegularExpressions;
using DIAN.Admin.Web;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Admin.Pruebas;

public class AdminUsuariosTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-admin-usr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Valida_la_clave_correcta_y_rechaza_la_incorrecta_sin_guardarla_en_claro()
    {
        var usuarios = new AdminUsuarios(_raiz);
        usuarios.Crear("ana", "una-clave-bien-larga");

        Assert.True(await usuarios.ValidarAsync("ana", "una-clave-bien-larga"));
        Assert.True(await usuarios.ValidarAsync("ANA", "una-clave-bien-larga"));
        Assert.False(await usuarios.ValidarAsync("ana", "otra-clave-distinta"));
        Assert.False(await usuarios.ValidarAsync("nadie", "una-clave-bien-larga"));
        Assert.DoesNotContain("una-clave-bien-larga", File.ReadAllText(Path.Combine(_raiz, "admin-usuarios.json")));
    }

    [Fact]
    public async Task Tras_cinco_intentos_fallidos_se_bloquea_incluso_con_la_clave_correcta()
    {
        var usuarios = new AdminUsuarios(_raiz);
        usuarios.Crear("ana", "una-clave-bien-larga");

        for (var i = 0; i < 5; i++)
        {
            Assert.False(await usuarios.ValidarAsync("ana", "mala-" + i));
        }

        Assert.True(usuarios.EstaBloqueado("ana"));
        Assert.False(await usuarios.ValidarAsync("ana", "una-clave-bien-larga"));
    }

    [Theory]
    [InlineData("ana", "corta")]
    [InlineData("", "una-clave-bien-larga")]
    [InlineData("a b", "una-clave-bien-larga")]
    public void No_acepta_usuarios_ni_claves_debiles(string usuario, string clave)
    {
        Assert.Throws<ArgumentException>(() => new AdminUsuarios(_raiz).Crear(usuario, clave));
    }
}

public class AdminWebTests : IDisposable
{
    private const string Usuario = "tester";
    private const string Clave = "clave-de-prueba-123";
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-admin-web-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _app;

    public AdminWebTests()
    {
        // Program lee la configuración al arrancar; por eso se usan variables de entorno y no la configuración en memoria.
        Environment.SetEnvironmentVariable("Almacen__Carpeta", Path.Combine(_raiz, "almacen"));
        Environment.SetEnvironmentVariable("Secretos__Llaves", Path.Combine(_raiz, "llaves"));
        Environment.SetEnvironmentVariable("Datos__Carpeta", Path.Combine(_raiz, "datos"));
        new AdminUsuarios(Path.Combine(_raiz, "almacen")).Crear(Usuario, Clave);
        _app = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _app.Dispose();
        Environment.SetEnvironmentVariable("Almacen__Carpeta", null);
        Environment.SetEnvironmentVariable("Secretos__Llaves", null);
        Environment.SetEnvironmentVariable("Datos__Carpeta", null);
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    private HttpClient Cliente() => _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> Token(HttpClient cliente, string ruta)
    {
        var html = await cliente.GetStringAsync(ruta);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, string rutaToken, string rutaPost, Dictionary<string, string> campos)
    {
        campos["__RequestVerificationToken"] = await Token(cliente, rutaToken);
        return await cliente.PostAsync(rutaPost, new FormUrlEncodedContent(campos));
    }

    private async Task<HttpClient> ConSesion()
    {
        var cliente = Cliente();
        var respuesta = await Enviar(cliente, "/Login", "/Login", new() { ["usuario"] = Usuario, ["clave"] = Clave });
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Auditoria")]
    [InlineData("/Cliente/ALGUNO")]
    [InlineData("/logo/ALGUNO")]
    public async Task Sin_sesion_todo_redirige_al_login(string ruta)
    {
        var respuesta = await Cliente().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Login", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Un_formulario_sin_token_antifalsificacion_se_rechaza()
    {
        var respuesta = await Cliente().PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["usuario"] = Usuario, ["clave"] = Clave }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_clave_incorrecta_no_entra_y_tras_cinco_intentos_bloquea()
    {
        var cliente = Cliente();
        HttpResponseMessage respuesta = null!;
        for (var i = 0; i < 5; i++)
        {
            respuesta = await Enviar(cliente, "/Login", "/Login", new() { ["usuario"] = Usuario, ["clave"] = "mala-" + i });
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        Assert.Contains("bloqueado", await respuesta.Content.ReadAsStringAsync());
        var buena = await Enviar(cliente, "/Login", "/Login", new() { ["usuario"] = Usuario, ["clave"] = Clave });
        Assert.Equal(HttpStatusCode.OK, buena.StatusCode);
    }

    [Fact]
    public async Task Las_respuestas_llevan_las_cabeceras_de_seguridad()
    {
        var respuesta = await Cliente().GetAsync("/Login");

        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", respuesta.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Con_sesion_se_crea_un_cliente_se_genera_su_clave_una_vez_y_queda_auditado()
    {
        var cliente = await ConSesion();

        var alta = await Enviar(cliente, "/", "/?handler=Crear", new() { ["id"] = "MARLIT", ["nit"] = "901033307", ["dv"] = "6", ["razon"] = "MARLIT GROUP S.A.S." });
        Assert.Equal(HttpStatusCode.Redirect, alta.StatusCode);
        Assert.Equal("/Cliente/MARLIT", alta.Headers.Location!.OriginalString);

        var lista = await cliente.GetStringAsync("/");
        Assert.Contains("MARLIT GROUP S.A.S.", lista);

        var generada = await Enviar(cliente, "/Cliente/MARLIT", "/Cliente/MARLIT?handler=GenerarClave", new() { ["descripcion"] = "ERP" });
        var html = await generada.Content.ReadAsStringAsync();
        var clave = Regex.Match(html, "class=\"clave\">([0-9a-f]{64})<").Groups[1].Value;
        Assert.Equal(64, clave.Length);

        // La clave no queda guardada: solo su hash; y al volver a abrir la página ya no aparece.
        var archivo = File.ReadAllText(Path.Combine(_raiz, "almacen", "clientes", "MARLIT", "cliente.json"));
        Assert.DoesNotContain(clave, archivo);
        Assert.Contains(ClaveApi.Hash(clave), archivo);
        Assert.DoesNotContain(clave, await cliente.GetStringAsync("/Cliente/MARLIT"));

        var auditoria = await cliente.GetStringAsync("/Auditoria");
        Assert.Contains("clave-crear", auditoria);
        Assert.DoesNotContain(clave, auditoria);
    }

    [Fact]
    public async Task Dar_de_alta_un_id_repetido_o_invalido_muestra_el_error_sin_romper_la_pagina()
    {
        var cliente = await ConSesion();
        await Enviar(cliente, "/", "/?handler=Crear", new() { ["id"] = "AB", ["nit"] = "900000001", ["dv"] = "1", ["razon"] = "A SAS" });

        var repetido = await Enviar(cliente, "/", "/?handler=Crear", new() { ["id"] = "AB", ["nit"] = "900000001", ["dv"] = "1", ["razon"] = "A SAS" });
        var invalido = await Enviar(cliente, "/", "/?handler=Crear", new() { ["id"] = "a b", ["nit"] = "900000001", ["dv"] = "1", ["razon"] = "A SAS" });

        Assert.Contains("Ya existe", await repetido.Content.ReadAsStringAsync());
        Assert.Contains("solo admite letras", await invalido.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_cliente_inexistente_responde_404_y_el_html_se_escapa()
    {
        var cliente = await ConSesion();
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/Cliente/NOEXISTE")).StatusCode);

        await Enviar(cliente, "/", "/?handler=Crear", new() { ["id"] = "XSS", ["nit"] = "900000001", ["dv"] = "1", ["razon"] = "<script>alert(1)</script>" });
        var pagina = await cliente.GetStringAsync("/Cliente/XSS");

        Assert.DoesNotContain("<script>alert(1)</script>", pagina);
        Assert.Contains("&lt;script&gt;", pagina);
    }

    // ---- Portal de clientes: un usuario de cliente solo ve y opera lo suyo ----

    /// <summary>Crea dos clientes y un usuario de portal para el primero; devuelve el cliente HTTP ya con su sesión.</summary>
    private async Task<(HttpClient Operador, HttpClient Portal, string Clave)> ConPortal()
    {
        var operador = await ConSesion();
        await Enviar(operador, "/", "/?handler=Crear", new() { ["id"] = "UNO", ["nit"] = "900000001", ["dv"] = "8", ["razon"] = "UNO SAS" });
        await Enviar(operador, "/", "/?handler=Crear", new() { ["id"] = "DOS", ["nit"] = "900000002", ["dv"] = "5", ["razon"] = "DOS SAS" });

        var alta = await Enviar(operador, "/Cliente/UNO", "/Cliente/UNO?handler=CrearUsuario", new() { ["usuario"] = "ana-uno" });
        var clave = Regex.Match(await alta.Content.ReadAsStringAsync(), "class=\"clave\">([^<]+)<").Groups[1].Value;
        Assert.True(clave.Length >= AdminUsuarios.ClaveMinima);

        var portal = Cliente();
        var entrada = await Enviar(portal, "/Login", "/Login", new() { ["usuario"] = "ana-uno", ["clave"] = clave });
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        return (operador, portal, clave);
    }

    [Fact]
    public async Task Un_usuario_de_cliente_cae_directo_en_su_cliente_y_no_ve_los_ajenos()
    {
        var (_, portal, _) = await ConPortal();

        var inicio = await portal.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, inicio.StatusCode);
        Assert.Equal("/Cliente/UNO", inicio.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/Cliente/DOS")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/logo/DOS")).StatusCode);

        var propia = await portal.GetStringAsync("/Cliente/UNO");
        Assert.Contains("Webhook de documentos", propia);
        Assert.Contains("Claves de API", propia);
        Assert.DoesNotContain("Autorizar producción", propia);
        Assert.DoesNotContain("Certificado de firma", propia);
        Assert.DoesNotContain("Usuarios del portal", propia);
    }

    [Fact]
    public async Task Un_usuario_de_cliente_puede_generar_su_clave_y_poner_su_webhook_pero_no_tocar_lo_del_operador()
    {
        var (_, portal, _) = await ConPortal();

        var clave = await Enviar(portal, "/Cliente/UNO", "/Cliente/UNO?handler=GenerarClave", new() { ["descripcion"] = "ERP" });
        Assert.Equal(HttpStatusCode.OK, clave.StatusCode);
        Assert.Matches("class=\"clave\">[0-9a-f]{64}<", await clave.Content.ReadAsStringAsync());

        var webhook = await Enviar(portal, "/Cliente/UNO", "/Cliente/UNO?handler=Webhook", new() { ["url"] = "https://erp.ejemplo.com/aviso" });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        // Acciones reservadas al operador: se rechazan aunque la petición se arme a mano.
        foreach (var (accion, campos) in new[]
        {
            ("Produccion", new Dictionary<string, string> { ["permitir"] = "true", ["confirmacion"] = "x" }),
            ("Estado", new Dictionary<string, string> { ["activo"] = "false" }),
            ("Emisor", new Dictionary<string, string> { ["nit"] = "1", ["razon"] = "X" }),
            ("CrearUsuario", new Dictionary<string, string> { ["usuario"] = "intruso" })
        })
        {
            var respuesta = await Enviar(portal, "/Cliente/UNO", "/Cliente/UNO?handler=" + accion, campos);
            Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);
        }

        var archivo = File.ReadAllText(Path.Combine(_raiz, "almacen", "clientes", "UNO", "cliente.json"));
        Assert.Contains("erp.ejemplo.com/aviso", archivo);
        Assert.DoesNotContain("\"PermiteProduccion\": true", archivo);
        Assert.Contains("\"Activo\": true", archivo);
        Assert.DoesNotContain("intruso", File.ReadAllText(Path.Combine(_raiz, "almacen", "admin-usuarios.json")));

        // Ni escribir en el cliente ajeno ni entrar a la auditoría.
        var ajeno = await portal.PostAsync("/Cliente/DOS?handler=GenerarClave", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(portal, "/Cliente/UNO") }));
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await portal.GetAsync("/Auditoria")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await Enviar(portal, "/Cliente/UNO", "/?handler=Crear", new() { ["id"] = "NUEVO", ["nit"] = "9", ["razon"] = "X" })).StatusCode);
    }

    private void SembrarDocumento(string cliente, string numero, int estado, bool conPdf)
    {
        var carpeta = Path.Combine(_raiz, "datos", cliente, "documentos");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, numero + ".json"),
            $"{{\"Numero\":\"{numero}\",\"TipoDocErp\":\"FV\",\"Estado\":{estado},\"Pdf\":{(conPdf ? "true" : "false")},\"Mensaje\":\"msg-{numero}\",\"Habilitacion\":true,\"FechaActualizado\":\"2026-11-08T10:00:00Z\"}}");
        if (conPdf)
        {
            File.WriteAllBytes(Path.Combine(carpeta, numero + ".pdf"), System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 de " + cliente));
        }
    }

    [Fact]
    public async Task El_portal_lista_los_documentos_y_baja_el_pdf_solo_del_propio_cliente()
    {
        var (operador, portal, _) = await ConPortal();
        SembrarDocumento("UNO", "SETP1", 1, true);
        SembrarDocumento("UNO", "SETP2", 2, false);
        SembrarDocumento("DOS", "SETP9", 1, true);
        // Un archivo fuera de la carpeta del cliente que alguien intenta alcanzar con el número.
        File.WriteAllBytes(Path.Combine(_raiz, "datos", "secreto.pdf"), System.Text.Encoding.ASCII.GetBytes("no debe salir"));

        var pagina = await portal.GetStringAsync("/Cliente/UNO");
        Assert.Contains("SETP1", pagina);
        Assert.Contains("SETP2", pagina);
        Assert.Contains("Aprobado", pagina);
        Assert.Contains("Rechazado", pagina);
        Assert.DoesNotContain("SETP9", pagina);
        Assert.Contains("/pdf/UNO/SETP1", pagina);
        Assert.DoesNotContain("/pdf/UNO/SETP2", pagina);

        var pdf = await portal.GetAsync("/pdf/UNO/SETP1");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Contains("%PDF", await pdf.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/pdf/DOS/SETP9")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/pdf/UNO/SETP2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/pdf/UNO/..%2Fsecreto")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync("/pdf/UNO/..%5C..%5Csecreto")).StatusCode);

        // El operador ve el de cualquiera.
        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/pdf/DOS/SETP9")).StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarPorEntradaPublica(HttpClient cliente, string usuario, string clave)
    {
        var token = await Token(cliente, "/Login");
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["usuario"] = usuario, ["clave"] = clave, ["__RequestVerificationToken"] = token })
        };
        peticion.Headers.Add("X-Dian-Entrada", "publica");
        return await cliente.SendAsync(peticion);
    }

    [Fact]
    public async Task El_operador_no_puede_entrar_por_la_entrada_publica_pero_el_cliente_si()
    {
        var (_, _, clavePortal) = await ConPortal();

        // Operador por la entrada pública: se rechaza como una contraseña mala, sin sesión.
        var operador = Cliente();
        var rechazo = await EnviarPorEntradaPublica(operador, Usuario, Clave);
        Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
        Assert.Contains("incorrectos", await rechazo.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await operador.GetAsync("/")).StatusCode);
        Assert.Equal("/Login", (await operador.GetAsync("/")).Headers.Location!.AbsolutePath);

        // Cliente por la entrada pública: entra.
        var portal = Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await EnviarPorEntradaPublica(portal, "ana-uno", clavePortal)).StatusCode);

        // Una cookie de operador obtenida por la entrada interna no sirve en la pública.
        var interno = await ConSesion();
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/");
        peticion.Headers.Add("X-Dian-Entrada", "publica");
        Assert.Equal(HttpStatusCode.Forbidden, (await interno.SendAsync(peticion)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await interno.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Autorizar_produccion_con_la_lista_incompleta_se_rechaza_en_el_servidor()
    {
        var (operador, _, _) = await ConPortal();

        var pagina = await operador.GetStringAsync("/Cliente/UNO");
        Assert.Contains("Camino a producci", pagina);
        Assert.Contains("Set de pruebas de la DIAN", pagina);

        var respuesta = await Enviar(operador, "/Cliente/UNO", "/Cliente/UNO?handler=Produccion",
            new() { ["permitir"] = "true", ["confirmacion"] = ClienteAdminServicio.TextoAutorizacionProduccion });

        Assert.Contains("lista de verificaci", await respuesta.Content.ReadAsStringAsync());
        Assert.DoesNotContain("\"PermiteProduccion\": true", File.ReadAllText(Path.Combine(_raiz, "almacen", "clientes", "UNO", "cliente.json")));
    }

    [Fact]
    public async Task El_operador_puede_restablecer_y_eliminar_usuarios_de_portal_y_el_usuario_cambia_su_clave()
    {
        var (operador, portal, clave) = await ConPortal();

        var cambio = await Enviar(portal, "/Cuenta", "/Cuenta", new() { ["actual"] = clave, ["nueva"] = "otra-clave-bien-larga", ["repetir"] = "otra-clave-bien-larga" });
        Assert.Contains("cambiada", await cambio.Content.ReadAsStringAsync());
        var mala = await Enviar(portal, "/Cuenta", "/Cuenta", new() { ["actual"] = "no-es-esta", ["nueva"] = "otra-clave-bien-larga", ["repetir"] = "otra-clave-bien-larga" });
        Assert.Contains("no es correcta", await mala.Content.ReadAsStringAsync());

        var reinicio = await Enviar(operador, "/Cliente/UNO", "/Cliente/UNO?handler=RestablecerUsuario", new() { ["usuario"] = "ana-uno" });
        Assert.Contains("restablecida", await reinicio.Content.ReadAsStringAsync());

        var baja = await Enviar(operador, "/Cliente/UNO", "/Cliente/UNO?handler=EliminarUsuario", new() { ["usuario"] = "ana-uno" });
        Assert.Contains("Usuario eliminado", await baja.Content.ReadAsStringAsync());
        Assert.Empty(new AdminUsuarios(Path.Combine(_raiz, "almacen")).DeCliente("UNO"));
    }
}

public class AdminUsuariosRolesTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "dian-admin-rol-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_raiz, true); } catch (IOException) { }
    }

    [Fact]
    public void Un_usuario_de_cliente_no_puede_pisar_a_un_operador_ni_al_reves()
    {
        var usuarios = new AdminUsuarios(_raiz);
        usuarios.Crear("jefe", "una-clave-bien-larga");
        usuarios.Crear("ana", "una-clave-bien-larga", "UNO");

        Assert.Throws<ArgumentException>(() => usuarios.Crear("jefe", "una-clave-bien-larga", "UNO"));
        Assert.Throws<ArgumentException>(() => usuarios.Crear("ana", "una-clave-bien-larga"));
        Assert.Throws<ArgumentException>(() => usuarios.Crear("ana", "una-clave-bien-larga", "DOS"));
        Assert.Null(usuarios.ClienteDe("jefe"));
        Assert.Equal("UNO", usuarios.ClienteDe("ANA"));
        Assert.Equal(new[] { "ana" }, usuarios.DeCliente("UNO"));
    }

    [Fact]
    public void Eliminar_de_un_cliente_nunca_borra_a_un_operador_ni_a_un_usuario_de_otro_cliente()
    {
        var usuarios = new AdminUsuarios(_raiz);
        usuarios.Crear("jefe", "una-clave-bien-larga");
        usuarios.Crear("ana", "una-clave-bien-larga", "UNO");

        Assert.False(usuarios.EliminarDeCliente("UNO", "jefe"));
        Assert.False(usuarios.EliminarDeCliente("DOS", "ana"));
        Assert.True(usuarios.EliminarDeCliente("UNO", "ana"));
        Assert.True(usuarios.HayUsuarios());
    }

    [Fact]
    public void Las_claves_generadas_son_largas_y_distintas()
    {
        var a = AdminUsuarios.GenerarClave();
        var b = AdminUsuarios.GenerarClave();

        Assert.True(a.Length >= AdminUsuarios.ClaveMinima);
        Assert.NotEqual(a, b);
    }
}

public class ListaProduccionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static ClienteRegistro Completo() => new()
    {
        Id = "UNO",
        Emisor = new EmisorConfig { Nit = "900000001", DV = "8", RazonSocial = "UNO SAS", MatriculaMercantil = "1", Email = "a@b.co", NumeroTel = "300" },
        ArchivoCertificado = "cert.p12",
        CertificadoVenceUtc = Ahora.AddDays(200),
        ArchivoLogo = "logo.png",
        Software = { new SoftwareRegistro { TipoSoftware = TipoSoftware.FacturacionElectronica, Identificador = Guid.NewGuid().ToString(), AmbientePruebas = false } },
        Claves = { new ClaveRegistro { Activa = true } }
    };

    private static DocumentoVista Doc(string tipo, int estado = 1, bool habilitacion = true) =>
        new() { Numero = tipo + Guid.NewGuid().ToString("N")[..4], TipoDocDian = tipo, Estado = estado, Habilitacion = habilitacion };

    [Fact]
    public void Un_cliente_completo_con_el_set_aprobado_no_tiene_impedimentos()
    {
        var lista = ListaProduccion.Evaluar(Completo(), new[] { Doc("01"), Doc("91"), Doc("92") }, Ahora);

        Assert.All(lista, i => Assert.Equal(ResultadoItem.Cumplido, i.Resultado));
        Assert.Null(ListaProduccion.Impedimentos(lista));
    }

    [Fact]
    public void Un_cliente_nuevo_tiene_pendiente_todo_lo_bloqueante()
    {
        var nuevo = new ClienteRegistro { Id = "N", Emisor = new EmisorConfig { Nit = "900000002", RazonSocial = "N SAS" } };

        var impedimentos = ListaProduccion.Impedimentos(ListaProduccion.Evaluar(nuevo, Array.Empty<DocumentoVista>(), Ahora))!;

        foreach (var esperado in new[] { "Datos del emisor", "Certificado de firma", "Software de facturación electrónica", "Set de pruebas de la DIAN", "Software de producción" })
        {
            Assert.Contains(esperado, impedimentos);
        }
    }

    [Fact]
    public void Solo_cuentan_las_facturas_aprobadas_en_habilitacion()
    {
        var docs = new[] { Doc("01", estado: 2), Doc("01", estado: 3), Doc("01", habilitacion: false) };

        var set = ListaProduccion.Evaluar(Completo(), docs, Ahora).Single(i => i.Titulo.StartsWith("Set de pruebas"));

        Assert.Equal(ResultadoItem.Pendiente, set.Resultado);
    }

    [Fact]
    public void Sin_notas_el_set_es_aviso_y_un_certificado_vencido_o_por_vencer_se_distingue()
    {
        var c = Completo();
        Assert.Equal(ResultadoItem.Aviso, ListaProduccion.Evaluar(c, new[] { Doc("01") }, Ahora).Single(i => i.Titulo.StartsWith("Set de pruebas")).Resultado);

        c.CertificadoVenceUtc = Ahora.AddDays(10);
        Assert.Equal(ResultadoItem.Aviso, ListaProduccion.Evaluar(c, new[] { Doc("01") }, Ahora).Single(i => i.Titulo == "Certificado de firma").Resultado);

        c.CertificadoVenceUtc = Ahora.AddDays(-1);
        Assert.Equal(ResultadoItem.Pendiente, ListaProduccion.Evaluar(c, new[] { Doc("01") }, Ahora).Single(i => i.Titulo == "Certificado de firma").Resultado);
    }

    [Fact]
    public void Con_el_software_aun_en_pruebas_falta_el_de_produccion()
    {
        var c = Completo();
        c.Software[0].AmbientePruebas = true;

        var item = ListaProduccion.Evaluar(c, new[] { Doc("01"), Doc("91"), Doc("92") }, Ahora).Single(i => i.Titulo == "Software de producción");

        Assert.Equal(ResultadoItem.Pendiente, item.Resultado);
    }

    [Fact]
    public void Un_cliente_solo_con_POS_o_Nomina_en_produccion_no_queda_bloqueado_por_facturacion_electronica()
    {
        var c = Completo();
        c.Software.Clear();
        c.Software.Add(new SoftwareRegistro { TipoSoftware = TipoSoftware.DocumentoEquivalente, Identificador = Guid.NewGuid().ToString(), AmbientePruebas = false });
        c.Software.Add(new SoftwareRegistro { TipoSoftware = TipoSoftware.NominaElectronica, Identificador = Guid.NewGuid().ToString(), AmbientePruebas = false });

        var lista = ListaProduccion.Evaluar(c, Array.Empty<DocumentoVista>(), Ahora);

        Assert.Null(ListaProduccion.Impedimentos(lista));
        Assert.DoesNotContain(lista, i => i.Titulo == "Software de facturación electrónica");
        Assert.Equal(ResultadoItem.Aviso, lista.Single(i => i.Titulo == "Set de pruebas de documento equivalente (POS)").Resultado);
        Assert.Equal(ResultadoItem.Aviso, lista.Single(i => i.Titulo == "Set de pruebas de nómina electrónica").Resultado);
    }
}
