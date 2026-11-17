using System;
using System.Text;
using DIAN.Pos.Servicios;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api.Controllers
{
    // Mismas rutas que la API anterior. Atiende documento soporte ("05"), tiquete POS ("20") y nota de ajuste del POS ("94").
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DocumentoElectronicoController : ControllerBase
    {
        private readonly ServiciosPos _servicios;
        private readonly ILogger<DocumentoElectronicoController> _registro;

        public DocumentoElectronicoController(ServiciosPos servicios, ILogger<DocumentoElectronicoController> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        [HttpPost]
        public ActionResult<ResponseDian> EnviarDE(MetaDatosDE de)
        {
            if (de?.TipoDocumento == null)
            {
                return BadRequest("No se ha especificado un tipo de dcumento electrónico válido");
            }

            switch (de.TipoDocumento.Codigo)
            {
                case "05":
                    try
                    {
                        return Ok(_servicios.DocumentoSoporte(HttpContext.ObtenerTenant()).Enviar(de));
                    }
                    catch (Exception ex)
                    {
                        _registro.LogError(ex, "Error enviando documento soporte");
                        return BadRequest($"Ocurrió un error inesperado el documento no se envió: {ex.Message}");
                    }
                case "94": // Nota de ajuste crédito del POS
                    try
                    {
                        NotaAjustePosServicio.Validar(de);
                        return Ok(_servicios.NotaAjustePos(HttpContext.ObtenerTenant()).Enviar(de));
                    }
                    catch (ArgumentException ex)
                    {
                        return BadRequest(ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Conflict(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _registro.LogError(ex, "Error enviando nota de ajuste POS");
                        return StatusCode(StatusCodes.Status502BadGateway, "No fue posible confirmar el envío de la nota. Consulte su estado antes de reintentar.");
                    }
                case "20": // Tiquete POS electrónico
                    try
                    {
                        return Ok(_servicios.DocumentoEquivalente(HttpContext.ObtenerTenant()).Enviar(de));
                    }
                    catch (Exception ex)
                    {
                        _registro.LogError(ex, "Error enviando documento equivalente POS");
                        return BadRequest($"Ocurrió un error inesperado el documento no se envió: {ex.Message}");
                    }
            }

            return BadRequest($"Este servicio no está configurado para enviar documentos electrónicos de tipo: [{de.TipoDocumento.Codigo}]");
        }

        [HttpPost]
        public ActionResult<ResponseDian> EnviarDEJson(string JsonDE)
        {
            if (string.IsNullOrEmpty(JsonDE))
            {
                return BadRequest("No se ha recibido un objeto válidi");
            }

            MetaDatosDE? de;
            try
            {
                var original = Encoding.UTF8.GetString(Convert.FromBase64String(JsonDE));
                de = System.Text.Json.JsonSerializer.Deserialize<MetaDatosDE>(original);
            }
            catch (Exception)
            {
                return BadRequest("Ocurrió un error desserializando el ojeto");
            }

            return EnviarDE(de!);
        }

        [HttpPost]
        public ActionResult<ResponseDian> ConsultarDE(string track_id)
        {
            try
            {
                return Ok(_servicios.DocumentoEquivalente(HttpContext.ObtenerTenant()).Consultar(track_id));
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error consultando documento");
                return BadRequest($"Ocurrió un error inesperado en la consulta: {ex.Message}");
            }
        }
    }
}
