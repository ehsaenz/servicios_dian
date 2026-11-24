using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Pruebas;

namespace Pos.Pruebas;

// Envíos al set de pruebas (X-Envio-Set): se guardan en el almacén separado documentos-set, fuera de /cambios y de los
// reintentos, pero sus XML y respuesta DIAN se pueden descargar. Directamente sobre el servicio (sin HTTP).
public class DocumentoSetAlmacenTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private readonly FabricaFalsa _fabrica = new() { ConApplicationResponse = true, SetYaAceptado = true };
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-doc-set-" + Guid.NewGuid().ToString("N"));
    private readonly CarpetaClientes _carpeta;

    public DocumentoSetAlmacenTests(CarpetaClientes carpeta) => _carpeta = carpeta;

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private DocumentoServicio Servicio()
    {
        var tenant = new JsonTenantConfigProvider(_carpeta.Ruta).BuscarPorClaveAsync(CarpetaClientes.ClaveA).GetAwaiter().GetResult()!;
        return new DocumentoServicio(tenant, new CertificadoCache(), _fabrica, _datos, NullLogger.Instance)
        {
            EsperaSet = TimeSpan.Zero
        };
    }

    [Fact]
    public void Un_envio_al_set_no_entra_en_cambios_pero_sus_archivos_se_pueden_descargar()
    {
        var servicio = Servicio();
        var doc = FacturaSet("SETP-ALM-1");

        var resultado = servicio.ValidarEnSet(doc);

        Assert.Equal("aceptado", resultado.Estado);

        // Fuera del almacén principal: ni estado, ni cursor de cambios, ni reintentos.
        Assert.Null(servicio.Obtener("SETP-ALM-1"));
        Assert.DoesNotContain(servicio.Cambios(0, 100), c => c.Numero == "SETP-ALM-1");

        // Pero el XML firmado y la respuesta DIAN sí se entregan para la descarga.
        Assert.NotNull(servicio.XmlFirmado("SETP-ALM-1"));
        Assert.NotNull(servicio.XmlRespuestaDian("SETP-ALM-1"));
        var registro = servicio.ObtenerParaDescarga("SETP-ALM-1");
        Assert.NotNull(registro);
        Assert.Equal(EstadoDocumento.Aprobado, registro!.Estado);
        Assert.True(registro.Habilitacion);
    }

    [Fact]
    public void Sin_generador_de_PDF_el_set_aceptado_no_tiene_PDF_y_lo_dice()
    {
        var servicio = Servicio();
        servicio.ValidarEnSet(FacturaSet("SETP-ALM-2"));

        Assert.Null(servicio.PdfParaDescarga("SETP-ALM-2"));
        Assert.Contains("generador de PDF", servicio.ObtenerParaDescarga("SETP-ALM-2")!.PdfMensaje);
    }

    [Fact]
    public void Un_documento_real_sigue_en_el_almacen_principal_y_en_cambios()
    {
        var servicio = Servicio();
        servicio.Agregar(FacturaSet("SETP-ALM-3"));

        Assert.NotNull(servicio.Obtener("SETP-ALM-3"));
        Assert.Contains(servicio.Cambios(0, 100), c => c.Numero == "SETP-ALM-3");
    }

    private static FE.Models.Doc.DocumentoDian FacturaSet(string numero)
    {
        var doc = ModeloDianTests.Factura();
        doc.Numero = numero;
        return doc;
    }
}
