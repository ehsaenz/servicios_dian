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

// Notas crédito y débito del ERP -> UBL -> modelo de impresión. Datos ficticios.
public class TraductorNotaTests
{
    private static readonly ParametrosEmision Parametros = new()
    {
        Ambiente = AmbienteDestino.PRUEBAS,
        SoftwareNit = "901033307",
        SoftwareIdentificador = "00000000-0000-0000-0000-000000000000",
        SoftwarePin = "12345"
    };

    private const string CufeFactura = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    // La nota se arma como la factura de prueba pero con tipo 91/92 y el documento de referencia; pasa por el JSON.
    private static DocumentoDian Nota(string tipoDian, string numero, string? concepto)
    {
        var doc = ModeloDianTests.Factura();
        doc.TipoDocDian = tipoDian;
        doc.TipoDocErp = tipoDian == "91" ? "NC" : "ND";
        doc.Numero = numero;
        doc.DocumentoRef = new documento_ref { Numero = "SETP990000001", CUFE = CufeFactura, Fecha = doc.FechaHoraDoc.AddDays(-1), Concepto = concepto! };
        return JObject.Parse(JsonConvert.SerializeObject(doc)).ToObject<DocumentoDian>()!;
    }

    private static string Respuesta(string numero, string cufe) =>
        "<ApplicationResponse xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2\"" +
        " xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"" +
        " xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
        "<cbc:IssueDate>2026-11-08</cbc:IssueDate><cbc:IssueTime>10:15:32-05:00</cbc:IssueTime>" +
        "<cac:DocumentResponse><cac:Response><cbc:ResponseCode>02</cbc:ResponseCode></cac:Response>" +
        "<cac:DocumentReference><cbc:ID>" + numero + "</cbc:ID><cbc:UUID>" + cufe + "</cbc:UUID></cac:DocumentReference></cac:DocumentResponse>" +
        "</ApplicationResponse>";

    [Fact]
    public void La_nota_credito_del_ERP_conserva_referencia_concepto_y_totales()
    {
        var doc = Nota("91", "SETP990000010", "2");
        var nota = TraductorNota.NotaCredito(doc, Parametros);

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
            nota.SerializeXmlDocument().ToXmlString(), Respuesta(doc.Numero, nota.UUID.Value));

        Assert.Equal("NC", modelo.TipoDocErp);
        Assert.Equal("91", modelo.TipoDocDian);
        Assert.Equal("SETP990000001", modelo.DocumentoRef.Numero);
        Assert.Equal(CufeFactura, modelo.DocumentoRef.CUFE);
        Assert.Contains("Anulaci", modelo.Concepto);
        Assert.Equal(2, modelo.Items.Count);
        Assert.Equal(208000m, modelo.SubTotal);
        Assert.Equal(247520m, modelo.ValorTotal);
        Assert.Equal(nota.UUID.Value, modelo.CUFE);
    }

    [Fact]
    public void La_nota_debito_del_ERP_conserva_referencia_y_concepto()
    {
        var doc = Nota("92", "SETP990000011", "1");
        var nota = TraductorNota.NotaDebito(doc, Parametros);

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
            nota.SerializeXmlDocument().ToXmlString(), Respuesta(doc.Numero, nota.UUID.Value));

        Assert.Equal("ND", modelo.TipoDocErp);
        Assert.Equal("92", modelo.TipoDocDian);
        Assert.Equal("SETP990000001", modelo.DocumentoRef.Numero);
        Assert.Contains("Intereses", modelo.Concepto);
        Assert.Equal(247520m, modelo.ValorTotal);
    }

    [Fact]
    public void Sin_concepto_la_credito_es_devolucion_parcial_y_la_debito_otros()
    {
        var credito = XDocument.Parse(TraductorNota.NotaCredito(Nota("91", "SETP990000012", null), Parametros).SerializeXmlDocument().ToXmlString());
        var debito = XDocument.Parse(TraductorNota.NotaDebito(Nota("92", "SETP990000013", null), Parametros).SerializeXmlDocument().ToXmlString());

        Assert.Equal("1", credito.Descendants(Datos.Cbc + "ResponseCode").First().Value);
        Assert.Equal("4", debito.Descendants(Datos.Cbc + "ResponseCode").First().Value);
    }

    [Fact]
    public void Una_nota_sin_documento_origen_o_con_concepto_invalido_no_se_traduce()
    {
        var sinRef = Nota("91", "SETP990000014", "2");
        sinRef.DocumentoRef.CUFE = "";
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorNota.NotaCredito(sinRef, Parametros));

        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorNota.NotaCredito(Nota("91", "SETP990000015", "9"), Parametros));
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorNota.NotaCredito(Nota("92", "SETP990000016", "1"), Parametros));
        Assert.Throws<DocumentoNoTraducibleException>(() => TraductorNota.NotaDebito(Nota("91", "SETP990000017", "1"), Parametros));
    }
}
