using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Tenants;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shared.Pruebas;

namespace Pos.Pruebas;

/// <summary>
/// Prueba manual del tiquete POS y su nota de ajuste contra la HABILITACIÓN de la DIAN (solo con DIAN_REAL=1).
/// Variables: DIAN_REAL_POS (consecutivo del tiquete EPOS), DIAN_REAL_NOTA (consecutivo de la nota) y MARLIT_P12.
/// Sin TestSetId en la configuración temporal, el envío es síncrono y la DIAN devuelve el detalle de cada documento.
/// </summary>
public class HabilitacionRealPosTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string ConfigLocal()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta != null && !Directory.Exists(Path.Combine(carpeta.FullName, "config-local")))
        {
            carpeta = carpeta.Parent;
        }

        return Path.Combine(carpeta?.FullName ?? throw new DirectoryNotFoundException("No se encontró config-local."), "config-local");
    }

    private static async Task<(ResponseDian Respuesta, string Cuerpo, HttpStatusCode Codigo)> Enviar(WebApplicationFactory<Program> app, string clave, MetaDatosDE meta)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoElectronico/EnviarDE") { Content = JsonContent.Create(meta, options: Json) };
        peticion.Headers.Add("x-api-key", clave);
        var respuesta = await app.CreateClient().SendAsync(peticion);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var resultado = respuesta.StatusCode == HttpStatusCode.OK ? JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json)! : new ResponseDian();
        return (resultado, cuerpo, respuesta.StatusCode);
    }

    [RealFact]
    public async Task Tiquete_POS_y_nota_de_ajuste_de_MARLIT_por_la_API_nueva_en_habilitacion()
    {
        var tiquete = Environment.GetEnvironmentVariable("DIAN_REAL_POS") ?? throw new InvalidOperationException("Falta DIAN_REAL_POS.");
        var nota = Environment.GetEnvironmentVariable("DIAN_REAL_NOTA") ?? throw new InvalidOperationException("Falta DIAN_REAL_NOTA.");
        var local = ConfigLocal();
        var pos = JsonDocument.Parse(File.ReadAllText(Path.Combine(local, "pos.json"))).RootElement;

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-real-pos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.Copy(Path.Combine(local, "certificado.p12"), Path.Combine(carpeta, "certificado.p12"));
            File.WriteAllText(Path.Combine(carpeta, "marlit.json"), JsonSerializer.Serialize(new
            {
                Software = new
                {
                    Identificador = pos.GetProperty("softwareIdentificacion").GetString(),
                    PIN = pos.GetProperty("softwarePin").GetString(),
                    AmbientePruebas = true,
                    TestSetID = ""
                },
                Emisor = new { Nit = "901033307", DV = "6", RazonSocial = "MARLIT GROUP S.A.S." },
                Certificado = new { FileName = "certificado.p12" }
            }));

            var clave = ClaveApi.Generar();
            File.WriteAllText(Path.Combine(carpeta, "tenants.json"), JsonSerializer.Serialize(new[]
            {
                new { Id = "MARLIT", HashClave = ClaveApi.Hash(clave), ArchivoConfig = "marlit.json", Activo = true, PermiteProduccion = false, VariableEntornoClave = "MARLIT_P12" }
            }));

            using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenants:Carpeta"] = carpeta,
                    ["Datos:Carpeta"] = Path.Combine(carpeta, "datos")
                })));

            var numeroPos = "EPOS" + tiquete;
            var (rPos, cuerpoPos, codigoPos) = await Enviar(app, clave, DatosPos.Tiquete("901033307", "MARLIT GROUP S.A.S.", numeroPos));
            Console.WriteLine("TIQUETE: " + codigoPos + " | " + rPos.statusCode + " | " + rPos.statusMessage + " | " + string.Join("; ", rPos.Errors.Select(e => e.Codigo + " " + e.Descripcion)) + " | " + (rPos.statusCode == "" ? cuerpoPos : ""));
            Assert.Equal(HttpStatusCode.OK, codigoPos);
            Assert.Equal("00", rPos.statusCode);
            Assert.Empty(rPos.Errors);

            var numeroNota = "NATP" + nota;
            var (rNota, cuerpoNota, codigoNota) = await Enviar(app, clave, DatosPos.NotaAjuste("901033307", "MARLIT GROUP S.A.S.", numeroNota, numeroPos, rPos.UUID));
            Console.WriteLine("NOTA: " + codigoNota + " | " + rNota.statusCode + " | " + rNota.statusMessage + " | " + string.Join("; ", rNota.Errors.Select(e => e.Codigo + " " + e.Descripcion)) + " | " + (rNota.statusCode == "" ? cuerpoNota : ""));
            Assert.Equal(HttpStatusCode.OK, codigoNota);
            Assert.Equal("00", rNota.statusCode);
            Assert.Empty(rNota.Errors);
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }
}
