using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace DIAN.Impresion;

/// <summary>
/// Lee un documento electrónico YA APROBADO por la DIAN (factura, nota crédito, nota débito o documento soporte) y
/// arma el modelo de impresión. Es la única puerta para producir la representación gráfica oficial:
/// si no hay respuesta de aprobación (ResponseCode 02) del mismo documento, lanza DocumentoNoAprobadoException.
/// </summary>
public static class LectorDocumentoAprobado
{
    private const string CodigoValidado = "02";

    /// <summary>Entrada: el AttachedDocument (contenedor) que trae el documento firmado y la respuesta de la DIAN.</summary>
    public static DocumentoImpresion DesdeContenedor(string xmlContenedor)
    {
        var contenedor = Cargar(xmlContenedor, "el contenedor (AttachedDocument)");
        var raiz = contenedor.Root!;
        if (raiz.Name.LocalName != "AttachedDocument")
        {
            throw new DocumentoNoAprobadoException("El XML no es un AttachedDocument.");
        }

        var xmlDocumento = Hijo(raiz, "Attachment", "ExternalReference", "Description")?.Value;
        var xmlRespuesta = Hijo(raiz, "ParentDocumentLineReference", "DocumentReference", "Attachment", "ExternalReference", "Description")?.Value;

        if (string.IsNullOrWhiteSpace(xmlDocumento))
        {
            throw new DocumentoNoAprobadoException("El contenedor no trae el documento firmado.");
        }

        if (string.IsNullOrWhiteSpace(xmlRespuesta))
        {
            throw new DocumentoNoAprobadoException("El contenedor no trae la respuesta de la DIAN: el documento no está aprobado.");
        }

        return DesdeDocumentoYRespuesta(xmlDocumento, xmlRespuesta);
    }

    /// <summary>Entrada: el documento firmado (Invoice, CreditNote o DebitNote) y el ApplicationResponse de la DIAN.</summary>
    public static DocumentoImpresion DesdeDocumentoYRespuesta(string xmlDocumento, string xmlRespuesta)
    {
        var documento = Cargar(xmlDocumento, "el documento").Root!;
        var respuesta = Cargar(xmlRespuesta, "la respuesta de la DIAN").Root!;

        var cufe = Texto(documento, "UUID");
        var validacion = VerificarAprobacion(respuesta, cufe);

        var modelo = LeerDocumento(documento);
        modelo.CUFE = cufe;
        modelo.FechaValidacionDian = validacion;
        modelo.Aprobado = true;
        return modelo;
    }

    // Una respuesta de aprobación es un ApplicationResponse con ResponseCode 02 que se refiere al mismo CUFE.
    private static string VerificarAprobacion(XElement respuesta, string cufeDocumento)
    {
        if (respuesta.Name.LocalName != "ApplicationResponse")
        {
            throw new DocumentoNoAprobadoException("La respuesta de la DIAN no es un ApplicationResponse.");
        }

        var documentResponse = Hijo(respuesta, "DocumentResponse");
        var codigo = Texto(documentResponse, "Response", "ResponseCode");
        if (codigo != CodigoValidado)
        {
            var detalle = Texto(documentResponse, "Response", "Description");
            throw new DocumentoNoAprobadoException(
                $"La DIAN no aprobó el documento (ResponseCode '{codigo}'){(detalle.Length > 0 ? ": " + detalle : string.Empty)}.", codigo);
        }

        var cufeRespuesta = Texto(documentResponse, "DocumentReference", "UUID");
        if (!string.Equals(cufeRespuesta, cufeDocumento, StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentoNoAprobadoException("La respuesta de la DIAN corresponde a otro documento (el CUFE no coincide).", codigo);
        }

        return FormatoFechaValidacion(Texto(respuesta, "IssueDate"), Texto(respuesta, "IssueTime"));
    }

    private static DocumentoImpresion LeerDocumento(XElement doc)
    {
        var tipo = TipoDeDocumento(doc);
        var modelo = new DocumentoImpresion
        {
            TipoDocErp = tipo.Erp,
            TipoDocDian = tipo.Dian,
            TipoDocObj = new TipoDocumentoImpresion { Codigo = tipo.Erp, Nombre = tipo.Nombre },
            Numero = Texto(doc, "ID"),
            FechaHoraDoc = FechaHora(Texto(doc, "IssueDate"), Texto(doc, "IssueTime")),
            Ambiente = Texto(doc, "ProfileExecutionID")
        };

        modelo.Notas = Hijos(doc, "Note").Select(n => n.Value).Where(n => n.Length > 0).ToList();

        var extension = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "DianExtensions");
        if (extension != null)
        {
            modelo.QR = Texto(extension, "QRCode").Trim();
            modelo.ResolucionTexto = TextoResolucion(Hijo(extension, "InvoiceControl"));
        }

        modelo.Emisor = LeerTercero(Hijo(doc, "AccountingSupplierParty", "Party"));
        modelo.Receptor = LeerTercero(Hijo(doc, "AccountingCustomerParty", "Party"));
        modelo.FormaPago = LeerFormaPago(doc, modelo.FechaHoraDoc);
        modelo.OrdenRef = new ReferenciaImpresion
        {
            Numero = Texto(doc, "OrderReference", "ID"),
            Fecha = Fecha(Texto(doc, "OrderReference", "IssueDate"))
        };

        var referencia = Hijo(doc, "BillingReference", "InvoiceDocumentReference");
        if (referencia != null)
        {
            modelo.DocumentoRef = new ReferenciaImpresion
            {
                Numero = Texto(referencia, "ID"),
                CUFE = Texto(referencia, "UUID"),
                Fecha = Fecha(Texto(referencia, "IssueDate"))
            };
        }

        modelo.Concepto = Texto(doc, "DiscrepancyResponse", "Description");
        modelo.Folio = Folio(modelo.Numero, Texto(Hijo(doc, "AccountingSupplierParty", "Party"), "PartyLegalEntity", "CorporateRegistrationScheme", "ID"));

        foreach (var linea in doc.Elements().Where(e => e.Name.LocalName is "InvoiceLine" or "CreditNoteLine" or "DebitNoteLine"))
        {
            modelo.Items.Add(LeerLinea(linea));
        }

        LeerTotales(doc, modelo);
        return modelo;
    }

