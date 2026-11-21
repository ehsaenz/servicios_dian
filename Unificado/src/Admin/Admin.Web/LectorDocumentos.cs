using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using DIAN.Tenants;

namespace DIAN.Admin.Web
{
    /// <summary>Último estado conocido del set de pruebas DIAN de un bloque (leído de <c>set-{bloque}.json</c>).</summary>
    public sealed class EstadoSetBloque
    {
        public string Estado { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public string ZipKey { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public DateTime? Fecha { get; set; }
        public DateTime? FechaUtc { get; set; }

        public string MensajeVisible => !string.IsNullOrWhiteSpace(Mensaje) ? Mensaje : StatusMessage;

        public bool Aceptado
        {
            get
            {
                var msg = MensajeVisible;
                var codigo = !string.IsNullOrWhiteSpace(StatusCode) ? StatusCode : Codigo;
                if (!string.IsNullOrWhiteSpace(codigo) && !string.IsNullOrWhiteSpace(msg))
                {
                    return EstadoSetDian.Interpretar(codigo, msg) == EstadoSetDian.Resultado.Aceptado;
                }

                if (string.Equals(Estado?.Trim(), "aceptado", StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(msg) ||
                           EstadoSetDian.Interpretar("2", msg) == EstadoSetDian.Resultado.Aceptado;
                }

                return false;
            }
        }
    }

    /// <summary>Conteo informativo y estado oficial del set DIAN por módulo (POS, soporte, nómina, RADIAN, FE).</summary>
    public sealed class EvidenciaSets
    {
        public int TiquetesPosAprobados { get; set; }
        public int NotasAjustePosAprobadas { get; set; }
        public bool SetPosAceptado { get; set; }
        public int DocumentosSoporteAprobados { get; set; }
        public int NotasAjusteDocumentoSoporteAprobadas { get; set; }
        public int NominasIndividualesAprobadas { get; set; }
        public int NominasAjusteAprobadas { get; set; }
        public bool SetNominaAceptado { get; set; }
        public int EventosRadianAprobados { get; set; }
        public Dictionary<TipoSoftware, EstadoSetBloque> Sets { get; } = new();

        public EstadoSetBloque? SetDe(TipoSoftware tipo) => Sets.TryGetValue(tipo, out var s) ? s : null;
    }
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

        private string CarpetaCliente(string clienteId) => Path.Combine(_carpetaDatos, Nombre(clienteId));

        private string CarpetaDocumentos(string clienteId) => Path.Combine(CarpetaCliente(clienteId), "documentos");

        private static readonly (TipoSoftware Tipo, string[] Nombres)[] BloquesSet =
        {
            (TipoSoftware.FacturacionElectronica, new[] { "set-facturacion.json", "set-fe.json", $"set-{TipoSoftware.FacturacionElectronica}.json" }),
            (TipoSoftware.DocumentoEquivalente, new[] { "set-pos.json", "set-documento-equivalente.json", $"set-{TipoSoftware.DocumentoEquivalente}.json" }),
            (TipoSoftware.DocumentoSoporte, new[] { "set-ds.json", "set-documento-soporte.json", $"set-{TipoSoftware.DocumentoSoporte}.json" }),
            (TipoSoftware.NominaElectronica, new[] { "set-nomina.json", $"set-{TipoSoftware.NominaElectronica}.json" }),
            (TipoSoftware.EventosRadian, new[] { "set-radian.json", "set-eventos-radian.json", $"set-{TipoSoftware.EventosRadian}.json" })
        };

        /// <summary>Lee el estado del set DIAN (<c>set-*.json</c>) y los conteos informativos de POS, soporte, nómina y eventos RADIAN.</summary>
        public EvidenciaSets LeerSets(string clienteId)
        {
            var cliente = CarpetaCliente(clienteId);
            var evidencia = new EvidenciaSets();
            foreach (var (tipo, nombres) in BloquesSet)
            {
                var estado = LeerEstadoSet(cliente, nombres);
                if (estado == null) continue;
                evidencia.Sets[tipo] = estado;
                if (tipo == TipoSoftware.DocumentoEquivalente && estado.Aceptado) evidencia.SetPosAceptado = true;
                if (tipo == TipoSoftware.NominaElectronica && estado.Aceptado) evidencia.SetNominaAceptado = true;
            }

            var xml = Path.Combine(cliente, "XML");
            if (Directory.Exists(xml))
            {
                evidencia.TiquetesPosAprobados = ContarAppResponsesAprobados(xml, "DETP-*-appresponse.xml");
                evidencia.NotasAjustePosAprobadas = ContarAppResponsesAprobados(xml, "NATP-*-appresponse.xml");
                evidencia.DocumentosSoporteAprobados = ContarAppResponsesAprobados(xml, "DSNO-*-appresponse.xml");
                evidencia.NotasAjusteDocumentoSoporteAprobadas = ContarAppResponsesAprobados(xml, "NSDS-*-appresponse.xml");
                foreach (var archivo in Directory.GetFiles(xml, "*-dian.json"))
                {
                    var reg = LeerJson<RegistroNominaVista>(archivo);
                    if (reg != null && EsSetAceptado(reg.StatusCode, reg.StatusMessage, reg.Mensajes))
                    {
                        evidencia.SetPosAceptado = true;
                        RegistrarSetAceptadoDesdeRegistro(evidencia, TipoSoftware.DocumentoEquivalente, reg);
                        break;
                    }
                }
            }

            var nominaDocs = Path.Combine(cliente, "nomina", "docs");
            if (Directory.Exists(nominaDocs))
            {
                foreach (var archivo in Directory.GetFiles(nominaDocs, "*.json"))
                {
                    var reg = LeerJson<RegistroNominaVista>(archivo);
                    if (reg == null) continue;
                    if (EsSetAceptado(reg.StatusCode, reg.StatusMessage, reg.Mensajes))
                    {
                        evidencia.SetNominaAceptado = true;
                        RegistrarSetAceptadoDesdeRegistro(evidencia, TipoSoftware.NominaElectronica, reg);
                    }
                    var aprobado = reg.Estado == 2 || ((reg.Estado == 4 || reg.Estado == 5) && reg.StatusCode == "00");
                    if (!aprobado) continue;
                    if (reg.TipoDocumento == 103 || !string.IsNullOrEmpty(reg.CuneReferencia))
                        evidencia.NominasAjusteAprobadas++;
                    else
                        evidencia.NominasIndividualesAprobadas++;
                }
            }

            var radianEventos = Path.Combine(cliente, "radian", "eventos");
            if (Directory.Exists(radianEventos))
            {
                foreach (var archivo in Directory.GetFiles(radianEventos, "*.json"))
                {
                    var ev = LeerJson<EventoRadianVista>(archivo);
                    if (ev != null && ev.EstadoDian == "00") evidencia.EventosRadianAprobados++;
                }
            }

            return evidencia;
        }

        private static EstadoSetBloque? LeerEstadoSet(string cliente, string[] nombres)
        {
            var carpetas = new[] { cliente, Path.Combine(cliente, "sets") };
            foreach (var carpeta in carpetas)
            {
                foreach (var nombre in nombres)
                {
                    var archivo = Path.Combine(carpeta, nombre);
                    if (!File.Exists(archivo)) continue;
                    var estado = LeerJson<EstadoSetBloque>(archivo);
                    if (estado != null) return estado;
                }
            }

            return null;
        }

        private static void RegistrarSetAceptadoDesdeRegistro(EvidenciaSets evidencia, TipoSoftware tipo, RegistroNominaVista reg)
        {
            var mensaje = !string.IsNullOrWhiteSpace(reg.StatusMessage)
                ? reg.StatusMessage
                : reg.Mensajes.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m) && m.Contains("se encuentra Aceptado", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            evidencia.Sets.TryAdd(tipo, new EstadoSetBloque
            {
                Estado = "aceptado",
                StatusCode = reg.StatusCode,
                Mensaje = mensaje
            });
        }

        private static int ContarAppResponsesAprobados(string carpetaXml, string patron)
        {
            var aprobados = 0;
            var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            foreach (var archivo in Directory.GetFiles(carpetaXml, patron))
            {
                try
                {
                    using var lector = XmlReader.Create(new StringReader(File.ReadAllText(archivo).TrimStart('\uFEFF', ' ', '\r', '\n', '\t')), ajustes);
                    var doc = XDocument.Load(lector);
                    var codigo = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResponseCode")?.Value.Trim();
                    if (codigo == "02") aprobados++;
                }
                catch (Exception ex) when (ex is XmlException or IOException)
                {
                    // Un XML incompleto o inválido no tumba la evaluación.
                }
            }

            return aprobados;
        }

        private static T? LeerJson<T>(string archivo) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(archivo), Opciones);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }
        }

        private static bool EsSetAceptado(string? codigo, string? mensaje, IEnumerable<string>? mensajes = null) =>
            codigo == "2" &&
            ((mensaje != null && mensaje.Contains("se encuentra Aceptado", StringComparison.OrdinalIgnoreCase)) ||
             (mensajes != null && mensajes.Any(m => m != null && m.Contains("se encuentra Aceptado", StringComparison.OrdinalIgnoreCase))));

        private sealed class RegistroNominaVista
        {
            public int TipoDocumento { get; set; }
            public int Estado { get; set; }
            public string StatusCode { get; set; } = string.Empty;
            public string StatusMessage { get; set; } = string.Empty;
            public string[] Mensajes { get; set; } = Array.Empty<string>();
            public string CuneReferencia { get; set; } = string.Empty;
        }

        private sealed class EventoRadianVista
        {
            public string EstadoDian { get; set; } = string.Empty;
        }

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
