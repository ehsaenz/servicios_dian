using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DIAN.Tenants;

namespace DIAN.Admin.Web
{
    /// <summary>
    /// Da de alta un cliente desde un archivo en el formato de configuración de la API anterior de POS/DSNO
    /// (<c>ConfigAPI*.json</c>), con su certificado. Es la vía de migración de los clientes actuales y la forma de dar
    /// de alta uno sin escribir secretos en un navegador. Los secretos entran al almacén ya cifrados.
    /// </summary>
    public static class ImportadorCliente
    {
        private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private sealed class Config
        {
            public EmisorConfig? Emisor { get; set; }
            public CertificadoLegado? Certificado { get; set; }
            public SoftwareLegado? Software { get; set; }
            public List<SoftwareLegado>? Modulos { get; set; }
        }

        private sealed class CertificadoLegado
        {
            public string FileName { get; set; } = string.Empty;
            public string? Clave { get; set; }
        }

        private sealed class SoftwareLegado
        {
            public TipoSoftware TipoSoftware { get; set; } = TipoSoftware.DocumentoSoporte;
            public string Identificador { get; set; } = string.Empty;
            public string PIN { get; set; } = string.Empty;
            public bool AmbientePruebas { get; set; } = true;
            public string? TestSetID { get; set; }
        }

        /// <param name="variableClaveCertificado">Variable de entorno con la clave del certificado; si no se indica, se usa la del archivo.</param>
        /// <returns>La clave de API generada, o null si no se pidió generarla.</returns>
        public static async Task<string?> ImportarAsync(ClienteAdminServicio admin, string usuario, string id, string archivoConfig,
            string? variableClaveCertificado, bool generarClave)
        {
            var config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(archivoConfig), Opciones)
                         ?? throw new ErrorAdministracion("No se pudo leer el archivo de configuración.");
            if (config.Emisor == null)
            {
                throw new ErrorAdministracion("El archivo no trae la sección Emisor.");
            }

            // Un software suelto (formato antiguo) vale para los dos tipos de documento que atiende esta API.
            var modulos = config.Modulos is { Count: > 0 }
                ? config.Modulos
                : config.Software != null
                    ? new List<SoftwareLegado> { Copiar(config.Software, TipoSoftware.DocumentoSoporte), Copiar(config.Software, TipoSoftware.DocumentoEquivalente) }
                    : new List<SoftwareLegado>();

            // Todo se valida ANTES de crear nada, para que un error no deje un cliente a medias.
            foreach (var s in modulos)
            {
                if (!Guid.TryParse(s.Identificador, out _) || string.IsNullOrWhiteSpace(s.PIN))
                {
                    throw new ErrorAdministracion($"El software {s.TipoSoftware} necesita un identificador GUID y un PIN.");
                }
            }

            byte[]? p12 = null;
            string? claveCertificado = null;
            if (config.Certificado != null && !string.IsNullOrEmpty(config.Certificado.FileName))
            {
                var ruta = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(archivoConfig))!, config.Certificado.FileName);
                claveCertificado = !string.IsNullOrEmpty(variableClaveCertificado)
                    ? Environment.GetEnvironmentVariable(variableClaveCertificado)
                    : config.Certificado.Clave;
                if (string.IsNullOrEmpty(claveCertificado))
                {
                    throw new ErrorAdministracion("Falta la clave del certificado (variable de entorno o archivo).");
                }

                p12 = await File.ReadAllBytesAsync(ruta);
                ClienteAdminServicio.ValidarCertificado(p12, claveCertificado, config.Emisor.Nit);
            }

            await admin.CrearAsync(usuario, id, config.Emisor);
            foreach (var s in modulos)
            {
                await admin.GuardarSoftwareAsync(usuario, id, s.TipoSoftware, s.Identificador, s.PIN, s.AmbientePruebas, s.TestSetID);
            }

            if (p12 != null)
            {
                await admin.SubirCertificadoAsync(usuario, id, p12, claveCertificado!);
            }

            return generarClave ? await admin.GenerarClaveAsync(usuario, id, "Importado") : null;
        }

        private static SoftwareLegado Copiar(SoftwareLegado origen, TipoSoftware tipo) => new SoftwareLegado
        {
            TipoSoftware = tipo,
            Identificador = origen.Identificador,
            PIN = origen.PIN,
            AmbientePruebas = origen.AmbientePruebas,
            TestSetID = origen.TestSetID
        };
    }
}
