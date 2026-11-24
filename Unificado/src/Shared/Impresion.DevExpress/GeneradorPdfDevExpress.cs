namespace DIAN.Impresion.DevExpress;

/// <summary>
/// <see cref="IGeneradorPdf"/> con DevExpress Reporting. La API lo carga con
/// <c>Pdf:Generador = "DIAN.Impresion.DevExpress.GeneradorPdfDevExpress, Impresion.DevExpress"</c>, pasándole
/// <c>Pdf:Plantillas</c> (carpeta con las .repx) y <c>Pdf:Fuentes</c> (carpeta con las Liberation Sans).
/// </summary>
public sealed class GeneradorPdfDevExpress : IGeneradorPdf
{
    // Una plantilla por tipo de documento de la DIAN.
    private static readonly Dictionary<string, string> Plantillas = new()
    {
        ["01"] = "FacturaVentaElectronica0.repx",
        ["05"] = "DocumentoSoporteElectronico0.repx",
        ["20"] = "TiqueteVentaElectronica80.repx",
        ["91"] = "NotaCreditoElectronica0.repx",
        ["92"] = "NotaDebitoElectronica0.repx",
        ["94"] = "NotaAjustePosElectronica94.repx",
        ["95"] = "NotaAjusteDocumentoSoporteElectronica95.repx"
    };

    private readonly string _carpetaPlantillas;
    private readonly RenderizadorPdf _renderizador;

    public GeneradorPdfDevExpress(string? carpetaPlantillas, string? carpetaFuentes)
    {
        _carpetaPlantillas = carpetaPlantillas ?? Path.Combine(AppContext.BaseDirectory, "plantillas");
        _renderizador = new RenderizadorPdf(carpetaFuentes ?? Path.Combine(AppContext.BaseDirectory, "fuentes"));
    }

    public byte[] Generar(DocumentoImpresion documento, byte[]? logo)
    {
        if (!Plantillas.TryGetValue(documento.TipoDocDian, out var archivo))
        {
            throw new SinPlantillaException($"Todavía no hay plantilla de impresión para el tipo de documento {documento.TipoDocDian}.");
        }

        var ruta = Path.Combine(_carpetaPlantillas, archivo);
        if (!File.Exists(ruta))
        {
            throw new SinPlantillaException($"Falta el archivo de plantilla {archivo} en {_carpetaPlantillas}.");
        }

        return _renderizador.Generar(documento, File.ReadAllBytes(ruta), logo);
    }
}
