// Cliente de ejemplo para la API de facturación electrónica (api/documento).
// .NET 8 o superior. Variables de entorno: DIAN_URL y DIAN_API_KEY. Uso: dotnet run -- ..\factura.json
// Los datos del JSON son ficticios. Probar primero en habilitación.
using System.Net;
using System.Text;
using System.Text.Json;

var url = Environment.GetEnvironmentVariable("DIAN_URL")?.TrimEnd('/') ?? throw new InvalidOperationException("Falta DIAN_URL");
var clave = Environment.GetEnvironmentVariable("DIAN_API_KEY") ?? throw new InvalidOperationException("Falta DIAN_API_KEY");
var archivo = args.Length > 0 ? args[0] : "factura.json";

// El envío de rutas síncronas puede tardar: tiempo de espera generoso.
using var http = new HttpClient { BaseAddress = new Uri(url + "/"), Timeout = TimeSpan.FromSeconds(200) };
http.DefaultRequestHeaders.Add("x-api-key", clave);

var cuerpo = await File.ReadAllTextAsync(archivo, Encoding.UTF8);
var numero = JsonDocument.Parse(cuerpo).RootElement.GetProperty("Numero").GetString()!;

// 1. Enviar. 202 = recibido (no aprobado). 400 = el documento no es válido y consulte el número si el error indica un fallo de persistencia.
var respuesta = await http.PostAsync("api/documento/agregar", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
var texto = await respuesta.Content.ReadAsStringAsync();
if (respuesta.StatusCode == HttpStatusCode.BadRequest) throw new InvalidOperationException("Documento no válido: " + texto);
if (respuesta.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("Clave de API inválida.");
respuesta.EnsureSuccessStatusCode();
Console.WriteLine($"Recibido {numero}: {Campo(texto, "Estado")}, CUFE {Campo(texto, "CUFE")}");

// 2. Esperar el resultado (en producción real use el webhook y deje el polling como respaldo).
string estado = Campo(texto, "Estado");
for (var intento = 0; intento < 60 && estado is "Recibido" or "Error"; intento++)
{
    await Task.Delay(TimeSpan.FromSeconds(5));
    texto = await http.GetStringAsync($"api/documento/estado/{Uri.EscapeDataString(numero)}");
    estado = Campo(texto, "Estado");
    Console.WriteLine($"  estado: {estado}");
}

if (estado == "Rechazado")
{
    Console.WriteLine("La DIAN lo rechazó: " + texto); // Errores trae las reglas; corrija y reenvíe el MISMO número.
    return;
}

if (estado != "Aprobado") { Console.WriteLine("Sigue pendiente: " + estado); return; }

// 3. Guardar lo oficial. El PDF puede tardar un instante tras la aprobación (503/Pdf=false): se reintenta.
async Task Descargar(string ruta, string destino)
{
    for (var i = 0; i < 6; i++)
    {
        var r = await http.GetAsync(ruta);
        if (r.IsSuccessStatusCode) { await File.WriteAllBytesAsync(destino, await r.Content.ReadAsByteArrayAsync()); Console.WriteLine("  guardado " + destino); return; }
        if (r.StatusCode != HttpStatusCode.ServiceUnavailable) { Console.WriteLine($"  {ruta}: {(int)r.StatusCode}"); return; }
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

var seguro = Uri.EscapeDataString(numero);
await Descargar($"api/documento/pdf/{seguro}", numero + ".pdf");
await Descargar($"api/documento/xml/{seguro}", numero + ".xml");
await Descargar($"api/documento/respuesta-dian/{seguro}", numero + "-respuesta.xml");

static string Campo(string json, string nombre)
{
    using var d = JsonDocument.Parse(json);
    return d.RootElement.TryGetProperty(nombre, out var v) ? v.ToString() : string.Empty;
}
