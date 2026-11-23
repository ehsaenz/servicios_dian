using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace DIAN.Admin.Web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AdminUsuarios _usuarios;
        private readonly Auditoria _auditoria;
        private readonly SegundoFactor _segundoFactor;
        private readonly OpcionesWhatsApp _whatsapp;

        private readonly bool _operadorSoloInterno;
        private readonly bool _segundoFactorOperador;

        public LoginModel(AdminUsuarios usuarios, Auditoria auditoria, SegundoFactor segundoFactor, OpcionesWhatsApp whatsapp, IConfiguration configuracion)
        {
            _usuarios = usuarios;
            _auditoria = auditoria;
            _segundoFactor = segundoFactor;
            _whatsapp = whatsapp;
            _operadorSoloInterno = configuracion.GetValue("Portal:OperadorSoloInterno", true);
            _segundoFactorOperador = configuracion.GetValue("Portal:SegundoFactorOperador", true);
        }

        public string? Error { get; private set; }

        /// <summary>
        /// Volver al ingreso anula el codigo pendiente: no debe quedar uno vivo mientras se pide otro, ni seguir
        /// sirviendo si quien volvio no era el dueño de la sesion.
        /// </summary>
        public void OnGet()
        {
            var ticket = Request.Cookies[Ingreso.CookieTicket];
            if (!string.IsNullOrEmpty(ticket))
            {
                _segundoFactor.Cancelar(ticket);
                Ingreso.BorrarTicket(HttpContext);
            }
        }

        public async Task<IActionResult> OnPostAsync(string usuario, string clave)
        {
            if (await _usuarios.ValidarAsync(usuario ?? string.Empty, clave ?? string.Empty))
            {
                var clienteId = _usuarios.ClienteDe(usuario!);
                var esOperador = string.IsNullOrEmpty(clienteId);

                // Un operador que entra por la dirección pública tiene que comprobar un código enviado a su celular.
                // Si no hay segundo factor disponible se aplica la regla anterior: el panel del operador no sale a internet.
                if (esOperador && Roles.EntradaPublica(Request))
                {
                    if (!_segundoFactorOperador || !_whatsapp.Configurado)
                    {
                        if (_operadorSoloInterno)
                        {
                            // Se responde igual que ante una contraseña mala para no confirmar que la cuenta existe.
                            _auditoria.Registrar(usuario!, "ingreso-operador-bloqueado", "", "entrada pública");
                            Error = "Usuario o contraseña incorrectos.";
                            return Page();
                        }
                    }
                    else
                    {
                        var telefono = _usuarios.TelefonoDe(usuario!);
                        if (telefono == null)
                        {
                            // Sin celular registrado no hay segundo factor posible: por internet no entra.
                            _auditoria.Registrar(usuario!, "ingreso-operador-bloqueado", "", "sin celular para el segundo factor");
                            Error = "Usuario o contraseña incorrectos.";
                            return Page();
                        }

                        var ticket = await _segundoFactor.IniciarAsync(usuario!, telefono);
                        if (ticket == null)
                        {
                            _auditoria.Registrar(usuario!, "segundo-factor-no-enviado", "", "el gateway de WhatsApp no aceptó el mensaje");
                            Error = "No se pudo enviar el código por WhatsApp. Intente de nuevo o entre por la red interna.";
                            return Page();
                        }

                        _auditoria.Registrar(usuario!, "segundo-factor-enviado", "", AdminUsuarios.Enmascarar(telefono));
                        Ingreso.GuardarTicket(HttpContext, ticket);
                        return RedirectToPage("/Codigo");
                    }
                }

                await Ingreso.FirmarAsync(HttpContext, usuario!, clienteId, segundoFactorVerificado: false);
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
