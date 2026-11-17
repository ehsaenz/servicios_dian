using DianModelDoc.Servicios;
using System.Text.Json;

namespace Shared.Pruebas;

public class SecretosLocalesTests
{
    [Fact]
    public void El_cifrado_y_la_validacion_usan_los_valores_del_JSON()
    {
        Assert.True(new FE.Models.Doc.DocumentoDian("desarrollo-ficticio").ReadyDevTime);
        Assert.False(new FE.Models.Doc.DocumentoDian("incorrecta").ReadyDevTime);
        Assert.False(new FE.Models.Doc.DocumentoDian().ReadyDevTime);
    }

    [Fact]
    public void Lee_los_valores_sin_transformarlos()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            var valores = new Dictionary<string, string>
            {
                ["ModeloDian.ClaveDesarrollo"] = "ejemplo-desarrollo",
                ["ModeloDian.PasswordHash"] = "ejemplo-contraseña-ñ",
                ["ModeloDian.SaltKey"] = "ejemplo-sal",
                ["ModeloDian.VIKey"] = "1234567890123456"
            };
            File.WriteAllText(ruta, JsonSerializer.Serialize(valores));
            var leidos = SecretosLocales.LeerArchivo(ruta);
            foreach (var par in valores) Assert.Equal(par.Value, leidos[par.Key]);
        }
        finally { File.Delete(ruta); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"ModeloDian.ClaveDesarrollo\":123}")]
    [InlineData("{contenido-privado-no-valido")]
    public void Rechaza_configuracion_incompleta_o_invalida_sin_revelar_contenido(string json)
    {
        var ruta = Path.GetTempFileName();
        try
        {
            File.WriteAllText(ruta, json);
            var error = Assert.Throws<InvalidOperationException>(() => SecretosLocales.LeerArchivo(ruta));
            Assert.DoesNotContain("contenido-privado", error.ToString());
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void Archivo_ausente_no_utiliza_una_clave_alternativa()
    {
        var ruta = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.Throws<InvalidOperationException>(() => SecretosLocales.LeerArchivo(ruta));
    }
}
