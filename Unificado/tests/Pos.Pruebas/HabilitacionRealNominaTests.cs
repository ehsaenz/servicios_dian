using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DIAN.Tenants;
using FacturacionColombia_Metadatos;
using NominaElectronica.MetaDatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pos.Pruebas;

/// <summary>
/// Prueba manual de la nómina contra la HABILITACIÓN de la DIAN, por la API nueva (solo con DIAN_REAL=1).
/// Variables: DIAN_REAL_NOMINA (consecutivo NOM) y MARLIT_P12. Envía al set de pruebas y consulta el resultado por ZipKey.
/// </summary>
public class HabilitacionRealNominaTests
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

    private static async Task<ResponseDian> EnviarYEsperar(HttpClient cliente, MetaDatosNEI nomina, string etiqueta)
    {
        var envio = await cliente.PostAsync("/api/v1/Nomina/EnviarNE", JsonContent.Create(nomina, options: Json));
        var enviado = JsonSerializer.Deserialize<ResponseDian>(await envio.Content.ReadAsStringAsync(), Json)!;
        Console.WriteLine($"NOMINA {etiqueta} envío: HTTP {(int)envio.StatusCode} | {enviado.statusCode} | {enviado.statusMessage} | " +
                          string.Join("; ", enviado.Errors.Select(e => e.Codigo + " " + e.Descripcion)));
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Equal("SP00", enviado.statusCode);

        for (var intento = 1; intento <= 10; intento++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var resp = await cliente.GetAsync("/api/v1/Nomina/ConsultarNE/" + enviado.TrackID);
            var consulta = JsonSerializer.Deserialize<ResponseDian>(await resp.Content.ReadAsStringAsync(), Json)!;
            Console.WriteLine($"NOMINA {etiqueta} consulta {intento}: HTTP {(int)resp.StatusCode} | {consulta.statusCode} | {consulta.statusMessage} | " +
                              string.Join("; ", consulta.Errors.Select(e => e.Codigo + " " + e.Descripcion)));
            if (consulta.statusCode == "00" || consulta.statusCode == "99")
            {
                consulta.UUID = enviado.UUID; // el CUNE del documento enviado
                return consulta;
            }
        }

        throw new TimeoutException("La DIAN no devolvió un resultado definitivo.");
    }

    [RealFact]
    public async Task Ajuste_de_nomina_de_MARLIT_reemplazar_y_eliminar_por_la_API_nueva_en_habilitacion()
    {
        var primero = long.Parse(Environment.GetEnvironmentVariable("DIAN_REAL_NOMINA") ?? throw new InvalidOperationException("Falta DIAN_REAL_NOMINA."));
        var local = ConfigLocal();
        var nomina = JsonDocument.Parse(File.ReadAllText(Path.Combine(local, "nomina.json"))).RootElement;

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-real-ajuste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.Copy(Path.Combine(local, "certificado.p12"), Path.Combine(carpeta, "certificado.p12"));
            File.WriteAllText(Path.Combine(carpeta, "marlit.json"), JsonSerializer.Serialize(new
            {
                Modulos = new[]
                {
                    new
                    {
                        TipoSoftware = 2,
                        Identificador = nomina.GetProperty("softwareIdentificacion").GetString(),
                        PIN = nomina.GetProperty("softwarePin").GetString(),
                        AmbientePruebas = true,
                        TestSetID = nomina.GetProperty("testSetId").GetString()
                    }
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
            var cliente = app.CreateClient();
            cliente.DefaultRequestHeaders.Add("x-api-key", clave);

            // Reemplazar: se envía una nómina, se espera su aprobación y se envía otra que la reemplaza.
            var originalR = await EnviarYEsperar(cliente, DatosNomina.Individual("901033307", "MARLIT GROUP S.A.S.", "NOM", primero), $"NOM{primero}");
            Assert.Equal("00", originalR.statusCode);
            var reemplazo = DatosNomina.Ajuste("901033307", "MARLIT GROUP S.A.S.", "NOM", primero, NominaElectronica.Enums.enmTipoEnvio.Reemplazar, originalR.UUID, "NOM" + primero);
            var rReemplazo = await EnviarYEsperar(cliente, reemplazo, $"NOMR{primero}");
            Assert.Equal("00", rReemplazo.statusCode);

            // Eliminar: igual, con otra nómina.
            var originalE = await EnviarYEsperar(cliente, DatosNomina.Individual("901033307", "MARLIT GROUP S.A.S.", "NOM", primero + 1), $"NOM{primero + 1}");
            Assert.Equal("00", originalE.statusCode);
            var eliminar = DatosNomina.Ajuste("901033307", "MARLIT GROUP S.A.S.", "NOM", primero + 1, NominaElectronica.Enums.enmTipoEnvio.Eliminar, originalE.UUID, "NOM" + (primero + 1));
            var rEliminar = await EnviarYEsperar(cliente, eliminar, $"NOME{primero + 1}");
            Assert.Equal("00", rEliminar.statusCode);
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }

    [RealFact]
    public async Task Nomina_de_MARLIT_por_la_API_nueva_en_habilitacion()
    {
        var consecutivo = long.Parse(Environment.GetEnvironmentVariable("DIAN_REAL_NOMINA") ?? throw new InvalidOperationException("Falta DIAN_REAL_NOMINA."));
        var local = ConfigLocal();
        var nomina = JsonDocument.Parse(File.ReadAllText(Path.Combine(local, "nomina.json"))).RootElement;

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-real-nomina-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            File.Copy(Path.Combine(local, "certificado.p12"), Path.Combine(carpeta, "certificado.p12"));
            File.WriteAllText(Path.Combine(carpeta, "marlit.json"), JsonSerializer.Serialize(new
            {
                Modulos = new[]
                {
                    new
                    {
                        TipoSoftware = 2,
                        Identificador = nomina.GetProperty("softwareIdentificacion").GetString(),
                        PIN = nomina.GetProperty("softwarePin").GetString(),
                        AmbientePruebas = true,
                        TestSetID = nomina.GetProperty("testSetId").GetString()
                    }
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
            var cliente = app.CreateClient();
            cliente.DefaultRequestHeaders.Add("x-api-key", clave);

            var envio = await cliente.PostAsync("/api/v1/Nomina/EnviarNE",
                JsonContent.Create(DatosNomina.Individual("901033307", "MARLIT GROUP S.A.S.", "NOM", consecutivo), options: Json));
            var cuerpo = await envio.Content.ReadAsStringAsync();
            var enviado = JsonSerializer.Deserialize<ResponseDian>(cuerpo, Json)!;
            Console.WriteLine($"NOMINA envío: HTTP {(int)envio.StatusCode} | {enviado.statusCode} | {enviado.statusMessage} | ZipKey {enviado.TrackID} | " +
                              string.Join("; ", enviado.Errors.Select(e => e.Codigo + " " + e.Descripcion)));
            Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
            Assert.Equal("SP00", enviado.statusCode);

            // El resultado del set tarda unos segundos.
            ResponseDian consulta = new();
            for (var intento = 1; intento <= 8; intento++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                var resp = await cliente.GetAsync("/api/v1/Nomina/ConsultarNE/" + enviado.TrackID);
                consulta = JsonSerializer.Deserialize<ResponseDian>(await resp.Content.ReadAsStringAsync(), Json)!;
                Console.WriteLine($"NOMINA consulta {intento}: HTTP {(int)resp.StatusCode} | {consulta.statusCode} | {consulta.statusMessage} | " +
                                  string.Join("; ", consulta.Errors.Select(e => e.Codigo + " " + e.Descripcion)) + " | " + string.Join("; ", consulta.Warnings.Select(e => e.Codigo + " " + e.Descripcion)));
                if (consulta.statusCode == "00" || consulta.statusCode == "99")
                {
                    break;
                }
            }

            Assert.Equal("00", consulta.statusCode);
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }
}
