using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DIAN.Admin.Web.Pages
{
    /// <summary>
    /// Segundo paso del ingreso del operador por la entrada pública: el código de seis dígitos que llegó por WhatsApp.
    /// Hasta que no se acierta no existe sesión; lo único que identifica el intento es el ticket de la cookie.
    /// </summary>
    public class CodigoModel : PageModel
    {
        private readonly AdminUsuarios _usuarios;
        private readonly Auditoria _auditoria;
        private readonly SegundoFactor _segundoFactor;

        public CodigoModel(AdminUsuarios usuarios, Auditoria auditoria, SegundoFactor segundoFactor)
        {
            _usuarios = usuarios;
            _auditoria = auditoria;
            _segundoFactor = segundoFactor;
        }

        public string? Error { get; private set; }

        public int Minutos => (int)SegundoFactor.Vigencia.TotalMinutes;

        private string? Ticket => Request.Cookies[Ingreso.CookieTicket];

        public IActionResult OnGet() =>
            _segundoFactor.UsuarioDe(Ticket) == null ? RedirectToPage("/Login") : Page();

        public async Task<IActionResult> OnPostAsync(string codigo)
        {
            var usuario = _segundoFactor.UsuarioDe(Ticket);
            if (usuario == null)
            {
                Ingreso.BorrarTicket(HttpContext);
                return RedirectToPage("/Login");
            }

            switch (_segundoFactor.Verificar(Ticket, codigo))
            {
                case ResultadoSegundoFactor.Correcto:
                    Ingreso.BorrarTicket(HttpContext);
                    await Ingreso.FirmarAsync(HttpContext, usuario, _usuarios.ClienteDe(usuario), segundoFactorVerificado: true);
                    _auditoria.Registrar(usuario, "ingreso", "", "segundo factor por WhatsApp");
                    return RedirectToPage("/Index");

                case ResultadoSegundoFactor.Incorrecto:
                    _auditoria.Registrar(usuario, "segundo-factor-fallido", "");
                    Error = "Código incorrecto.";
                    return Page();

                case ResultadoSegundoFactor.Agotado:
                    _auditoria.Registrar(usuario, "segundo-factor-agotado", "", "tres intentos fallidos");
                    Ingreso.BorrarTicket(HttpContext);
                    return RedirectToPage("/Login");

                default:
                    Ingreso.BorrarTicket(HttpContext);
                    return RedirectToPage("/Login");
            }
        }
    }
}
