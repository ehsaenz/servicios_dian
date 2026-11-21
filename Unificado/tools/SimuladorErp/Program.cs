// Simulador del ERP para probar la API de DIAN (Pos.Api).
//
// Hace lo mismo que un ERP real: envía JSON con la cabecera x-api-key, pide el estado y lee los cambios.
// No genera documentos: el documento lo arma el ERP (o el archivo JSON que se le pasa).
//
// Seguridad:
//  - La clave se lee SOLO de la variable de entorno DIAN_API_KEY. Nunca se escribe en el código ni en la salida.
//  - Por defecto NO envía nada: muestra lo que enviaría. Para enviar hace falta --confirmar.
//  - Los envíos al set de pruebas (--set) consumen consecutivos y cuentan para la DIAN: requieren --confirmar.
//
// Uso (PowerShell):
//   $env:DIAN_API_KEY = "<clave>"            # la clave del cliente de habilitación
//   dotnet run -- salud
//   dotnet run -- quien
//   dotnet run -- enviar --tipo factura --archivo .\factura.json                 (muestra, no envía)
//   dotnet run -- enviar --tipo factura --archivo .\factura.json --set --confirmar
//   dotnet run -- estado --numero SETP990000001
//   dotnet run -- cambios --desde 0 --limite 20

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

const string Ayuda = """
Simulador del ERP para la API de DIAN.

Comandos:
  salud                                  GET /salud (sin clave)
  quien                                  GET /api/Servicio/Quien
  enviar --tipo T --archivo F [--set] [--confirmar]
        T = factura | ds | pos | ds-nota |
        factura  -> POST /api/documento/agregar           (JSON DocumentoDian)
        ds       -> POST /api/DocumentoSoporte/EnviarDSNO (JSON MetaDatosDE, tipo 05)
        ds-nota  -> POST /api/DocumentoSoporte/EnviarDSNO (JSON MetaDatosDE, tipo 95)
        pos      -> POST /api/DocumentoElectronico/EnviarDE (JSON MetaDatosDE, tipo 20 o 94)
        --set    cabecera X-Envio-Set: true (envío al set de pruebas)
  estado --numero N                      GET /api/documento/estado/{N}
  cambios [--desde N] [--limite M]       GET /api/documento/cambios

Variables:
  DIAN_API_URL   URL base (por defecto http://localhost:5290)
  DIAN_API_KEY   clave de API del cliente (obligatoria salvo en 'salud')
""";

if (args.Length == 0 || args[0] is "-h" or "--help" or "ayuda")
{
    Console.WriteLine(Ayuda);
    return 0;
}

var opciones = Opciones.Leer(args.Skip(1).ToArray());
var baseUrl = (Environment.GetEnvironmentVariable("DIAN_API_URL") ?? "http://localhost:5290").TrimEnd('/');
var clave = Environment.GetEnvironmentVariable("DIAN_API_KEY")?.Trim();

