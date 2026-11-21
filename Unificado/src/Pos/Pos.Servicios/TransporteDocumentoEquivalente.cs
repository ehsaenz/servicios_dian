using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using eFacturacionColombia_NETCore.Tipos.Standard;
using FacturacionColombia_Metadatos;

namespace DIAN.Pos.Servicios
{
    /// <summary>Carpetas de trabajo de un cliente: <c>datos\{cliente}\XML</c>.</summary>
    internal sealed class RutasCliente
    {
        private readonly string _xml;

        public RutasCliente(string carpetaDatos, string idCliente)
        {
            _xml = Path.Combine(carpetaDatos, Nombres.Seguro(idCliente), "XML");
        }

        public string Xml(string idDoc) => Path.Combine(_xml, Nombres.Seguro(idDoc) + ".xml");

        public string XmlSinFirmar(string idDoc) => Xml(idDoc + "-unsigned");

        public string AppResponse(string idDoc) => Path.Combine(_xml, Nombres.Seguro(idDoc) + "-appresponse.xml");

        public string RespuestaDian(string idDoc) => Path.Combine(_xml, Nombres.Seguro(idDoc) + "-dian.json");

        public void Crear() => Directory.CreateDirectory(_xml);
    }

    internal static class Nombres
    {
        public static string Seguro(string? texto)
        {
            var limpio = new string((texto ?? string.Empty).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }
    }

    /// <summary>
    /// Envío y consulta de documentos equivalentes (POS y notas de ajuste) de UN cliente.
    /// En habilitación, si el software tiene TestSetId, el envío va al set de pruebas y luego se consulta (como la API
    /// anterior); sin TestSetId, o en producción, el envío es síncrono.
    /// </summary>
    internal sealed class TransporteDocumentoEquivalente
    {
        private readonly IEnviadorDian _enviador;
        private readonly bool _habilitacion;
        private readonly SoftwareConfig _software;
        private readonly RutasCliente _rutas;
        private readonly TimeSpan _esperaSet;

        public TransporteDocumentoEquivalente(IEnviadorDian enviador, bool habilitacion, SoftwareConfig software, RutasCliente rutas, TimeSpan esperaSet)
        {
            _enviador = enviador;
            _habilitacion = habilitacion;
            _software = software;
            _rutas = rutas;
            _esperaSet = esperaSet;
        }

        public ResponseDian Enviar(byte[] firmado, string idDoc)
        {
            var resp = new ResponseDian();

            if (_habilitacion && !string.IsNullOrEmpty(_software.TestSetId))
            {
                var subida = _enviador.EnviarSetPruebas(new List<byte[]> { firmado }, _software.TestSetId);
                if (_esperaSet > TimeSpan.Zero)
                {
                    Thread.Sleep(_esperaSet);
                }

                var dian = _enviador.ConsultarDocumentos(subida.ZipKey).FirstOrDefault()
                           ?? throw new InvalidOperationException("La DIAN no devolvió información del envío al set de pruebas.");

                resp.TrackID = dian.XmlDocumentKey ?? subida.ZipKey;
                resp.UUID = dian.XmlDocumentKey ?? string.Empty;
                resp.statusCode = dian.StatusCode;
                resp.statusMessage = dian.StatusDescription ?? dian.StatusMessage;
                AgregarMensajes(resp, dian);
                GuardarAppResponse(resp, dian, idDoc);
                return resp;
            }

            var respuesta = _enviador.EnviarDocumento(firmado);
            resp.statusCode = respuesta.StatusCode;
            resp.statusMessage = respuesta.StatusMessage;
            if (respuesta.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE)
            {
                resp.TrackID = respuesta.XmlDocumentKey;
                resp.UUID = respuesta.XmlDocumentKey;
            }

            AgregarMensajes(resp, respuesta);
            GuardarAppResponse(resp, respuesta, idDoc);
            return resp;
        }

        public ResponseDian Consultar(string trackId, string idDoc, bool porCude = false)
        {
            var resp = new ResponseDian();

            if (_habilitacion && !porCude)
            {
                var dian = _enviador.ConsultarDocumentos(trackId).FirstOrDefault()
                           ?? throw new InvalidOperationException("La DIAN no devolvió información para ese identificador.");
                resp.statusCode = dian.StatusCode;
                resp.statusMessage = dian.StatusDescription ?? dian.StatusMessage;
                AgregarMensajes(resp, dian);
                GuardarAppResponse(resp, dian, idDoc);
                return resp;
            }

            var respuesta = _enviador.ConsultarDocumento(trackId);
            resp.statusCode = respuesta.StatusCode;
            resp.statusMessage = respuesta.StatusMessage;
            AgregarMensajes(resp, respuesta);
            GuardarAppResponse(resp, respuesta, idDoc);
            return resp;
        }

        private static void AgregarMensajes(ResponseDian resp, DianResponse dian)
        {
            if (dian.ErrorMessage == null)
            {
                return;
            }

            foreach (var mensaje in dian.ErrorMessage)
            {
                resp.AddMessage(mensaje);
            }
        }

        private void GuardarAppResponse(ResponseDian resp, DianResponse dian, string idDoc)
        {
            if (string.IsNullOrEmpty(idDoc))
            {
                return;
            }

            if (dian.XmlBase64Bytes == null)
            {
                var mensaje = dian.StatusDescription ?? dian.StatusMessage ?? string.Empty;
                if (dian.StatusCode == "2" && mensaje.Contains("se encuentra Aceptado", StringComparison.OrdinalIgnoreCase))
                {
                    _rutas.Crear();
                    File.WriteAllText(_rutas.RespuestaDian(idDoc), JsonSerializer.Serialize(new
                    {
                        StatusCode = dian.StatusCode,
                        StatusMessage = mensaje
                    }));
                }

                return;
            }

            var texto = Encoding.UTF8.GetString(dian.XmlBase64Bytes);
            _rutas.Crear();
            File.WriteAllText(_rutas.AppResponse(idDoc), texto);

            var ar = ApplicationResponseType.DeserializeXmlString(texto);
            if (ar != null && DateTime.TryParse(ar.IssueDate.Value.ToString("yyyy-MM-dd") + " " + ar.IssueTime.Value, out var fecha))
            {
                resp.FechaValidacion = fecha;
            }
        }
    }
}
