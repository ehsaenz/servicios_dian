using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FacturacionColombia_Metadatos;
using Shared.Pruebas;

namespace Pos.Pruebas;

/// <summary>Corre solo con DIAN_PILOTO_URL: ejercita una API YA DESPLEGADA (por ejemplo en Linux) contra la habilitación de la DIAN.</summary>
public sealed class PilotoFactAttribute : FactAttribute
{
    public PilotoFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DIAN_PILOTO_URL")))
        {
            Skip = "Manual: defina DIAN_PILOTO_URL (y DIAN_PILOTO_INSEGURO=1 si el certificado es autofirmado), DIAN_PILOTO_CLAVE, DIAN_PILOTO_NIT, DIAN_PILOTO_RAZON y los consecutivos DIAN_PILOTO_DS, DIAN_PILOTO_POS y DIAN_PILOTO_NOTA.";
        }
    }
}

public class PilotoRemotoTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Variable(string nombre) => Environment.GetEnvironmentVariable(nombre) ?? throw new InvalidOperationException("Falta " + nombre);

    private static async Task<ResponseDian> Enviar(HttpClient cliente, string ruta, MetaDatosDE meta)
    {
        var respuesta = await cliente.PostAsync(ruta, JsonContent.Create(meta, options: Json));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{(int)respuesta.StatusCode}: {cuerpo}");
        var resultado = JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json)!;
        Console.WriteLine($"PILOTO {meta.Numero}: {resultado.statusCode} | {resultado.statusMessage} | {string.Join("; ", resultado.Errors.Select(e => e.Codigo + " " + e.Descripcion))}");
        return resultado;
    }

    [PilotoFact]
    public async Task Documento_soporte_tiquete_y_nota_por_la_API_desplegada_quedan_autorizados_en_habilitacion()
    {
        var nit = Variable("DIAN_PILOTO_NIT");
        var razon = Variable("DIAN_PILOTO_RAZON");
        // DIAN_PILOTO_INSEGURO=1 acepta el certificado autofirmado de un servidor de pruebas. Nunca para producción.
        HttpMessageHandler Manejador() => Environment.GetEnvironmentVariable("DIAN_PILOTO_INSEGURO") == "1"
            ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
            : new HttpClientHandler();

        using var cliente = new HttpClient(Manejador()) { BaseAddress = new Uri(Variable("DIAN_PILOTO_URL")), Timeout = TimeSpan.FromMinutes(3) };
        cliente.DefaultRequestHeaders.Add("x-api-key", Variable("DIAN_PILOTO_CLAVE"));

        // Sin clave no debe entrar nadie.
        using var anonimo = new HttpClient(Manejador()) { BaseAddress = cliente.BaseAddress };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/Servicio/Quien")).StatusCode);

        var quien = await cliente.GetStringAsync("/api/Servicio/Quien");
        Assert.Contains(nit, quien);

        var ds = Datos.MetaDocumentoSoporte();
        ds.Numero = "SEDS" + Variable("DIAN_PILOTO_DS");
        ds.Adquiriente.Identificacion = nit;
        ds.Adquiriente.RazonSocial = razon;
        var rDs = await Enviar(cliente, "/api/DocumentoSoporte/EnviarDSNO", ds);
        Assert.Equal("00", rDs.statusCode);

        var numeroPos = "EPOS" + Variable("DIAN_PILOTO_POS");
        var rPos = await Enviar(cliente, "/api/DocumentoElectronico/EnviarDE", DatosPos.Tiquete(nit, razon, numeroPos));
        Assert.Equal("00", rPos.statusCode);

        var nota = DatosPos.NotaAjuste(nit, razon, "NATP" + Variable("DIAN_PILOTO_NOTA"), numeroPos, rPos.UUID);
        var rNota = await Enviar(cliente, "/api/DocumentoElectronico/EnviarDE", nota);
        Assert.Equal("00", rNota.statusCode);
    }
}
