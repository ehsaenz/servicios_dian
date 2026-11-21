using DIAN.Pos.Servicios.Documentos;
using Xunit;

namespace Pos.Pruebas;

public class ResultadoSetTests
{
    [Fact]
    public void Codigo_2_con_se_encuentra_Aceptado_es_aceptado()
    {
        var r = ResultadoSet.Desde("2", "Set de prueba con identificador 1234 se encuentra Aceptado.", "zip1", "FV1");
        Assert.Equal("aceptado", r.Estado);
        Assert.Equal("zip1", r.ZipKey);
    }

    [Fact]
    public void Codigo_99_es_rechazado()
    {
        Assert.Equal("rechazado", ResultadoSet.Desde("99", "Regla NSAJ25a", "zip2", "FV2").Estado);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "Batch en proceso")]
    [InlineData("2", "Batch en proceso")]
    [InlineData("00", "Procesado correctamente")]
    public void Cualquier_otra_respuesta_queda_en_proceso(string? codigo, string? mensaje)
    {
        Assert.Equal("en_proceso", ResultadoSet.Desde(codigo, mensaje, null, "FV3").Estado);
    }
}
