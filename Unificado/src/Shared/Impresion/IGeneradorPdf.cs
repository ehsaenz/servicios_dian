using System;

namespace DIAN.Impresion;

/// <summary>No hay plantilla de impresión para ese tipo de documento (todavía).</summary>
public sealed class SinPlantillaException : Exception
{
    public SinPlantillaException(string mensaje) : base(mensaje) { }
}

/// <summary>
/// Genera la representación gráfica (PDF) de un documento aprobado. La implementación con DevExpress vive fuera de la
/// solución (necesita DevExpress instalado) y la API la carga por configuración (<c>Pdf:Generador</c>).
/// </summary>
public interface IGeneradorPdf
{
    /// <exception cref="SinPlantillaException">No hay plantilla para el tipo de documento.</exception>
    byte[] Generar(DocumentoImpresion documento, byte[]? logo);
}
