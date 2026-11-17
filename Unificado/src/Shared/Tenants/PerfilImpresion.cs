using System;
using System.IO;
using System.Linq;

namespace DIAN.Tenants
{
    /// <summary>Lo que se necesita para imprimir la representación gráfica de un documento de un cliente y una sucursal.</summary>
    public sealed class PerfilImpresion
    {
        public string RazonSocial { get; set; } = string.Empty;

        /// <summary>Nombre del establecimiento (el de la sucursal; si no hay, la razón social del cliente).</summary>
        public string Establecimiento { get; set; } = string.Empty;

        public string RegimenFiscal { get; set; } = string.Empty;

        /// <summary>Logo de la sucursal; si no tiene, el del cliente; si tampoco, null.</summary>
        public byte[]? Logo { get; set; }

        /// <summary>Matrícula de la sucursal encontrada, o vacía si se usó el perfil general del cliente.</summary>
        public string Matricula { get; set; } = string.Empty;
    }

    public static class PerfilImpresionResolver
    {
        /// <summary>
        /// Busca el perfil de impresión por la matrícula mercantil que trae el XML del documento (sin distinguir mayúsculas).
        /// Si la sucursal no existe o no tiene logo, cae al perfil general del cliente.
        /// </summary>
        public static PerfilImpresion Resolver(ClienteRegistro cliente, string? matricula, ITenantStore almacen)
        {
            var perfil = new PerfilImpresion
            {
                RazonSocial = cliente.Emisor.RazonSocial,
                Establecimiento = cliente.Emisor.RazonSocial
            };

            var sucursal = string.IsNullOrWhiteSpace(matricula)
                ? null
                : cliente.Sucursales.FirstOrDefault(s => string.Equals(s.MatriculaMercantil, matricula.Trim(), StringComparison.OrdinalIgnoreCase));

            if (sucursal != null)
            {
                perfil.Matricula = sucursal.MatriculaMercantil;
                perfil.Establecimiento = sucursal.Nombre;
                perfil.RegimenFiscal = sucursal.RegimenFiscal;
                perfil.Logo = LeerLogo(cliente.Id, sucursal.ArchivoLogo, almacen);
            }

            perfil.Logo ??= LeerLogo(cliente.Id, cliente.ArchivoLogo, almacen);
            return perfil;
        }

        private static byte[]? LeerLogo(string idCliente, string? archivo, ITenantStore almacen)
        {
            if (string.IsNullOrEmpty(archivo))
            {
                return null;
            }

            var ruta = almacen.RutaArchivo(idCliente, archivo!);
            return File.Exists(ruta) ? File.ReadAllBytes(ruta) : null;
        }
    }
}
