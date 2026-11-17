using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace DIAN.Tenants
{
    /// <summary>
    /// Protege secretos con ASP.NET Data Protection. La API y la app Admin deben apuntar a la MISMA carpeta de llaves
    /// para poder leer lo que la otra protegió. Esa carpeta debe tener permisos restringidos: quien la lea puede descifrar.
    /// </summary>
    public sealed class ProtectorSecretos : ISecretProtector
    {
        private readonly IDataProtector _protector;

        public ProtectorSecretos(string carpetaLlaves)
        {
            Directory.CreateDirectory(carpetaLlaves);
            var servicios = new ServiceCollection();
            servicios.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(carpetaLlaves))
                .SetApplicationName("DIAN.Unificado");
            var proveedor = servicios.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
            _protector = proveedor.CreateProtector("DIAN.Tenants.Secretos.v1");
        }

        public string Proteger(string texto) => _protector.Protect(texto);

        public string Desproteger(string protegido) => _protector.Unprotect(protegido);
    }
}
