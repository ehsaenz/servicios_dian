using eFacturacionColombia_NETCore.Tipos.Standard;
using FacturacionColombia_Metadatos;

namespace Shared.Pruebas;

// Documentos de POS armados como lo hace la herramienta de habilitación. Datos ficticios.
public static class DatosPos
{
    private static void AgregarEmisor(MetaDatosDE meta, string nit, string razon)
    {
        var direccion = Datos.Cali();
        meta.AddEmisor(
            new DatosFiscales(razon)
            {
                Identificacion = nit,
                DV = "1",
                Direccion = direccion,
                Telefono = "6020000000",
                Email = "prueba@ejemplo.com"
            },
            new DatosComerciales
            {
                CodigoCliente = nit,
                NombreComercial = razon,
                Telefono = "6020000000",
                Email = "prueba@ejemplo.com",
                MatriculaMercantil = "1",
                Direccion = direccion
            });
    }

    private static void AgregarConsumidorFinal(MetaDatosDE meta)
    {
        #pragma warning disable CS0612 // el método obsoleto es lo que se prueba
        meta.AddAdquirientePersonaNatural("222222222222", "Consumidor Final", Datos.Cali(), "3000000000", "consumidor@ejemplo.com");
        #pragma warning restore CS0612
    }

    public static MetaDatosDE Tiquete(string nit, string razon, string numero)
    {
        var fecha = new DateTime(2026, 11, 7, 10, 0, 0);
        var meta = new MetaDatosDE(TipoDocumentoElectronico.DOCUMENTO_EQUIVALENTE_POS)
        {
            FechaEmision = fecha,
            FechaVencimiento = fecha.AddDays(30),
            Numero = numero
        };

        meta.AddResolucion(18760000001, new DateTime(2019, 1, 19), new DateTime(2019, 1, 19), new DateTime(2030, 1, 19), "EPOS", 1, 1000000);
        AgregarEmisor(meta, nit, razon);
        AgregarConsumidorFinal(meta);
        meta.AddPeriodoFacturacion(fecha);
        meta.AddDatosSoftware(nit, "00000000-0000-0000-0000-000000000000", "12345");
        meta.AddFormaPago(FormaPago.CONTADO, MetodoPago.GetFromCodigo("10"), fecha);
        meta.AddItem("PRODUCTO001", "PRODUCTO DE PRUEBA POS", "GENERICA", "REF001", 1M, "EA", 100000M, false, 0.19M);

        var bruto = meta.Items.Sum(i => i.BaseGeneral);
        var @base = meta.Items.SelectMany(i => i.Impuestos).Sum(i => i.BaseImponible);
        var impuestos = meta.Items.SelectMany(i => i.Impuestos).Sum(i => i.Importe);
        meta.Totales = new Totales
        {
            ValorBruto = bruto,
            BaseImponible = @base,
            ValorBrutoConImpuestos = bruto + impuestos,
            TotalPagable = bruto + impuestos
        };

        meta.InformacionCaja.PlacaCaja = "234566";
        meta.InformacionCaja.NombreCajero = "Cajero de prueba";
        meta.InformacionCaja.UbicacionCaja = "Caja principal";
        meta.InformacionCaja.TipoCaja = "Caja de apoyo";
        return meta;
    }

    public static MetaDatosDE NotaAjuste(string nit, string razon, string numero, string numeroPos, string cudePos)
    {
        var fecha = new DateTime(2026, 11, 7, 12, 0, 0);
        var meta = new MetaDatosDE(TipoDocumentoElectronico.NOTA_AJUSTE_DOCUMENTO_EQUIVALENTE)
        {
            FechaEmision = fecha,
            FechaVencimiento = fecha.Date,
            Numero = numero,
            ConceptoAjuste = ConceptoNotaAjusteDocumentoEquivalente.DEVOLUCION_PARCIAL
        };

        meta.AddDocumentoReferencia(cudePos, numeroPos, fecha.Date.AddDays(-1));
        AgregarEmisor(meta, nit, razon);
        AgregarConsumidorFinal(meta);
        meta.AddDatosSoftware(nit, "00000000-0000-0000-0000-000000000000", "12345");
        meta.AddFormaPago(FormaPago.CONTADO, MetodoPago.GetFromCodigo("10"), fecha);
        meta.AddItem("PRODUCTO001", "PRODUCTO DE PRUEBA POS", "GENERICA", "REF001", 1M, "EA", 100000M, false, 0.19M);

        var bruto = meta.Items.Sum(i => i.BaseGeneral);
        var impuestos = meta.Items.SelectMany(i => i.Impuestos).Sum(i => i.Importe);
        meta.Totales = new Totales
        {
            ValorBruto = bruto,
            BaseImponible = bruto,
            ValorBrutoConImpuestos = bruto + impuestos,
            TotalPagable = bruto + impuestos
        };
        return meta;
    }
}
