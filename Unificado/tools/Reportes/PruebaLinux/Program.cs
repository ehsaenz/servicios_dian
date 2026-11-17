using System.Diagnostics;
using DIAN.Impresion;
using DIAN.Impresion.DevExpress;

// Prueba de la cadena completa: AttachedDocument aprobado -> modelo de impresión -> PDF (DevExpress, motor Skia).
//
// Uso: PruebaLinux <contenedor.xml> <plantilla.repx> <salida.pdf> [logo.png]
// Las fuentes (Liberation Sans) se toman de la carpeta fuentes junto al programa.
if (args.Length < 3)
{
    Console.Error.WriteLine("Uso: PruebaLinux <contenedor.xml> <plantilla.repx> <salida.pdf> [logo.png]");
    return 1;
}

Console.WriteLine($"SO: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} | .NET {Environment.Version}");

if (Environment.GetEnvironmentVariable("DIAN_DIAG") == "1")
{
    // Si DevExpress no carga el motor Skia, esto muestra la causa real (dependencia o nativo que falta).
    try
    {
        var skia = System.Reflection.Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "DevExpress.Drawing.v24.1.Skia.dll"));
        Console.WriteLine("Skia cargado: " + skia.FullName + ", tipos: " + skia.GetTypes().Length);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Fallo al cargar Skia: " + ex);
        if (ex is System.Reflection.ReflectionTypeLoadException rtl)
        {
            foreach (var le in rtl.LoaderExceptions.Take(5))
            {
                Console.WriteLine("  Loader: " + le?.Message);
            }
        }
    }
}

var reloj = Stopwatch.StartNew();
var modelo = LectorDocumentoAprobado.DesdeContenedor(File.ReadAllText(args[0]));
Console.WriteLine($"Modelo leído: {modelo.TipoDocObj.Nombre} {modelo.Numero}, CUFE {modelo.CUFE[..12]}..., validado {modelo.FechaValidacionDian} ({reloj.ElapsedMilliseconds} ms)");

var plantilla = File.ReadAllBytes(args[1]);
var logo = args.Length >= 4 ? File.ReadAllBytes(args[3]) : null;
var renderizador = new RenderizadorPdf(Path.Combine(AppContext.BaseDirectory, "fuentes"));

reloj.Restart();
var pdf = renderizador.Generar(modelo, plantilla, logo);
File.WriteAllBytes(args[2], pdf);
Console.WriteLine($"PDF: {args[2]} ({pdf.Length} bytes, {reloj.ElapsedMilliseconds} ms)");

reloj.Restart();
renderizador.Generar(modelo, plantilla, logo);
Console.WriteLine($"Segundo PDF: {reloj.ElapsedMilliseconds} ms");

// Un modelo que no viene de un documento aprobado no se imprime.
try
{
    renderizador.Generar(new DocumentoImpresion(), plantilla);
    Console.WriteLine("ERROR: se imprimió un modelo sin aprobación.");
    return 2;
}
catch (DocumentoNoAprobadoException)
{
    Console.WriteLine("Modelo sin aprobación rechazado: correcto.");
}

return 0;
