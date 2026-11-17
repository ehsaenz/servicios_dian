using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DIAN.Tenants
{
    /// <summary>
    /// Quita de un PNG los bloques que no son de imagen (por ejemplo los metadatos C2PA <c>caBX</c>, o <c>tEXt</c>,
    /// <c>eXIf</c>). DevExpress no puede leer un logo con esos bloques y el PDF de todas las facturas del cliente falla.
    /// Los bloques de imagen se copian tal cual (con su CRC), así que los píxeles no cambian.
    /// </summary>
    public static class LimpiadorLogo
    {
        private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Bloques que describen la imagen o su color. Todo lo demás se quita.
        private static readonly HashSet<string> BloquesDeImagen = new HashSet<string>(StringComparer.Ordinal)
        {
            "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "pHYs", "bKGD", "sBIT"
        };

        /// <summary>
        /// Devuelve el PNG sin los bloques que no son de imagen. <paramref name="quitados"/> lista los tipos quitados
        /// (vacía si no había nada que quitar; en ese caso devuelve el mismo arreglo).
        /// Lanza <see cref="InvalidDataException"/> si el archivo no es un PNG bien formado.
        /// </summary>
        public static byte[] LimpiarPng(byte[] png, out List<string> quitados)
        {
            quitados = new List<string>();
            if (png == null || png.Length < FirmaPng.Length || !png.Take(FirmaPng.Length).SequenceEqual(FirmaPng))
            {
                throw new InvalidDataException("El archivo no es un PNG.");
            }

            var salida = new MemoryStream(png.Length);
            salida.Write(png, 0, FirmaPng.Length);

            var posicion = FirmaPng.Length;
            var primero = true;
            var vioFin = false;
            while (posicion < png.Length)
            {
                if (posicion + 12 > png.Length)
                {
                    throw new InvalidDataException("El PNG está truncado.");
                }

                var longitud = ((uint)png[posicion] << 24) | ((uint)png[posicion + 1] << 16) | ((uint)png[posicion + 2] << 8) | png[posicion + 3];
                var tipo = Encoding.ASCII.GetString(png, posicion + 4, 4);
                var total = 12L + longitud;
                if (posicion + total > png.Length)
                {
                    throw new InvalidDataException("El PNG está truncado.");
                }

                if (primero && tipo != "IHDR")
                {
                    throw new InvalidDataException("El primer bloque del PNG no es IHDR.");
                }
                primero = false;

                if (BloquesDeImagen.Contains(tipo))
                {
                    salida.Write(png, posicion, (int)total);
                }
                else
                {
                    quitados.Add(tipo);
                }

                posicion += (int)total;
                if (tipo == "IEND")
                {
                    vioFin = true;
                    break;
                }
            }

            if (!vioFin)
            {
                throw new InvalidDataException("El PNG no tiene el bloque final IEND.");
            }

            if (quitados.Count == 0)
            {
                return png;
            }

            return salida.ToArray();
        }

        /// <summary>
        /// Quita de un JPEG los segmentos de metadatos (EXIF, XMP, IPTC, COM y los APPn de C2PA/JUMBF). Conserva los
        /// segmentos de imagen, las tablas, el perfil de color (APP2 ICC), APP0 (JFIF) y APP14 (Adobe).
        /// Lanza <see cref="InvalidDataException"/> si la estructura del archivo no es válida.
        /// </summary>
        public static byte[] LimpiarJpeg(byte[] jpg, out List<string> quitados)
        {
            quitados = new List<string>();
            if (jpg == null || jpg.Length < 4 || jpg[0] != 0xFF || jpg[1] != 0xD8)
            {
                throw new InvalidDataException("El archivo no es un JPEG.");
            }

            var salida = new MemoryStream(jpg.Length);
            salida.Write(jpg, 0, 2);

            var posicion = 2;
            var vioInicioDeDatos = false;
            while (posicion < jpg.Length)
            {
                if (jpg[posicion] != 0xFF)
                {
                    throw new InvalidDataException("Se esperaba un marcador JPEG.");
                }

                // Bytes de relleno: varios 0xFF seguidos antes del marcador.
                while (posicion + 2 < jpg.Length && jpg[posicion + 1] == 0xFF)
                {
                    posicion++;
                }

                if (posicion + 1 >= jpg.Length)
                {
                    throw new InvalidDataException("El JPEG está truncado.");
                }

                var marcador = jpg[posicion + 1];

                if (marcador == 0xD9) // EOI: fin de la imagen
                {
                    salida.Write(jpg, posicion, 2);
                    break;
                }

                if (marcador == 0xDA) // SOS: a partir de aquí van los datos de la imagen, se copian tal cual
                {
                    salida.Write(jpg, posicion, jpg.Length - posicion);
                    vioInicioDeDatos = true;
                    break;
                }

                if (marcador == 0x01 || (marcador >= 0xD0 && marcador <= 0xD7)) // marcadores sin longitud
                {
                    salida.Write(jpg, posicion, 2);
                    posicion += 2;
                    continue;
                }

                if (posicion + 4 > jpg.Length)
                {
                    throw new InvalidDataException("El JPEG está truncado.");
                }

                var longitud = (jpg[posicion + 2] << 8) | jpg[posicion + 3];
                var total = 2 + longitud;
                if (longitud < 2 || posicion + total > jpg.Length)
                {
                    throw new InvalidDataException("Un segmento del JPEG está truncado.");
                }

                if (EsMetadatoJpeg(marcador))
                {
                    quitados.Add(marcador == 0xFE ? "COM" : "APP" + (marcador - 0xE0));
                }
                else
                {
                    salida.Write(jpg, posicion, total);
                }

                posicion += total;
            }

            if (!vioInicioDeDatos)
            {
                throw new InvalidDataException("El JPEG no tiene datos de imagen.");
            }

            if (quitados.Count == 0)
            {
                return jpg;
            }

            return salida.ToArray();
        }

        // APP0 (JFIF), APP2 (perfil ICC) y APP14 (Adobe) describen la imagen o sus colores: se conservan.
        // El resto de APPn (EXIF en APP1, XMP, IPTC, C2PA en APP11...) y COM son metadatos.
        private static bool EsMetadatoJpeg(byte marcador) =>
            marcador == 0xFE || (marcador >= 0xE1 && marcador <= 0xEF && marcador != 0xE2 && marcador != 0xEE);
    }
}
