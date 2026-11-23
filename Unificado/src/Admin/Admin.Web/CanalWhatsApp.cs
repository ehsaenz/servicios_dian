using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Admin.Web
{
    /// <summary>Envía un mensaje de WhatsApp. Se separa en interfaz para poder probar el ingreso sin gateway.</summary>
    public interface ICanalWhatsApp
    {
        /// <summary>True si el gateway aceptó el mensaje. «Aceptado» no es «leído»: no hay acuse de entrega.</summary>
        Task<bool> EnviarAsync(string telefono, string mensaje, CancellationToken cancelar = default);
    }

    /// <summary>
    /// Gateway «WhatsApp Multi-Client API» de SISTEMAS 360, el mismo que usa QuantyERP:
    /// <c>POST {url}/clients/{sesion}/send-message</c> con <c>multipart/form-data</c> (<c>phone</c> y <c>message</c>)
    /// y la clave en la cabecera <c>x-api-key</c>.
    /// </summary>
    /// <remarks>
    /// La sesión del gateway es un WhatsApp vinculado por QR, no la API oficial de empresa: si se desvincula, los envíos
    /// fallan hasta que soporte la reconecte. Por eso el segundo factor solo se exige en la entrada pública y el panel
    /// sigue alcanzable por la red interna.
    /// </remarks>
    public sealed class CanalWhatsApp : ICanalWhatsApp
    {
        private readonly IHttpClientFactory _fabrica;
        private readonly OpcionesWhatsApp _opciones;
        private readonly Microsoft.Extensions.Logging.ILogger<CanalWhatsApp> _registro;

        public CanalWhatsApp(IHttpClientFactory fabrica, OpcionesWhatsApp opciones, Microsoft.Extensions.Logging.ILogger<CanalWhatsApp> registro)
        {
            _fabrica = fabrica;
            _opciones = opciones;
            _registro = registro;
        }

        public async Task<bool> EnviarAsync(string telefono, string mensaje, CancellationToken cancelar = default)
        {
            if (!_opciones.Configurado)
            {
                _registro.LogWarning("WhatsApp sin configurar: falta la URL, la sesión o la clave (Whatsapp:*).");
                return false;
            }

            try
            {
                using var cliente = _fabrica.CreateClient("whatsapp");
                cliente.Timeout = TimeSpan.FromSeconds(_opciones.SegundosEspera);
                using var contenido = new MultipartFormDataContent
                {
                    { new StringContent(telefono), "phone" },
                    { new StringContent(mensaje), "message" }
                };

                var url = _opciones.Url.TrimEnd('/') + "/clients/" + Uri.EscapeDataString(_opciones.Sesion) + "/send-message";
                using var peticion = new HttpRequestMessage(HttpMethod.Post, url) { Content = contenido };
                peticion.Headers.TryAddWithoutValidation("x-api-key", _opciones.ClaveApi);

                using var respuesta = await cliente.SendAsync(peticion, cancelar);
                if (respuesta.IsSuccessStatusCode)
                {
                    return true;
                }

                // El cuerpo del gateway no se registra: puede repetir el número de destino.
                _registro.LogWarning("El gateway de WhatsApp respondió {Codigo}.", (int)respuesta.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _registro.LogWarning(ex, "No se pudo enviar el WhatsApp del segundo factor.");
                return false;
            }
        }
    }

    /// <summary>Configuración del gateway. La clave vive en el entorno del servidor (<c>Whatsapp__ClaveApi</c>), nunca en el repositorio.</summary>
    public sealed class OpcionesWhatsApp
    {
        public string Url { get; init; } = "https://services.sistemas360.com:47112";
        public string Sesion { get; init; } = "QuantyERPWhatsapp";
        public string ClaveApi { get; init; } = string.Empty;
        public int SegundosEspera { get; init; } = 20;

        public bool Configurado =>
            !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Sesion) && !string.IsNullOrWhiteSpace(ClaveApi);

        public static OpcionesWhatsApp Desde(Microsoft.Extensions.Configuration.IConfiguration configuracion) => new OpcionesWhatsApp
        {
            Url = configuracion["Whatsapp:Url"] ?? "https://services.sistemas360.com:47112",
            Sesion = configuracion["Whatsapp:Sesion"] ?? "QuantyERPWhatsapp",
            ClaveApi = configuracion["Whatsapp:ClaveApi"] ?? string.Empty,
            SegundosEspera = configuracion.GetValue("Whatsapp:SegundosEspera", 20)
        };
    }
}
