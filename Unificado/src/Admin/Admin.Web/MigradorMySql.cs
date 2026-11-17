using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DIAN.Tenants;

namespace DIAN.Admin.Web
{
    /// <summary>Copia los clientes de un almacén a otro (por ejemplo de archivos a MySQL) con sus archivos. No sobrescribe: omite los que ya existen.</summary>
    public static class MigradorMySql
    {
        public static async Task<int> MigrarAsync(ITenantStore origen, ITenantStore destino)
        {
            var copiados = 0;
            foreach (var cliente in await origen.ListarAsync())
            {
                if (await destino.ObtenerAsync(cliente.Id) != null)
                {
                    continue;
                }

                var archivos = new[] { cliente.ArchivoCertificado, cliente.ArchivoLogo }
                    .Concat(cliente.Sucursales.Select(s => s.ArchivoLogo))
                    .Where(n => !string.IsNullOrEmpty(n));
                foreach (var nombre in archivos)
                {
                    var ruta = origen.RutaArchivo(cliente.Id, nombre!);
                    if (File.Exists(ruta))
                    {
                        await destino.GuardarArchivoAsync(cliente.Id, nombre!, await File.ReadAllBytesAsync(ruta));
                    }
                }

                cliente.Version = 0; // es nuevo para el destino
                await destino.GuardarAsync(cliente);
                copiados++;
            }

            return copiados;
        }
    }
}
