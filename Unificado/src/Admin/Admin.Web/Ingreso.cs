using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace DIAN.Admin.Web
{
    /// <summary>Firma la cookie de sesión. Lo comparten el ingreso normal y el paso del código por WhatsApp.</summary>
    public static class Ingreso
    {
        /// <summary>Nombre de la cookie con el ticket del segundo factor (el intento a medias, antes de la sesión real).</summary>
        public const string CookieTicket = "dian-admin-2fa";

        /// <param name="segundoFactorVerificado">
        /// true cuando el usuario acaba de comprobar el código de WhatsApp. Solo con esa marca puede un operador circular
        /// por la entrada pública.
        /// </param>
        public static async Task FirmarAsync(HttpContext contexto, string usuario, string? clienteId, bool segundoFactorVerificado)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, usuario) };
            if (string.IsNullOrEmpty(clienteId))
            {
                claims.Add(new Claim(ClaimTypes.Role, Roles.Operador));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, Roles.Cliente));
                claims.Add(new Claim(Roles.ClaimCliente, clienteId));
            }

            if (segundoFactorVerificado)
            {
                claims.Add(new Claim(Roles.ClaimSegundoFactor, "whatsapp"));
            }

            var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
        }

        /// <summary>Guarda el ticket del intento en una cookie de sesión, con la misma dureza que la de autenticación.</summary>
        public static void GuardarTicket(HttpContext contexto, string ticket) =>
            contexto.Response.Cookies.Append(CookieTicket, ticket, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = contexto.Request.IsHttps,
                IsEssential = true,
                Expires = System.DateTimeOffset.UtcNow.Add(SegundoFactor.Vigencia)
            });

        public static void BorrarTicket(HttpContext contexto) =>
            contexto.Response.Cookies.Delete(CookieTicket, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = contexto.Request.IsHttps
            });
    }
}
