using System.Runtime.CompilerServices;
using System.Text;
using DIAN.Impresion.DevExpress;
using DIAN.Impresion;

namespace Pos.Pruebas;

// Lote 3 de plantillas PDF: documento soporte en adquisiciones a no obligados (05, DSNO)
// y nota de ajuste al documento soporte (95, NSDS). Datos sintéticos: no requieren documentos
// aprobados reales ni config-local.
public class PlantillasPdfLote3Tests
{
    private static readonly string CarpetaReportes = Path.GetFullPath(
        Path.Combine(Path.GetDirectoryName(CallerPath())!, "..", "..", "reportes"));

    private static string CallerPath([CallerFilePath] string ruta = "") => ruta;

    [Theory]
    [InlineData("05", "DocumentoSoporteElectronico0.repx")]
    [InlineData("95", "NotaAjusteDocumentoSoporteElectronica95.repx")]
    public void Plantilla_genera_pdf_valido(string tipo, string archivo)
    {
        Assert.True(File.Exists(Path.Combine(CarpetaReportes, archivo)), $"Falta {archivo}");

        var generador = new GeneradorPdfDevExpress(CarpetaReportes, Path.Combine(CarpetaReportes, "fuentes"));
        // Documento sintético marcado como aprobado por reflexión (setter internal), solo para probar la plantilla.
        var documento = ConstruirDocumento(tipo);
        typeof(DocumentoImpresion).GetProperty(nameof(DocumentoImpresion.Aprobado))!
            .SetValue(documento, true);

        var pdf = generador.Generar(documento, null);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "El PDF es demasiado pequeño");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
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
        // En el documento soporte el receptor es el proveedor no obligado a facturar.
        var receptor = new TerceroImpresion
        {
            RazonSocial = "PROVEEDOR NO OBLIGADO DE PRUEBA",
            NIT = "1234567890",
            LocacionPrincipal = new LocacionImpresion { Direccion = "Carrera 4 # 5-6", NombreMunicipio = "Medellín", NombreDepartamento = "Antioquia" }
        };

        var esAjuste = tipo == "95";
        return new DocumentoImpresion
        {
            TipoDocDian = tipo,
            TipoDocObj = new TipoDocumentoImpresion
            {
                Codigo = tipo,
                Nombre = esAjuste
                    ? "NOTA DE AJUSTE AL DOCUMENTO SOPORTE EN ADQUISICIONES A NO OBLIGADOS"
                    : "DOCUMENTO SOPORTE EN ADQUISICIONES A NO OBLIGADOS"
            },
            Numero = esAjuste ? "NSDS-0001" : "DS-0001",
            FechaHoraDoc = new DateTime(2026, 11, 24, 10, 30, 0),
            FormaPago = new FormaPagoImpresion { Nombre = "Contado" },
            DocumentoRef = esAjuste
                ? new ReferenciaImpresion
                {
                    TipoDoc = "05",
                    Numero = "DS-0001",
                    CUFE = "b2c3d4e5f60718293a4b5c6d7e8f90123456789a"
                }
                : null,
            Concepto = esAjuste ? "Ajuste de valor" : null,
            Items = new List<ItemImpresion>
            {
                new() { Cant = 2, Codigo = "S-01", Referencia = "REF-001", Detalle = "Servicio de prueba con descripción larga para el documento soporte", ValorUnit = 5000, ValorTotal = 10000 },
                new() { Cant = 1, Codigo = "S-02", Referencia = "REF-002", Detalle = "Otro servicio", ValorUnit = 3000, ValorTotal = 3000 }
            },
            SubTotal = 13000,
            ValorDescuento = 0,
            IVA = 0,
            ReteFuente = 130,
            ReteIVA = 0,
            ReteICA = 0,
            ValorTotal = 12870,
            Emisor = emisor,
            Receptor = receptor,
            CUFE = "b2c3d4e5f60718293a4b5c6d7e8f90123456789a",
            QR = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=b2c3d4",
            Ambiente = "2",
            FechaValidacionDian = "2026-11-24 10:31:00",
            ResolucionTexto = "Resolución 18760000002 de 2026"
        };
    }
}
