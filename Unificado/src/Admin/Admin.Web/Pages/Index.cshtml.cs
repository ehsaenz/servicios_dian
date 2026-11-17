using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DIAN.Admin.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ClienteAdminServicio _admin;

        public IndexModel(ClienteAdminServicio admin)
        {
            _admin = admin;
        }

        public IReadOnlyList<ClienteRegistro> Clientes { get; private set; } = new List<ClienteRegistro>();
        public string? Error { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Un usuario de cliente no tiene lista: va directo a su propio cliente.
            if (!User.EsOperador())
            {
                var propio = User.ClienteId();
                return string.IsNullOrEmpty(propio) ? Forbid() : RedirectToPage("/Cliente", new { id = propio });
            }

            Clientes = (await _admin.ListarAsync()).OrderBy(c => c.Id).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostCrearAsync(string id, string nit, string? dv, string razon)
        {
            if (!User.EsOperador())
            {
                return Forbid();
            }

            try
            {
                var cliente = await _admin.CrearAsync(User.Identity!.Name!, (id ?? string.Empty).Trim(),
                    new EmisorConfig { Nit = (nit ?? string.Empty).Trim(), DV = (dv ?? string.Empty).Trim(), RazonSocial = (razon ?? string.Empty).Trim() });
                return RedirectToPage("/Cliente", new { id = cliente.Id });
            }
            catch (ErrorAdministracion ex)
            {
                Error = ex.Message;
                Clientes = (await _admin.ListarAsync()).OrderBy(c => c.Id).ToList();
                return Page();
            }
        }
    }
}
