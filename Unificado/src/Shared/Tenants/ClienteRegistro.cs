using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DIAN.Tenants
{
    /// <summary>Software DIAN de un cliente tal como se guarda: el PIN va protegido, nunca en claro.</summary>
    public sealed class SoftwareRegistro
    {
        public TipoSoftware TipoSoftware { get; set; }
        public string Identificador { get; set; } = string.Empty;
        public string PinProtegido { get; set; } = string.Empty;
        public bool AmbientePruebas { get; set; } = true;
        public string TestSetId { get; set; } = string.Empty;
    }

    /// <summary>Clave de API de un cliente. Solo se guarda su hash; la clave completa se muestra una única vez al crearla.</summary>
    public sealed class ClaveRegistro
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string HashClave { get; set; } = string.Empty;
        /// <summary>Últimos 4 caracteres de la clave, para reconocerla en la lista.</summary>
        public string Sufijo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Activa { get; set; } = true;
        public DateTime CreadaUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sucursal (establecimiento) de un cliente: la licencia de facturación electrónica de cada sucursal trae su propia
    /// matrícula mercantil, su nombre y su logo. La matrícula mercantil viaja en el XML del documento
    /// (CorporateRegistrationScheme/Name del emisor), así que con ella se encuentra el perfil de impresión.
    /// </summary>
    public sealed class SucursalRegistro
    {
        private static readonly Regex MatriculaValida = new Regex(@"\A[A-Za-z0-9._-]{1,30}\z", RegexOptions.Compiled);

        /// <summary>Matrícula mercantil de la sucursal; es la llave dentro del cliente.</summary>
        public string MatriculaMercantil { get; set; } = string.Empty;

        /// <summary>Nombre del establecimiento tal como se imprime en el documento.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Texto del régimen fiscal que se imprime (por ejemplo "Responsable del impuesto sobre las ventas - IVA").</summary>
        public string RegimenFiscal { get; set; } = string.Empty;

        public string? ArchivoLogo { get; set; }

        public static bool EsMatriculaValida(string? matricula) => matricula != null && MatriculaValida.IsMatch(matricula);
    }

    /// <summary>Un cliente (tenant) tal como lo guarda y administra la app Admin.</summary>
    public sealed class ClienteRegistro
    {
        private static readonly Regex IdValido = new Regex(@"\A[A-Za-z0-9_-]{1,40}\z", RegexOptions.Compiled);

        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Versión del registro en el almacén (0 = cliente nuevo). El almacén de MySQL la usa para detectar que otro
        /// administrador cambió el cliente mientras se editaba. El almacén de archivos la ignora.
        /// </summary>
        public int Version { get; set; }

        public bool Activo { get; set; } = true;

        /// <summary>Autorización explícita para enviar a producción (ver <see cref="TenantConfig.PermiteProduccion"/>).</summary>
        public bool PermiteProduccion { get; set; }

        public EmisorConfig Emisor { get; set; } = new EmisorConfig();
        public List<SoftwareRegistro> Software { get; set; } = new List<SoftwareRegistro>();

        public string? ArchivoCertificado { get; set; }
        public string? ClaveCertificadoProtegida { get; set; }
        public string? CertificadoSujeto { get; set; }
        public DateTime? CertificadoVenceUtc { get; set; }

        public string? ArchivoLogo { get; set; }
        public string? PerfilCorreo { get; set; }

        /// <summary>Webhook opcional: URL fija a la que el servicio avisa cuando un documento queda aprobado o rechazado.</summary>
        public string? WebhookUrl { get; set; }

        /// <summary>Secreto con el que se firma (HMAC-SHA256) cada aviso; va protegido, nunca en claro.</summary>
        public string? WebhookSecretoProtegido { get; set; }

        public List<ClaveRegistro> Claves { get; set; } = new List<ClaveRegistro>();

        /// <summary>Sucursales con su perfil de impresión (nombre, régimen y logo). Puede estar vacía: se usa el logo del cliente.</summary>
        public List<SucursalRegistro> Sucursales { get; set; } = new List<SucursalRegistro>();

        public static bool EsIdValido(string? id) => id != null && IdValido.IsMatch(id);
    }
}
