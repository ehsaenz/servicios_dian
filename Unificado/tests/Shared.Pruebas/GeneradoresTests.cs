using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;

namespace Shared.Pruebas;

// Pruebas de regresión de los arreglos que la DIAN pidió al validar en habilitación (ver docs\fase1-bitacora-migracion.md).
// Los datos son ficticios: ningún software, PIN ni certificado real.
public static class Datos
{
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public static DireccionFisica Cali(string postal = "760003") => new DireccionFisica
    {
        Departamento = new Departamento { Codigo = "76", Nombre = "Valle del Cauca" },
        Municipio = new Municipio { Codigo = "76001", Nombre = "Cali" },
        Linea = "CR 1 D 1 45 37",
        CodigoPostal = postal,
        Pais = new Pais { Codigo = "CO", Nombre = "Colombia" }
    };

    public static ExtensionDian Software() => new ExtensionDian
    {
        PaisOrigen = Pais.COLOMBIA,
        SoftwareProveedorNit = "901033307",
        SoftwareIdentificador = "00000000-0000-0000-0000-000000000000",
        SoftwarePin = "12345"
    };

    public static Emisor Emisor(string? nombreComercial, string postal = "760003") => new Emisor
    {
        Identificacion = "901033307",
        RazonSocial = "EMPRESA DE PRUEBA S.A.S.",
        DireccionFiscal = Cali(postal),
        TipoPersona = TipoPersona.JURIDICA,
        RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN"),
        ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("01"),
        PrefijoRangoNumeracion = "SETP",
        CorreoElectronico = "prueba@ejemplo.com",
        NumeroTelefonico = "6020000000",
        DatosComerciales = new DatosComerciales
        {
            MatriculaMercantil = "1",
            Direccion = Cali(postal),
            NombreComercial = nombreComercial
        }
    };

    // Documento soporte armado como lo hace la herramienta de habilitación.
    public static MetaDatosDE MetaDocumentoSoporte()
    {
        var fecha = new DateTime(2026, 11, 7, 10, 0, 0);
        var meta = new MetaDatosDE
        {
            FechaEmision = fecha,
            FechaVencimiento = fecha,
            TipoDocumento = TipoDocumentoElectronico.DOCUMENTO_SOPORTE,
            Numero = "SEDS1"
        };

        meta.AddResolucion(18760000001, new DateTime(2019, 1, 19), new DateTime(2019, 1, 19), new DateTime(2030, 1, 19), "SEDS", 984000000, 985000000);

        meta.AddEmisor(
            new DatosFiscales
            {
                TipoPersona = TipoPersona.NATURAL,
                TipoIdentificacion = TipoIdentificacion.GetFromCodigo("13"),
                Identificacion = "1000000001",
                DV = NitHelper.CalcularDigitoVerificador("1000000001").ToString(),
                PrimerNombre = "PROVEEDOR",
                PrimerApellido = "PRUEBA",
                Direccion = Cali(),
                Email = "proveedor@ejemplo.com",
                Telefono = "3000000000",
                RegimenFiscal = RegimenFiscal.Default,
                ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ")
            },
            new DatosComerciales { CodigoCliente = "1000000001", NombreComercial = "PROVEEDOR PRUEBA", Direccion = Cali() });

        meta.AddPeriodoFacturacion(fecha);
        meta.AddDatosSoftware("901033307", "00000000-0000-0000-0000-000000000000", "12345");
        meta.AddFormaPago(FormaPago.CONTADO, MetodoPago.GetFromCodigo("10"), fecha);

        meta.AddAdquiriente(
            new DatosFiscales
            {
                TipoPersona = TipoPersona.JURIDICA,
                TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                Identificacion = "901033307",
                DV = "6",
                RazonSocial = "EMPRESA DE PRUEBA S.A.S.",
                Direccion = Cali(),
                Email = "prueba@ejemplo.com",
                Telefono = "6020000000",
                RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN"),
                ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("01")
            },
            new DatosComerciales { CodigoCliente = "901033307", NombreComercial = "EMPRESA DE PRUEBA S.A.S.", Direccion = Cali() });

        meta.AddItem("SER001", "SERVICIOS DE PRUEBA", "GENERICA", "REF001", 1M, "EA", 100000);
        meta.AddTotales(100000M, 0M, 100000M, 0, 0, 100000M);
        return meta;
    }

    public static XDocument Serializar(string xml) => XDocument.Parse(xml);
}

public class FacturaTests
{
    [Fact]
    public void El_nombre_del_emisor_sale_de_NombreComercial()
    {
        var emisor = Datos.Emisor("EMPRESA DE PRUEBA S.A.S.");

        var generador = new GeneradorFactura(AmbienteDestino.PRUEBAS, TipoFactura.FACTURA_VENTA)
            .ConNumero("SETP990000001")
            .ConFecha(new DateTime(2026, 11, 7, 10, 0, 0))
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConEmisor(emisor);

        var xml = Datos.Serializar(generador.Obtener().SerializeXmlDocument().ToXmlString());
        var proveedor = xml.Descendants(Datos.Cac + "AccountingSupplierParty").Single();

        Assert.Equal("EMPRESA DE PRUEBA S.A.S.", proveedor.Descendants(Datos.Cbc + "RegistrationName").First().Value);
        Assert.Equal("EMPRESA DE PRUEBA S.A.S.", proveedor.Descendants(Datos.Cac + "PartyName").Single().Element(Datos.Cbc + "Name")!.Value);
    }
}

