using System;

namespace eFacturacionColombia_NETCore.Util
{
    public static class DateTimeHelper
    {
        /// <summary>
        /// Identificador IANA de la zona horaria de Colombia (UTC-05:00, sin horario de verano).
        /// Es el que usa Linux.
        /// </summary>
        public const string COLOMBIA_TIMEZONE_ID_IANA = "America/Bogota";

        /// <summary>
        /// Identificador de Windows de la misma zona. Se conserva por compatibilidad con la copia original.
        /// </summary>
        public const string COLOMBIA_TIMEZONE_ID = "SA Pacific Standard Time";

        private static readonly Lazy<TimeZoneInfo> ZonaColombia = new Lazy<TimeZoneInfo>(ObtenerZona);

        private static TimeZoneInfo ObtenerZona()
        {
            // Linux usa el identificador IANA; Windows acepta el suyo. En Linux con globalización
            // invariante el identificador de Windows no se resuelve, por eso se prueba primero el IANA.
            if (TimeZoneInfo.TryFindSystemTimeZoneById(COLOMBIA_TIMEZONE_ID_IANA, out var zona))
            {
                return zona;
            }

            if (TimeZoneInfo.TryFindSystemTimeZoneById(COLOMBIA_TIMEZONE_ID, out zona))
            {
                return zona;
            }

            // Respaldo si el sistema no tiene datos de zonas horarias: Colombia es UTC-05:00 fijo.
            return TimeZoneInfo.CreateCustomTimeZone("Colombia", TimeSpan.FromHours(-5), "Colombia", "Colombia");
        }

        /// <summary>
        /// Obtiene la hora local de Colombia.
        /// </summary>
        /// <remarks>
        /// Igual que en la copia original: una fecha de Kind <c>Unspecified</c> se interpreta como hora
        /// local del servidor antes de convertirla. En un servidor con zona horaria distinta de Colombia,
        /// una fecha que ya está en hora colombiana se desplazaría. Si el servidor Linux no está en
        /// America/Bogota, hay que pasar fechas con Kind Utc o Local explícito.
        /// </remarks>
        public static DateTime GetColombianDate(DateTime fecha = default)
        {
            if (fecha == default(DateTime))
            {
                fecha = DateTime.Now;
            }

            return TimeZoneInfo.ConvertTime(fecha, ZonaColombia.Value);
        }
    }
}
