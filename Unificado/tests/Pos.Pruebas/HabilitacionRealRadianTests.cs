using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DIAN.Nomina.Servicios;
using DIAN.Tenants;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pos.Pruebas;

/// <summary>
/// Prueba manual de los eventos RADIAN contra la HABILITACIÓN de la DIAN, por la API nueva (solo con DIAN_REAL=1).
/// Variables: DIAN_REAL_FACTURA (número de una factura a crédito de MARLIT ya autorizada y presente en config-local\salida),
/// DIAN_REAL_EVT (primer número libre de evento) y MARLIT_P12. Envía acuse (030), recibo (032) y aceptación (033) en ese orden.
/// </summary>
public class HabilitacionRealRadianTests
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
    public async Task Acuse_recibo_y_aceptacion_de_una_factura_de_MARLIT_por_la_API_nueva_en_habilitacion()
    {
        var factura = Environment.GetEnvironmentVariable("DIAN_REAL_FACTURA") ?? throw new InvalidOperationException("Falta DIAN_REAL_FACTURA.");
        var primero = long.Parse(Environment.GetEnvironmentVariable("DIAN_REAL_EVT") ?? throw new InvalidOperationException("Falta DIAN_REAL_EVT."));
        var local = ConfigLocal();
        var software = JsonDocument.Parse(File.ReadAllText(Path.Combine(local, "habilitacion-sw-propio.json"))).RootElement.GetProperty("informacionSoftware");

        var xmlFactura = Path.Combine(local, "salida", factura + "-firmado.xml");
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        var cufe = XDocument.Load(xmlFactura).Root!.Element(cbc + "UUID")!.Value;

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-real-radian-" + Guid.NewGuid().ToString("N"));
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
                        TipoSoftware = 3,
                        Identificador = software.GetProperty("identificacion").GetString(),
                        PIN = software.GetProperty("pin").GetString(),
                        AmbientePruebas = true,
                        TestSetID = ""
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

            // La factura NO está en la carpeta del cliente: la API tiene que pedírsela a la DIAN.
            // DIAN_REAL_DESDE (030, 032 o 033) permite seguir desde un evento si los anteriores ya se enviaron.
            var secuencia = new[] { ("AcuseFactura", "030"), ("ReciboBienesServicios", "032"), ("AceptacionExpresa", "033") };
            var desde = Environment.GetEnvironmentVariable("DIAN_REAL_DESDE") ?? "030";
            secuencia = secuencia.SkipWhile(e => e.Item2 != desde).ToArray();

            for (var i = 0; i < secuencia.Length; i++)
            {
                var (accion, codigo) = secuencia[i];
                var evento = new EventoRadian { Codigo = codigo, UUID = cufe, Numero = "EVT" + (primero + i) };

                // La DIAN registra cada evento de forma asíncrona: si el anterior aún no consta (LGC09, LGC10...), se espera y se repite.
                ResponseDian cuerpo = null!;
                for (var intento = 1; intento <= 8; intento++)
                {
                    var respuesta = await cliente.PostAsync("/api/v1/Eventos/" + accion, JsonContent.Create(evento, options: Json));
                    cuerpo = JsonSerializer.Deserialize<ResponseDian>(await respuesta.Content.ReadAsStringAsync(), Json)!;
                    Console.WriteLine($"RADIAN {codigo} {evento.Numero} (intento {intento}): HTTP {(int)respuesta.StatusCode} | {cuerpo.statusCode} | {cuerpo.statusMessage} | " +
                                      string.Join("; ", cuerpo.Errors.Select(e => e.Codigo + " " + e.Descripcion)));
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
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }
}
