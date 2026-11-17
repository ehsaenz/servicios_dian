using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DianModelDoc.Servicios
{
    // Se carga solo al utilizar las funciones heredadas de cifrado/desarrollo.
    // No forma parte del contrato JSON de DocumentoDian.
    internal static class SecretosLocales
    {
        private static readonly Lazy<IReadOnlyDictionary<string, string>> Valores =
            new Lazy<IReadOnlyDictionary<string, string>>(() => LeerArchivo(RutaArchivo()));

        internal static string Obtener(string clave) => Valores.Value[clave];

        private static string RutaArchivo()
        {
            var configurada = Environment.GetEnvironmentVariable("DIAN_SECRETOS_ARCHIVO");
            if (!string.IsNullOrWhiteSpace(configurada))
            {
                if (!Path.IsPathFullyQualified(configurada))
                    throw new InvalidOperationException("DIAN_SECRETOS_ARCHIVO debe ser una ruta absoluta.");
                return configurada;
            }

            return OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dian", "secretos.json")
                : "/var/lib/dian/secretos.json";
        }

        internal static IReadOnlyDictionary<string, string> LeerArchivo(string ruta)
        {
            Dictionary<string, string> valores;
            try
            {
                valores = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ruta));
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("El archivo local secretos.json debe contener pares clave/valor de texto válidos.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new InvalidOperationException("No se pudo leer secretos.json. Configure DIAN_SECRETOS_ARCHIVO o instálelo en la ruta predeterminada con permisos de lectura para el servicio.");
            }

            foreach (var clave in new[] { "ModeloDian.ClaveDesarrollo", "ModeloDian.PasswordHash", "ModeloDian.SaltKey", "ModeloDian.VIKey" })
            {
                if (valores == null || !valores.TryGetValue(clave, out var valor) || string.IsNullOrWhiteSpace(valor))
                    throw new InvalidOperationException("Falta el valor local obligatorio: " + clave + ".");
            }

            return valores;
        }
    }
}
