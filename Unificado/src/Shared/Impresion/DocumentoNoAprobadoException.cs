using System;

namespace DIAN.Impresion;

/// <summary>
/// El documento no tiene una respuesta de aprobación de la DIAN (o la respuesta no corresponde al documento).
/// Con validación previa, un documento así NO puede tener representación gráfica oficial.
/// </summary>
public sealed class DocumentoNoAprobadoException : Exception
{
    public DocumentoNoAprobadoException(string mensaje, string? codigoRespuesta = null)
        : base(mensaje)
    {
        CodigoRespuesta = codigoRespuesta;
    }

    /// <summary>Código de respuesta de la DIAN (ResponseCode) si la hubo.</summary>
    public string? CodigoRespuesta { get; }
}
