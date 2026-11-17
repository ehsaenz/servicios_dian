using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    /// <summary>
    /// Proveedor de arranque: lee un índice <c>tenants.json</c> y, por cada cliente, el archivo de configuración
    /// en el formato actual de la API de POS/DSNO (<c>ConfigAPI*.json</c>). Sirve para migrar sin riesgo; luego lo
    /// reemplaza el proveedor de MySQL detrás de la misma interfaz.
    /// </summary>
    public sealed class JsonTenantConfigProvider : ITenantConfigProvider
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly string _carpeta;
        private readonly Func<string, string?> _resolverClave;

        /// <param name="carpeta">Carpeta con tenants.json y los archivos que este referencia.</param>
        /// <param name="resolverClave">Convierte el nombre de la variable de entorno de la clave del certificado en su valor.</param>
        public JsonTenantConfigProvider(string carpeta, Func<string, string?>? resolverClave = null)
        {
            _carpeta = carpeta;
            _resolverClave = resolverClave ?? Environment.GetEnvironmentVariable;
        }

        public async Task<TenantConfig?> BuscarPorClaveAsync(string claveApi, CancellationToken cancelacion = default)
        {
            if (string.IsNullOrEmpty(claveApi))
            {
                return null;
            }

            var hash = ClaveApi.Hash(claveApi);
            var indice = await LeerAsync<List<EntradaIndice>>(Path.Combine(_carpeta, "tenants.json"), cancelacion);
            var entrada = indice?.FirstOrDefault(e => e.Activo && string.Equals(e.HashClave, hash, StringComparison.OrdinalIgnoreCase));
            if (entrada == null)
            {
                return null;
            }

            var legado = await LeerAsync<ConfigLegada>(Path.Combine(_carpeta, entrada.ArchivoConfig), cancelacion)
                         ?? throw new InvalidOperationException($"No se pudo leer {entrada.ArchivoConfig}.");

            var tenant = new TenantConfig
            {
                Id = entrada.Id,
                Activo = true,
                PermiteProduccion = entrada.PermiteProduccion,
                Emisor = legado.Emisor ?? new EmisorConfig(),
                Certificado = new CertificadoConfig
                {
                    RutaArchivo = Path.Combine(_carpeta, legado.Certificado?.FileName ?? string.Empty),
                    Clave = ClaveDelCertificado(entrada, legado)
                }
            };

            var modulos = legado.Modulos?.ToList() ?? new List<SoftwareConfig>();
            if (modulos.Count == 0 && legado.Software != null)
            {
                // Formato antiguo de un solo software: vale para los dos tipos que atiende esta API.
                modulos.Add(Copiar(legado.Software, TipoSoftware.DocumentoSoporte));
                modulos.Add(Copiar(legado.Software, TipoSoftware.DocumentoEquivalente));
            }

            tenant.Software = modulos;
            return tenant;
        }

        private string ClaveDelCertificado(EntradaIndice entrada, ConfigLegada legado)
        {
            if (!string.IsNullOrEmpty(entrada.VariableEntornoClave))
            {
                return _resolverClave(entrada.VariableEntornoClave) ?? string.Empty;
            }

            return legado.Certificado?.Clave ?? string.Empty;
        }

        private static SoftwareConfig Copiar(SoftwareConfig origen, TipoSoftware tipo) => new SoftwareConfig
        {
            TipoSoftware = tipo,
            Identificador = origen.Identificador,
            PIN = origen.PIN,
            AmbientePruebas = origen.AmbientePruebas,
            TestSetId = origen.TestSetId
        };

        private static async Task<T?> LeerAsync<T>(string ruta, CancellationToken cancelacion)
        {
            await using var flujo = File.OpenRead(ruta);
            return await JsonSerializer.DeserializeAsync<T>(flujo, Opciones, cancelacion);
        }

        private sealed class EntradaIndice
        {
            public string Id { get; set; } = string.Empty;
            public string HashClave { get; set; } = string.Empty;
            public string ArchivoConfig { get; set; } = string.Empty;
            public bool Activo { get; set; } = true;
            public bool PermiteProduccion { get; set; }
            public string? VariableEntornoClave { get; set; }
        }

        private sealed class ConfigLegada
        {
            public EmisorConfig? Emisor { get; set; }
            public CertificadoLegado? Certificado { get; set; }
            public SoftwareConfig? Software { get; set; }
            public List<SoftwareConfig>? Modulos { get; set; }
        }

        private sealed class CertificadoLegado
        {
            public string FileName { get; set; } = string.Empty;
            public string Clave { get; set; } = string.Empty;
        }
    }
}