// La clave viaja en la cabecera: solo por HTTPS, salvo en una máquina local (autenticacion.md).
if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !new Uri(baseUrl).IsLoopback)
{
    Console.Error.WriteLine("DIAN_API_URL debe usar https:// salvo en localhost. No se envía la clave por HTTP.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

try
{
    switch (args[0])
    {
        case "salud":
            return await Enviar(HttpMethod.Get, baseUrl + "/salud", null, sinClave: true, set: false, confirmar: true);

        case "quien":
            return await Enviar(HttpMethod.Get, baseUrl + "/api/Servicio/Quien", null, sinClave: false, set: false, confirmar: true);

        case "enviar":
        {
            var tipo = opciones.Valor("--tipo") ?? throw new ArgumentException("Falta --tipo (factura, ds, ds-nota o pos).");
            var archivo = opciones.Valor("--archivo") ?? throw new ArgumentException("Falta --archivo con el JSON del documento.");
            var set = opciones.Bandera("--set");
            var confirmar = opciones.Bandera("--confirmar");

            var ruta = tipo switch
            {
                "factura" => "/api/documento/agregar",
                "ds" or "ds-nota" => "/api/DocumentoSoporte/EnviarDSNO",
                "pos" => "/api/DocumentoElectronico/EnviarDE",
                _ => throw new ArgumentException("Tipo desconocido: " + tipo + ". Use factura, ds, ds-nota o pos.")
            };
            var cuerpo = await File.ReadAllTextAsync(archivo, Encoding.UTF8);
            JsonDocument.Parse(cuerpo); // falla pronto si el JSON no es válido

            return await Enviar(HttpMethod.Post, baseUrl + ruta, cuerpo, sinClave: false, set: set, confirmar: confirmar);
        }

        case "estado":
        {
            var numero = opciones.Valor("--numero") ?? throw new ArgumentException("Falta --numero.");
            return await Enviar(HttpMethod.Get, baseUrl + "/api/documento/estado/" + Uri.EscapeDataString(numero), null, sinClave: false, set: false, confirmar: true);
        }

        case "cambios":
        {
            var desde = opciones.Valor("--desde") ?? "0";
            var limite = opciones.Valor("--limite") ?? "100";
            return await Enviar(HttpMethod.Get, $"{baseUrl}/api/documento/cambios?desde={desde}&limite={limite}", null, sinClave: false, set: false, confirmar: true);
        }

        default:
            Console.Error.WriteLine("Comando desconocido: " + args[0]);
            Console.Error.WriteLine(Ayuda);
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    return 1;
}

async Task<int> Enviar(HttpMethod metodo, string url, string? cuerpo, bool sinClave, bool set, bool confirmar)
{
    var pedido = new HttpRequestMessage(metodo, url);
    if (!sinClave)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            Console.Error.WriteLine("Falta la variable de entorno DIAN_API_KEY.");
            return 1;
        }
        pedido.Headers.Add("x-api-key", clave);
    }
    if (set) pedido.Headers.Add("X-Envio-Set", "true");
    if (cuerpo != null) pedido.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");

    Console.WriteLine($"{metodo} {url}" + (set ? "  [X-Envio-Set: true]" : "") + (sinClave ? "" : "  [x-api-key: ****]"));
    if (cuerpo != null) Console.WriteLine($"Cuerpo: {cuerpo.Length} caracteres");

    // Las peticiones que escriben en la DIAN solo se hacen con --confirmar.
    var escribe = metodo == HttpMethod.Post;
    if (escribe && !confirmar)
    {
        Console.WriteLine();
        Console.WriteLine("NO SE ENVIÓ. Falta --confirmar: esta petición escribe en la DIAN.");
        if (set) Console.WriteLine("El envío al set consume consecutivos y cuenta para el set de pruebas.");
        return 0;
    }

    using var respuesta = await http.SendAsync(pedido);
    var texto = await respuesta.Content.ReadAsStringAsync();
    Console.WriteLine($"HTTP {(int)respuesta.StatusCode}");
    Console.WriteLine(FormatearJson(texto));
    return respuesta.IsSuccessStatusCode ? 0 : 1;
}

static string FormatearJson(string texto)
{
    try
    {
        using var doc = JsonDocument.Parse(texto);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
    catch (JsonException)
    {
        return texto;
    }
}

// Lectura mínima de --nombre valor y banderas.
sealed class Opciones
{
    private readonly Dictionary<string, string?> _valores;

    private Opciones(Dictionary<string, string?> valores) => _valores = valores;

    public static Opciones Leer(string[] args)
    {
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var siguiente = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
            valores[args[i]] = siguiente;
            if (siguiente != null) i++;
        }
        return new Opciones(valores);
    }

    public string? Valor(string nombre) => _valores.TryGetValue(nombre, out var v) ? v : null;

    public bool Bandera(string nombre) => _valores.ContainsKey(nombre);
}
