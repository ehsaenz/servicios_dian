using System;
using System.Collections.Generic;

namespace DIAN.Tenants
{
    /// <summary>Tipos de software registrados ante la DIAN. Los valores coinciden con los de la API actual de POS/DSNO.</summary>
    public enum TipoSoftware
    {
        FacturacionElectronica = 1,
        NominaElectronica = 2,
        EventosRadian = 3,
        DocumentoSoporte = 4,
        DocumentoEquivalente = 5
    }

    public sealed class EmisorConfig
    {
        public string Nit { get; set; } = string.Empty;
        public string DV { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string MatriculaMercantil { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NumeroTel { get; set; } = string.Empty;
    }

    public sealed class SoftwareConfig
    {
        public TipoSoftware TipoSoftware { get; set; }
        public string Identificador { get; set; } = string.Empty;
        public string PIN { get; set; } = string.Empty;
        public bool AmbientePruebas { get; set; } = true;
        public string TestSetId { get; set; } = string.Empty;
    }

    /// <summary>Dónde está el certificado y con qué clave se abre. La clave llega ya descifrada, solo en memoria.</summary>
    public sealed class CertificadoConfig
    {
        public string RutaArchivo { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
    }

    /// <summary>Configuración completa de un cliente (tenant). No se guarda en variables estáticas.</summary>
    public sealed class TenantConfig
    {
        public string Id { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Autorización explícita para enviar a producción. Mientras sea false, todo va a habilitación
        /// aunque el software diga AmbientePruebas = false.
        /// </summary>
        public bool PermiteProduccion { get; set; }

        /// <summary>URL del webhook de documentos (vacía = el ERP consulta por polling) y su secreto ya descifrado, solo en memoria.</summary>
        public string WebhookUrl { get; set; } = string.Empty;
        public string WebhookSecreto { get; set; } = string.Empty;

        public EmisorConfig Emisor { get; set; } = new EmisorConfig();
        public CertificadoConfig Certificado { get; set; } = new CertificadoConfig();
        public List<SoftwareConfig> Software { get; set; } = new List<SoftwareConfig>();

        public SoftwareConfig? ObtenerSoftware(TipoSoftware tipo)
        {
            return Software.Find(s => s.TipoSoftware == tipo);
        }

        /// <summary>True si el documento debe ir a habilitación. Solo es false con software en producción Y producción autorizada.</summary>
        public bool EsHabilitacion(TipoSoftware tipo)
        {
            var software = ObtenerSoftware(tipo) ?? throw new InvalidOperationException($"El cliente {Id} no tiene software configurado para {tipo}.");
            return software.AmbientePruebas || !PermiteProduccion;
        }
    }
}
