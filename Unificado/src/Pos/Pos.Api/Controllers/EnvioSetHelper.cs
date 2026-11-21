using System;
using DIAN.Pos.Servicios.Documentos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api.Controllers
{
    internal static class EnvioSetHelper
    {
        public static bool EsEnvioSet(this HttpRequest peticion) =>
            string.Equals(peticion.Headers["X-Envio-Set"].ToString(), "true", StringComparison.OrdinalIgnoreCase);

        public static IActionResult Ejecutar(ControllerBase controlador, ILogger registro, Func<ResultadoSet> accion)
        {
            try
            {
                return controlador.Ok(accion());
            }
            catch (ArgumentException ex)
            {
                return controlador.BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return controlador.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                registro.LogError(ex, "Error en el envío al set de pruebas");
                return controlador.StatusCode(StatusCodes.Status502BadGateway, "No se pudo enviar al set de pruebas: " + ex.Message);
            }
        }
    }
}
