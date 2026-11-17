using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DIAN.Admin.Web
{
    /// <summary>Un documento tal como lo ve el portal (subconjunto de lo que guarda la API en <c>documentos\{numero}.json</c>).</summary>
    public sealed class DocumentoVista
    {
        public string Numero { get; set; } = string.Empty;
        public string TipoDocErp { get; set; } = string.Empty;

        /// <summary>Código DIAN del tipo: 01 factura, 91 nota crédito, 92 nota débito...</summary>
        public string TipoDocDian { get; set; } = string.Empty;
        public int Estado { get; set; }
        public string CUFE { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public bool Habilitacion { get; set; }
        public bool Pdf { get; set; }
        public string PdfMensaje { get; set; } = string.Empty;
        public bool CorreoEnviado { get; set; }
        public bool WebhookPendiente { get; set; }
        public DateTime? WebhookEntregado { get; set; }
        public DateTime FechaRecibido { get; set; }
        public DateTime FechaActualizado { get; set; }

        /// <summary>Mismos valores que <c>EstadoDocumento</c> de la API: 0 recibido, 1 aprobado, 2 rechazado, 3 error.</summary>
        public string EstadoTexto => Estado switch { 0 => "Recibido", 1 => "Aprobado", 2 => "Rechazado", 3 => "Error", _ => "?" };
    }

    /// <summary>
    /// Lectura (solo lectura) de los documentos que emite la API para un cliente. Comparte la carpeta de datos con la API
    /// (<c>Datos:Carpeta</c>); el Admin nunca escribe ahí.
    /// </summary>
    public sealed class LectorDocumentos
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly string _carpetaDatos;

        public LectorDocumentos(string carpetaDatos)
        {
            _carpetaDatos = Path.GetFullPath(carpetaDatos);
        }

        private string CarpetaDocumentos(string clienteId) => Path.Combine(_carpetaDatos, Nombre(clienteId), "documentos");

        /// <summary>Los documentos más recientes del cliente, el último movimiento primero.</summary>
        public IReadOnlyList<DocumentoVista> Recientes(string clienteId, int cantidad = 100)
        {
            var carpeta = CarpetaDocumentos(clienteId);
            if (!Directory.Exists(carpeta))
            {
                return Array.Empty<DocumentoVista>();
            }

            var lista = new List<DocumentoVista>();
            foreach (var archivo in Directory.GetFiles(carpeta, "*.json"))
            {
                try
                {
                    var doc = JsonSerializer.Deserialize<DocumentoVista>(File.ReadAllText(archivo), Opciones);
                    if (doc != null && !string.IsNullOrEmpty(doc.Numero))
                    {
                        lista.Add(doc);
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    // Un archivo a medio escribir no debe tumbar la lista.
                }
            }

            return lista.OrderByDescending(d => d.FechaActualizado).Take(Math.Max(1, cantidad)).ToList();
        }

        /// <summary>El PDF oficial del documento, solo si existe (la API lo genera únicamente para documentos aprobados).</summary>
        public byte[]? Pdf(string clienteId, string numero)
        {
            var carpeta = Path.GetFullPath(CarpetaDocumentos(clienteId));
            var archivo = Path.GetFullPath(Path.Combine(carpeta, Nombre(numero) + ".pdf"));
            // Nunca salir de la carpeta del cliente, diga lo que diga el número.
            return archivo.StartsWith(carpeta + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(archivo)
                ? File.ReadAllBytes(archivo)
                : null;
        }

        // Misma regla que usa la API para nombrar los archivos.
        private static string Nombre(string? texto)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = new string((texto ?? string.Empty).Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }
    }
}
