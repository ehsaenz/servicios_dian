using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DIAN.Impresion;

// Modelo plano de un documento electrónico para imprimirlo. Tiene la forma del JSON que ya usan las plantillas
// .repx (los mismos nombres que arma QuantyERP en ReportesService.BuildDocumentoAsync), más los campos de la
// validación de la DIAN: CUFE, QR, Ambiente, FechaValidacionDian y ResolucionTexto.
// Se llena SOLO desde un documento aprobado (ver LectorDocumentoAprobado).
public sealed class DocumentoImpresion
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    /// <summary>
    /// Verdadero solo si el modelo lo armó LectorDocumentoAprobado a partir de una respuesta de aprobación de la DIAN.
    /// El renderizador de la representación gráfica oficial se niega a imprimir un modelo que no sea aprobado.
    /// </summary>
    public bool Aprobado { get; internal set; }

    /// <summary>Tipo de documento del ERP que corresponde: FV, NC, ND, DS...</summary>
    public string TipoDocErp { get; set; } = string.Empty;

    /// <summary>Tipo de documento de la DIAN: 01, 02, 03, 05, 91, 92.</summary>
    public string TipoDocDian { get; set; } = string.Empty;

    public TipoDocumentoImpresion TipoDocObj { get; set; } = new();
    public string Numero { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public List<string> Notas { get; set; } = new();
    public DateTime? FechaHoraDoc { get; set; }
    public FormaPagoImpresion FormaPago { get; set; } = new();
    public ReferenciaImpresion OrdenRef { get; set; } = new();

    /// <summary>Documento que corrige una nota crédito o débito.</summary>
    public ReferenciaImpresion DocumentoRef { get; set; } = new();

    /// <summary>Concepto de corrección de una nota crédito o débito.</summary>
    public string Concepto { get; set; } = string.Empty;

    public List<ItemImpresion> Items { get; set; } = new();
    public List<ImpuestoImpresion> Impuestos { get; set; } = new();
    public List<RetencionImpresion> Retenciones { get; set; } = new();

    public decimal SubTotal { get; set; }
    public decimal ValorDescuento { get; set; }
    public decimal BaseGravada { get; set; }
    public decimal BaseNoGravada { get; set; }
    public decimal ValorImp { get; set; }
    public decimal IVA { get; set; }
    public decimal ReteIVA { get; set; }
    public decimal ReteFuente { get; set; }
    public decimal ReteICA { get; set; }
    public decimal ValorTotal { get; set; }

    public TerceroImpresion Emisor { get; set; } = new();
    public TerceroImpresion Receptor { get; set; } = new();

    // Validación de la DIAN.
    public string CUFE { get; set; } = string.Empty;
    public string QR { get; set; } = string.Empty;

    /// <summary>"1" producción, "2" habilitación.</summary>
    public string Ambiente { get; set; } = string.Empty;

    public string FechaValidacionDian { get; set; } = string.Empty;
    public string ResolucionTexto { get; set; } = string.Empty;

    public string ToJson() => JsonSerializer.Serialize(this, OpcionesJson);
}

public sealed class TipoDocumentoImpresion
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class FormaPagoImpresion
{
    public string ID { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CodigoMedioPago { get; set; } = string.Empty;
    public int Plazo { get; set; }
    public DateTime? FechaVencimiento { get; set; }
}

public sealed class ReferenciaImpresion
{
    public string TipoDoc { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public DateTime? Fecha { get; set; }
    public string CUFE { get; set; } = string.Empty;
}

public sealed class ItemImpresion
{
    public string ID { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public decimal Cant { get; set; }
    public decimal ValorUnit { get; set; }
    public decimal TarifaIVA { get; set; }
    public decimal ValorDescuento { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ValorIVA { get; set; }
    public decimal ValorTotal { get; set; }
    public List<ImpuestoItemImpresion> Impuestos { get; set; } = new();
}

public sealed class ImpuestoItemImpresion
{
    public string ID { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Porcentaje { get; set; }
    public decimal ValorBase { get; set; }
    public decimal ValorImp { get; set; }
}

public sealed class ImpuestoImpresion
{
    public string ID { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Porcentaje { get; set; }
    public decimal TotalBase { get; set; }
    public decimal TotalImpuesto { get; set; }
}

public sealed class RetencionImpresion
{
    public string ID { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Porcentaje { get; set; }
    public decimal TotalBase { get; set; }
    public decimal TotalRetencion { get; set; }
}

public sealed class TerceroImpresion
{
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string NIT { get; set; } = string.Empty;
    public string EmailContacto { get; set; } = string.Empty;
    public string TelefonoContacto { get; set; } = string.Empty;

    /// <summary>
    /// Matrícula mercantil del emisor (CorporateRegistrationScheme/Name del XML): es el dato con el que se
    /// identifica la licencia/sucursal y, con ella, el logo y los datos de impresión.
    /// </summary>
    public string MatriculaMercantil { get; set; } = string.Empty;

    public LocacionImpresion LocacionPrincipal { get; set; } = new();
}

public sealed class LocacionImpresion
{
    public string Direccion { get; set; } = string.Empty;
    public string NombreMunicipio { get; set; } = string.Empty;
    public string NombreDepartamento { get; set; } = string.Empty;
    public string NombrePais { get; set; } = string.Empty;
}
