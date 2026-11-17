using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DIAN.Admin.Web.Pages
{
    public class ClienteModel : PageModel
    {
        private const long MaximoArchivo = 1024 * 1024;
        private readonly ClienteAdminServicio _admin;
        private readonly AdminUsuarios _usuarios;
        private readonly Auditoria _auditoria;

        private readonly LectorDocumentos _documentos;

        public ClienteModel(ClienteAdminServicio admin, AdminUsuarios usuarios, Auditoria auditoria, LectorDocumentos documentos)
        {
            _admin = admin;
            _usuarios = usuarios;
            _auditoria = auditoria;
            _documentos = documentos;
        }

        /// <summary>Lista de verificación para pasar a producción (solo la ve el operador).</summary>
        public System.Collections.Generic.IReadOnlyList<ItemProduccion> Lista { get; private set; } = System.Array.Empty<ItemProduccion>();

        public string? Impedimentos => ListaProduccion.Impedimentos(Lista);

        /// <summary>Documentos recientes del cliente (solo lectura).</summary>
        public System.Collections.Generic.IReadOnlyList<DocumentoVista> Documentos { get; private set; } = System.Array.Empty<DocumentoVista>();

        public ClienteRegistro Cliente { get; private set; } = new ClienteRegistro();
        public string? Mensaje { get; private set; }
        public string? Error { get; private set; }

        /// <summary>Clave de API recién creada. Solo existe en esta respuesta; no se guarda en ningún lado.</summary>
        public string? ClaveNueva { get; private set; }

        /// <summary>Secreto de firma del webhook recién creado o regenerado. Solo existe en esta respuesta.</summary>
        public string? SecretoWebhookNuevo { get; private set; }

        public SoftwareRegistro? Software(TipoSoftware tipo) => Cliente.Software.FirstOrDefault(s => s.TipoSoftware == tipo);

        private string Usuario => User.Identity!.Name!;

        public bool EsOperador => User.EsOperador();

        /// <summary>Usuarios del portal de este cliente (solo se muestran al operador).</summary>
        public System.Collections.Generic.IReadOnlyList<string> UsuariosPortal { get; private set; } = System.Array.Empty<string>();

        /// <summary>Contraseña inicial recién generada para un usuario de portal. Solo existe en esta respuesta.</summary>
        public string? ClaveUsuarioNueva { get; private set; }

        public string? UsuarioNuevo { get; private set; }

        // Lo que un usuario de cliente puede hacer sobre su propio cliente. Todo lo demás (emisor, software, certificado,
        // estado, producción y usuarios) es del operador, aunque alguien arme la petición a mano.
        private static readonly string[] AccionesDeCliente = { "Logo", "Sucursal", "LogoSucursal", "EliminarSucursal", "Webhook", "GenerarClave", "Revocar" };

        public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext contexto)
        {
            var id = contexto.RouteData.Values["id"]?.ToString();
            if (!User.PuedeVer(id))
            {
                // 404 y no 403: un cliente no debe poder averiguar qué otros ids existen.
                contexto.Result = NotFound();
                return;
            }

            if (!User.EsOperador() && contexto.HandlerMethod != null)
            {
                var nombre = contexto.HandlerMethod.MethodInfo.Name;
                var accion = nombre.StartsWith("OnPost", StringComparison.Ordinal) ? nombre.Substring(6).Replace("Async", "") : null;
                if (accion != null && !AccionesDeCliente.Contains(accion))
                {
                    contexto.Result = Forbid();
                }
            }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            return await Cargar(id) ? Page() : NotFound();
        }

        private async Task<bool> Cargar(string id)
        {
            var cliente = await _admin.ObtenerAsync(id);
            if (cliente == null)
            {
                return false;
            }

            Cliente = cliente;
            UsuariosPortal = _usuarios.DeCliente(cliente.Id);
            Documentos = _documentos.Recientes(cliente.Id, 50);
            if (User.EsOperador())
            {
                Lista = ListaProduccion.Evaluar(cliente, _documentos.Recientes(cliente.Id, 1000), DateTime.UtcNow);
            }
            return true;
        }

        public Task<IActionResult> OnPostCrearUsuarioAsync(string id, string usuario)
        {
            return Ejecutar(id, () =>
            {
                usuario = (usuario ?? string.Empty).Trim();
                var clave = AdminUsuarios.GenerarClave();
                try
                {
                    _usuarios.Crear(usuario, clave, id);
                }
                catch (ArgumentException ex)
                {
                    throw new ErrorAdministracion(ex.Message);
                }

                _auditoria.Registrar(Usuario, "usuario-portal-crear", id, usuario);
                UsuarioNuevo = usuario;
                ClaveUsuarioNueva = clave;
                return Task.FromResult("Usuario creado. Entregue la contraseña por un canal seguro: no se vuelve a mostrar.");
            });
        }

        public Task<IActionResult> OnPostRestablecerUsuarioAsync(string id, string usuario)
        {
            return Ejecutar(id, () =>
            {
                if (!_usuarios.DeCliente(id).Contains(usuario ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ErrorAdministracion("Ese usuario no pertenece a este cliente.");
                }

                var clave = AdminUsuarios.GenerarClave();
                _usuarios.Crear(usuario!, clave, id);
                _auditoria.Registrar(Usuario, "usuario-portal-restablecer", id, usuario!);
                UsuarioNuevo = usuario;
                ClaveUsuarioNueva = clave;
                return Task.FromResult("Contraseña restablecida. Entréguela por un canal seguro: no se vuelve a mostrar.");
            });
        }

        public Task<IActionResult> OnPostEliminarUsuarioAsync(string id, string usuario)
        {
            return Ejecutar(id, () =>
            {
                if (!_usuarios.EliminarDeCliente(id, usuario ?? string.Empty))
                {
                    throw new ErrorAdministracion("Ese usuario no pertenece a este cliente.");
                }

                _auditoria.Registrar(Usuario, "usuario-portal-eliminar", id, usuario!);
                return Task.FromResult("Usuario eliminado.");
            });
        }

        /// <summary>Ejecuta una acción de administración y muestra su resultado en la misma página.</summary>
        private async Task<IActionResult> Ejecutar(string id, Func<Task<string>> accion)
        {
            try
            {
                Mensaje = await accion();
            }
            catch (ErrorAdministracion ex)
            {
                Error = ex.Message;
            }

            return await Cargar(id) ? Page() : NotFound();
        }

        public Task<IActionResult> OnPostEmisorAsync(string id, string nit, string? dv, string razon, string? matricula, string? email, string? telefono, string? perfilCorreo)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.ActualizarEmisorAsync(Usuario, id, new EmisorConfig
                {
                    Nit = (nit ?? string.Empty).Trim(),
                    DV = (dv ?? string.Empty).Trim(),
                    RazonSocial = (razon ?? string.Empty).Trim(),
                    MatriculaMercantil = (matricula ?? string.Empty).Trim(),
                    Email = (email ?? string.Empty).Trim(),
                    NumeroTel = (telefono ?? string.Empty).Trim()
                }, perfilCorreo);
                return "Datos del emisor guardados.";
            });
        }

        public Task<IActionResult> OnPostSoftwareAsync(string id, TipoSoftware tipo, string identificador, string? pin, bool pruebas, string? testSet)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.GuardarSoftwareAsync(Usuario, id, tipo, identificador ?? string.Empty, pin, pruebas, testSet);
                return "Software guardado.";
            });
        }

        public Task<IActionResult> OnPostCertificadoAsync(string id, IFormFile? certificado, string? clave)
        {
            return Ejecutar(id, async () =>
            {
                if (certificado == null || certificado.Length == 0 || certificado.Length > MaximoArchivo)
                {
                    throw new ErrorAdministracion("Seleccione el archivo .p12 (hasta 1 MB).");
                }

                using var memoria = new MemoryStream();
                await certificado.CopyToAsync(memoria);
                var resultado = await _admin.SubirCertificadoAsync(Usuario, id, memoria.ToArray(), clave ?? string.Empty);
                var aviso = resultado.Advertencias.Count == 0 ? "" : " Atención: " + string.Join(" ", resultado.Advertencias);
                return $"Certificado guardado: {resultado.Sujeto}, vence {resultado.VenceUtc:yyyy-MM-dd}.{aviso}";
            });
        }

        public Task<IActionResult> OnPostLogoAsync(string id, IFormFile? logo)
        {
            return Ejecutar(id, async () =>
            {
                if (logo == null || logo.Length == 0 || logo.Length > MaximoArchivo)
                {
                    throw new ErrorAdministracion("Seleccione una imagen PNG o JPG (hasta 1 MB).");
                }

                using var memoria = new MemoryStream();
                await logo.CopyToAsync(memoria);
                await _admin.SubirLogoAsync(Usuario, id, logo.FileName, memoria.ToArray());
                return "Logo guardado.";
            });
        }

        public Task<IActionResult> OnPostSucursalAsync(string id, string matricula, string nombre, string? regimen)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.GuardarSucursalAsync(Usuario, id, matricula, nombre, regimen);
                return "Sucursal guardada.";
            });
        }

        public Task<IActionResult> OnPostLogoSucursalAsync(string id, string matricula, IFormFile? logo)
        {
            return Ejecutar(id, async () =>
            {
                if (logo == null || logo.Length == 0 || logo.Length > MaximoArchivo)
                {
                    throw new ErrorAdministracion("Seleccione una imagen PNG o JPG (hasta 1 MB).");
                }

                using var memoria = new MemoryStream();
                await logo.CopyToAsync(memoria);
                await _admin.SubirLogoSucursalAsync(Usuario, id, matricula, logo.FileName, memoria.ToArray());
                return "Logo de la sucursal guardado.";
            });
        }

        public Task<IActionResult> OnPostEliminarSucursalAsync(string id, string matricula)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.EliminarSucursalAsync(Usuario, id, matricula);
                return "Sucursal eliminada.";
            });
        }

        public Task<IActionResult> OnPostWebhookAsync(string id, string? url, bool regenerar)
        {
            return Ejecutar(id, async () =>
            {
                SecretoWebhookNuevo = await _admin.ConfigurarWebhookAsync(Usuario, id, url, regenerar);
                return string.IsNullOrWhiteSpace(url)
                    ? "Webhook quitado: el ERP consulta por polling."
                    : SecretoWebhookNuevo != null ? "Webhook guardado. Copie el secreto ahora: no se vuelve a mostrar." : "Webhook guardado.";
            });
        }

        public Task<IActionResult> OnPostGenerarClaveAsync(string id, string? descripcion)
        {
            return Ejecutar(id, async () =>
            {
                ClaveNueva = await _admin.GenerarClaveAsync(Usuario, id, descripcion ?? string.Empty);
                return "Clave de API creada. Cópiela ahora: no se vuelve a mostrar.";
            });
        }

        public Task<IActionResult> OnPostRevocarAsync(string id, string claveId)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.RevocarClaveAsync(Usuario, id, claveId);
                return "Clave revocada.";
            });
        }

        public Task<IActionResult> OnPostEstadoAsync(string id, bool activo)
        {
            return Ejecutar(id, async () =>
            {
                await _admin.CambiarEstadoAsync(Usuario, id, activo);
                return activo ? "Cliente activado." : "Cliente desactivado: sus claves dejan de funcionar.";
            });
        }

        public Task<IActionResult> OnPostProduccionAsync(string id, bool permitir, string? confirmacion)
        {
            return Ejecutar(id, async () =>
            {
                if (permitir)
                {
                    // Se revisa en el servidor, no solo en la pantalla: la lista decide, no el botón.
                    var actual = await _admin.ObtenerAsync(id) ?? throw new ErrorAdministracion("El cliente no existe.");
                    var impedimento = ListaProduccion.Impedimentos(ListaProduccion.Evaluar(actual, _documentos.Recientes(id, 1000), DateTime.UtcNow));
                    if (impedimento != null)
                    {
                        throw new ErrorAdministracion(impedimento + " Complete la lista de verificación antes de autorizar producción.");
                    }
                }

                await _admin.CambiarProduccionAsync(Usuario, id, permitir, confirmacion);
                return permitir ? "Producción autorizada para este cliente." : "Producción retirada: el cliente vuelve a habilitación.";
            });
        }
    }
}
