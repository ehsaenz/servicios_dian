using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DIAN.Pos.Servicios.Documentos
{
    /// <summary>
    /// Almacén de documentos de UN cliente, en archivos: <c>documentos\{numero}.json</c> (estado), <c>.xml</c> (firmado) y
    /// <c>-respuesta.xml</c> (ApplicationResponse de la DIAN). Un solo proceso escribe; el candado es por proceso.
    /// Los registros se mantienen en memoria (índice): se leen del disco una sola vez y cada <see cref="Guardar"/> actualiza
    /// la copia y el archivo. Las lecturas devuelven copias, así que un llamador no altera el índice sin guardar.
    /// </summary>
    public sealed class AlmacenDocumentos
    {
        private static readonly object Candado = new object();
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions { WriteIndented = true };

        private readonly string _carpeta;
        private readonly Dictionary<string, DocumentoRegistro> _indice = new Dictionary<string, DocumentoRegistro>(StringComparer.Ordinal);
        private bool _cargado;

        public AlmacenDocumentos(string carpetaCliente)
        {
            _carpeta = Path.Combine(carpetaCliente, "documentos");
        }

        private string Ruta(string numero, string sufijo) => Path.Combine(_carpeta, Nombre(numero) + sufijo);

        // Carga inicial del índice desde los .json. Debe llamarse dentro del candado.
        private void CargarSiFalta()
        {
            if (_cargado) return;
            if (Directory.Exists(_carpeta))
            {
                foreach (var archivo in Directory.GetFiles(_carpeta, "*.json"))
                {
                    var registro = JsonSerializer.Deserialize<DocumentoRegistro>(File.ReadAllText(archivo));
                    if (registro != null) _indice[Path.GetFileNameWithoutExtension(archivo)] = registro;
                }
            }

            _cargado = true;
        }

        // Copia independiente: serializar y volver a leer evita que el llamador altere el índice sin guardar.
        private static DocumentoRegistro Copia(DocumentoRegistro registro) =>
            JsonSerializer.Deserialize<DocumentoRegistro>(JsonSerializer.Serialize(registro))!;

        public DocumentoRegistro? Buscar(string numero)
        {
            lock (Candado)
            {
                CargarSiFalta();
                return _indice.TryGetValue(Nombre(numero), out var registro) ? Copia(registro) : null;
            }
        }

        /// <summary>Guarda el registro asignando la secuencia de cambio (y la de creación la primera vez).</summary>
        /// <param name="esCambioParaElERP">
        /// false para anotaciones internas (el intento de un aviso, por ejemplo): no mueve el cursor de cambios que lee el ERP.
        /// </param>
        public DocumentoRegistro Guardar(DocumentoRegistro registro, bool esCambioParaElERP = true)
        {
            lock (Candado)
            {
                CargarSiFalta();
                Directory.CreateDirectory(_carpeta);
                if (esCambioParaElERP || registro.Secuencia == 0)
                {
                    var secuencia = SiguienteSecuencia();
                    if (registro.Secuencia == 0) registro.Secuencia = secuencia;
                    registro.SecuenciaCambio = secuencia;
                }

                registro.FechaActualizado = DateTime.UtcNow;
                File.WriteAllText(Ruta(registro.Numero, ".json"), JsonSerializer.Serialize(registro, Opciones));
                _indice[Nombre(registro.Numero)] = Copia(registro);
                return registro;
            }
        }

        public void GuardarXml(string numero, string sufijo, byte[] contenido)
        {
            lock (Candado)
            {
                Directory.CreateDirectory(_carpeta);
                File.WriteAllBytes(Ruta(numero, sufijo), contenido);
            }
        }

        public byte[]? LeerXml(string numero, string sufijo)
        {
            lock (Candado)
            {
                var archivo = Ruta(numero, sufijo);
                return File.Exists(archivo) ? File.ReadAllBytes(archivo) : null;
            }
        }

        /// <summary>Documentos modificados después del cursor, en orden de cambio.</summary>
        public List<DocumentoRegistro> CambiosDesde(long cursor, int limite)
        {
            lock (Candado)
            {
                CargarSiFalta();
                return _indice.Values
                    .Where(d => d.SecuenciaCambio > cursor)
                    .OrderBy(d => d.SecuenciaCambio)
                    .Take(Math.Max(1, limite))
                    .Select(Copia)
                    .ToList();
            }
        }

        public List<DocumentoRegistro> Todos()
        {
            lock (Candado)
            {
                CargarSiFalta();
                return _indice.Values.Select(Copia).ToList();
            }
        }

        private long SiguienteSecuencia()
        {
            var archivo = Path.Combine(_carpeta, "secuencia.txt");
            var actual = File.Exists(archivo) && long.TryParse(File.ReadAllText(archivo), out var n) ? n : 0;
            actual++;
            File.WriteAllText(archivo, actual.ToString());
            return actual;
        }

        internal static string Nombre(string? texto)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = new string((texto ?? string.Empty).Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }
    }
}
