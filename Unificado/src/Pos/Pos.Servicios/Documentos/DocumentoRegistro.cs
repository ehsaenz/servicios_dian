using System;
using System.Collections.Generic;

namespace DIAN.Pos.Servicios.Documentos
{
    public enum EstadoDocumento
    {
        /// <summary>Traducido y firmado, aún sin enviar a la DIAN (o con el envío pendiente de reintento).</summary>
        Recibido = 0,

        /// <summary>La DIAN lo validó (validación previa): solo en este estado existe una representación gráfica oficial.</summary>
        Aprobado = 1,

        /// <summary>La DIAN lo rechazó; el ERP corrige y reenvía el mismo número.</summary>
        Rechazado = 2,

        /// <summary>Falló el envío o la DIAN no respondió; se puede reintentar sin cambiar nada.</summary>
        Error = 3
    }

    /// <summary>Estado persistido de un documento electrónico emitido por el servicio unificado (uno por cliente y número).</summary>
    public sealed class DocumentoRegistro
    {
        /// <summary>Consecutivo interno por cliente; sirve de cursor para que el ERP pida "lo que cambió desde".</summary>
        public long Secuencia { get; set; }

        public string Numero { get; set; } = string.Empty;
        public string TipoDocDian { get; set; } = string.Empty;
        public string TipoDocErp { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public EstadoDocumento Estado { get; set; }
        public string CUFE { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public List<string> Errores { get; set; } = new List<string>();
        public bool Habilitacion { get; set; }

        /// <summary>Existe el PDF oficial (solo se genera para documentos aprobados).</summary>
        public bool Pdf { get; set; }

        /// <summary>Por qué no hay PDF (sin plantilla, falla del generador...). Vacío si hay o aún no se intenta.</summary>
        public string PdfMensaje { get; set; } = string.Empty;

        public bool CorreoEnviado { get; set; }
        public string CorreoDestino { get; set; } = string.Empty;

        /// <summary>Hay un aviso al webhook del cliente sin entregar todavía (se reintenta con espera creciente).</summary>
        public bool WebhookPendiente { get; set; }
        public int WebhookIntentos { get; set; }
        public string WebhookError { get; set; } = string.Empty;
        public DateTime? WebhookEntregado { get; set; }
        public int Intentos { get; set; }
        public DateTime FechaRecibido { get; set; }
        public DateTime FechaActualizado { get; set; }

        /// <summary>Secuencia de la última modificación (cursor de cambios).</summary>
        public long SecuenciaCambio { get; set; }
    }
}
