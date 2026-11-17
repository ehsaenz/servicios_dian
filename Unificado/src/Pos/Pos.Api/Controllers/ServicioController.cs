using DIAN.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace DIAN.Pos.Api.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ServicioController : ControllerBase
    {
        /// <summary>True si los envíos de este cliente van a habilitación (es lo que ocurre mientras no tenga producción autorizada).</summary>
        [HttpGet]
        public ActionResult<bool> GetAmbientePruebas()
        {
            var tenant = HttpContext.ObtenerTenant();
            var tipo = tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte) != null ? TipoSoftware.DocumentoSoporte : TipoSoftware.DocumentoEquivalente;
            return tenant.ObtenerSoftware(tipo) == null ? true : tenant.EsHabilitacion(tipo);
        }

        /// <summary>Diagnóstico: con qué cliente identificó la clave de API.</summary>
        [HttpGet]
        public IActionResult Quien()
        {
            var tenant = HttpContext.ObtenerTenant();
            return Ok(new { tenant.Id, tenant.Emisor.Nit, tenant.Emisor.RazonSocial });
        }
    }
}
