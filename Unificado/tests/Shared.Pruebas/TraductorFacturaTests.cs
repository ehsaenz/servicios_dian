using System.Xml.Linq;
using DIAN.Impresion;
using DIAN.Traduccion;
using FacturacionColombia_Metadatos;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Util;
using FE.Models.Doc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shared.Pruebas;

// Ida y vuelta: documento del ERP (JSON de QuantyERP) -> factura UBL -> modelo de impresión. Datos ficticios.
public class TraductorFacturaTests
{
    private static readonly ParametrosEmision Parametros = new()
    {
        Ambiente = AmbienteDestino.PRUEBAS,
        SoftwareNit = "901033307",
        SoftwareIdentificador = "00000000-0000-0000-0000-000000000000",
        SoftwarePin = "12345"
    };

    // El documento pasa por el JSON como lo hace en producción: QuantyERP lo serializa y la API lo lee.
    private static DocumentoDian DelJson() =>
        JObject.Parse(JsonConvert.SerializeObject(ModeloDianTests.Factura(), Formatting.Indented)).ToObject<DocumentoDian>()!;

    private static string Respuesta(string numero, string cufe) =>
        "<ApplicationResponse xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2\"" +
        " xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"" +
        " xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
        "<cbc:IssueDate>2026-11-08</cbc:IssueDate><cbc:IssueTime>10:15:32-05:00</cbc:IssueTime>" +
        "<cac:DocumentResponse><cac:Response><cbc:ResponseCode>02</cbc:ResponseCode></cac:Response>" +
        "<cac:DocumentReference><cbc:ID>" + numero + "</cbc:ID><cbc:UUID>" + cufe + "</cbc:UUID></cac:DocumentReference></cac:DocumentResponse>" +
        "</ApplicationResponse>";

    [Fact]
    public void Del_documento_del_ERP_al_modelo_de_impresion_conserva_los_datos()
    {
        var doc = DelJson();
        var factura = TraductorFactura.Factura(doc, Parametros);
        var xml = factura.SerializeXmlDocument().ToXmlString();

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(xml, Respuesta(doc.Numero, factura.UUID.Value));

        Assert.Equal("FV", modelo.TipoDocErp);
        Assert.Equal("SETP990000001", modelo.Numero);
        Assert.Equal(doc.FechaHoraDoc, modelo.FechaHoraDoc);
        Assert.Equal("EMPRESA DE PRUEBA S.A.S.", modelo.Emisor.RazonSocial);
        Assert.StartsWith("901033307", modelo.Emisor.NIT);
        Assert.Equal("1", modelo.Emisor.MatriculaMercantil);
        Assert.Equal("ADQUIRIENTE DE EJEMPLO", modelo.Receptor.RazonSocial);
        Assert.Contains("Bogot", modelo.Receptor.LocacionPrincipal.NombreMunicipio);
        Assert.Contains("18760000001", modelo.ResolucionTexto);
        Assert.Contains("990000000 al 995000000", modelo.ResolucionTexto);

        Assert.Equal(2, modelo.Items.Count);
        Assert.Equal("A001", modelo.Items[0].Codigo);
        Assert.Equal(2m, modelo.Items[0].Cant);
        Assert.Equal(50000m, modelo.Items[0].ValorUnit);
        Assert.Equal(100000m, modelo.Items[0].SubTotal);
        Assert.Equal(19m, modelo.Items[0].TarifaIVA);
        Assert.Equal(19000m, modelo.Items[0].ValorIVA);

        // Segunda línea: 1 x 120.000 con 10 % de descuento = 108.000 + IVA 20.520.
        Assert.Equal(108000m, modelo.Items[1].SubTotal);
        Assert.Equal(12000m, modelo.Items[1].ValorDescuento);
        Assert.Equal(20520m, modelo.Items[1].ValorIVA);
        Assert.Equal(128520m, modelo.Items[1].ValorTotal);

        Assert.Equal(208000m, modelo.SubTotal);
        Assert.Equal(39520m, modelo.IVA);
        Assert.Equal(247520m, modelo.ValorTotal);
        Assert.Equal("Contado", modelo.FormaPago.Nombre);
    }

    [Fact]
    public void Los_importes_de_la_factura_son_consistentes_entre_lineas_totales_y_resumen()
    {
        var factura = TraductorFactura.Factura(DelJson(), Parametros);
        var xml = XDocument.Parse(factura.SerializeXmlDocument().ToXmlString());

        decimal Valor(XElement e) => decimal.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);
        var lineas = xml.Root!.Elements(Datos.Cac + "InvoiceLine").ToList();
        var sumaLineas = lineas.Sum(l => Valor(l.Element(Datos.Cbc + "LineExtensionAmount")!));
        var total = xml.Root.Element(Datos.Cac + "LegalMonetaryTotal")!;

        Assert.Equal(sumaLineas, Valor(total.Element(Datos.Cbc + "LineExtensionAmount")!));
        Assert.Equal(208000m, sumaLineas);
        var impuestoDocumento = xml.Root.Elements(Datos.Cac + "TaxTotal").Sum(t => Valor(t.Element(Datos.Cbc + "TaxAmount")!));
        Assert.Equal(39520m, impuestoDocumento);
        Assert.Equal(sumaLineas + impuestoDocumento, Valor(total.Element(Datos.Cbc + "PayableAmount")!));

        // El descuento de la segunda línea va como AllowanceCharge con su base.
        var descuento = lineas[1].Element(Datos.Cac + "AllowanceCharge")!;
        Assert.Equal(12000m, Valor(descuento.Element(Datos.Cbc + "Amount")!));
        Assert.Equal(120000m, Valor(descuento.Element(Datos.Cbc + "BaseAmount")!));
    }

    [Fact]
    public void El_marcador_48_de_responsable_de_IVA_define_el_tributo_y_no_va_como_codigo_de_responsabilidad()
    {
        var doc = DelJson();
        doc.Emisor.responsabilidades_fiscales.Add(new responsabilidad { Codigo = "48", CodigoFE = "48", Descripcion = "Responsable de IVA" });
        var xml = XDocument.Parse(TraductorFactura.Factura(doc, Parametros).SerializeXmlDocument().ToXmlString());

        var proveedor = xml.Descendants(Datos.Cac + "AccountingSupplierParty").Single();
        Assert.Equal("O-13", proveedor.Descendants(Datos.Cbc + "TaxLevelCode").First().Value);
        Assert.Equal("01", proveedor.Descendants(Datos.Cac + "TaxScheme").First().Element(Datos.Cbc + "ID")!.Value);
    }

    [Fact]
    public void Un_documento_que_no_es_factura_de_venta_o_incompleto_no_se_traduce()
    {
        var nota = DelJson();
        nota.TipoDocDian = "91";
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorFactura.Factura(nota, Parametros));

        var sinClave = DelJson();
        sinClave.Resolucion.ClaveTecnica = "";
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorFactura.Factura(sinClave, Parametros));

        var sinSoftware = new ParametrosEmision { SoftwareNit = "901033307" };
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorFactura.Factura(DelJson(), sinSoftware));
    }
}
