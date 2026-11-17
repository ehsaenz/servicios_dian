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

/// <summary>Prueba manual contra la HABILITACIÓN de la DIAN. Solo corre con DIAN_REAL=1 (y no se ejecuta en una corrida normal).</summary>
public sealed class RealFactAttribute : FactAttribute
{
    public RealFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DIAN_REAL") != "1")
        {
            Skip = "Manual: defina DIAN_REAL=1, DIAN_REAL_CONSECUTIVO y la variable MARLIT_P12 para enviar a la habilitación de la DIAN.";
        }
    }
}

public class HabilitacionRealTests
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

    [RealFact]
    public async Task Documento_soporte_de_MARLIT_por_la_API_nueva_queda_autorizado_en_habilitacion()
    {
        var consecutivo = Environment.GetEnvironmentVariable("DIAN_REAL_CONSECUTIVO") ?? throw new InvalidOperationException("Falta DIAN_REAL_CONSECUTIVO.");
        var local = ConfigLocal();
        var ds = JsonDocument.Parse(File.ReadAllText(Path.Combine(local, "documento-soporte.json"))).RootElement;

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.Copy(Path.Combine(local, "certificado.p12"), Path.Combine(carpeta, "certificado.p12"));
            File.WriteAllText(Path.Combine(carpeta, "marlit.json"), JsonSerializer.Serialize(new
            {
                Software = new
                {
                    Identificador = ds.GetProperty("softwareIdentificacion").GetString(),
                    PIN = ds.GetProperty("softwarePin").GetString(),
                    AmbientePruebas = true,
                    TestSetID = ds.GetProperty("testSetId").GetString()
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

            var meta = Datos.MetaDocumentoSoporte();
            meta.Numero = "SEDS" + consecutivo;
            meta.Adquiriente.Identificacion = "901033307";
            meta.Adquiriente.RazonSocial = "MARLIT GROUP S.A.S.";

            var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoSoporte/EnviarDSNO") { Content = JsonContent.Create(meta, options: Json) };
            peticion.Headers.Add("x-api-key", clave);
            var respuesta = await app.CreateClient().SendAsync(peticion);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, cuerpo);
            var resultado = JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json)!;
            Assert.Equal("00", resultado.statusCode);
            Assert.Empty(resultado.Errors);
            Console.WriteLine("DIAN: " + resultado.statusMessage + " | UUID " + resultado.UUID);
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }
}
