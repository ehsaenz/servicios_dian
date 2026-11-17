using System;
using System.Linq;
using System.Threading.Tasks;
using BasicLibraryExt;
using DIAN.Nomina.Servicios;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NominaElectronica.MetaDatos;

namespace DIAN.Pos.Api.Controllers
{
    // Mismas rutas y cuerpos que la API de nómina anterior: api/v1/Nomina/{EnviarNE, CrearXML, ConsultarNE/{TractID}}.
    [Route("api/v1/[controller]/[action]")]
    [ApiController]
    public class NominaController : ControllerBase
    {
        private readonly ServiciosNomina _servicios;
        private readonly ILogger<NominaController> _registro;

        public NominaController(ServiciosNomina servicios, ILogger<NominaController> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        // El estado "bueno" de la API anterior: códigos 00 a 99 y SP00 a SP99 (envío al set de pruebas) responden 200.
        // Igual que la API anterior (se midió su comportamiento): 00 a 99 y SP00 a SP99 responden 200 (el 99 es la validación con
        // errores de la DIAN, con su detalle en el cuerpo); 101, 108, etc. responden 400. Única diferencia: el 100 de CrearXML
        // ("XML creado, no enviado") es un éxito y ahora responde 200; antes respondía 400 por una comparación de rango.
        private static bool EsExito(string codigo)
        {
            if (codigo == "100")
            {
                return true;
            }

            if (codigo.StartsWith("SP", StringComparison.Ordinal))
            {
                codigo = codigo.Substring(2);
            }

            return int.TryParse(codigo, out var numero) && numero >= 0 && numero <= 99;
        }

        private async Task<ActionResult<ResponseDian>> Procesar(MetaDatosNEI nei, bool soloCrearXml)
        {
            var tenant = HttpContext.ObtenerTenant();
            if (nei?.Empleador?.NIT != tenant.Emisor.Nit)
            {
                return BadRequest(new ResponseDian
                {
                    statusCode = "108",
                    statusMessage = $"Este servicio no está registrado al NIT del emisor de éste documento ({nei?.Empleador?.NIT})"
                });
            }

            ResponseDian respuesta;
            try
            {
                respuesta = await _servicios.Nomina(tenant).GenerarAsync(nei, soloCrearXml);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ResponseDian { statusCode = "108", statusMessage = ex.Message });
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error procesando la nómina");
                return BadRequest(new ResponseDian { statusCode = "99", statusMessage = $"Ocurrió un error inesperado el documento no se envió: {ex.Message}" });
            }

            respuesta.statusCode ??= "99";
            return EsExito(respuesta.statusCode) ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPost]
        public Task<ActionResult<ResponseDian>> EnviarNE(MetaDatosNEI nei) => Procesar(nei, false);

        [HttpPost]
        public Task<ActionResult<ResponseDian>> CrearXML(MetaDatosNEI nei) => Procesar(nei, true);

        [HttpGet("{TractID}")]
        public async Task<ActionResult<ResponseDian>> ConsultarNE(string TractID)
        {
            ResponseDian respuesta;
            try
            {
                respuesta = await _servicios.Nomina(HttpContext.ObtenerTenant()).ConsultarAsync(TractID);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ResponseDian { statusCode = "108", statusMessage = ex.Message, TrackID = TractID });
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error consultando la nómina");
                return BadRequest(new ResponseDian { statusCode = "99", statusMessage = $"Ocurrió un error inesperado en la consulta: {ex.Message}", TrackID = TractID });
            }

            if (int.TryParse(respuesta.statusCode, out var codigo) && codigo >= 0 && codigo <= 99)
            {
                return Ok(respuesta);
            }

            if (respuesta.Errors.Any(e => e.Codigo == "DB_NOT_FOUND"))
            {
                return NotFound(respuesta);
            }

            return BadRequest(respuesta);
        }
    }
}
