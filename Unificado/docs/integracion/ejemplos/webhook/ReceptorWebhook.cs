// Receptor de webhook de ejemplo (ASP.NET Core, .NET 8 o superior, API mínima).
// Variable de entorno: DIAN_WEBHOOK_SECRETO = el secreto de firma que le entregó el portal (se muestra una sola vez).
// Ponga este servicio detrás de HTTPS (proxy inverso) y registre su URL en el portal de clientes.
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var secreto = Environment.GetEnvironmentVariable("DIAN_WEBHOOK_SECRETO") ?? throw new InvalidOperationException("Falta DIAN_WEBHOOK_SECRETO");
var procesados = new ConcurrentDictionary<string, bool>(); // idempotencia: en producción, su base de datos

var app = WebApplication.CreateBuilder(args).Build();

app.MapPost("/dian/aviso", async (HttpRequest peticion) =>
{
    // La firma se calcula sobre los BYTES EXACTOS recibidos: lea el cuerpo crudo antes de interpretarlo.
    using var memoria = new MemoryStream();
    await peticion.Body.CopyToAsync(memoria);
    var crudo = memoria.ToArray();

    // La clave HMAC es el TEXTO del secreto en UTF-8 (no se decodifica de Base64).
    var esperada = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), crudo)).ToLowerInvariant();
    var recibida = peticion.Headers["X-Dian-Firma"].ToString();
    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(recibida)))
    {
        return Results.Unauthorized(); // firma inválida: no procesar
    }

    using var documento = JsonDocument.Parse(crudo);
    var raiz = documento.RootElement;
    var llave = $"{raiz.GetProperty("Numero").GetString()}|{raiz.GetProperty("Estado").GetString()}|{raiz.GetProperty("SecuenciaCambio").GetInt64()}";
    if (procesados.TryAdd(llave, true))
    {
        // El aviso es solo una pista: confirme el estado con GET /api/documento/estado/{numero}
        // antes de actuar, y baje el PDF/XML. Haga ese trabajo FUERA de esta petición (cola/trabajador).
        Console.WriteLine($"Aviso: {raiz.GetProperty("Evento").GetString()} {llave} Pdf={raiz.GetProperty("Pdf").GetBoolean()}");
    }

    return Results.Ok(); // cualquier 2xx = entregado; responda rápido (el servicio espera 10 s)
});

app.Run("http://0.0.0.0:8080");