public class DocumentoSoporteTests
{
    private static XDocument Generar()
    {
        var meta = Datos.MetaDocumentoSoporte();

        var generador = new GeneradorDocumentoSoporte(AmbienteDestino.PRUEBAS)
            .ConNumero(meta.Numero)
            .ConFecha(meta.FechaEmision)
            .ConVencimiento(meta.FechaVencimiento)
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConExtensionDian(meta.ExtensionDian)
            .ConProveedor(meta.Proveedor)
            .ConAdquiriente(meta.Adquiriente)
            .AgregarLineas(meta.Items)
            .ConTotales(meta.Totales)
            .ConDetallePago(meta.DetallePago)
            .AsignarCUDS("12345")
            .ConPeriodoFacturacion(meta.Periodo);

        return Datos.Serializar(generador.Obtener().SerializeXmlDocument().ToXmlString());
    }

    [Fact]
    public void El_adquiriente_no_lleva_localizacion_fisica()
    {
        // DSAK57: el anexo eliminó PhysicalLocation del adquiriente.
        var adquiriente = Generar().Descendants(Datos.Cac + "AccountingCustomerParty").Single();

        Assert.Empty(adquiriente.Descendants(Datos.Cac + "PhysicalLocation"));
    }

    [Fact]
    public void El_proveedor_lleva_el_codigo_postal_y_la_ciudad_con_la_escritura_de_la_lista()
    {
        // DSAJ73, DSAJ10a y DSAJ11.
        var proveedor = Generar().Descendants(Datos.Cac + "AccountingSupplierParty").Single();
        var direccion = proveedor.Descendants(Datos.Cac + "PhysicalLocation").Single();

        Assert.Equal("760003", direccion.Descendants(Datos.Cbc + "PostalZone").Single().Value);
        Assert.Equal("Cali", direccion.Descendants(Datos.Cbc + "CityName").Single().Value);
        Assert.Equal("Valle del Cauca", direccion.Descendants(Datos.Cbc + "CountrySubentity").Single().Value);
    }

    [Fact]
    public void AddEmisorPersonaNatural_llena_al_proveedor_en_un_documento_soporte()
    {
        // Antes comparaba el tipo de documento por referencia, nunca coincidía y dejaba al proveedor vacío.
        var meta = new MetaDatosDE { TipoDocumento = TipoDocumentoElectronico.DOCUMENTO_SOPORTE };

        #pragma warning disable CS0612 // el método obsoleto es lo que se prueba
        meta.AddEmisorPersonaNatural("1000000001", "PROVEEDOR DE PRUEBA", Datos.Cali(), "3000000000", "proveedor@ejemplo.com");
        #pragma warning restore CS0612

        Assert.Equal("1000000001", meta.Proveedor.Identificacion);
        Assert.Equal("PROVEEDOR DE PRUEBA", meta.Proveedor.Nombre);
        Assert.Null(meta.Emisor.Identificacion);
    }
}

public class NotaAjusteDocumentoSoporteTests
{
    private static XDocument Generar()
    {
        var meta = Datos.MetaDocumentoSoporte();
        var referencia = new ReferenciaDocumento
        {
            Numero = "SEDS1",
            UUID = new string('a', 96),
            AlgoritmoCufe = new AlgoritmoSeguridadUUID { Codigo = "CUDS-SHA384" },
            TipoDocumento = TipoDocumentoElectronico.DOCUMENTO_SOPORTE,
            Fecha = new DateTime(2026, 11, 7)
        };

        var generador = new GeneradorNotaAjusteDocumentoSoporte(AmbienteDestino.PRUEBAS, ConceptoNotaAjusteDocumentoSoporte.ANULACION_TOTAL, referencia)
            .ConNumero("SEDS2")
            .ConFecha(meta.FechaEmision)
            .ConNota("Prueba")
            .ConMoneda(Moneda.PESO_COLOMBIANO)
            .ConExtensionDian(Datos.Software())
            .ConProveedor(meta.Proveedor)
            .ConAdquiriente(meta.Adquiriente);

        foreach (var item in meta.Items)
        {
            generador.AgregarLinea(item);
        }

        generador.ConTotales(meta.Totales)
            .ConDetallePago(meta.DetallePago)
            .AsignarCUDS("12345");

        return Datos.Serializar(generador.Obtener().SerializeXmlDocument().ToXmlString());
    }

