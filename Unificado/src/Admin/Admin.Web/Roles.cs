using System.Security.Claims;

namespace DIAN.Admin.Web
{
    /// <summary>Roles de la app: el operador de la plataforma ve todos los clientes; un usuario de cliente solo el suyo.</summary>
    public static class Roles
    {
        public const string Operador = "Operador";
        public const string Cliente = "Cliente";
        public const string ClaimCliente = "cliente";

        /// <summary>
        /// Marca que la sesión se abrió comprobando el código de WhatsApp. Un operador solo circula por la entrada
        /// pública con esta marca.
        /// </summary>
        public const string ClaimSegundoFactor = "segundo-factor";

        /// <summary>
        /// Cabecera que fija nginx según por dónde entró la petición: <c>publica</c> (internet, portal de clientes) o
        /// <c>interna</c> (red interna/VPN). La app solo escucha en 127.0.0.1, así que solo nginx puede ponerla.
        /// </summary>
        public const string CabeceraEntrada = "X-Dian-Entrada";

        public static bool EntradaPublica(Microsoft.AspNetCore.Http.HttpRequest peticion) =>
            string.Equals(peticion.Headers[CabeceraEntrada].ToString(), "publica", System.StringComparison.OrdinalIgnoreCase);

        public static bool EsOperador(this ClaimsPrincipal usuario) => usuario.IsInRole(Operador);

        /// <summary>True si esta sesión pasó por el segundo factor.</summary>
        public static bool ConSegundoFactor(this ClaimsPrincipal usuario) => usuario.FindFirst(ClaimSegundoFactor) != null;

        /// <summary>Id del cliente de un usuario de portal; null para el operador.</summary>
        public static string? ClienteId(this ClaimsPrincipal usuario) => usuario.FindFirst(ClaimCliente)?.Value;

        /// <summary>True si el usuario puede ver y operar ese cliente: el operador cualquiera, un cliente solo el propio.</summary>
        public static bool PuedeVer(this ClaimsPrincipal usuario, string? clienteId) =>
            usuario.EsOperador() || (usuario.IsInRole(Cliente) && !string.IsNullOrEmpty(clienteId)
                                     && string.Equals(usuario.ClienteId(), clienteId, System.StringComparison.OrdinalIgnoreCase));
    }
}
