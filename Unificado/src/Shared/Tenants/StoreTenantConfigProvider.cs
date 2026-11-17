using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    /// <summary>Resuelve la clave de API contra el almacén de clientes que administra la app Admin.</summary>
    public sealed class StoreTenantConfigProvider : ITenantConfigProvider, ITenantEnumerador
    {
        /// <summary>Clientes activos con su configuración (para los trabajos de fondo: reintentos y avisos).</summary>
        public async Task<IReadOnlyList<TenantConfig>> ListarActivosAsync(CancellationToken cancelacion = default)
        {
            var clientes = await _almacen.ListarAsync(cancelacion);
            return clientes.Where(c => c.Activo).Select(Convertir).ToList();
        }

        private readonly ITenantStore _almacen;
        private readonly ISecretProtector _protector;

        public StoreTenantConfigProvider(ITenantStore almacen, ISecretProtector protector)
        {
            _almacen = almacen;
            _protector = protector;
        }

        public async Task<TenantConfig?> BuscarPorClaveAsync(string claveApi, CancellationToken cancelacion = default)
        {
            if (string.IsNullOrEmpty(claveApi))
            {
                return null;
            }

            var hash = Encoding.ASCII.GetBytes(ClaveApi.Hash(claveApi));
            foreach (var cliente in await _almacen.ListarAsync(cancelacion))
            {
                if (!cliente.Activo)
                {
                    continue;
                }

                // Comparación de tiempo constante contra cada hash activo.
                var coincide = false;
                foreach (var clave in cliente.Claves.Where(c => c.Activa))
                {
                    coincide |= CryptographicOperations.FixedTimeEquals(hash, Encoding.ASCII.GetBytes(clave.HashClave));
                }

                if (coincide)
                {
                    return Convertir(cliente);
                }
            }

            return null;
        }

        private TenantConfig Convertir(ClienteRegistro cliente)
        {
            var tenant = new TenantConfig
            {
                Id = cliente.Id,
                Activo = cliente.Activo,
                PermiteProduccion = cliente.PermiteProduccion,
                Emisor = cliente.Emisor,
                WebhookUrl = cliente.WebhookUrl ?? string.Empty,
                WebhookSecreto = string.IsNullOrEmpty(cliente.WebhookSecretoProtegido) ? string.Empty : _protector.Desproteger(cliente.WebhookSecretoProtegido!),
                Software = cliente.Software.Select(s => new SoftwareConfig
                {
                    TipoSoftware = s.TipoSoftware,
                    Identificador = s.Identificador,
                    PIN = string.IsNullOrEmpty(s.PinProtegido) ? string.Empty : _protector.Desproteger(s.PinProtegido),
                    AmbientePruebas = s.AmbientePruebas,
                    TestSetId = s.TestSetId
                }).ToList()
            };

            if (!string.IsNullOrEmpty(cliente.ArchivoCertificado))
            {
                tenant.Certificado = new CertificadoConfig
                {
                    RutaArchivo = _almacen.RutaArchivo(cliente.Id, cliente.ArchivoCertificado),
                    Clave = string.IsNullOrEmpty(cliente.ClaveCertificadoProtegida) ? string.Empty : _protector.Desproteger(cliente.ClaveCertificadoProtegida)
                };
            }

            return tenant;
        }
    }
}
