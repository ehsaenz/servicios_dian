using Anonimizador;

var opciones = LeerArgumentos(args);
if (opciones.Ayuda)
{
    Console.WriteLine("""
    Anonimizador de muestras JSON (sin conexión a BD).

    Uso:
      dotnet run -- [--origen <carpeta>] [--destino <carpeta>]

    Por defecto:
      --origen   %LOCALAPPDATA%\Dian\muestras-dev
      --destino  tools/SimuladorErp/muestras-anonimas (respecto a Unificado)
    """);
    return 0;
}

var origen = opciones.Origen ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Dian",
    "muestras-dev");

var destino = opciones.Destino ?? ResolverDestinoPorDefecto();

try
{
    var anonimizador = new AnonimizadorMuestras();
    var resumen = anonimizador.AnonimizarDirectorio(origen, destino);
    Console.WriteLine($"Anonimizados {resumen.ArchivosProcesados} archivos ({resumen.RegistrosProcesados} registros; FV aceptadas: {resumen.FacturasAceptadas}, descartadas: {resumen.FacturasDescartadas}) en: {resumen.CarpetaDestino}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error al anonimizar: {ex.Message}");
    return 1;
}

static string ResolverDestinoPorDefecto()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var candidato = Path.Combine(dir.FullName, "tools", "SimuladorErp");
        if (Directory.Exists(candidato))
        {
            return Path.Combine(candidato, "muestras-anonimas");
        }
        dir = dir.Parent;
    }

    return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "SimuladorErp", "muestras-anonimas"));
}

static (bool Ayuda, string? Origen, string? Destino) LeerArgumentos(string[] args)
{
    var ayuda = false;
    string? origen = null;
    string? destino = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-h":
            case "--help":
            case "ayuda":
                ayuda = true;
                break;
            case "--origen" when i + 1 < args.Length:
                origen = args[++i];
                break;
            case "--destino" when i + 1 < args.Length:
                destino = args[++i];
                break;
        }
    }

    return (ayuda, origen, destino);
}
