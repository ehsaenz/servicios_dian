using System;
using System.Net;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using DIAN.Impresion;
using DIAN.Pos.Api;
using DIAN.Nomina.Servicios;
using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Mismo formato JSON que la API anterior: nombres tal cual, enumeraciones como texto, tildes sin escapar.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Los clientes salen del almacén que administra la app Admin: MySQL (MySql:Cadena) o archivos (Almacen:Carpeta), más
// Secretos:Llaves, las mismas de la app Admin.
// Si no se configura, se usa el índice tenants.json anterior (JsonTenantConfigProvider), que solo sirve para migrar.
// Las carpetas se leen al crear el servicio (no antes) para que las pruebas puedan cambiarlas.
builder.Services.AddSingleton(sp =>
{
    var configuracion = sp.GetRequiredService<IConfiguration>();
    var almacen = configuracion["Almacen:Carpeta"];
    var mysql = configuracion["MySql:Cadena"];
    if (!string.IsNullOrEmpty(almacen) || !string.IsNullOrEmpty(mysql))
    {
        var cache = configuracion["Cache:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "cache");
        return new AlmacenOpcional(AlmacenClientes.Crear(mysql, almacen ?? Path.Combine(AppContext.BaseDirectory, "almacen"), cache));
    }

    return new AlmacenOpcional(null);
});
builder.Services.AddSingleton<ITenantConfigProvider>(sp =>
{
    var configuracion = sp.GetRequiredService<IConfiguration>();
    var almacen = sp.GetRequiredService<AlmacenOpcional>().Almacen;
    if (almacen != null)
    {
        var llaves = configuracion["Secretos:Llaves"] ?? throw new InvalidOperationException("Falta Secretos:Llaves.");
        return new StoreTenantConfigProvider(almacen, new ProtectorSecretos(llaves));
    }

    return new JsonTenantConfigProvider(configuracion["Tenants:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "tenants"));
});
builder.Services.AddHostedService<ReintentosWorker>();
builder.Services.AddSingleton<CertificadoCache>();
builder.Services.AddSingleton<IFabricaEnviadorDian, FabricaEnviadorDian>();
builder.Services.AddSingleton(sp =>
{
    var configuracion = sp.GetRequiredService<IConfiguration>();
    return new ServiciosPos(
        sp.GetRequiredService<CertificadoCache>(),
        sp.GetRequiredService<IFabricaEnviadorDian>(),
        configuracion["Datos:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "datos"),
        sp.GetRequiredService<ILoggerFactory>())
    {
        EsperaSetPruebas = TimeSpan.FromSeconds(configuracion.GetValue("Dian:EsperaSetSegundos", 5.0)),
        EnvioEnSegundoPlano = configuracion.GetValue("Documentos:EnvioEnSegundoPlano", true),
        Salidas = CrearSalidas(sp)
    };
});

builder.Services.AddSingleton(sp => new ServiciosNomina(
    sp.GetRequiredService<CertificadoCache>(),
    sp.GetRequiredService<IFabricaEnviadorDian>(),
    sp.GetRequiredService<IConfiguration>()["Datos:Carpeta"] ?? Path.Combine(AppContext.BaseDirectory, "datos"),
    sp.GetRequiredService<ILoggerFactory>()));

// Detrás de un proxy inverso (nginx) con HTTPS: solo se confía en las cabeceras X-Forwarded-* que llegan del propio equipo.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<TenantMiddleware>();

app.MapGet("/salud", () => Results.Ok(new { estado = "ok" }));
app.MapControllers();

app.Run();

public partial class Program
{
    // Generador de PDF: se carga por nombre de tipo (Pdf:Generador = "Espacio.Tipo, Ensamblado") porque la implementación con
    // DevExpress no forma parte de la solución. Si está configurado y no carga, el servicio no arranca (mejor que emitir sin PDF).
    // El tipo recibe en su constructor (Pdf:Plantillas, Pdf:Fuentes): carpetas con las .repx y las fuentes.
    internal static SalidasDocumento CrearSalidas(IServiceProvider sp)
    {
        var configuracion = sp.GetRequiredService<IConfiguration>();
        var salidas = new SalidasDocumento();

        var generador = configuracion["Pdf:Generador"];
        if (!string.IsNullOrEmpty(generador))
        {
            var carpeta = configuracion["Pdf:Carpeta"];
            if (!string.IsNullOrEmpty(carpeta))
            {
                // Lo normal: el generador vive en su carpeta y su propio contexto de carga (ver CargadorGeneradorPdf).
                salidas.Pdf = CargadorGeneradorPdf.Crear(carpeta, generador, configuracion["Pdf:Plantillas"], configuracion["Pdf:Fuentes"]);
            }
            else
            {
                var tipo = Type.GetType(generador, throwOnError: true)!;
                salidas.Pdf = (IGeneradorPdf)Activator.CreateInstance(tipo, configuracion["Pdf:Plantillas"], configuracion["Pdf:Fuentes"])!;
            }
        }

        var almacen = sp.GetRequiredService<AlmacenOpcional>().Almacen;
        if (almacen != null)
        {
            salidas.Perfil = (tenant, matricula) =>
            {
                var cliente = almacen.ObtenerAsync(tenant.Id).GetAwaiter().GetResult();
                return cliente == null
                    ? new PerfilImpresion { RazonSocial = tenant.Emisor.RazonSocial, Establecimiento = tenant.Emisor.RazonSocial }
                    : PerfilImpresionResolver.Resolver(cliente, matricula, almacen);
            };
        }

        return salidas;
    }
}

/// <summary>El almacén de clientes (de la app Admin), si está configurado; null cuando se usa el índice de archivos antiguo.</summary>
public sealed class AlmacenOpcional
{
    public AlmacenOpcional(ITenantStore? almacen) => Almacen = almacen;
    public ITenantStore? Almacen { get; }
}
