using System;

namespace DIAN.Admin.Web
{
    /// <summary>
    /// Interpreta la respuesta de la DIAN al enviar un documento al set de pruebas de un bloque. Solo «se encuentra Aceptado»
    /// (código 2) cuenta como set aprobado: es la única fuente de verdad para pasar a producción, no los conteos locales.
    /// </summary>
    public static class EstadoSetDian
    {
        public enum Resultado
        {
            /// <summary>La DIAN confirma que el set del bloque ya está aceptado.</summary>
            Aceptado,

            /// <summary>Cualquier otra respuesta: el set no está aprobado (o no se pudo confirmar). Bloquea producción.</summary>
            NoAceptado
        }

        private const string CodigoAceptado = "2";
        private const string TextoAceptado = "se encuentra Aceptado";

        public static Resultado Interpretar(string? codigoEstado, string? mensaje)
        {
            if (string.IsNullOrWhiteSpace(codigoEstado) || string.IsNullOrWhiteSpace(mensaje)) return Resultado.NoAceptado;
            return codigoEstado.Trim() == CodigoAceptado && mensaje.Contains(TextoAceptado, StringComparison.OrdinalIgnoreCase)
                ? Resultado.Aceptado
                : Resultado.NoAceptado;
        }
    }
}
