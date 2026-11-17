using System.Text;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using DIAN.Impresion;

namespace Shared.Pruebas;

// Pruebas del lector del documento aprobado: se arman documentos con los mismos generadores que usa la API
// (datos ficticios), se les pone una respuesta de la DIAN y se comprueba el modelo de impresión.
public class ImpresionTests
{
    private const string Numero = "SETP990000001";
    private static readonly DateTime Fecha = new(2026, 11, 8, 10, 0, 0);

    private static ExtensionDian Extension()
    {
        var ext = Datos.Software();
        ext.RangoNumeracion = new RangoNumeracion
        {
            NumeroResolucion = 18760000001,
            FechaResolucion = new DateTime(2019, 1, 19),
            Prefijo = "SETP",
            Desde = 990000000,
            Hasta = 995000000,
            VigenciaDesde = new DateTime(2019, 1, 19),
            VigenciaHasta = new DateTime(2030, 1, 19)
        };
        return ext;
    }

    private static Adquiriente Adquiriente()
    {
        var direccion = new DireccionFisica
        {
            Departamento = Departamento.BOGOTA,
            Municipio = Municipio.BOGOTA_BOGOTA_DC,
            Linea = "CR 9 A N0 99 - 07 OF 802",
            Pais = Pais.COLOMBIA
        };

        return new Adquiriente
        {
            TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
            Identificacion = "900108281",
            RazonSocial = "ADQUIRIENTE DE EJEMPLO",
            NombreComercial = "ADQUIRIENTE DE EJEMPLO",
            DireccionComercial = direccion,
            DireccionFiscal = direccion,
            TipoPersona = TipoPersona.JURIDICA,
            RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN"),
            MatriculaMercantil = "00000",
            CorreoElectronico = "pruebas@ejemplo.com",
            NumeroTelefonico = "3100000000"
        };
    }

