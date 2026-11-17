using System.Runtime.Loader;
using DevExpress.Drawing;
using DevExpress.DataAccess.Json;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;

namespace DIAN.Impresion.DevExpress;

/// <summary>
/// Genera el PDF de la representación gráfica de un documento electrónico APROBADO a partir de una plantilla .repx.
/// Solo acepta el modelo que arma LectorDocumentoAprobado: sin aprobación de la DIAN no hay representación gráfica.
/// </summary>
public sealed class RenderizadorPdf
{
    private static readonly object Candado = new();
    private static readonly HashSet<string> CarpetasCargadas = new(StringComparer.OrdinalIgnoreCase);

    // DevExpress busca por nombre el ensamblado del motor Skia (DevExpress.Drawing.v24.1.Skia) y el del PDF con Skia, que no
    // son dependencia de compilación y no quedan en el .deps.json de la aplicación. Si no los encuentra, cae al motor GDI+
    // y en Linux falla pidiendo libgdiplus. Este resolvedor los toma de la carpeta de la aplicación, donde se copian.
    static RenderizadorPdf()
    {
        AssemblyLoadContext.Default.Resolving += (contexto, nombre) =>
        {
            if (nombre.Name is null || !nombre.Name.StartsWith("DevExpress.", StringComparison.Ordinal))
            {
                return null;
            }

            var ruta = Path.Combine(AppContext.BaseDirectory, nombre.Name + ".dll");
            return File.Exists(ruta) ? contexto.LoadFromAssemblyPath(ruta) : null;
        };
    }

    /// <param name="carpetaFuentes">
    /// Carpeta con los .ttf que usa la plantilla (Liberation Sans). Se registran una sola vez por proceso, de modo que
    /// el PDF sale igual en Windows y en Linux sin depender de las fuentes instaladas.
    /// </param>
    public RenderizadorPdf(string carpetaFuentes)
    {
        RegistrarFuentes(carpetaFuentes);
    }

    /// <param name="documento">Modelo leído de un documento aprobado.</param>
    /// <param name="plantilla">Contenido del .repx.</param>
    /// <param name="logo">Logo de la sucursal (png/jpg); opcional.</param>
    public byte[] Generar(DocumentoImpresion documento, byte[] plantilla, byte[]? logo = null)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(plantilla);

        if (!documento.Aprobado)
        {
            throw new DocumentoNoAprobadoException(
                "Solo se genera la representación gráfica de un documento aprobado por la DIAN (use LectorDocumentoAprobado).");
        }

        using var reporte = XtraReport.FromStream(new MemoryStream(plantilla), true);

        // Las plantillas de QuantyERP traen scripts: no se ejecutan en la API.
        reporte.ScriptReferences = null;
        reporte.ScriptsSource = string.Empty;

        var origen = new JsonDataSource { JsonSource = new CustomJsonSource(documento.ToJson()) };
        origen.Fill();
        reporte.DataSource = origen;

        if (logo is { Length: > 0 } && reporte.FindControl("pictureBox1", true) is XRPictureBox imagen)
        {
            using var flujoLogo = new MemoryStream(logo);
            imagen.ImageSource = new ImageSource(DXImage.FromStream(flujoLogo));
        }

        using var salida = new MemoryStream();
        reporte.ExportToPdf(salida);
        return salida.ToArray();
    }

    private static void RegistrarFuentes(string carpeta)
    {
        if (!Directory.Exists(carpeta))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de fuentes: {carpeta}");
        }

        var completa = Path.GetFullPath(carpeta);
        lock (Candado)
        {
            if (!CarpetasCargadas.Add(completa))
            {
                return;
            }

            foreach (var ttf in Directory.GetFiles(completa, "*.ttf"))
            {
                // En Windows (DirectWrite), registrar también las variantes hace que todo el texto salga en negrita:
                // solo se registra la Regular. En Linux (Skia) se registran todas.
                if (OperatingSystem.IsWindows() && !ttf.EndsWith("Regular.ttf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DXFontRepository.Instance.AddFont(ttf);
            }
        }
    }
}
