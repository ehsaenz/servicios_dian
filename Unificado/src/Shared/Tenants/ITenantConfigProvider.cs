using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    /// <summary>Lista los clientes activos. Solo lo implementa el almacén de la app Admin (el índice de archivos antiguo no).</summary>
    public interface ITenantEnumerador
    {
        Task<System.Collections.Generic.IReadOnlyList<TenantConfig>> ListarActivosAsync(CancellationToken cancelacion = default);
    }

    public interface ITenantConfigProvider
    {
        /// <summary>Busca el cliente de una clave de API. Devuelve null si la clave no existe o el cliente está inactivo.</summary>
        Task<TenantConfig?> BuscarPorClaveAsync(string claveApi, CancellationToken cancelacion = default);
    }
}