    private static Linea Linea(string codigo, string descripcion, decimal valor)
    {
        var iva = valor * 0.19m;
        return new Linea
        {
            Cantidad = 1M,
            CodigoProducto = new CodigoProducto { Tipo = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO"), Valor = codigo },
            Descripcion = descripcion,
            PrecioUnitario = valor,
            BaseGeneral = valor,
            Impuestos = { new Impuesto { Tipo = TipoImpuesto.GetFromNombre("IVA"), Porcentaje = 19.00m, Importe = iva, BaseImponible = valor } }
        };
    }

    private static ResumenImpuesto Resumen(decimal valor) => new()
    {
        Tipo = TipoImpuesto.GetFromNombre("IVA"),
        Importe = valor * 0.19m,
        Detalles = { new Impuesto { Porcentaje = 19.00m, Importe = valor * 0.19m, BaseImponible = valor } }
    };

    private static Totales Totales(decimal valor) => new()
    {
        ValorBruto = valor,
        BaseImponible = valor,
        ValorBrutoConImpuestos = valor * 1.19m,
        TotalPagable = valor * 1.19m
    };

    private static DetallePago Pago() => new() { Forma = FormaPago.CONTADO, Metodo = MetodoPago.GetFromCodigo("10"), Fecha = Fecha.AddDays(30) };

    private static InvoiceType Factura()
    {
        return new GeneradorFactura(AmbienteDestino.PRUEBAS, TipoFactura.FACTURA_VENTA)
            .ConNumero(Numero)
            .ConFecha(Fecha)
            .ConVencimiento(Fecha.AddDays(30))
            .ConNota("Nota de prueba")
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConExtensionDian(Extension())
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S."))
            .ConAdquiriente(Adquiriente())
            .AgregarLinea(Linea("PRUEBA001", "Producto de prueba", 100000m))
            .AgregarResumenImpuesto(Resumen(100000m))
            .ConTotales(Totales(100000m))
            .ConDetallePago(Pago())
            .AsignarCUFE("clave-tecnica-de-prueba", "12345")
            .Obtener();
    }

    private static ReferenciaDocumento Referencia(InvoiceType factura) => new()
    {
        Numero = factura.ID.Value,
        UUID = factura.UUID.Value,
        AlgoritmoCufe = new AlgoritmoSeguridadUUID { Codigo = factura.UUID.schemeName },
        TipoDocumento = new TipoDocumentoElectronico { Codigo = "01" },
        Fecha = Fecha
    };

    // Respuesta mínima de la DIAN (ApplicationResponse) para el documento dado.
    private static string Respuesta(string numero, string cufe, string codigo = "02", string descripcion = "Procesado Correctamente.") =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<ApplicationResponse xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2\"" +
        " xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\"" +
        " xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">" +
        "<cbc:IssueDate>2026-11-08</cbc:IssueDate><cbc:IssueTime>10:15:32-05:00</cbc:IssueTime>" +
        "<cac:DocumentResponse><cac:Response><cbc:ResponseCode>" + codigo + "</cbc:ResponseCode><cbc:Description>" + descripcion + "</cbc:Description></cac:Response>" +
        "<cac:DocumentReference><cbc:ID>" + numero + "</cbc:ID><cbc:UUID schemeName=\"CUFE-SHA384\">" + cufe + "</cbc:UUID></cac:DocumentReference></cac:DocumentResponse>" +
        "</ApplicationResponse>";

    private static string Xml(InvoiceType factura) => factura.SerializeXmlDocument().ToXmlString();

    [Fact]
    public void La_factura_aprobada_se_lee_completa()
    {
        var factura = Factura();
        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(Xml(factura), Respuesta(Numero, factura.UUID.Value));

        Assert.Equal("FV", modelo.TipoDocErp);
        Assert.Equal("01", modelo.TipoDocDian);
        Assert.Equal("FACTURA ELECTRÓNICA DE VENTA", modelo.TipoDocObj.Nombre);
        Assert.Equal(Numero, modelo.Numero);
        Assert.Equal("990000001", modelo.Folio);
        Assert.Equal(new DateTime(2026, 11, 8, 10, 0, 0), modelo.FechaHoraDoc);
        Assert.Contains("Nota de prueba", modelo.Notas);
        Assert.Equal("2", modelo.Ambiente);

        Assert.Equal(factura.UUID.Value, modelo.CUFE);
        Assert.Contains(modelo.CUFE, modelo.QR);
        Assert.Equal("2026-11-08 10:15:32 (UTC-5)", modelo.FechaValidacionDian);
        Assert.Contains("18760000001", modelo.ResolucionTexto);
        Assert.Contains("SETP", modelo.ResolucionTexto);
        Assert.Contains("990000000 al 995000000", modelo.ResolucionTexto);

        Assert.Equal("EMPRESA DE PRUEBA S.A.S.", modelo.Emisor.RazonSocial);
        Assert.StartsWith("901033307", modelo.Emisor.NIT);
        Assert.Equal("1", modelo.Emisor.MatriculaMercantil);
        Assert.Equal("Cali", modelo.Emisor.LocacionPrincipal.NombreMunicipio);
        Assert.Equal("ADQUIRIENTE DE EJEMPLO", modelo.Receptor.RazonSocial);
        Assert.StartsWith("900108281", modelo.Receptor.NIT);

        var item = Assert.Single(modelo.Items);
        Assert.Equal("PRUEBA001", item.Codigo);
        Assert.Equal("Producto de prueba", item.Detalle);
        Assert.Equal(1m, item.Cant);
        Assert.Equal(100000m, item.ValorUnit);
        Assert.Equal(100000m, item.SubTotal);
        Assert.Equal(19m, item.TarifaIVA);
        Assert.Equal(19000m, item.ValorIVA);
        Assert.Equal(119000m, item.ValorTotal);

        Assert.Equal(100000m, modelo.SubTotal);
        Assert.Equal(19000m, modelo.IVA);
        Assert.Equal(19000m, modelo.ValorImp);
        Assert.Equal(100000m, modelo.BaseGravada);
        Assert.Equal(119000m, modelo.ValorTotal);
        Assert.Equal("Contado", modelo.FormaPago.Nombre);
        Assert.Equal("10", modelo.FormaPago.CodigoMedioPago);
        Assert.True(modelo.Aprobado);
    }

    // Opcional, con documentos REALES aprobados por la DIAN (por ejemplo de habilitación): defina DIAN_REAL_DOC (documento
    // firmado) y DIAN_REAL_AR (ApplicationResponse). Sin las variables no hace nada. Los archivos no se versionan.
    [Fact]
    public void Un_documento_real_aprobado_por_la_DIAN_se_lee()
    {
        var rutaDoc = Environment.GetEnvironmentVariable("DIAN_REAL_DOC");
        var rutaAr = Environment.GetEnvironmentVariable("DIAN_REAL_AR");
        if (string.IsNullOrEmpty(rutaDoc) || string.IsNullOrEmpty(rutaAr))
        {
            return;
        }

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(File.ReadAllText(rutaDoc), File.ReadAllText(rutaAr));

        Assert.True(modelo.Aprobado);
        Assert.NotEmpty(modelo.CUFE);
        Assert.NotEmpty(modelo.Numero);
        Assert.NotEmpty(modelo.FechaValidacionDian);
        Assert.NotEmpty(modelo.Items);
        Assert.True(modelo.ValorTotal > 0m);
        Console.WriteLine($"REAL: {modelo.TipoDocObj.Nombre} {modelo.Numero} validado {modelo.FechaValidacionDian}; total {modelo.ValorTotal}; QR {(modelo.QR.Length > 0 ? "sí" : "NO")}; resolución {(modelo.ResolucionTexto.Length > 0 ? "sí" : "NO")}");
    }

    [Fact]
    public void Un_modelo_armado_a_mano_no_esta_aprobado()
    {
        Assert.False(new DocumentoImpresion().Aprobado);
    }

    [Fact]
    public void La_nota_credito_aprobada_trae_el_documento_que_corrige()
    {
        var factura = Factura();
        var nota = new GeneradorNotaCredito(AmbienteDestino.PRUEBAS, ConceptoNotaCredito.ANULACION_TOTAL, Referencia(factura))
            .ConNumero("SETP990000002")
            .ConFecha(Fecha.AddHours(1))
            .ConNota("Anulación")
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConExtensionDian(Extension())
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S."))
            .ConAdquiriente(Adquiriente())
            .AgregarLinea(Linea("PRUEBA001", "Producto de prueba", 100000m))
            .AgregarResumenImpuesto(Resumen(100000m))
            .ConTotales(Totales(100000m))
            .ConDetallePago(Pago())
            .AsignarCUDE("12345")
            .Obtener();

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
            nota.SerializeXmlDocument().ToXmlString(), Respuesta("SETP990000002", nota.UUID.Value));

        Assert.Equal("NC", modelo.TipoDocErp);
        Assert.Equal("91", modelo.TipoDocDian);
        Assert.Equal("NOTA CRÉDITO ELECTRÓNICA", modelo.TipoDocObj.Nombre);
        Assert.Equal(Numero, modelo.DocumentoRef.Numero);
        Assert.Equal(factura.UUID.Value, modelo.DocumentoRef.CUFE);
        Assert.NotEmpty(modelo.Concepto);
        Assert.Equal(119000m, modelo.ValorTotal);
        Assert.Equal(nota.UUID.Value, modelo.CUFE);
    }

