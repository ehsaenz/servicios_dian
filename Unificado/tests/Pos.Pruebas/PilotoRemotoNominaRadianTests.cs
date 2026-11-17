using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Nomina.Servicios;
using FacturacionColombia_Metadatos;
using NominaElectronica.Enums;
using NominaElectronica.MetaDatos;

namespace Pos.Pruebas;

/// <summary>
/// Prueba manual de nómina (individual y ajuste) y eventos RADIAN contra una API YA DESPLEGADA (por ejemplo en Linux), con la
/// habilitación de la DIAN. Se activa con DIAN_PILOTO_URL. Variables: DIAN_PILOTO_CLAVE, DIAN_PILOTO_NIT, DIAN_PILOTO_RAZON,
/// DIAN_PILOTO_INSEGURO=1 (certificado autofirmado), DIAN_PILOTO_NOMINA (número libre de nómina), DIAN_PILOTO_CUFE (CUFE de una
/// factura a crédito de la que el cliente es receptor) y DIAN_PILOTO_EVT (primer número libre de evento).
/// </summary>
public class PilotoRemotoNominaRadianTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Variable(string nombre) => Environment.GetEnvironmentVariable(nombre) ?? throw new InvalidOperationException("Falta " + nombre);

    private static async Task<ResponseDian> EnviarNomina(HttpClient cliente, MetaDatosNEI nomina, string etiqueta)
    {
        var envio = await cliente.PostAsync("/api/v1/Nomina/EnviarNE", JsonContent.Create(nomina, options: Json));
        var enviado = JsonSerializer.Deserialize<ResponseDian>(await envio.Content.ReadAsStringAsync(), Json)!;
        Console.WriteLine($"PILOTO {etiqueta} envío: HTTP {(int)envio.StatusCode} | {enviado.statusCode} | {enviado.statusMessage}");
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Equal("SP00", enviado.statusCode);

        for (var intento = 1; intento <= 10; intento++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var resp = await cliente.GetAsync("/api/v1/Nomina/ConsultarNE/" + enviado.TrackID);
            var consulta = JsonSerializer.Deserialize<ResponseDian>(await resp.Content.ReadAsStringAsync(), Json)!;
            Console.WriteLine($"PILOTO {etiqueta} consulta {intento}: HTTP {(int)resp.StatusCode} | {consulta.statusCode} | {consulta.statusMessage}");
            if (consulta.statusCode == "00" || consulta.statusCode == "99" || consulta.statusCode == "2")
            {
                consulta.UUID = enviado.UUID;
                return consulta;
            }
        }

        throw new TimeoutException("La DIAN no devolvió un resultado definitivo.");
    }

    [PilotoFact]
    public async Task Nomina_ajuste_y_eventos_RADIAN_por_la_API_desplegada_quedan_autorizados_en_habilitacion()
    {
        var nit = Variable("DIAN_PILOTO_NIT");
        var razon = Variable("DIAN_PILOTO_RAZON");
        var numero = long.Parse(Variable("DIAN_PILOTO_NOMINA"));
        // Los eventos RADIAN son opcionales: sin DIAN_PILOTO_CUFE solo se prueba la nómina.
        var cufe = Environment.GetEnvironmentVariable("DIAN_PILOTO_CUFE");
        var primerEvento = long.Parse(Environment.GetEnvironmentVariable("DIAN_PILOTO_EVT") ?? "0");

        HttpMessageHandler Manejador() => Environment.GetEnvironmentVariable("DIAN_PILOTO_INSEGURO") == "1"
            ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
            : new HttpClientHandler();

        using var cliente = new HttpClient(Manejador()) { BaseAddress = new Uri(Variable("DIAN_PILOTO_URL")), Timeout = TimeSpan.FromMinutes(3) };
        cliente.DefaultRequestHeaders.Add("x-api-key", Variable("DIAN_PILOTO_CLAVE"));

        // Nómina: se envía desde Linux (firmada con el certificado del cliente) al set de pruebas. Si el set aún no estaba aceptado, la DIAN
        // detalla el documento y se prueba también el ajuste; si ya estaba aceptado, solo informa el estado del set (código 2).
        var original = await EnviarNomina(cliente, DatosNomina.Individual(nit, razon, "NOM", numero), $"NOM{numero}");
        Assert.True(original.statusCode == "00" || original.statusCode == "2", "Resultado inesperado de la nómina: " + original.statusCode);
        if (original.statusCode == "00")
        {
            var reemplazo = DatosNomina.Ajuste(nit, razon, "NOM", numero, enmTipoEnvio.Reemplazar, original.UUID, "NOM" + numero);
            var ajuste = await EnviarNomina(cliente, reemplazo, $"NOMR{numero}");
            Assert.Equal("00", ajuste.statusCode);
        }

        if (string.IsNullOrEmpty(cufe))
        {
            return;
        }

        // Eventos RADIAN: acuse, recibo y aceptación, esperando a que la DIAN registre cada uno.
        var secuencia = new[] { ("AcuseFactura", "030"), ("ReciboBienesServicios", "032"), ("AceptacionExpresa", "033") };
        for (var i = 0; i < secuencia.Length; i++)
        {
            var (accion, codigo) = secuencia[i];
            var evento = new EventoRadian { Codigo = codigo, UUID = cufe, Numero = "EVT" + (primerEvento + i) };
            ResponseDian cuerpo = null!;
            for (var intento = 1; intento <= 8; intento++)
            {
                var respuesta = await cliente.PostAsync("/api/v1/Eventos/" + accion, JsonContent.Create(evento, options: Json));
                cuerpo = JsonSerializer.Deserialize<ResponseDian>(await respuesta.Content.ReadAsStringAsync(), Json)!;
                Console.WriteLine($"PILOTO evento {codigo} {evento.Numero} (intento {intento}): HTTP {(int)respuesta.StatusCode} | {cuerpo.statusCode} | {cuerpo.statusMessage} | " +
                                  string.Join("; ", cuerpo.Errors.Select(e => e.Codigo)));
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                if (cuerpo.statusCode == "00" || !(cuerpo.Errors.Any(e => e.Codigo.StartsWith("LGC")) || (cuerpo.statusMessage ?? "").Contains("Intente")))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(15));
            }

            Assert.Equal("00", cuerpo.statusCode);
        }
    }
}
