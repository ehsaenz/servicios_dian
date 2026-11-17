using System;
using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DIAN.Admin.Web.Pages
{
    /// <summary>Cambio de la propia contraseña (operadores y usuarios de cliente).</summary>
    public class CuentaModel : PageModel
    {
        private readonly AdminUsuarios _usuarios;
        private readonly Auditoria _auditoria;

        public CuentaModel(AdminUsuarios usuarios, Auditoria auditoria)
        {
            _usuarios = usuarios;
            _auditoria = auditoria;
        }

        public string? Mensaje { get; private set; }
        public string? Error { get; private set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(string actual, string nueva, string repetir)
        {
            var usuario = User.Identity!.Name!;
            if (nueva != repetir)
            {
                Error = "La contraseña nueva y su repetición no coinciden.";
                return Page();
            }

            try
            {
                if (await _usuarios.CambiarClaveAsync(usuario, actual ?? string.Empty, nueva ?? string.Empty))
                {
                    _auditoria.Registrar(usuario, "clave-cambiar", User.ClienteId() ?? "");
                    Mensaje = "Contraseña cambiada.";
                }
                else
                {
                    _auditoria.Registrar(usuario, "clave-cambiar-fallido", User.ClienteId() ?? "");
                    Error = "La contraseña actual no es correcta.";
                }
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
            }

            return Page();
        }
    }
}