    [Fact]
    public void La_nota_debito_aprobada_se_lee_con_su_linea()
    {
        var factura = Factura();
        var nota = new GeneradorNotaDebito(AmbienteDestino.PRUEBAS, ConceptoNotaDebito.INTERESES, Referencia(factura))
            .ConNumero("SETP990000003")
            .ConFecha(Fecha.AddHours(2))
            .ConNota("Intereses")
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConExtensionDian(Extension())
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S."))
            .ConAdquiriente(Adquiriente())
            .AgregarLinea(Linea("INTERES001", "Intereses de mora de prueba", 10000m))
            .AgregarResumenImpuesto(Resumen(10000m))
            .ConTotales(Totales(10000m))
            .ConDetallePago(Pago())
            .AsignarCUDE("12345")
            .Obtener();

        var modelo = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
            nota.SerializeXmlDocument().ToXmlString(), Respuesta("SETP990000003", nota.UUID.Value));

        Assert.Equal("ND", modelo.TipoDocErp);
        Assert.Equal("92", modelo.TipoDocDian);
        Assert.Equal(Numero, modelo.DocumentoRef.Numero);
        var item = Assert.Single(modelo.Items);
        Assert.Equal("Intereses de mora de prueba", item.Detalle);
        Assert.Equal(11900m, modelo.ValorTotal);
    }

    [Fact]
    public void El_contenedor_con_la_respuesta_de_la_DIAN_se_lee_igual_que_el_par()
    {
        var factura = Factura();
        var xml = Xml(factura);
        var respuesta = Respuesta(Numero, factura.UUID.Value);
        var referencia = Referencia(factura);

        var contenedor = new GeneradorContenedor(AmbienteDestino.PRUEBAS)
            .ConNumero(Numero)
            .ConFecha(Fecha)
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S."))
            .ConAdquiriente(Adquiriente())
            .ConDocumento(Encoding.UTF8.GetBytes(xml), referencia)
            .ConRespuesta(Encoding.UTF8.GetBytes(respuesta), "02", new DateTime(2026, 11, 8, 10, 15, 32))
            .Obtener()
            .SerializeXmlDocument().ToXmlString();

        // Para probar el PDF a mano: DIAN_IMPRESION_CONTENEDOR=<archivo> y pasarlo a tools\Reportes\PruebaLinux.
        var destino = Environment.GetEnvironmentVariable("DIAN_IMPRESION_CONTENEDOR");
        if (!string.IsNullOrEmpty(destino))
        {
            File.WriteAllText(destino, contenedor);
        }

        var delContenedor = LectorDocumentoAprobado.DesdeContenedor(contenedor);
        var delPar = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(xml, respuesta);

        Assert.Equal(delPar.ToJson(), delContenedor.ToJson());
        Assert.Equal("2026-11-08 10:15:32 (UTC-5)", delContenedor.FechaValidacionDian);
    }

    [Fact]
    public void Si_la_DIAN_rechazo_el_documento_no_hay_modelo_de_impresion()
    {
        var factura = Factura();
        var respuesta = Respuesta(Numero, factura.UUID.Value, "04", "Documento rechazado por la DIAN.");

        var error = Assert.Throws<DocumentoNoAprobadoException>(() => LectorDocumentoAprobado.DesdeDocumentoYRespuesta(Xml(factura), respuesta));
        Assert.Equal("04", error.CodigoRespuesta);
        Assert.Contains("rechazado", error.Message);
    }

    [Fact]
    public void La_respuesta_de_otro_documento_no_sirve()
    {
        var factura = Factura();
        var respuesta = Respuesta(Numero, "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

        var error = Assert.Throws<DocumentoNoAprobadoException>(() => LectorDocumentoAprobado.DesdeDocumentoYRespuesta(Xml(factura), respuesta));
        Assert.Contains("otro documento", error.Message);
    }

    [Fact]
    public void El_contenedor_sin_respuesta_de_la_DIAN_no_se_imprime()
    {
        var factura = Factura();
        var contenedor = new GeneradorContenedor(AmbienteDestino.PRUEBAS)
            .ConNumero(Numero)
            .ConFecha(Fecha)
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S."))
            .ConAdquiriente(Adquiriente())
            .ConDocumento(Encoding.UTF8.GetBytes(Xml(factura)), Referencia(factura))
            .Obtener()
            .SerializeXmlDocument().ToXmlString();

        var error = Assert.Throws<DocumentoNoAprobadoException>(() => LectorDocumentoAprobado.DesdeContenedor(contenedor));
        Assert.Contains("no está aprobado", error.Message);
    }

    [Fact]
    public void El_json_del_modelo_tiene_los_nombres_que_usa_la_plantilla()
    {
        var factura = Factura();
        var json = LectorDocumentoAprobado.DesdeDocumentoYRespuesta(Xml(factura), Respuesta(Numero, factura.UUID.Value)).ToJson();

        // Para revisar a mano cómo queda impreso: DIAN_IMPRESION_JSON=<archivo> y pasar ese JSON al generador de reportes.
        var destino = Environment.GetEnvironmentVariable("DIAN_IMPRESION_JSON");
        if (!string.IsNullOrEmpty(destino))
        {
            File.WriteAllText(destino, json);
        }

        foreach (var campo in new[] { "\"Numero\"", "\"TipoDocObj\"", "\"Emisor\"", "\"Receptor\"", "\"Items\"", "\"CUFE\"", "\"QR\"", "\"Ambiente\"", "\"FechaValidacionDian\"", "\"ResolucionTexto\"", "\"LocacionPrincipal\"", "\"ValorTotal\"" })
        {
            Assert.Contains(campo, json);
        }
    }

    [Fact]
    public void Un_xml_con_DTD_se_rechaza()
    {
        var factura = Factura();
        var conDtd = "<!DOCTYPE x [<!ENTITY a \"b\">]>" + Xml(factura);
        Assert.ThrowsAny<Exception>(() => LectorDocumentoAprobado.DesdeDocumentoYRespuesta(conDtd, Respuesta(Numero, factura.UUID.Value)));
    }
}
