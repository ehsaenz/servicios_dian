using System.Runtime.CompilerServices;
using System.Text;
using DIAN.Impresion.DevExpress;
using DIAN.Impresion;

namespace Pos.Pruebas;

// Lote 2 de plantillas PDF: documento equivalente POS (20, tirilla 80 mm) y nota de ajuste POS (94).
// Datos sintéticos: no requieren documentos aprobados reales ni config-local.
public class PlantillasPdfLote2Tests
{
    private static readonly string CarpetaReportes = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(CallerPath())!, "..", "..", "reportes"));

    private static string CallerPath([CallerFilePath] string ruta = "") => ruta;

    [Theory]
    [InlineData("20", "TiqueteVentaElectronica80.repx")]
    [InlineData("94", "NotaAjustePosElectronica94.repx")]
    public void Plantilla_genera_pdf_valido(string tipo, string archivo)
    {
        Assert.True(File.Exists(Path.Combine(CarpetaReportes, archivo)), $"Falta {archivo}");

        var generador = new GeneradorPdfDevExpress(CarpetaReportes, Path.Combine(CarpetaReportes, "fuentes"));
        var pdf = generador.Generar(DocumentoSintetico(tipo), null);
        // Nota: si el renderizador exige Aprobado, la prueba lo fija por reflexión (setter internal).


        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "El PDF es demasiado pequeño");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    // El renderizador solo acepta documentos aprobados (flag Aprobado, setter internal que fija
    // LectorDocumentoAprobado al leer un XML aprobado). Aquí es un modelo sintético: se marca por
    // reflexión únicamente para probar la plantilla; no se usa para documentos reales.
    private static DocumentoImpresion DocumentoSintetico(string tipo)
    {
        var documento = ConstruirDocumento(tipo);
        typeof(DocumentoImpresion).GetProperty(nameof(DocumentoImpresion.Aprobado))!
            .SetValue(documento, true);
        return documento;
    }

    private static DocumentoImpresion ConstruirDocumento(string tipo)
    {
        var emisor = new TerceroImpresion
        {
            RazonSocial = "MARLIT GROUP SAS",
            NIT = "900000000",
            EmailContacto = "emisor@ejemplo.com",
            TelefonoContacto = "3000000000",
            LocacionPrincipal = new LocacionImpresion
            {
                Direccion = "Calle 1 # 2-3",
                NombreMunicipio = "Bogotá",
                NombreDepartamento = "Bogotá D.C."
            }
        };
        var receptor = new TerceroImpresion
        {
            RazonSocial = "CLIENTE DE PRUEBA",
            NIT = "1234567890",
            LocacionPrincipal = new LocacionImpresion { Direccion = "Carrera 4 # 5-6", NombreMunicipio = "Medellín", NombreDepartamento = "Antioquia" }
        };

        return new DocumentoImpresion
        {
            TipoDocDian = tipo,
            TipoDocObj = new TipoDocumentoImpresion { Codigo = tipo, Nombre = tipo == "20" ? "DOCUMENTO EQUIVALENTE POS ELECTRÓNICO" : "NOTA DE AJUSTE AL DOCUMENTO EQUIVALENTE POS" },
            Numero = tipo == "20" ? "TV-0001" : "NATP-0001",
            FechaHoraDoc = new DateTime(2026, 11, 24, 10, 30, 0),
            FormaPago = new FormaPagoImpresion { Nombre = "Contado" },
            DocumentoRef = new ReferenciaImpresion
            {
                TipoDoc = "20",
                Numero = "TV-0001",
                CUFE = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678"
            },
            Concepto = "Ajuste de precio",
            Items = new List<ItemImpresion>
            {
                new() { Cant = 2, Codigo = "P-01", Referencia = "REF-001", Detalle = "Producto de prueba con descripción larga para la tirilla", ValorUnit = 5000, ValorTotal = 10000 },
                new() { Cant = 1, Codigo = "P-02", Referencia = "REF-002", Detalle = "Otro producto", ValorUnit = 3000, ValorTotal = 3000 }
            },
            SubTotal = 13000,
            ValorDescuento = 0,
            IVA = 2470,
            ReteFuente = 130,
            ReteIVA = 0,
            ReteICA = 0,
            ValorTotal = 15340,
            Emisor = emisor,
            Receptor = receptor,
            CUFE = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
            QR = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=a1b2c3",
            Ambiente = "2",
            FechaValidacionDian = "2026-11-24 10:31:00",
            ResolucionTexto = "Resolución 18760000001 de 2026"
        };
    }
}
