using System;
using DIAN.Pos.Servicios;
using eFacturacionColombia_NETCore.Tipos.Standard;
using FacturacionColombia_Metadatos;
using Xunit;

namespace Pos.Pruebas;

public class NotaAjusteDocumentoSoporteTests
{
    private static readonly DateTime Fecha = new(2026, 11, 20);

    private static MetaDatosDE Base(string tipo = "95", string referencia = "05", string concepto = "2") => new()
    {
        TipoDocumento = new TipoDocumentoElectronico { Codigo = tipo },
        Numero = "NSDS1",
        FechaEmision = Fecha,
        ConceptoAjuste = new ConceptoNotaAjusteDocumentoEquivalente { Codigo = concepto, Descripcion = "Anulación" },
        DocumentoReferencia = new ReferenciaDocumento
        {
            Numero = "SEDS1",
            TipoDocumento = new TipoDocumentoElectronico { Codigo = referencia },
            AlgoritmoCufe = AlgoritmoSeguridadUUID.CUDS_SHA384,
            UUID = new string('a', 96),
            Fecha = Fecha.AddDays(-1)
        }
    };

    [Fact]
    public void Rechaza_un_tipo_distinto_de_95()
    {
        var ex = Assert.Throws<ArgumentException>(() => NotaAjusteDocumentoSoporteServicio.Validar(Base(tipo: "05")));
        Assert.Contains("95", ex.Message);
    }

    [Fact]
    public void Rechaza_una_referencia_que_no_sea_documento_soporte_05()
    {
        var ex = Assert.Throws<ArgumentException>(() => NotaAjusteDocumentoSoporteServicio.Validar(Base(referencia: "01")));
        Assert.Contains("05", ex.Message);
    }

    [Fact]
    public void Rechaza_un_concepto_fuera_de_1_a_5()
    {
        Assert.Throws<ArgumentException>(() => NotaAjusteDocumentoSoporteServicio.Validar(Base(concepto: "9")));
    }

    [Fact]
    public void Rechaza_una_referencia_que_no_use_CUDS()
    {
        var de = Base();
        de.DocumentoReferencia.AlgoritmoCufe = AlgoritmoSeguridadUUID.CUFE_SHA384;
        Assert.Throws<ArgumentException>(() => NotaAjusteDocumentoSoporteServicio.Validar(de));
    }
}
