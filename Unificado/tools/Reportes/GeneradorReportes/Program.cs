using System.Drawing;
using DevExpress.Drawing;
using DevExpress.DataAccess.Json;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;

// Genera plantillas DIAN (.repx) a partir de la plantilla 03FacturaVenta0 de QuantyERP (mismo diseño y campos) y agrega
// el bloque electrónico DIAN: QR, CUFE/CUDE, fecha y hora de validación, resolución o documento modificado y leyenda.
//
// Uso: GeneradorReportes [--nota] <origen.repx> <destino.repx> [<ejemplo.json> <salida.pdf> [<vista.png>]]
//
// Campos nuevos esperados en el JSON del documento (los demás son los de 03FacturaVenta0):
//   CUFE                 CUFE/CUDE del documento (cadena de 96 caracteres hexadecimales).
//   QR                   (opcional) URL del código QR. Si falta, se arma con el CUFE/CUDE y el Ambiente.
//   Ambiente             "1" producción, "2" habilitación (solo para armar el QR cuando no viene).
//   FechaValidacionDian  Fecha y hora de validación de la DIAN, ya con formato.
//   ResolucionTexto      Texto de la resolución de numeración (en facturas).
//   DocumentoRef/Concepto Documento que modifica y concepto de corrección (en notas crédito/débito).
var esNota = args.Any(a => a == "--nota");
var posicionales = args.Where(a => a != "--nota").ToArray();
if (posicionales.Length < 2)
{
    Console.Error.WriteLine("Uso: GeneradorReportes [--nota] <origen.repx> <destino.repx> [<ejemplo.json> <salida.pdf> [<vista.png>]]");
    return 1;
}

// Fuente libre empaquetada (Liberation Sans, licencia OFL): se registra en DevExpress para que el reporte salga
// igual en Windows y en Linux sin depender de las fuentes instaladas. Carpeta: DIAN_FUENTES o ..\..\..\reportes\fuentes.
const string FuenteReporte = "Liberation Sans";
var carpetaFuentes = Environment.GetEnvironmentVariable("DIAN_FUENTES") ?? Path.GetFullPath(Path.Combine("..", "..", "..", "reportes", "fuentes"));
foreach (var ttf in Directory.Exists(carpetaFuentes) ? Directory.GetFiles(carpetaFuentes, "*.ttf") : Array.Empty<string>())
{
    // En Windows (DirectWrite), registrar también las variantes hace que todo el texto salga en negrita; con solo la
    // Regular, la negrita se sintetiza y la vista previa es aceptable. En Linux (Skia) se registran las cuatro.
    if (OperatingSystem.IsWindows() && !ttf.EndsWith("Regular.ttf", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    DXFontRepository.Instance.AddFont(ttf);
}

var reporte = XtraReport.FromFile(posicionales[0], true);
reporte.ScriptReferences = null;
reporte.ScriptsSource = string.Empty;

// La plantilla de QuantyERP usa Segoe UI (de Windows): se cambia la familia en todos los controles y estilos.
DXFont ConFuente(DXFont f) => new DXFont(FuenteReporte, f.Size, f.Style);
foreach (var control in reporte.AllControls<XRControl>())
{
    if (control.Font != null)
    {
        control.Font = ConFuente(control.Font);
    }
}

foreach (var estilo in reporte.StyleSheet.Cast<XRControlStyle>())
{
    if (estilo.Font != null)
    {
        estilo.Font = ConFuente(estilo.Font);
    }
}

if (reporte.Font != null)
{
    reporte.Font = ConFuente(reporte.Font);
}

// El título del documento ("FACTURA ELECTRÓNICA DE VENTA") venía con AutoWidth y sin ajuste de línea: crece hacia la
// derecha y se sale de la hoja. Con ancho fijo y ajuste de línea se parte en dos renglones dentro de su recuadro.
if (reporte.FindControl("xrLabel17", true) is XRLabel titulo)
{
    titulo.AutoWidth = false;
    titulo.Multiline = true;
    titulo.WordWrap = true;
    // Recuadro de 380 de ancho centrado sobre el número (centro 633.7) y fuente de 15.5 pt: el título cabe en un renglón.
    titulo.LocationF = new PointF(443.7f, titulo.TopF);
    titulo.SizeF = new SizeF(380f, 53.2f);
    titulo.Font = new DXFont(FuenteReporte, 15.5f, DXFontStyle.Bold);
    titulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
}

if (esNota)
{
    // En notas crédito y débito, la fila "Ref:" muestra la factura modificada.
    if (reporte.FindControl("tableCell40", true) is XRTableCell celdaRefTitulo)
    {
        celdaRefTitulo.Text = "Factura:";
    }

    if (reporte.FindControl("tableCell41", true) is XRTableCell celdaRefValor)
    {
        celdaRefValor.ExpressionBindings.Clear();
        celdaRefValor.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            "Iif(IsNullOrEmpty([DocumentoRef].[Numero]), Trim([OrdenRef].[TipoDoc] + ' ' + [OrdenRef].[Numero]), [DocumentoRef].[Numero])"));
    }
}

var pie = (GroupFooterBand)reporte.FindControl("GroupFooter1", true);
var fuenteChica = new DXFont(FuenteReporte, 7.5f);
var fuenteMuyChica = new DXFont(FuenteReporte, 6.8f);
var fuenteTitulo = new DXFont(FuenteReporte, 8f, DXFontStyle.Bold);

const string visibleSiHayCufe = "Not IsNullOrEmpty([CUFE])";

