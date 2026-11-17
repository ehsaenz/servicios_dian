using System.Threading.Tasks;
using DIAN.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api
{
    /// <summary>
    /// Identifica al cliente por la cabecera <c>x-api-key</c>. Sin clave o con clave desconocida o inactiva responde 401.
    /// La clave nunca se escribe en el registro; solo el id del cliente.
    /// </summary>
    public sealed class TenantMiddleware
    {
        public const string Cabecera = "x-api-key";
        public const string ClaveItem = "DIAN.Tenant";

        private readonly RequestDelegate _siguiente;

        public TenantMiddleware(RequestDelegate siguiente)
        {
            _siguiente = siguiente;
        }

        public async Task InvokeAsync(HttpContext contexto, ITenantConfigProvider proveedor, ILogger<TenantMiddleware> registro)
        {
            // Rutas públicas: solo la de salud, sin datos.
            if (contexto.Request.Path.StartsWithSegments("/salud"))
            {
                await _siguiente(contexto);
                return;
            }

            var clave = contexto.Request.Headers[Cabecera].ToString();
            var tenant = string.IsNullOrEmpty(clave) ? null : await proveedor.BuscarPorClaveAsync(clave, contexto.RequestAborted);

            if (tenant == null || !tenant.Activo)
            {
                registro.LogWarning("Petición rechazada: clave de API ausente, desconocida o inactiva ({Ruta})", contexto.Request.Path.Value);
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await contexto.Response.WriteAsync("Clave de API inválida.");
                return;
            }

            contexto.Items[ClaveItem] = tenant;
            using (registro.BeginScope("Cliente {Tenant}", tenant.Id))
            {
                await _siguiente(contexto);
            }
        }
    }

    public static class TenantHttpContextExtensions
    {
        /// <summary>Cliente de la petición actual. Solo existe después del middleware.</summary>
        public static TenantConfig ObtenerTenant(this HttpContext contexto)
        {
            return (TenantConfig)contexto.Items[TenantMiddleware.ClaveItem]!;
        }
    }
}
