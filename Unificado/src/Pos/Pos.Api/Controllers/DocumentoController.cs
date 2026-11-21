using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Traduccion;
using FE.Models.Doc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DIAN.Pos.Api.Controllers
{
    /// <summary>
    /// Documentos electrónicos del servicio unificado. El ERP envía el JSON de <c>DocumentoDian</c> y después consulta el
    /// estado. El PDF oficial solo existe cuando la DIAN aprobó el documento.
    /// </summary>
    [Route("api/documento")]
    [ApiController]
    public class DocumentoController : ControllerBase
    {
        private readonly ServiciosPos _servicios;
        private readonly ILogger<DocumentoController> _registro;

        public DocumentoController(ServiciosPos servicios, ILogger<DocumentoController> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        /// <summary>Recibe el documento: 202 con su estado (y CUFE). Idempotente por número.</summary>
        [HttpPost("agregar")]
        public async Task<IActionResult> Agregar()
        {
            DocumentoDian? doc;
            try
            {
                using var lector = new StreamReader(Request.Body);
                // Se lee igual que la API anterior: JObject.ToObject, el contrato de QuantyERP.
                doc = JObject.Parse(await lector.ReadToEndAsync()).ToObject<DocumentoDian>();
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is ArgumentException)
            {
                return BadRequest("El cuerpo no es un documento válido: " + ex.Message);
            }

            var servicio = _servicios.Documentos(HttpContext.ObtenerTenant());

            // Envío al set de pruebas (lo pide el ERP al pasar a producción): no guarda el documento en el almacén.
            if (string.Equals(Request.Headers["X-Envio-Set"].ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Ok(servicio.ValidarEnSet(doc!));
                }
                catch (DocumentoNoTraducibleException ex)
                {
                    return BadRequest(ex.Message);
                }
            }

            DocumentoRegistro registro;
            try
            {
                registro = servicio.Agregar(doc!);
            }
            catch (DocumentoNoTraducibleException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error recibiendo documento");
                return BadRequest("No se pudo procesar el documento: " + ex.Message);
            }

            if (registro.Estado == EstadoDocumento.Recibido)
            {
                if (_servicios.EnvioEnSegundoPlano)
                {
                    var numero = registro.Numero;
                    _ = Task.Run(() => servicio.Procesar(numero));
                }
                else
                {
                    registro = servicio.Procesar(registro.Numero);
                }
            }

            return Accepted(registro);
        }

        /// <summary>Reintenta el envío de un documento recibido, rechazado o con error (el ERP puede pedirlo).</summary>
        [HttpPost("reintentar/{numero}")]
        public IActionResult Reintentar(string numero)
        {
            var servicio = _servicios.Documentos(HttpContext.ObtenerTenant());
            if (servicio.Obtener(numero) == null) return NotFound();
            return Ok(servicio.Procesar(numero));
        }

        [HttpGet("estado/{numero}")]
        public IActionResult Estado(string numero)
        {
            var registro = _servicios.Documentos(HttpContext.ObtenerTenant()).Obtener(numero);
            return registro == null ? NotFound() : Ok(registro);
        }

        /// <summary>Documentos que cambiaron después del cursor: el ERP guarda la última <c>SecuenciaCambio</c> que vio.</summary>
        [HttpGet("cambios")]
        public IActionResult Cambios([FromQuery] long desde = 0, [FromQuery] int limite = 100)
        {
            var cambios = _servicios.Documentos(HttpContext.ObtenerTenant()).Cambios(desde, Math.Min(limite, 500));
            return Ok(new
            {
                Cursor = cambios.Count == 0 ? desde : cambios.Max(c => c.SecuenciaCambio),
                Documentos = cambios
            });
        }

        /// <summary>
        /// PDF oficial. Solo existe para documentos aprobados por la DIAN: un documento no aprobado responde 409 (el borrador
        /// con la marca de "no validado" es cosa del ERP, no de este servicio).
        /// </summary>
        [HttpGet("pdf/{numero}")]
        public IActionResult Pdf(string numero)
        {
            var servicio = _servicios.Documentos(HttpContext.ObtenerTenant());
            var registro = servicio.Obtener(numero);
            if (registro == null) return NotFound();
            if (registro.Estado != EstadoDocumento.Aprobado) return Conflict("El documento no está aprobado por la DIAN: no hay PDF oficial.");

            var pdf = servicio.Pdf(numero);
            if (pdf == null) return StatusCode(StatusCodes.Status503ServiceUnavailable, servicio.Obtener(numero)!.PdfMensaje);
            return File(pdf, "application/pdf", numero + ".pdf");
        }

        [HttpGet("xml/{numero}")]
        public IActionResult Xml(string numero)
        {
            var xml = _servicios.Documentos(HttpContext.ObtenerTenant()).XmlFirmado(numero);
            return xml == null ? NotFound() : File(xml, "application/xml", numero + ".xml");
        }

        [HttpGet("respuesta-dian/{numero}")]
        public IActionResult RespuestaDian(string numero)
        {
            var xml = _servicios.Documentos(HttpContext.ObtenerTenant()).XmlRespuestaDian(numero);
            return xml == null ? NotFound() : File(xml, "application/xml", numero + "-respuesta.xml");
        }
    }
}
