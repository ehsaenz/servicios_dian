using System;
using System.Security.Cryptography;
using System.Text;

namespace DIAN.Tenants
{
    /// <summary>Las claves de API nunca se guardan: solo su hash SHA-256 en hexadecimal.</summary>
    public static class ClaveApi
    {
        public static string Hash(string clave)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave))).ToLowerInvariant();
        }

        /// <summary>Genera una clave nueva (256 bits aleatorios). Se muestra una sola vez; se guarda el hash.</summary>
        public static string Generar()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }
    }
}
