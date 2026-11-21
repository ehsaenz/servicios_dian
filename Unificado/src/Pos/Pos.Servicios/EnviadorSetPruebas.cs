using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DIAN.Pos.Servicios.Documentos;
using eFacturacionColombia_NETCore.ServiciosWeb;

namespace DIAN.Pos.Servicios
{
    /// <summary>
    /// Envía un documento firmado al set de pruebas de la DIAN (<c>EnviarSetPruebas</c>), consulta el estado del lote
    /// (<c>ConsultarDocumentos</c>) y persiste el resultado en <c>set-{bloque}.json</c> dentro de la carpeta del cliente.
    /// </summary>
    public static class EnviadorSetPruebas
    {
        private static readonly JsonSerializerOptions JsonOpciones = new() { WriteIndented = true };

        public static ResultadoSet Enviar(
            IEnviadorDian enviador,
            byte[] firmado,
            string testSetId,
            string numero,
            string carpetaCliente,
            string nombreArchivoSet,
            TimeSpan esperaSet,
            string uuid = "")
        {
            var subida = enviador.EnviarSetPruebas(new List<byte[]> { firmado }, testSetId);

            // La DIAN no aceptó el envío: no hay ZipKey que consultar. Se registra como rechazado con sus mensajes.
            var errores = subida.ErrorMessageList?
                .Select(e => ((e.DocumentKey ?? string.Empty) + " " + (e.ProcessedMessage ?? string.Empty)).Trim())
                .Where(s => s.Length > 0)
                .ToList() ?? new List<string>();
            if (string.IsNullOrEmpty(subida.ZipKey) || errores.Count > 0)
            {
                var sinZip = ResultadoSet.Desde("99", string.Join(" | ", errores.DefaultIfEmpty("La DIAN no devolvió ZipKey.")), subida.ZipKey, numero, uuid);
                Guardar(carpetaCliente, nombreArchivoSet, sinZip);
                return sinZip;
            }

            if (esperaSet > TimeSpan.Zero) Thread.Sleep(esperaSet);

            var dian = enviador.ConsultarDocumentos(subida.ZipKey).FirstOrDefault();
            var mensaje = dian?.StatusDescription ?? dian?.StatusMessage;
            if (dian?.StatusCode == "99" && dian.ErrorMessage != null && dian.ErrorMessage.Length > 0)
            {
                mensaje = (mensaje ?? string.Empty) + " | " + string.Join(" | ", dian.ErrorMessage);
            }

            var resultado = ResultadoSet.Desde(dian?.StatusCode, mensaje, subida.ZipKey, numero, uuid);
            Guardar(carpetaCliente, nombreArchivoSet, resultado);
            return resultado;
        }

        private static void Guardar(string carpetaCliente, string nombreArchivoSet, ResultadoSet resultado)
        {
            Directory.CreateDirectory(carpetaCliente);
            var archivo = Path.Combine(carpetaCliente, nombreArchivoSet);
            var temporal = archivo + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(resultado, JsonOpciones));
            File.Move(temporal, archivo, true);
        }
    }
}
