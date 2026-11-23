using System;
using System.Net;
using System.IO;
using System.Linq;
using System.Security.Claims;
using DIAN.Admin.Web;
using DIAN.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Misma carpeta de almacén y de llaves que la API pública (Pos.Api): es lo que las comunica.
var almacen = builder.Configuration["Almacen:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "almacen");
var llaves = builder.Configuration["Secretos:Llaves"] ?? Path.Combine(AppContext.BaseDirectory, "llaves");
var cadenaMySql = builder.Configuration["MySql:Cadena"];
var carpetaCache = builder.Configuration["Cache:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "cache");
var usuarios = new AdminUsuarios(almacen);

// Alta del primer administrador desde la línea de comandos, sin dejar la contraseña en ningún archivo de configuración:
//   dotnet Admin.Web.dll crear-admin <usuario>      (la contraseña se pide por consola o viene en ADMIN_CLAVE)
if (args.Length >= 2 && args[0] == "crear-admin")
{
    var clave = Environment.GetEnvironmentVariable("ADMIN_CLAVE");
    if (string.IsNullOrEmpty(clave))
    {
        Console.Write("Contraseña (mínimo " + AdminUsuarios.ClaveMinima + " caracteres): ");
        clave = Console.ReadLine();
    }

    usuarios.Crear(args[1], clave ?? string.Empty);
    new Auditoria(almacen).Registrar("consola", "admin-crear", "", args[1]);
    Console.WriteLine("Administrador " + args[1] + " creado.");
    return;
}

// Celular del segundo factor de un operador (WhatsApp). Sin argumento de teléfono, lo quita:
//   dotnet Admin.Web.dll telefono-admin <usuario> [<celular>]
if (args.Length >= 2 && args[0] == "telefono-admin")
{
    var telefono = args.Length >= 3 ? args[2] : null;
    if (!usuarios.FijarTelefono(args[1], telefono))
    {
        Console.Error.WriteLine("No existe el usuario " + args[1] + ".");
        Environment.ExitCode = 1;
        return;
    }

    var guardado = usuarios.TelefonoDe(args[1]);
    new Auditoria(almacen).Registrar("consola", "admin-telefono", "", guardado == null ? "sin celular" : AdminUsuarios.Enmascarar(guardado));
    Console.WriteLine(guardado == null
        ? "Se quitó el celular de " + args[1] + "."
        : "Celular de " + args[1] + ": " + AdminUsuarios.Enmascarar(guardado) + ".");
    return;
}

// Esquema y migración al almacén de MySQL (requieren MySql:Cadena, por ejemplo en la variable MySql__Cadena):
//   dotnet Admin.Web.dll crear-esquema            crea las tablas si no existen
//   dotnet Admin.Web.dll migrar-almacen           copia los clientes del almacén de archivos (Almacen:Carpeta) a MySQL
if (args.Length >= 1 && (args[0] == "crear-esquema" || args[0] == "migrar-almacen"))
{
    if (string.IsNullOrWhiteSpace(cadenaMySql))
    {
        Console.Error.WriteLine("Falta la cadena de conexión (MySql:Cadena o la variable de entorno MySql__Cadena).");
        Environment.ExitCode = 1;
        return;
    }

    await MySqlTenantStore.CrearEsquemaAsync(cadenaMySql);
    Console.WriteLine("Esquema listo.");
    if (args[0] == "migrar-almacen")
    {
        var copiados = await MigradorMySql.MigrarAsync(new FileTenantStore(almacen), AlmacenClientes.Crear(cadenaMySql, almacen, carpetaCache));
        Console.WriteLine("Clientes copiados: " + copiados);
    }

    return;
}

// Alta de un cliente desde un archivo de configuración en el formato de la API anterior, con su certificado:
//   dotnet Admin.Web.dll importar-cliente <id> <config.json> [--clave-cert-env=VARIABLE] [--generar-clave=true]
// Las opciones propias van en la forma --opcion=valor: el lector de configuración de .NET toma "--opcion valor" como un par
// y se comería el argumento siguiente (por ejemplo --Almacen:Carpeta). Con --generar-clave=true la última línea de la
// salida es "CLAVE_API=<clave>" (se muestra una sola vez).
if (args.Length >= 3 && args[0] == "importar-cliente")
{
    var servicio = new ClienteAdminServicio(AlmacenClientes.Crear(cadenaMySql, almacen, carpetaCache), new ProtectorSecretos(llaves), new Auditoria(almacen));
    const string prefijoVariable = "--clave-cert-env=";
    var variable = args.FirstOrDefault(a => a.StartsWith(prefijoVariable, StringComparison.Ordinal))?.Substring(prefijoVariable.Length);
    try
    {
        var claveApi = await ImportadorCliente.ImportarAsync(servicio, "consola", args[1], args[2], variable, args.Contains("--generar-clave=true"));
        Console.WriteLine("Cliente " + args[1] + " importado.");
        if (claveApi != null)
        {
            Console.WriteLine("CLAVE_API=" + claveApi);
        }
    }
    catch (ErrorAdministracion ex)
    {
        Console.Error.WriteLine("No se pudo importar: " + ex.Message);
        Environment.ExitCode = 1;
    }

    return;
}

builder.Services.AddSingleton(usuarios);
builder.Services.AddSingleton(new LectorDocumentos(builder.Configuration["Datos:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "datos")));
builder.Services.AddSingleton<ITenantStore>(_ => AlmacenClientes.Crear(cadenaMySql, almacen, carpetaCache));
builder.Services.AddSingleton<ISecretProtector>(_ => new ProtectorSecretos(llaves));
builder.Services.AddSingleton(_ => new Auditoria(almacen));
builder.Services.AddSingleton<ClienteAdminServicio>();

// Segundo factor del operador por WhatsApp (gateway de SISTEMAS 360).
builder.Services.AddHttpClient("whatsapp");
builder.Services.AddSingleton(OpcionesWhatsApp.Desde(builder.Configuration));
builder.Services.AddSingleton<ICanalWhatsApp, CanalWhatsApp>();
builder.Services.AddSingleton<SegundoFactor>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.Cookie.Name = "dian-admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// La cookie antifalsificación también debe ser "Secure" cuando la petición llega por HTTPS (detrás del proxy).
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.HttpOnly = true;
});

builder.Services.AddRazorPages(o => {
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Codigo");
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 2 * 1024 * 1024);

// Detrás de un proxy inverso (nginx) con HTTPS: solo se confía en las cabeceras X-Forwarded-* que llegan del propio equipo.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers["X-Content-Type-Options"] = "nosniff";
    contexto.Response.Headers["X-Frame-Options"] = "DENY";
    contexto.Response.Headers["Referrer-Policy"] = "no-referrer";
    contexto.Response.Headers["Cache-Control"] = "no-store";
    if (contexto.Request.IsHttps)
    {
        contexto.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
    }

    contexto.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:";
    await siguiente();
});

app.UseAuthentication();

// Una sesión de operador (por ejemplo, una cookie compartida con el puerto interno) no sirve en la entrada pública,
// salvo que se haya abierto comprobando el código de WhatsApp.
var operadorSoloInterno = app.Configuration.GetValue("Portal:OperadorSoloInterno", true);
app.Use(async (contexto, siguiente) =>
{
    if (operadorSoloInterno && Roles.EntradaPublica(contexto.Request) && contexto.User.EsOperador() && !contexto.User.ConSegundoFactor())
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
        await contexto.Response.WriteAsync("El panel del operador solo está disponible desde la red interna.");
        return;
    }

    await siguiente();
});

app.UseAuthorization();

app.MapGet("/logo/{id}", async (string id, ITenantStore almacenClientes, System.Security.Claims.ClaimsPrincipal quien) =>
{
    if (!quien.PuedeVer(id))
    {
        return Results.NotFound();
    }

    var cliente = await almacenClientes.ObtenerAsync(id);
    if (cliente?.ArchivoLogo == null)
    {
        return Results.NotFound();
    }

    var ruta = almacenClientes.RutaArchivo(id, cliente.ArchivoLogo);
    return File.Exists(ruta)
        ? Results.File(File.ReadAllBytes(ruta), cliente.ArchivoLogo.EndsWith(".png") ? "image/png" : "image/jpeg")
        : Results.NotFound();
});

app.MapGet("/logo/{id}/{matricula}", async (string id, string matricula, ITenantStore almacenClientes, System.Security.Claims.ClaimsPrincipal quien) =>
{
    if (!quien.PuedeVer(id))
    {
        return Results.NotFound();
    }

    var sucursal = (await almacenClientes.ObtenerAsync(id))?.Sucursales
        .FirstOrDefault(s => string.Equals(s.MatriculaMercantil, matricula, StringComparison.OrdinalIgnoreCase));
    if (sucursal?.ArchivoLogo == null)
    {
        return Results.NotFound();
    }

    var ruta = almacenClientes.RutaArchivo(id, sucursal.ArchivoLogo);
    return File.Exists(ruta)
        ? Results.File(File.ReadAllBytes(ruta), sucursal.ArchivoLogo.EndsWith(".png") ? "image/png" : "image/jpeg")
        : Results.NotFound();
});

app.MapGet("/pdf/{id}/{numero}", (string id, string numero, LectorDocumentos lector, System.Security.Claims.ClaimsPrincipal quien) =>
{
    if (!quien.PuedeVer(id))
    {
        return Results.NotFound();
    }

    var pdf = lector.Pdf(id, numero);
    return pdf == null ? Results.NotFound() : Results.File(pdf, "application/pdf", numero + ".pdf");
});

app.MapRazorPages();

if (!usuarios.HayUsuarios())
{
    app.Logger.LogWarning("No hay administradores. Cree el primero con: dotnet Admin.Web.dll crear-admin <usuario>");
}

app.Run();

public partial class Program
{
}
