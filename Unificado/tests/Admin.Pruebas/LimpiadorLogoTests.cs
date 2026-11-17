using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DIAN.Tenants;
using Xunit;

namespace Admin.Pruebas;

public class LimpiadorLogoTests
{
    // PNG de 1x1 píxel válido: firma, IHDR, IDAT, IEND.
    private const string PngMinimoBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private const int FinIhdr = 8 + 25; // firma (8) + bloque IHDR (4 + 4 + 13 + 4)

    private static byte[] Bloque(string tipo, byte[] datos)
    {
        var lista = new List<byte>();
        lista.AddRange(BitConverter.GetBytes((uint)datos.Length).Reverse());
        lista.AddRange(Encoding.ASCII.GetBytes(tipo));
        lista.AddRange(datos);
        lista.AddRange(new byte[4]); // el limpiador no valida el CRC; el bloque de imagen se copia tal cual
        return lista.ToArray();
    }

    private static byte[] PngConMetadatos(byte[] png, string tipo)
    {
        var metadatos = Bloque(tipo, Encoding.ASCII.GetBytes("c2pa-de-prueba"));
        return png.Take(FinIhdr).Concat(metadatos).Concat(png.Skip(FinIhdr)).ToArray();
    }

    [Fact]
    public void Quita_el_bloque_C2PA_y_deja_los_bloques_de_imagen_intactos()
    {
        var original = Convert.FromBase64String(PngMinimoBase64);
        var conMetadatos = PngConMetadatos(original, "caBX");

        var limpio = LimpiadorLogo.LimpiarPng(conMetadatos, out var quitados);

        Assert.Equal(new[] { "caBX" }, quitados);
        Assert.Equal(original, limpio);
    }

    [Fact]
    public void Quita_tambien_bloques_de_texto()
    {
        var original = Convert.FromBase64String(PngMinimoBase64);
        var conTexto = PngConMetadatos(original, "tEXt");

        var limpio = LimpiadorLogo.LimpiarPng(conTexto, out var quitados);

        Assert.Equal(new[] { "tEXt" }, quitados);
        Assert.Equal(original, limpio);
    }

    [Fact]
    public void Un_PNG_sin_metadatos_se_devuelve_igual()
    {
        var original = Convert.FromBase64String(PngMinimoBase64);

        var resultado = LimpiadorLogo.LimpiarPng(original, out var quitados);

        Assert.Empty(quitados);
        Assert.Same(original, resultado);
    }

    [Fact]
    public void Rechaza_lo_que_no_es_un_PNG()
    {
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 };

        Assert.Throws<InvalidDataException>(() => LimpiadorLogo.LimpiarPng(jpg, out _));
    }

    [Fact]
    public void Rechaza_un_PNG_truncado()
    {
        var original = Convert.FromBase64String(PngMinimoBase64);
        var truncado = original.Take(original.Length - 6).ToArray();

        Assert.Throws<InvalidDataException>(() => LimpiadorLogo.LimpiarPng(truncado, out _));
    }

    // Segmento JPEG: marcador (FF xx), longitud (incluye los 2 bytes de la longitud) y datos.
    private static byte[] Segmento(byte marcador, params byte[] datos)
    {
        var longitud = datos.Length + 2;
        return new byte[] { 0xFF, marcador, (byte)(longitud >> 8), (byte)longitud }.Concat(datos).ToArray();
    }

    private static readonly byte[] Inicio = { 0xFF, 0xD8 };
    private static readonly byte[] Fin = { 0xFF, 0xD9 };
    private static readonly byte[] DatosDeImagen = { 0x12, 0x34, 0xFF, 0x00, 0x56, 0x78 };

    private static byte[] Concatenar(params byte[][] partes) => partes.SelectMany(p => p).ToArray();

    [Fact]
    public void JPEG_quita_EXIF_C2PA_y_comentarios_y_conserva_la_imagen()
    {
        var app0 = Segmento(0xE0, Encoding.ASCII.GetBytes("JFIF\0"));
        var exif = Segmento(0xE1, Encoding.ASCII.GetBytes("Exif\0\0datos"));
        var icc = Segmento(0xE2, Encoding.ASCII.GetBytes("ICC_PROFILE\0perfil"));
        var c2pa = Segmento(0xEB, Encoding.ASCII.GetBytes("jumb-c2pa"));
        var dqt = Segmento(0xDB, new byte[] { 0, 1, 2, 3 });
        var sof = Segmento(0xC0, new byte[] { 8, 0, 1, 0, 1, 1, 1, 0x11 });
        var dht = Segmento(0xC4, new byte[] { 0, 1, 2 });
        var comentario = Segmento(0xFE, Encoding.ASCII.GetBytes("nota"));
        var sos = Segmento(0xDA, new byte[] { 1, 1, 0, 0, 0x3F, 0 });

        var conMetadatos = Concatenar(Inicio, app0, exif, icc, c2pa, dqt, sof, dht, comentario, sos, DatosDeImagen, Fin);
        var esperado = Concatenar(Inicio, app0, icc, dqt, sof, dht, sos, DatosDeImagen, Fin);

        var limpio = LimpiadorLogo.LimpiarJpeg(conMetadatos, out var quitados);

        Assert.Equal(new[] { "APP1", "APP11", "COM" }, quitados);
        Assert.Equal(esperado, limpio);
    }

    [Fact]
    public void JPEG_sin_metadatos_se_devuelve_igual()
    {
        var jpg = Concatenar(Inicio, Segmento(0xE0, Encoding.ASCII.GetBytes("JFIF\0")),
            Segmento(0xDA, new byte[] { 1, 1, 0, 0, 0x3F, 0 }), DatosDeImagen, Fin);

        var resultado = LimpiadorLogo.LimpiarJpeg(jpg, out var quitados);

        Assert.Empty(quitados);
        Assert.Same(jpg, resultado);
    }

    [Fact]
    public void JPEG_rechaza_un_archivo_que_no_es_JPEG()
    {
        var png = Convert.FromBase64String(PngMinimoBase64);

        Assert.Throws<InvalidDataException>(() => LimpiadorLogo.LimpiarJpeg(png, out _));
    }

    [Fact]
    public void JPEG_rechaza_un_archivo_sin_datos_de_imagen()
    {
        var sinImagen = Concatenar(Inicio, Segmento(0xE0, Encoding.ASCII.GetBytes("JFIF\0")), Fin);

        Assert.Throws<InvalidDataException>(() => LimpiadorLogo.LimpiarJpeg(sinImagen, out _));
    }

    [Fact]
    public void JPEG_rechaza_un_segmento_truncado()
    {
        // El segmento APP1 declara 0xFFFF bytes pero el archivo termina antes.
        var truncado = Concatenar(Inicio, new byte[] { 0xFF, 0xE1, 0xFF, 0xFF, 0x01, 0x02 });

        Assert.Throws<InvalidDataException>(() => LimpiadorLogo.LimpiarJpeg(truncado, out _));
    }
}
