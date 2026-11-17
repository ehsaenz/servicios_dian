using System;
using System.Collections.Generic;
using DIAN.Impresion;
using DIAN.Tenants;

namespace DIAN.Pos.Servicios.Documentos
{
    public sealed class AdjuntoCorreo
    {
        public string Nombre { get; set; } = string.Empty;
        public string TipoMime { get; set; } = "application/octet-stream";
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
    }

    /// <summary>Envío del correo con el documento al adquiriente. Implementación: el servicio EmailAPI (por definir).</summary>
    public interface IEnviadorCorreo
    {
        void Enviar(string destinatario, string asunto, string cuerpo, IReadOnlyList<AdjuntoCorreo> adjuntos);
    }

    /// <summary>Lo que se hace con un documento una vez aprobado. Todo es opcional: sin generador no hay PDF, sin correo no se envía.</summary>
    public sealed class SalidasDocumento
    {
        public IGeneradorPdf? Pdf { get; set; }
        public IEnviadorCorreo? Correo { get; set; }

        /// <summary>Perfil de impresión (razón social y logo) del cliente para una matrícula. Sin él se usa solo la razón social.</summary>
        public Func<TenantConfig, string, PerfilImpresion>? Perfil { get; set; }

        /// <summary>Aviso al webhook del cliente (solo si el cliente tiene uno configurado).</summary>
        public IEntregaWebhook Webhook { get; set; } = new EntregaWebhookHttp();
    }
}

namespace DIAN.Pos.Servicios.Documentos
{
    /// <summary>Entrega de un aviso al webhook del cliente. Lanza una excepción si no se entregó (no hubo respuesta 2xx).</summary>
    public interface IEntregaWebhook
    {
        void Entregar(string url, string secreto, string cuerpoJson);
    }

    /// <summary>Entrega por HTTP: POST del JSON con <c>X-Dian-Firma: sha256=HMAC(secreto, cuerpo)</c>. Sin seguir redirecciones.</summary>
    public sealed class EntregaWebhookHttp : IEntregaWebhook
    {
        private static readonly System.Net.Http.HttpClient Cliente = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public void Entregar(string url, string secreto, string cuerpoJson)
        {
            using var peticion = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, url)
            {
                Content = new System.Net.Http.StringContent(cuerpoJson, System.Text.Encoding.UTF8, "application/json")
            };
            peticion.Headers.Add("X-Dian-Firma", Firma(secreto, cuerpoJson));
            peticion.Headers.Add("X-Dian-Evento", "documento.estado");

            using var respuesta = Cliente.Send(peticion);
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("El webhook respondió " + (int)respuesta.StatusCode);
            }
        }

        public static string Firma(string secreto, string cuerpoJson)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secreto));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cuerpoJson))).ToLowerInvariant();
        }
    }
}
