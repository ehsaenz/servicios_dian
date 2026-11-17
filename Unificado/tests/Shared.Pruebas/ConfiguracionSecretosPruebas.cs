using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Shared.Pruebas;

// Las pruebas no dependen de los secretos reales del usuario ni los leen.
internal static class ConfiguracionSecretosPruebas
{
    [ModuleInitializer]
    internal static void Inicializar()
    {
        var ruta = Path.Combine(Path.GetTempPath(), "dian-secretos-pruebas-" + Guid.NewGuid() + ".json");
        File.WriteAllText(ruta, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["ModeloDian.ClaveDesarrollo"] = "desarrollo-ficticio",
            ["ModeloDian.PasswordHash"] = "password-ficticio",
            ["ModeloDian.SaltKey"] = "sal-ficticia",
            ["ModeloDian.VIKey"] = "1234567890123456"
        }));
        Environment.SetEnvironmentVariable("DIAN_SECRETOS_ARCHIVO", ruta);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { File.Delete(ruta); } catch (IOException) { } };
    }
}
