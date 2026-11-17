using System;

namespace DIAN.Tenants
{
    /// <summary>Elige el almacén de clientes según la configuración: MySQL si hay cadena de conexión, archivos si no.</summary>
    public static class AlmacenClientes
    {
        public static ITenantStore Crear(string? cadenaMySql, string carpetaAlmacen, string carpetaCache)
        {
            return string.IsNullOrWhiteSpace(cadenaMySql)
                ? new FileTenantStore(carpetaAlmacen)
                : new MySqlTenantStore(cadenaMySql, carpetaCache);
        }
    }
}
