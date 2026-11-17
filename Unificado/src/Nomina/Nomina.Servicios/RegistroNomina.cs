using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NominaElectronica.Enums;

namespace DIAN.Nomina.Servicios
{
    /// <summary>
    /// Estado de un documento de nómina enviado por un cliente. Reemplaza las tablas <c>documentos</c> y <c>envios_dian</c>
    /// de la API anterior: cada documento es un archivo JSON dentro de la carpeta del cliente.
    /// </summary>
    public sealed class RegistroNomina
    {
        public int TipoDocumento { get; set; }
        public string NumeroCompleto { get; set; } = string.Empty;
        public string Cune { get; set; } = string.Empty;
        public enmTipoEnvio TipoEnvio { get; set; }
        public enmEstadoEnvio Estado { get; set; }

        /// <summary>ZipKey (habilitación, por set de pruebas) o CUNE (envío síncrono).</summary>
        public string TrackId { get; set; } = string.Empty;

        /// <summary>True si se envió al set de pruebas de habilitación (se consulta por ZipKey).</summary>
        public bool PorSet { get; set; }

        public DateTime FechaEnvioUtc { get; set; }
        public DateTime? FechaAcuseUtc { get; set; }

        /// <summary>CUNE del documento al que ajusta (solo en ajustes).</summary>
        public string CuneReferencia { get; set; } = string.Empty;

        /// <summary>Resultado definitivo de la DIAN, guardado para no volver a consultarlo.</summary>
        public string StatusCode { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public string[] Mensajes { get; set; } = Array.Empty<string>();
        public string XmlDocumentKey { get; set; } = string.Empty;
    }

    /// <summary>Archivos de estado de un cliente: <c>datos\{cliente}\nomina\docs</c>, <c>track</c> y <c>cune</c>.</summary>
    internal sealed class AlmacenRegistrosNomina
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions { WriteIndented = true };
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Candados = new ConcurrentDictionary<string, SemaphoreSlim>();

        private readonly string _raiz;

        public AlmacenRegistrosNomina(string carpetaDatos, string idCliente)
        {
            _raiz = Path.Combine(carpetaDatos, Seguro(idCliente), "nomina");
        }

        public string CarpetaXml => Path.Combine(_raiz, "xml");

        public string RutaXml(string nombre) => Path.Combine(CarpetaXml, Seguro(nombre));

        public static string Seguro(string texto)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = new string((texto ?? string.Empty).Select(c => Array.IndexOf(invalidos, c) >= 0 ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }

        public static string Clave(int tipoDocumento, string numeroCompleto) => tipoDocumento + "_" + numeroCompleto;

        private SemaphoreSlim Candado() => Candados.GetOrAdd(_raiz, _ => new SemaphoreSlim(1, 1));

        public RegistroNomina Obtener(string clave)
        {
            var ruta = Path.Combine(_raiz, "docs", Seguro(clave) + ".json");
            return File.Exists(ruta) ? JsonSerializer.Deserialize<RegistroNomina>(File.ReadAllText(ruta), Opciones) : null;
        }

        public RegistroNomina PorTrackId(string trackId) => Indirecto("track", trackId);

        public RegistroNomina PorCune(string cune) => Indirecto("cune", cune);

        private RegistroNomina Indirecto(string indice, string valor)
        {
            var ruta = Path.Combine(_raiz, indice, Seguro(valor) + ".txt");
            return File.Exists(ruta) ? Obtener(File.ReadAllText(ruta).Trim()) : null;
        }

        public void Guardar(RegistroNomina registro)
        {
            var candado = Candado();
            candado.Wait();
            try
            {
                var clave = Clave(registro.TipoDocumento, registro.NumeroCompleto);
                Escribir(Path.Combine(_raiz, "docs", Seguro(clave) + ".json"), JsonSerializer.Serialize(registro, Opciones));
                if (!string.IsNullOrEmpty(registro.TrackId)) Escribir(Path.Combine(_raiz, "track", Seguro(registro.TrackId) + ".txt"), clave);
                if (!string.IsNullOrEmpty(registro.Cune)) Escribir(Path.Combine(_raiz, "cune", Seguro(registro.Cune) + ".txt"), clave);
            }
            finally
            {
                candado.Release();
            }
        }

        private static void Escribir(string ruta, string contenido)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var temporal = ruta + ".tmp";
            File.WriteAllText(temporal, contenido);
            File.Move(temporal, ruta, true);
        }
    }
}
