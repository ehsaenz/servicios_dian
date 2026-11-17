using System;
using System.Threading.Tasks;
using DIAN.Nomina.Servicios;
using FacturacionColombia_Metadatos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api.Controllers
{
    // Mismas rutas y cuerpo que la API anterior: api/v1/Eventos/{AcuseFactura, ReclamoFactura, ReciboBienesServicios, AceptacionExpresa, AceptacionTacita}.
    // Antes exigían un usuario con sesión y permiso por evento; ahora basta la clave de API del cliente y que tenga configurado
    // el software de eventos RADIAN.
    [ApiController]
    [Route("api/v1/[controller]/[action]")]
    public class EventosController : ControllerBase
    {
        private readonly ServiciosNomina _servicios;
        private readonly ILogger<EventosController> _registro;

        public EventosController(ServiciosNomina servicios, ILogger<EventosController> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        private async Task<ActionResult<ResponseDian>> Procesar(EventoRadian evento, string codigoEsperado)
        {
            if (evento == null || evento.Codigo != codigoEsperado)
            {
                return BadRequest(new ResponseDian { statusCode = "101", statusMessage = $"El codigo de evento {evento?.Codigo} no corresponde a ésta opción" });
            }

            if (codigoEsperado == "031")
            {
                if (evento.Causa == null)
                    return BadRequest(new ResponseDian { statusCode = "101", statusMessage = "El Evento 031 que se especifique una causa" });
                if (string.IsNullOrEmpty(evento.Causa.Codigo))
                    return BadRequest(new ResponseDian { statusCode = "101", statusMessage = "El Evento 031 requiere que se especifique el código de la causa" });
                if (string.IsNullOrEmpty(evento.Causa.Significado))
                    return BadRequest(new ResponseDian { statusCode = "101", statusMessage = "El Evento 031 requiere que se especifique el significado de la causa" });
            }

            ResponseDian respuesta;
            try
            {
                respuesta = await _servicios.Eventos(HttpContext.ObtenerTenant()).GenerarEventoAsync(evento);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ResponseDian { statusCode = "108", statusMessage = ex.Message });
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error generando el evento {Codigo}", codigoEsperado);
                return BadRequest(new ResponseDian { statusCode = "99", statusMessage = $"Ocurrió un error inesperado, el evento no se envió: {ex.Message}" });
            }

            // Como antes: códigos 00 a 99 responden 200; los demás (101, 108, 109, 199...) responden 400.
            return int.TryParse(respuesta.statusCode, out var codigo) && codigo >= 0 && codigo <= 99 ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPost]
        public Task<ActionResult<ResponseDian>> AcuseFactura(EventoRadian evento) => Procesar(evento, "030");

        [HttpPost]
        public Task<ActionResult<ResponseDian>> ReclamoFactura(EventoRadian evento) => Procesar(evento, "031");

        [HttpPost]
        public Task<ActionResult<ResponseDian>> ReciboBienesServicios(EventoRadian evento) => Procesar(evento, "032");

        [HttpPost]
        public Task<ActionResult<ResponseDian>> AceptacionExpresa(EventoRadian evento) => Procesar(evento, "033");

        [HttpPost]
        public Task<ActionResult<ResponseDian>> AceptacionTacita(EventoRadian evento) => Procesar(evento, "034");
    }
}