// Código QR (DIAN: URL de consulta del documento con el CUFE/CUDE).
var qr = new XRBarCode
{
    Name = "qrDian",
    LocationF = new PointF(80.64f, 8f),
    SizeF = new SizeF(120f, 120f),
    AutoModule = true,
    ShowText = false,
    Symbology = new QRCodeGenerator { CompactionMode = QRCodeCompactionMode.Byte, ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.M, Version = QRCodeVersion.AutoVersion }
};
qr.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
    "Iif(IsNullOrEmpty([QR]), 'https://catalogo-vpfe' + Iif([Ambiente] == '1', '', '-hab') + '.dian.gov.co/document/searchqr?documentkey=' + [CUFE], [QR])"));
qr.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Visible", visibleSiHayCufe));

XRLabel Etiqueta(string nombre, string texto, float x, float y, float ancho, float alto, DXFont fuente, string? expresion = null)
{
    var l = new XRLabel
    {
        Name = nombre,
        LocationF = new PointF(x, y),
        SizeF = new SizeF(ancho, alto),
        Font = fuente,
        Text = texto,
        Multiline = true,
        WordWrap = true,
        Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100f),
        Borders = DevExpress.XtraPrinting.BorderSide.None
    };
    if (expresion != null)
    {
        l.Text = string.Empty;
        l.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));
    }

    l.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Visible", visibleSiHayCufe));
    return l;
}

const float x0 = 208f;      // a la derecha del QR
const float ancho = 285f;   // hasta antes de la tabla de totales (x = 505)
var controles = esNota
    ? new XRControl[]
    {
        qr,
        Etiqueta("lblCufeTitulo", "CUDE:", x0, 6f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblCufe", string.Empty, x0, 20f, ancho, 36f, fuenteChica, "[CUFE]"),
        Etiqueta("lblValidacionTitulo", "Fecha y hora de validación DIAN:", x0, 58f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblValidacion", string.Empty, x0, 72f, ancho, 14f, fuenteChica, "[FechaValidacionDian]"),
        Etiqueta("lblResolucionTitulo", "Documento que modifica y concepto:", x0, 90f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblDocumentoRef", string.Empty, x0, 104f, ancho, 26f, fuenteChica,
            "Iif(IsNullOrEmpty([DocumentoRef].[Numero]), '', 'Factura ' + [DocumentoRef].[Numero]) + Iif(IsNullOrEmpty([Concepto]), '', Iif(IsNullOrEmpty([DocumentoRef].[Numero]), [Concepto], ' — ' + [Concepto]))"),
        Etiqueta("lblCufeRef", string.Empty, x0, 130f, ancho, 32f, fuenteMuyChica,
            "Iif(IsNullOrEmpty([DocumentoRef].[CUFE]), '', 'CUFE: ' + [DocumentoRef].[CUFE])"),
        Etiqueta("lblLeyenda", string.Empty, 80.64f, 166f, 412f, 50f, fuenteChica,
            "'Representación gráfica de ' + Iif([TipoDocDian] == '91', 'la nota crédito electrónica', Iif([TipoDocDian] == '92', 'la nota débito electrónica', 'la nota electrónica')) + '. Documento validado previamente por la DIAN. Puede verificarlo escaneando el código QR o consultando el CUDE en catalogo-vpfe.dian.gov.co.'")
    }
    : new XRControl[]
    {
        qr,
        Etiqueta("lblCufeTitulo", "CUFE:", x0, 6f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblCufe", string.Empty, x0, 20f, ancho, 40f, fuenteChica, "[CUFE]"),
        Etiqueta("lblValidacionTitulo", "Fecha y hora de validación DIAN:", x0, 64f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblValidacion", string.Empty, x0, 78f, ancho, 14f, fuenteChica, "[FechaValidacionDian]"),
        Etiqueta("lblResolucionTitulo", "Resolución de numeración DIAN:", x0, 96f, ancho, 14f, fuenteTitulo),
        Etiqueta("lblResolucion", string.Empty, x0, 110f, ancho, 40f, fuenteChica, "[ResolucionTexto]"),
        Etiqueta("lblLeyenda", "Representación gráfica de la factura electrónica de venta. Documento validado previamente por la DIAN. " +
            "Puede verificarlo escaneando el código QR o consultando el CUFE en catalogo-vpfe.dian.gov.co.",
            80.64f, 160f, 412f, 56f, fuenteChica)
    };

pie.Controls.AddRange(controles);
reporte.SaveLayoutToXml(posicionales[1]);
Console.WriteLine("Plantilla escrita: " + posicionales[1]);

if (posicionales.Length >= 4)
{
    var json = File.ReadAllText(posicionales[2]);
    var prueba = XtraReport.FromFile(posicionales[1], true);
    prueba.ScriptReferences = null;
    prueba.ScriptsSource = string.Empty;
    var fuente = new JsonDataSource { JsonSource = new CustomJsonSource(json) };
    fuente.Fill();
    prueba.DataSource = fuente;
    using var salida = File.Create(posicionales[3]);
    prueba.ExportToPdf(salida);
    Console.WriteLine("PDF de muestra: " + posicionales[3]);
    if (posicionales.Length >= 5)
    {
        // Vista previa en PNG (primera página) para revisar el diseño sin abrir el PDF.
        prueba.ExportToImage(posicionales[4], new DevExpress.XtraPrinting.ImageExportOptions { Format = System.Drawing.Imaging.ImageFormat.Png, PageRange = "1", Resolution = 110 });
        Console.WriteLine("PNG de muestra: " + posicionales[4]);
    }
}

return 0;
