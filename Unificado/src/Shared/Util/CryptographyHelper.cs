using System.Security.Cryptography;
using System.Text;

namespace eFacturacionColombia_NETCore.Util
{
    public static class CryptographyHelper
    {
        // Los métodos de bytes usan HashData (SHA1.HashData, etc.), en lugar de las clases
        // SHA1CryptoServiceProvider y similares, que están obsoletas. El resultado es el mismo.

        public static byte[] Sha1(byte[] data)
        {
            return SHA1.HashData(data);
        }

        public static string Sha1(string text)
        {
            return ToHex(Sha1(Encoding.UTF8.GetBytes(text)));
        }

        public static byte[] Sha256(byte[] data)
        {
            return SHA256.HashData(data);
        }

        public static string Sha256(string text)
        {
            return ToHex(Sha256(Encoding.UTF8.GetBytes(text)));
        }

        public static byte[] Sha384(byte[] data)
        {
            return SHA384.HashData(data);
        }

        public static string Sha384(string text)
        {
            return ToHex(Sha384(Encoding.UTF8.GetBytes(text)));
        }

        public static byte[] Sha512(byte[] data)
        {
            return SHA512.HashData(data);
        }

        public static string Sha512(string text)
        {
            return ToHex(Sha512(Encoding.UTF8.GetBytes(text)));
        }

        // Hexadecimal en minúsculas, como en la copia original.
        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
