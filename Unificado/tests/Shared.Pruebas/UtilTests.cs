using System.IO.Compression;
using System.Text;
using System.Xml;
using eFacturacionColombia_NETCore.Util;

namespace Shared.Pruebas;

public class DateTimeHelperTests
{
    [Fact]
    public void Una_fecha_UTC_se_convierte_a_hora_de_Colombia()
    {
        var utc = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

        var colombia = DateTimeHelper.GetColombianDate(utc);

        Assert.Equal(new DateTime(2026, 11, 7, 10, 0, 0), colombia);
    }

    [Fact]
    public void Sin_argumento_devuelve_la_hora_actual_de_Colombia()
    {
        var esperado = DateTime.UtcNow.AddHours(-5);

        var obtenido = DateTimeHelper.GetColombianDate();

        Assert.True(Math.Abs((obtenido - esperado).TotalSeconds) < 5, $"esperado cerca de {esperado:O}, obtenido {obtenido:O}");
    }
}

public class CryptographyHelperTests
{
    // Vectores estándar para la cadena "abc".
    [Theory]
    [InlineData("a9993e364706816aba3e25717850c26c9cd0d89d")]
    public void Sha1_de_texto(string esperado) => Assert.Equal(esperado, CryptographyHelper.Sha1("abc"));

    [Theory]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Sha256_de_texto(string esperado) => Assert.Equal(esperado, CryptographyHelper.Sha256("abc"));

    [Theory]
    [InlineData("cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7")]
    public void Sha384_de_texto(string esperado) => Assert.Equal(esperado, CryptographyHelper.Sha384("abc"));

    [Theory]
    [InlineData("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f")]
    public void Sha512_de_texto(string esperado) => Assert.Equal(esperado, CryptographyHelper.Sha512("abc"));

    [Fact]
    public void Las_variantes_de_bytes_coinciden_con_las_de_texto()
    {
        var datos = Encoding.UTF8.GetBytes("abc");

        Assert.Equal(CryptographyHelper.Sha384("abc"), Convert.ToHexString(CryptographyHelper.Sha384(datos)).ToLowerInvariant());
        Assert.Equal(CryptographyHelper.Sha1("abc"), Convert.ToHexString(CryptographyHelper.Sha1(datos)).ToLowerInvariant());
    }
}

public class ZipHelperTests
{
    [Fact]
    public void Comprime_un_archivo_con_el_nombre_indicado()
    {
        var contenido = Encoding.UTF8.GetBytes("<a>1</a>");

        var zip = ZipHelper.Compress(contenido, "document.xml");

        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entrada = Assert.Single(archivo.Entries);
        Assert.Equal("document.xml", entrada.FullName);
        using var lector = new StreamReader(entrada.Open(), Encoding.UTF8);
        Assert.Equal("<a>1</a>", lector.ReadToEnd());
    }

    [Fact]
    public void Comprime_varios_archivos()
    {
        var archivos = new Dictionary<string, byte[]>
        {
            ["document01.xml"] = Encoding.UTF8.GetBytes("uno"),
            ["document02.xml"] = Encoding.UTF8.GetBytes("dos"),
        };

        var zip = ZipHelper.Compress(archivos);

        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.Equal(new[] { "document01.xml", "document02.xml" }, archivo.Entries.Select(e => e.FullName).OrderBy(n => n));
    }
}

public class XmlDocumentExtensionsTests
{
    [Fact]
    public void ToXmlString_incluye_la_declaracion_por_defecto_y_la_omite_si_se_pide()
    {
        var documento = new XmlDocument();
        documento.LoadXml("<a><b>1</b></a>");

        var con = documento.ToXmlString();
        var sin = documento.ToXmlString(true);

        Assert.StartsWith("<?xml", con);
        Assert.DoesNotContain("<?xml", sin);
        Assert.Contains("<b>1</b>", sin);
    }
}
