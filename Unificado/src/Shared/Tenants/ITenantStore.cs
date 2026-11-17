using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DIAN.Tenants
{
    /// <summary>Protege y desprotege secretos (PIN, clave del certificado). Se implementa con ASP.NET Data Protection.</summary>
    public interface ISecretProtector
    {
        string Proteger(string texto);
        string Desproteger(string protegido);
    }

    /// <summary>Almacén de clientes que comparten la API y la app Admin. Hoy en archivos; luego en MySQL con la misma interfaz.</summary>
    public interface ITenantStore
    {
        Task<IReadOnlyList<ClienteRegistro>> ListarAsync(CancellationToken cancelacion = default);
        Task<ClienteRegistro?> ObtenerAsync(string id, CancellationToken cancelacion = default);
        Task GuardarAsync(ClienteRegistro cliente, CancellationToken cancelacion = default);

        /// <summary>Guarda un archivo del cliente (certificado, logo) y devuelve su ruta completa.</summary>
        Task<string> GuardarArchivoAsync(string idCliente, string nombre, byte[] contenido, CancellationToken cancelacion = default);

        /// <summary>Borra un archivo del cliente (por ejemplo un logo reemplazado). No falla si no existe.</summary>
        Task EliminarArchivoAsync(string idCliente, string nombre, CancellationToken cancelacion = default);

        /// <summary>Ruta completa de un archivo del cliente, exista o no.</summary>
        string RutaArchivo(string idCliente, string nombre);
    }
}