    private static (string Erp, string Dian, string Nombre) TipoDeDocumento(XElement doc)
    {
        switch (doc.Name.LocalName)
        {
            case "CreditNote":
                return ("NC", "91", "NOTA CRÉDITO ELECTRÓNICA");
            case "DebitNote":
                return ("ND", "92", "NOTA DÉBITO ELECTRÓNICA");
            case "Invoice":
                var codigo = Texto(doc, "InvoiceTypeCode");
                return codigo switch
                {
                    "02" => ("FE", "02", "FACTURA ELECTRÓNICA DE EXPORTACIÓN"),
                    "03" => ("FC", "03", "FACTURA ELECTRÓNICA DE CONTINGENCIA"),
                    "05" => ("DS", "05", "DOCUMENTO SOPORTE EN ADQUISICIONES A NO OBLIGADOS A FACTURAR"),
                    _ => ("FV", string.IsNullOrEmpty(codigo) ? "01" : codigo, "FACTURA ELECTRÓNICA DE VENTA")
                };
            default:
                throw new DocumentoNoAprobadoException($"Tipo de documento no soportado para impresión: {doc.Name.LocalName}.");
        }
    }

    private static ItemImpresion LeerLinea(XElement linea)
    {
        var item = new ItemImpresion
        {
            ID = Texto(linea, "ID"),
            Cant = Numero(linea.Elements().FirstOrDefault(e => e.Name.LocalName is "InvoicedQuantity" or "CreditedQuantity" or "DebitedQuantity")?.Value),
            SubTotal = Numero(Texto(linea, "LineExtensionAmount")),
            ValorUnit = Numero(Texto(linea, "Price", "PriceAmount")),
            Detalle = Texto(linea, "Item", "Description"),
            Codigo = Texto(linea, "Item", "StandardItemIdentification", "ID"),
            Referencia = Texto(linea, "Item", "SellersItemIdentification", "ID")
        };

        if (item.Codigo.Length == 0)
        {
            item.Codigo = item.Referencia;
        }

        item.ValorDescuento = Hijos(linea, "AllowanceCharge")
            .Where(a => string.Equals(Texto(a, "ChargeIndicator"), "false", StringComparison.OrdinalIgnoreCase))
            .Sum(a => Numero(Texto(a, "Amount")));

        foreach (var subtotal in Hijos(linea, "TaxTotal").SelectMany(t => Hijos(t, "TaxSubtotal")))
        {
            var esquema = Texto(subtotal, "TaxCategory", "TaxScheme", "ID");
            var impuesto = new ImpuestoItemImpresion
            {
                ID = esquema,
                Nombre = Texto(subtotal, "TaxCategory", "TaxScheme", "Name"),
                Porcentaje = Numero(Texto(subtotal, "TaxCategory", "Percent")),
                ValorBase = Numero(Texto(subtotal, "TaxableAmount")),
                ValorImp = Numero(Texto(subtotal, "TaxAmount"))
            };
            item.Impuestos.Add(impuesto);
            if (esquema == "01")
            {
                item.TarifaIVA = impuesto.Porcentaje;
                item.ValorIVA += impuesto.ValorImp;
            }
        }

        item.ValorTotal = item.SubTotal + item.Impuestos.Sum(i => i.ValorImp);
        return item;
    }