    [Fact]
    public void La_identificacion_del_proveedor_siempre_es_31()
    {
        // NSAJ25a.
        var empresa = Generar().Descendants(Datos.Cac + "AccountingSupplierParty").Single()
            .Descendants(Datos.Cbc + "CompanyID").First();

        Assert.Equal("31", empresa.Attribute("schemeName")!.Value);
    }

    [Fact]
    public void La_linea_sin_impuestos_se_reporta_como_IVA_exento_con_dos_decimales()
    {
        // NSAX04, NSAX16: la línea necesita TaxTotal/TaxSubtotal con un tributo real, y la tarifa va como 0.00.
        var linea = Generar().Descendants(Datos.Cac + "CreditNoteLine").Single();
        var subtotal = linea.Descendants(Datos.Cac + "TaxSubtotal").Single();

        Assert.Equal("01", subtotal.Descendants(Datos.Cac + "TaxScheme").Single().Element(Datos.Cbc + "ID")!.Value);
        Assert.Equal("0.00", subtotal.Descendants(Datos.Cbc + "Percent").Single().Value);
        Assert.Equal("100000", subtotal.Element(Datos.Cbc + "TaxableAmount")!.Value);
    }

    [Fact]
    public void La_base_imponible_del_total_incluye_las_lineas_exentas_y_hay_resumen_del_documento()
    {
        // NSAU04: la base imponible del total debe ser la suma de las bases de las líneas.
        var xml = Generar();

        var baseTotal = xml.Descendants(Datos.Cac + "LegalMonetaryTotal").Single().Element(Datos.Cbc + "TaxExclusiveAmount")!.Value;
        var resumen = xml.Root!.Elements(Datos.Cac + "TaxTotal").Single();

        Assert.Equal("100000", baseTotal);
        Assert.Equal("0.00", resumen.Descendants(Datos.Cbc + "Percent").Single().Value);
    }

    [Fact]
    public void La_linea_lleva_marca_y_modelo()
    {
        // NSAZ04 y NSAZ05.
        var item = Generar().Descendants(Datos.Cac + "CreditNoteLine").Single().Element(Datos.Cac + "Item")!;

        Assert.Equal("GENERICA", item.Element(Datos.Cbc + "BrandName")!.Value);
        Assert.Equal("REF001", item.Element(Datos.Cbc + "ModelName")!.Value);
    }
}

public class DocumentoEquivalenteTests
{
    private static XDocument GenerarConEmisor(string postal)
    {
        var generador = new GeneradorDocumentoEquivalente(AmbienteDestino.PRUEBAS, TipoDocumentoEquivalente.POS)
            .ConEmisor(Datos.Emisor("EMPRESA DE PRUEBA S.A.S.", postal));

        return Datos.Serializar(generador.Obtener().SerializeXmlDocument().ToXmlString());
    }

    [Fact]
    public void El_emisor_lleva_el_codigo_postal()
    {
        // DEAJ73.
        var direccion = GenerarConEmisor("760003").Descendants(Datos.Cac + "AccountingSupplierParty").Single()
            .Descendants(Datos.Cac + "PhysicalLocation").Single();

        Assert.Equal("760003", direccion.Descendants(Datos.Cbc + "PostalZone").Single().Value);
    }

    [Fact]
    public void El_valor_por_defecto_N_A_no_se_emite_como_codigo_postal()
    {
        var direccion = GenerarConEmisor("N/A").Descendants(Datos.Cac + "AccountingSupplierParty").Single()
            .Descendants(Datos.Cac + "PhysicalLocation").Single();

        Assert.Empty(direccion.Descendants(Datos.Cbc + "PostalZone"));
    }
}

public class FirmaElectronicaTests
{
    private const string FacturaMinima = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
         xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2""
         xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2""
         xmlns:ext=""urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2""
         xmlns:sts=""dian:gov:co:facturaelectronica:Structures-2-1"">
  <ext:UBLExtensions>
    <ext:UBLExtension><ext:ExtensionContent><sts:DianExtensions /></ext:ExtensionContent></ext:UBLExtension>
    <ext:UBLExtension><ext:ExtensionContent /></ext:UBLExtension>
  </ext:UBLExtensions>
  <cbc:ID>PRUEBA1</cbc:ID>
</Invoice>";

    private static X509Certificate2 CertificadoDePrueba()
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest("CN=Prueba", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable);
    }

    private static int PosicionDeLaFirma(byte[] firmado)
    {
        var xml = XDocument.Load(new MemoryStream(firmado));
        var extensiones = xml.Descendants(Datos.Ext + "UBLExtension").ToList();
        return extensiones.FindIndex(e => e.Descendants(Datos.Ds + "Signature").Any()) + 1;
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void La_firma_va_en_la_extension_indicada(int? numExtension, int esperada)
    {
        var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = CertificadoDePrueba() };
        var fecha = new DateTime(2026, 11, 7, 10, 0, 0);

        var firmado = numExtension == null
            ? firma.FirmarFactura(FacturaMinima, fecha)
            : firma.FirmarFactura(FacturaMinima, fecha, numExtension.Value);

        Assert.Equal(esperada, PosicionDeLaFirma(firmado));
    }
}
