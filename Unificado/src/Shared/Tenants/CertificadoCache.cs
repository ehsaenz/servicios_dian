using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace DIAN.Tenants
{
    /// <summary>
    /// Carga el certificado de cada cliente una sola vez y lo conserva en memoria. Se vuelve a cargar si el archivo
    /// cambia. La clave nunca se registra ni se devuelve.
    /// </summary>
    public sealed class CertificadoCache
    {
        private sealed record Entrada(DateTime FechaArchivo, X509Certificate2 Certificado);

        private readonly ConcurrentDictionary<string, Entrada> _cache = new ConcurrentDictionary<string, Entrada>();

        public X509Certificate2 Obtener(TenantConfig tenant)
        {
            var ruta = tenant.Certificado.RutaArchivo;
            if (!File.Exists(ruta))
            {
                throw new FileNotFoundException($"No se encontró el certificado del cliente {tenant.Id}.");
            }

            var fecha = File.GetLastWriteTimeUtc(ruta);
            var llave = tenant.Id + "|" + ruta;

            if (_cache.TryGetValue(llave, out var actual) && actual.FechaArchivo == fecha)
            {
                return actual.Certificado;
            }

            var certificado = X509CertificateLoader.LoadPkcs12FromFile(ruta, tenant.Certificado.Clave, X509KeyStorageFlags.Exportable);
            _cache[llave] = new Entrada(fecha, certificado);
            return certificado;
        }
    }
}