    private static void LeerTotales(XElement doc, DocumentoImpresion modelo)
    {
        // La nota débito llama a los totales RequestedMonetaryTotal; los demás documentos, LegalMonetaryTotal.
        var monetario = Hijo(doc, "LegalMonetaryTotal") ?? Hijo(doc, "RequestedMonetaryTotal");
        modelo.SubTotal = Numero(Texto(monetario, "LineExtensionAmount"));
        modelo.ValorDescuento = Numero(Texto(monetario, "AllowanceTotalAmount"));
        modelo.ValorTotal = Numero(Texto(monetario, "PayableAmount"));

        var impuestos = Hijos(doc, "TaxTotal")
            .SelectMany(t => Hijos(t, "TaxSubtotal"))
            .GroupBy(s => new
            {
                Id = Texto(s, "TaxCategory", "TaxScheme", "ID"),
                Nombre = Texto(s, "TaxCategory", "TaxScheme", "Name"),
                Porcentaje = Numero(Texto(s, "TaxCategory", "Percent"))
            })
            .Select(g => new ImpuestoImpresion
            {
                ID = g.Key.Id,
                Nombre = g.Key.Nombre,
                Porcentaje = g.Key.Porcentaje,
                TotalBase = g.Sum(s => Numero(Texto(s, "TaxableAmount"))),
                TotalImpuesto = g.Sum(s => Numero(Texto(s, "TaxAmount")))
            })
            .ToList();

        modelo.Impuestos = impuestos;
        modelo.ValorImp = impuestos.Sum(i => i.TotalImpuesto);
        modelo.IVA = impuestos.Where(i => i.ID == "01").Sum(i => i.TotalImpuesto);
        modelo.BaseGravada = impuestos.Where(i => i.ID == "01" && i.Porcentaje > 0m).Sum(i => i.TotalBase);
        modelo.BaseNoGravada = Math.Max(0m, modelo.SubTotal - modelo.BaseGravada);

        modelo.Retenciones = Hijos(doc, "WithholdingTaxTotal")
            .SelectMany(t => Hijos(t, "TaxSubtotal"))
            .Select(s => new RetencionImpresion
            {
                ID = Texto(s, "TaxCategory", "TaxScheme", "ID"),
                Nombre = Texto(s, "TaxCategory", "TaxScheme", "Name"),
                Porcentaje = Numero(Texto(s, "TaxCategory", "Percent")),
                TotalBase = Numero(Texto(s, "TaxableAmount")),
                TotalRetencion = Numero(Texto(s, "TaxAmount"))
            })
            .ToList();

        // Códigos de la DIAN: 05 ReteIVA, 06 ReteFuente, 07 ReteICA.
        modelo.ReteIVA = modelo.Retenciones.Where(r => r.ID == "05").Sum(r => r.TotalRetencion);
        modelo.ReteFuente = modelo.Retenciones.Where(r => r.ID == "06").Sum(r => r.TotalRetencion);
        modelo.ReteICA = modelo.Retenciones.Where(r => r.ID == "07").Sum(r => r.TotalRetencion);
    }

    private static TerceroImpresion LeerTercero(XElement? party)
    {
        var tercero = new TerceroImpresion();
        if (party == null)
        {
            return tercero;
        }

        var esquema = Hijo(party, "PartyTaxScheme");
        var legal = Hijo(party, "PartyLegalEntity");

        tercero.RazonSocial = Primero(Texto(esquema, "RegistrationName"), Texto(legal, "RegistrationName"));
        tercero.NombreComercial = Texto(party, "PartyName", "Name");

        var compania = Hijo(esquema, "CompanyID") ?? Hijo(legal, "CompanyID");
        if (compania != null)
        {
            var digito = compania.Attribute("schemeID")?.Value ?? string.Empty;
            var esNit = compania.Attribute("schemeName")?.Value == "31";
            tercero.NIT = esNit && digito.Length > 0 ? $"{compania.Value}-{digito}" : compania.Value;
        }

        tercero.MatriculaMercantil = Texto(legal, "CorporateRegistrationScheme", "Name");
        tercero.EmailContacto = Texto(party, "Contact", "ElectronicMail");
        tercero.TelefonoContacto = Texto(party, "Contact", "Telephone");

        var direccion = Hijo(party, "PhysicalLocation", "Address")
            ?? Hijo(esquema, "RegistrationAddress")
            ?? Hijo(legal, "RegistrationAddress");
        if (direccion != null)
        {
            tercero.LocacionPrincipal = new LocacionImpresion
            {
                Direccion = Texto(direccion, "AddressLine", "Line"),
                NombreMunicipio = Texto(direccion, "CityName"),
                NombreDepartamento = Texto(direccion, "CountrySubentity"),
                NombrePais = Texto(direccion, "Country", "Name")
            };
        }

        return tercero;
    }

