using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace DIAN.Admin.Web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AdminUsuarios _usuarios;
        private readonly Auditoria _auditoria;

        private readonly bool _operadorSoloInterno;

        public LoginModel(AdminUsuarios usuarios, Auditoria auditoria, Microsoft.Extensions.Configuration.IConfiguration configuracion)
        {
            _usuarios = usuarios;
            _auditoria = auditoria;
            _operadorSoloInterno = configuracion.GetValue("Portal:OperadorSoloInterno", true);
        }

        public string? Error { get; private set; }

        public async Task<IActionResult> OnPostAsync(string usuario, string clave)
        {
            if (await _usuarios.ValidarAsync(usuario ?? string.Empty, clave ?? string.Empty))
            {
                var clienteId = _usuarios.ClienteDe(usuario!);

                // Las cuentas de operador solo entran por la red interna o la VPN, nunca por la dirección pública del portal.
                // Se responde igual que ante una contraseña mala para no confirmar que la cuenta existe.
                if (string.IsNullOrEmpty(clienteId) && Roles.EntradaPublica(Request) && _operadorSoloInterno)
                {
                    _auditoria.Registrar(usuario!, "ingreso-operador-bloqueado", "", "entrada pública");
                    Error = "Usuario o contraseña incorrectos.";
                    return Page();
                }

                var claims = new List<Claim> { new Claim(ClaimTypes.Name, usuario!) };
                if (string.IsNullOrEmpty(clienteId))
                {
                    claims.Add(new Claim(ClaimTypes.Role, Roles.Operador));
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, Roles.Cliente));
                    claims.Add(new Claim(Roles.ClaimCliente, clienteId));
                }

                var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
                _auditoria.Registrar(usuario!, "ingreso", "");
                return RedirectToPage("/Index");
            }

            _auditoria.Registrar(usuario ?? "?", "ingreso-fallido", "", _usuarios.EstaBloqueado(usuario ?? string.Empty) ? "usuario bloqueado" : "");
            Error = _usuarios.EstaBloqueado(usuario ?? string.Empty)
                ? "Demasiados intentos. El usuario quedó bloqueado por 15 minutos."
                : "Usuario o contraseña incorrectos.";
            return Page();
        }
    }
}
