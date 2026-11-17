using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DIAN.Pos.Servicios;
using DIAN.Tenants;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Pruebas;

namespace Pos.Pruebas;

/// <summary>Corre solo con DIAN_AISLAMIENTO=1. No envía nada a la DIAN: usa un enviador falso.</summary>
public sealed class AislamientoFactAttribute : FactAttribute
{
    public AislamientoFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DIAN_AISLAMIENTO") != "1")
        {
            Skip = "Manual: defina DIAN_AISLAMIENTO=1, MARLIT_P12 y SIS360_P12, y deje CertificadoSISTEMAS.p12 en config-local.";
        }
    }
}

/// <summary>
/// Paso 2.4: dos clientes REALES (MARLIT y SISTEMAS 360) con sus certificados reales en la misma instancia.
/// Comprueba que cada documento sale firmado con el certificado de su cliente y que uno no puede usar al otro.
/// </summary>
public class AislamientoRealTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private static string ConfigLocal()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta != null && !Directory.Exists(Path.Combine(carpeta.FullName, "config-local")))
        {
            carpeta = carpeta.Parent;
        }

        return Path.Combine(carpeta?.FullName ?? throw new DirectoryNotFoundException("No se encontró config-local."), "config-local");
    }

    private static void CrearCliente(string carpeta, string id, string archivo, string p12Origen, string nit, string dv, string razon, string variable, string clave, string softwareId)
    {
        File.Copy(p12Origen, Path.Combine(carpeta, id + ".p12"));
        File.WriteAllText(Path.Combine(carpeta, id + ".json"), JsonSerializer.Serialize(new
        {
            Software = new { Identificador = softwareId, PIN = "12345", AmbientePruebas = true, TestSetID = "" },
            Emisor = new { Nit = nit, DV = dv, RazonSocial = razon },
            Certificado = new { FileName = id + ".p12" }
        }));
    }

    private static string HuellaDelXml(byte[] firmado)
    {
        var xml = XDocument.Load(new MemoryStream(firmado));
        var base64 = xml.Descendants(Ds + "X509Certificate").First().Value;
        return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64)).Thumbprint;
    }

    [AislamientoFact]
    public async Task MARLIT_y_SISTEMAS360_firman_cada_uno_con_su_certificado_real()
    {
        var local = ConfigLocal();
        var p12Marlit = Path.Combine(local, "certificado.p12");
        var p12Sis = Path.Combine(local, "CertificadoSISTEMAS.p12");
        Assert.True(File.Exists(p12Sis), "Falta config-local\\CertificadoSISTEMAS.p12");

        var claveMarlit = Environment.GetEnvironmentVariable("MARLIT_P12") ?? throw new InvalidOperationException("Falta MARLIT_P12.");
        var claveSis = Environment.GetEnvironmentVariable("SIS360_P12") ?? throw new InvalidOperationException("Falta SIS360_P12.");
        var huellaMarlit = X509CertificateLoader.LoadPkcs12FromFile(p12Marlit, claveMarlit).Thumbprint;
        var huellaSis = X509CertificateLoader.LoadPkcs12FromFile(p12Sis, claveSis).Thumbprint;
        Assert.NotEqual(huellaMarlit, huellaSis);

        var carpeta = Path.Combine(Path.GetTempPath(), "dian-aislamiento-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            var claveApiMarlit = ClaveApi.Generar();
            var claveApiSis = ClaveApi.Generar();
            CrearCliente(carpeta, "MARLIT", "marlit.json", p12Marlit, "901033307", "6", "MARLIT GROUP S.A.S.", "MARLIT_P12", claveMarlit, "id-marlit");
            CrearCliente(carpeta, "SIS360", "sis360.json", p12Sis, "901804437", "4", "SISTEMAS 360 SAS", "SIS360_P12", claveSis, "id-sis360");
            File.WriteAllText(Path.Combine(carpeta, "tenants.json"), JsonSerializer.Serialize(new object[]
            {
                new { Id = "MARLIT", HashClave = ClaveApi.Hash(claveApiMarlit), ArchivoConfig = "MARLIT.json", Activo = true, PermiteProduccion = false, VariableEntornoClave = "MARLIT_P12" },
                new { Id = "SIS360", HashClave = ClaveApi.Hash(claveApiSis), ArchivoConfig = "SIS360.json", Activo = true, PermiteProduccion = false, VariableEntornoClave = "SIS360_P12" }
            }));

            var fabrica = new FabricaFalsa();
            using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenants:Carpeta"] = carpeta,
                    ["Datos:Carpeta"] = Path.Combine(carpeta, "datos"),
                    ["Dian:EsperaSetSegundos"] = "0"
                }));
                b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IFabricaEnviadorDian>(fabrica)));
            });

            async Task<HttpResponseMessage> Enviar(string clave, MetaDatosDE meta)
            {
                var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/DocumentoElectronico/EnviarDE") { Content = JsonContent.Create(meta, options: Json) };
                peticion.Headers.Add("x-api-key", clave);
                return await app.CreateClient().SendAsync(peticion);
            }

            // Mismo número en los dos clientes, enviados de forma intercalada.
            var a1 = await Enviar(claveApiMarlit, DatosPos.Tiquete("901033307", "MARLIT GROUP S.A.S.", "EPOS1"));
            var b1 = await Enviar(claveApiSis, DatosPos.Tiquete("901804437", "SISTEMAS 360 SAS", "EPOS1"));
            var a2 = await Enviar(claveApiMarlit, DatosPos.Tiquete("901033307", "MARLIT GROUP S.A.S.", "EPOS2"));
            var b2 = await Enviar(claveApiSis, DatosPos.Tiquete("901804437", "SISTEMAS 360 SAS", "EPOS2"));

            foreach (var r in new[] { a1, b1, a2, b2 })
            {
                Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            }

            // Cada XML firmado lleva el certificado de su cliente, en el orden en que se enviaron.
            Assert.Equal(4, fabrica.Enviados.Count);
            Assert.Equal(new[] { huellaMarlit, huellaSis, huellaMarlit, huellaSis }, fabrica.Enviados.Select(HuellaDelXml).ToArray());

            // Un cliente no puede enviar documentos a nombre del otro.
            var cruzado = await Enviar(claveApiMarlit, DatosPos.Tiquete("901804437", "SISTEMAS 360 SAS", "EPOS3"));
            Assert.Equal(HttpStatusCode.BadRequest, cruzado.StatusCode);
            Assert.Equal(4, fabrica.Enviados.Count);

            // Cada cliente tiene su carpeta y no se mezclan.
            Assert.True(File.Exists(Path.Combine(carpeta, "datos", "MARLIT", "XML", "DETP-EPOS1.xml")));
            Assert.True(File.Exists(Path.Combine(carpeta, "datos", "SIS360", "XML", "DETP-EPOS1.xml")));
            Assert.False(File.Exists(Path.Combine(carpeta, "datos", "MARLIT", "XML", "DETP-EPOS3.xml")));
            Assert.False(File.Exists(Path.Combine(carpeta, "datos", "SIS360", "XML", "DETP-EPOS3.xml")));
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch (IOException) { }
        }
    }
}