    private static FormaPagoImpresion LeerFormaPago(XElement doc, DateTime? emision)
    {
        var pago = Hijo(doc, "PaymentMeans");
        var forma = new FormaPagoImpresion
        {
            ID = Texto(pago, "ID"),
            CodigoMedioPago = Texto(pago, "PaymentMeansCode"),
            FechaVencimiento = Fecha(Texto(pago, "PaymentDueDate"))
        };

        // DIAN: 1 = contado, 2 = crédito.
        forma.Nombre = forma.ID == "2" ? "Crédito" : "Contado";
        if (forma.ID == "2" && forma.FechaVencimiento.HasValue && emision.HasValue)
        {
            forma.Plazo = Math.Max(0, (int)(forma.FechaVencimiento.Value.Date - emision.Value.Date).TotalDays);
        }

        return forma;
    }

    private static string TextoResolucion(XElement? control)
    {
        if (control == null)
        {
            return string.Empty;
        }

        var numero = Texto(control, "InvoiceAuthorization");
        var desde = Texto(control, "AuthorizationPeriod", "StartDate");
        var hasta = Texto(control, "AuthorizationPeriod", "EndDate");
        var prefijo = Texto(control, "AuthorizedInvoices", "Prefix");
        var rangoDesde = Texto(control, "AuthorizedInvoices", "From");
        var rangoHasta = Texto(control, "AuthorizedInvoices", "To");
        if (numero.Length == 0)
        {
            return string.Empty;
        }

        return $"Resolución DIAN No. {numero} del {desde}. Prefijo {prefijo}. Rango {rangoDesde} al {rangoHasta}. Vigencia {desde} a {hasta}.";
    }

    // Folio = número sin el prefijo de la resolución.
    private static string Folio(string numero, string prefijo) =>
        prefijo.Length > 0 && numero.StartsWith(prefijo, StringComparison.Ordinal) ? numero.Substring(prefijo.Length) : numero;

    // "10:15:32-05:00" -> "(UTC-5)".
    private static string FormatoFechaValidacion(string fecha, string hora)
    {
        var fechaHora = FechaHora(fecha, hora);
        if (!fechaHora.HasValue)
        {
            return string.Empty;
        }

        var zona = string.Empty;
        if (hora.Length > 8)
        {
            var desfase = hora.Substring(8);
            if (desfase == "Z")
            {
                zona = " (UTC)";
            }
            else if (TimeSpan.TryParse(desfase.TrimStart('+'), CultureInfo.InvariantCulture, out var d))
            {
                zona = $" (UTC{(d < TimeSpan.Zero ? "-" : "+")}{Math.Abs(d.Hours)})";
            }
        }

        return fechaHora.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + zona;
    }

    private static DateTime? FechaHora(string fecha, string hora)
    {
        var dia = Fecha(fecha);
        if (!dia.HasValue)
        {
            return null;
        }

        if (hora.Length >= 8 && TimeSpan.TryParse(hora.Substring(0, 8), CultureInfo.InvariantCulture, out var tiempo))
        {
            return dia.Value.Date + tiempo;
        }

        return dia.Value.Date;
    }

    private static DateTime? Fecha(string texto) =>
        DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal Numero(string? texto) =>
        decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string Primero(params string[] valores) => valores.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? string.Empty;

    private static XDocument Cargar(string xml, string que)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new DocumentoNoAprobadoException($"Falta {que}.");
        }

        var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new StringReader(xml.TrimStart('﻿', ' ', '\r', '\n', '\t')), ajustes);
        return XDocument.Load(lector);
    }

    // Navegación por nombre local: no depende de los prefijos ni de las versiones de los espacios de nombres.
    private static IEnumerable<XElement> Hijos(XElement? padre, string nombre) =>
        padre == null ? Enumerable.Empty<XElement>() : padre.Elements().Where(e => e.Name.LocalName == nombre);

    private static XElement? Hijo(XElement? padre, params string[] ruta)
    {
        var actual = padre;
        foreach (var nombre in ruta)
        {
            actual = Hijos(actual, nombre).FirstOrDefault();
            if (actual == null)
            {
                return null;
            }
        }

        return actual;
    }

    private static string Texto(XElement? padre, params string[] ruta) => Hijo(padre, ruta)?.Value.Trim() ?? string.Empty;
}
