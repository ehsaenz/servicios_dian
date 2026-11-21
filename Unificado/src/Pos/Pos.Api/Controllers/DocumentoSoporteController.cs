using System;
using DIAN.Pos.Servicios;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api.Controllers
{
    // Mismas rutas y cuerpos que la API anterior: api/DocumentoSoporte/EnviarDSNO y ConsultarDSNO.
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DocumentoSoporteController : ControllerBase
    {
        private readonly ServiciosPos _servicios;
        private readonly ILogger<DocumentoSoporteController> _registro;

        public DocumentoSoporteController(ServiciosPos servicios, ILogger<DocumentoSoporteController> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        [HttpPost]
        public IActionResult EnviarDSNO(MetaDatosDE de)
        {
            if (de.TipoDocumento == null)
            {
                de.TipoDocumento = TipoDocumentoElectronico.DOCUMENTO_SOPORTE;
            }

            var tenant = HttpContext.ObtenerTenant();
            if (Request.EsEnvioSet())
            {
                return EnvioSetHelper.Ejecutar(this, _registro, () => _servicios.DocumentoSoporte(tenant).ValidarEnSet(de));
            }

            try
            {
                return Ok(_servicios.DocumentoSoporte(tenant).Enviar(de));
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error enviando documento soporte");
                return BadRequest($"Ocurrió un error inesperado el documento no se envió: {ex.Message}");
            }
        }

        [HttpPost]
        public ActionResult<ResponseDian> ConsultarDSNO(string track_id)
        {
            try
            {
                return Ok(_servicios.DocumentoSoporte(HttpContext.ObtenerTenant()).Consultar(track_id));
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error consultando documento soporte");
                return BadRequest($"Ocurrió un error inesperado en la consulta: {ex.Message}");
            }
        }
    }
}
