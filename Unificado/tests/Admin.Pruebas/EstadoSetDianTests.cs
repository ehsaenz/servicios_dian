using DIAN.Admin.Web;
using Xunit;

namespace Admin.Pruebas;

public class EstadoSetDianTests
{
    [Fact]
    public void Solo_el_codigo_2_con_se_encuentra_Aceptado_cuenta_como_aceptado()
    {
        Assert.Equal(EstadoSetDian.Resultado.Aceptado,
            EstadoSetDian.Interpretar("2", "Set de prueba con identificador 1234 se encuentra Aceptado."));
    }

    [Theory]
    [InlineData("2", "Set de prueba con identificador 1234 en proceso.")]
    [InlineData("99", "Set de prueba con identificador 1234 se encuentra Aceptado.")]
    [InlineData("", "Set de prueba con identificador 1234 se encuentra Aceptado.")]
    [InlineData("2", "")]
    [InlineData(null, null)]
    public void Cualquier_otra_respuesta_bloquea(string? codigo, string? mensaje)
    {
        Assert.Equal(EstadoSetDian.Resultado.NoAceptado, EstadoSetDian.Interpretar(codigo, mensaje));
    }
}
