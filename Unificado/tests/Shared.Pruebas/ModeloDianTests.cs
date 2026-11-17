using DianModelDoc.Models;
using FE.Enum;
using FE.Models.Doc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WS_V2_HttpClientLib.Builders;

namespace Shared.Pruebas;

// El modelo DocumentoDian y su constructor se copiaron tal cual del proyecto 01_FE_V2_Models / 05_FE_V2_HttpClientLib.
// Estas pruebas aseguran que el JSON que arma QuantyERP (DianRequestJsonBuilder) se lee igual que como lo leía la API
// vieja (JObject.ToObject<DocumentoDian>) y que los totales salen de las mismas cuentas. Datos ficticios.
public class ModeloDianTests
{
    private static readonly List<responsabilidad> Responsabilidades = new()
    {
        new responsabilidad { Codigo = "O-13", CodigoFE = "O-13", Descripcion = "Gran contribuyente" }
    };

    public static DocumentoDian Factura()
    {
        var builder = DianDocumentBuilder.FacturaVenta("FV", "SETP990000001", new DateTime(2026, 11, 8, 10, 0, 0))
            .ConFormaPago(MetodoPagoEnum.Contado, "10", 0, "")
            .ConParametrosFiscales(0, 0, 0, 0.15m, 0.025m, 0.035m, 0.00966m)
            .ConResolucion("18760000001", "SETP", new DateTime(2019, 1, 19), new DateTime(2030, 1, 19), 990000000, 995000000, "clave-tecnica-de-prueba")
            .ConEmisorPersonaJuridica("EMP1", "1", "901033307", "6", "EMPRESA DE PRUEBA S.A.S.", "EMPRESA DE PRUEBA S.A.S.", "Contacto", "prueba@ejemplo.com", "6020000000", "Gerente", Responsabilidades, "48")
            .ConLocacionPrincipalEmisor("CR 1 D 1 45 37", "76001")
            .ConLocacionComercialEmisor("CR 1 D 1 45 37", "76001")
            .ConReceptorPersonaJuridica("CLI1", "00000", "900108281", "3", "ADQUIRIENTE DE EJEMPLO", "ADQUIRIENTE DE EJEMPLO", "", "pruebas@ejemplo.com", "3100000000", "", Responsabilidades, "48")
            .ConLocacionPrincipalReceptor("CR 9 A 99 07", "11001")
            .ConLocacionComercialReceptor("CR 9 A 99 07", "11001")
            .AgregarItem("A001", "REF-001", "Camiseta de prueba", 2m, 50000m, 0.19m)
            .AgregarItem("A002", "REF-002", "Pantalón de prueba", 1m, 120000m, 0.19m, 0.10m);

        return builder.Construir(validar: true);
    }

    [Fact]
    public void El_documento_armado_como_lo_hace_el_ERP_no_trae_errores_y_totaliza()
    {
        var doc = Factura();

        Assert.Empty(doc.Errores);
        Assert.Equal("01", doc.TipoDocDian);
        Assert.Equal(2, doc.Items.Count);
        // 2 x 50.000 + 1 x 120.000 = 220.000 de subtotal; con 10 % de descuento en la segunda línea la base gravada es 208.000.
        Assert.Equal(220000m, doc.SubTotal);
        Assert.Equal(208000m, doc.BaseGravada);
        Assert.Equal(39520m, doc.Impuestos.Single(i => i.ID == "01").TotalImpuesto);
    }

    [Fact]
    public void El_JSON_del_ERP_se_lee_igual_que_lo_leia_la_API_vieja()
    {
        var original = Factura();
        var json = JsonConvert.SerializeObject(original, Formatting.Indented);

        // La API vieja: data (JObject) -> data.ToObject<DocumentoDian>().
        var leido = JObject.Parse(json).ToObject<DocumentoDian>()!;

        Assert.Equal(original.Numero, leido.Numero);
        Assert.Equal(original.TipoDocDian, leido.TipoDocDian);
        Assert.Equal(original.FechaHoraDoc, leido.FechaHoraDoc);
        Assert.Equal(original.Emisor.NIT, leido.Emisor.NIT);
        Assert.Equal(original.Receptor.LocacionPrincipal.CodigoMunicipio, leido.Receptor.LocacionPrincipal.CodigoMunicipio);
        Assert.Equal(original.Resolucion.ClaveTecnica, leido.Resolucion.ClaveTecnica);
        Assert.Equal(original.Items.Count, leido.Items.Count);
        Assert.Equal(original.Items[1].ValorTotal, leido.Items[1].ValorTotal);
        Assert.Equal(original.SubTotal, leido.SubTotal);
        Assert.Equal(original.IVA, leido.IVA);
        Assert.Equal(original.FormaPago.CodigoMedioPago, leido.FormaPago.CodigoMedioPago);
    }
}
