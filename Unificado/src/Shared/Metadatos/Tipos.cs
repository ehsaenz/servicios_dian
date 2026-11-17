using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FacturacionColombia_Metadatos
{

    public class TipoDocumentoElectronico
    {
        public string Codigo { get; set; }

        public static TipoDocumentoElectronico FACTURA_VENTA_NACIONAL => new TipoDocumentoElectronico { Codigo = "01" };
        public static TipoDocumentoElectronico FACTURA_EXPORTACION => new TipoDocumentoElectronico { Codigo = "02" };
        public static TipoDocumentoElectronico FACTURA_CONTINGENCIA_FACTURADOR => new TipoDocumentoElectronico { Codigo = "03" };

        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static TipoDocumentoElectronico FACTURA_CONTINGENCIA_DIAN => new TipoDocumentoElectronico { Codigo = "04" };
        public static TipoDocumentoElectronico FACTURA_VENTA_TIPO_4 => new TipoDocumentoElectronico { Codigo = "04" };
        public static TipoDocumentoElectronico DOCUMENTO_EQUIVALENTE_POS => new TipoDocumentoElectronico { Codigo = "20" };
        public static TipoDocumentoElectronico DOCUMENTO_JUEGO_LOCALIZADO => new TipoDocumentoElectronico { Codigo = "30" };
        public static TipoDocumentoElectronico TIQUETE_TRANSPORTE_TERRESTRE => new TipoDocumentoElectronico { Codigo = "35" };
        public static TipoDocumentoElectronico DOCUMENTO_COBRO_PEAJE => new TipoDocumentoElectronico { Codigo = "40" };
        public static TipoDocumentoElectronico EXTRACTO_DOCUMENTO_EQUIVALENTE => new TipoDocumentoElectronico { Codigo = "45" };
        public static TipoDocumentoElectronico TIQUETE_TRANSPORTE_AEREO => new TipoDocumentoElectronico { Codigo = "50" };
        public static TipoDocumentoElectronico OPERACION_BOLSA_VALORES => new TipoDocumentoElectronico { Codigo = "55" };
        public static TipoDocumentoElectronico DOCUMENTO_EQUIVALENTE_SPD => new TipoDocumentoElectronico { Codigo = "60" };
        public static TipoDocumentoElectronico NOTA_CREDITO => new TipoDocumentoElectronico { Codigo = "91" };
        public static TipoDocumentoElectronico NOTA_DEBITO => new TipoDocumentoElectronico { Codigo = "92" };
        public static TipoDocumentoElectronico NOTA_AJUSTE_DOCUMENTO_EQUIVALENTE => new TipoDocumentoElectronico { Codigo = "94" };
        public static TipoDocumentoElectronico NOMINA_INDIVIDUAL => new TipoDocumentoElectronico { Codigo = "102" };
        public static TipoDocumentoElectronico AJUSTE_NOMINA_INDIVIDUAL => new TipoDocumentoElectronico { Codigo = "103" };
        public static TipoDocumentoElectronico DOCUMENTO_SOPORTE => new TipoDocumentoElectronico { Codigo = "05" };
        public static TipoDocumentoElectronico NOTA_AJUSTE_DOCUMENTO_SOPORTE => new TipoDocumentoElectronico { Codigo = "95" };
    }


    public class PeriodoFacturacion
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public FormaGeneracionTransmision FormaGT { get; set; } = FormaGeneracionTransmision.GetFromCodigo("POR_OPERACION");

    }


    public class FormaGeneracionTransmision
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        private static readonly Dictionary<string, FormaGeneracionTransmision> _tiposMap = new Dictionary<string, FormaGeneracionTransmision>
        {
            { "POR_OPERACION", new FormaGeneracionTransmision { Codigo = "1", Descripcion = "Por Operación" } },
            { "ACUMULADO_SEMANAL", new FormaGeneracionTransmision { Codigo = "2", Descripcion = "Acumulado Semanal" } }
        };

        public static FormaGeneracionTransmision GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de <<FormaGeneracioTransmision>> no válido", nameof(codigo));
            }
        }

    }

    public class OrdenCompra
    {
        public string Numero { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class DetallesEntrega
    {
        public DateTime Fecha { get; set; }
        public DireccionFisica Direccion { get; set; }

        /// <summary>
        /// Información sobre la empresa de transporte.
        /// </summary>
        public Transportador Transportador { get; set; }
    }

    public class Transportador : Adquiriente
    {

    }

    public class UnidadMedida
    {
        public string Codigo { get; set; }

        public static UnidadMedida CADA => new UnidadMedida { Codigo = "EA" };
    }


    // *************************************************************************
    public enum AmbienteDestino { PRODUCCION = 1, PRUEBAS = 2 }


    public class RangoNumeracion
    {
        public long NumeroResolucion { get; set; }
        public DateTime FechaResolucion { get; set; }
        public string Prefijo { get; set; }
        public long Desde { get; set; }
        public long Hasta { get; set; }
        public DateTime VigenciaDesde { get; set; }
        public DateTime VigenciaHasta { get; set; }
    }

    public class ExtensionDian
    {
        public RangoNumeracion RangoNumeracion { get; set; }
        public Pais PaisOrigen { get; set; } = Pais.COLOMBIA;
        public string SoftwareProveedorNit { get; set; }
        public string SoftwareIdentificador { get; set; }
        public string SoftwarePin { get; set; }
    }

    public class AlgoritmoSeguridadUUID
    {
        public string Codigo { get; set; }

        public static AlgoritmoSeguridadUUID CUFE_SHA384 => new AlgoritmoSeguridadUUID { Codigo = "CUFE-SHA384" };
        public static AlgoritmoSeguridadUUID CUDE_SHA384 => new AlgoritmoSeguridadUUID { Codigo = "CUDE-SHA384" };
        public static AlgoritmoSeguridadUUID CUNE_SHA384 => new AlgoritmoSeguridadUUID { Codigo = "CUNE-SHA384" };
        public static AlgoritmoSeguridadUUID CUDS_SHA384 => new AlgoritmoSeguridadUUID { Codigo = "CUDS-SHA384" };
    }

    public enum TipoFactura { FACTURA_VENTA = 1, FACTURA_EXPORTACION = 2, FACTURA_CONTINGENCIA_FACTURADOR = 3, FACTURA_CONTINGENCIA_DIAN = 4 }

    //public class TipoFactura
    //{
    //    public string Codigo { get; set; }

    //    public static TipoFactura VENTA => new TipoFactura { Codigo = "01" };
    //    public static TipoFactura EXPORTACION => new TipoFactura { Codigo = "02" };
    //    public static TipoFactura CONTINGENCIA_FACTURADOR => new TipoFactura { Codigo = "03" };
    //    public static TipoFactura CONTINGENCIA_DIAN => new TipoFactura { Codigo = "04" };
    //}

    public class OperacionFactura
    {
        public string Codigo { get; set; }

        public static OperacionFactura ESTANDAR => new OperacionFactura { Codigo = "10" };
        public static OperacionFactura AIU => new OperacionFactura { Codigo = "09" };
        public static OperacionFactura MANDATOS => new OperacionFactura { Codigo = "11" };
        public static OperacionFactura TRANSPORTE => new OperacionFactura { Codigo = "12" };
        [Obsolete("Opción obsoleta desde la versión 1.9")]
        public static OperacionFactura CAMBIARIO => new OperacionFactura { Codigo = "13" };
        public static OperacionFactura NOTARIOS => new OperacionFactura { Codigo = "14" };
        public static OperacionFactura COMPRA_DIVISAS => new OperacionFactura { Codigo = "15" };
        public static OperacionFactura VENTA_DIVISAS => new OperacionFactura { Codigo = "16" };
        public static OperacionFactura SS_CUFE => new OperacionFactura { Codigo = "SS-CUFE" };
        public static OperacionFactura SS_SNUM => new OperacionFactura { Codigo = "SS-SNum" };
        public static OperacionFactura SS_RECAUDO => new OperacionFactura { Codigo = "SS-Recaudo" };
        public static OperacionFactura SS_REPORTE => new OperacionFactura { Codigo = "SS-Reporte" };
        public static OperacionFactura SS_SIN_APORTE => new OperacionFactura { Codigo = "SS-SinAporte" };
    }

    public class TipoDocumentoEquivalente
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Version { get; set; }

        public static TipoDocumentoEquivalente POS => new TipoDocumentoEquivalente
        {
            Codigo = "20",
            Nombre = "Documento equivalente electrónico del tiquete de máquina registradora con sistema P.O.S.",
            Version = "DIAN 2.1: Documento Equivalente POS"
        };
        public static TipoDocumentoEquivalente BOLETA_INGRESO_CINE => new TipoDocumentoEquivalente
        {
            Codigo = "25",
            Nombre = "Boleta de ingreso a cine",
            Version = "DIAN 2.1: Boleta de ingreso a cine"
        };
        public static TipoDocumentoEquivalente BOLETA_ESPECTACULO_PUBLICO => new TipoDocumentoEquivalente
        {
            Codigo = "27",
            Nombre = "Boleta de ingreso a espectáculos públicos",
            Version = "DIAN 2.1: Boleta de ingreso a espectáculos públicos"
        };
        public static TipoDocumentoEquivalente DOCUMENTO_JUEGO_LOCALIZADO => new TipoDocumentoEquivalente
        {
            Codigo = "30",
            Nombre = "Documento en juegos localizados - relación diaria de control de ventas",
            Version = "DIAN 2.1: Documento en juegos localizados - relación diaria de control de ventas"
        };
        public static TipoDocumentoEquivalente TIQUETE_TRANSPORTE_TERRESTRE => new TipoDocumentoEquivalente
        {
            Codigo = "35",
            Nombre = "Tiquete de transporte de pasajeros Terrestre",
            Version = "DIAN 2.1: Documento Equivalente Tiquete de Transporte Terrestre de Pasajeros"
        };
        public static TipoDocumentoEquivalente DOCUMENTO_COBRO_PEAJE => new TipoDocumentoEquivalente
        {
            Codigo = "40",
            Nombre = "Documento expedido para el cobro de peajes",
            Version = "DIAN 2.1: Documento Equivalente para el Cobro de Peajes"
        };
        public static TipoDocumentoEquivalente EXTRACTO_SOCIEDAD_FINANCIERA_FONDOS => new TipoDocumentoEquivalente
        {
            Codigo = "45",
            Nombre = "Extracto Expedido por Sociedades Financieras y Fondos",
            Version = "DIAN 2.1: Extracto"
        };
        public static TipoDocumentoEquivalente BILLETE_TRANSPORTE_AEREO => new TipoDocumentoEquivalente
        {
            Codigo = "50",
            Nombre = "Tiquete de Billete de Transporte Aéreo de Pasajeros",
            Version = "DIAN 2.1: Documento equivalente – Tiquete o boleto aéreo de pasajeros"
        };
        public static TipoDocumentoEquivalente BOLSA_VALORES_AGROPECUARIA_OTROS => new TipoDocumentoEquivalente
        {
            Codigo = "55",
            Nombre = "Documento de Operación de Bolsa de Valores, Agropecuaria y de Otros Comodities",
            Version = "DIAN 2.1: Comprobante de liquidación de operaciones expedido por Bolsa de Valores y operaciones de la bolsa agropecuaria y de otros commodities"
        };
        public static TipoDocumentoEquivalente SERVICIO_PUBLICO_DOMICILIARIO => new TipoDocumentoEquivalente
        {
            Codigo = "60",
            Nombre = "Documento Equivalente SPD"
        };
        public static TipoDocumentoEquivalente CONTINGENCIA_EMISOR => new TipoDocumentoEquivalente
        {
            Codigo = "07",
            Nombre = "Contingencia Por parte del emisor"
        };
        public static TipoDocumentoEquivalente CONTINGENCIA_DIAN => new TipoDocumentoEquivalente
        {
            Codigo = "08",
            Nombre = "Contingencia por parte DIAN"
        };
    }

    public class TipoAjusteDocumentoEquivalente
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static TipoAjusteDocumentoEquivalente AJUSTE_DEBITO => new TipoAjusteDocumentoEquivalente
        {
            Codigo = "93",
            Nombre = "Nota de Ajuste de tipo debito al Documento Equivalente"
        };
        public static TipoAjusteDocumentoEquivalente AJUSTE_CREDITO => new TipoAjusteDocumentoEquivalente
        {
            Codigo = "94",
            Nombre = "Nota de Ajuste de tipo crédito al Documento Equivalente"
        };
    }

    public class OperacionDocumentoEquivalente
    {
        public string Codigo { get; set; }
        public string Version { get; set; }

        public static OperacionDocumentoEquivalente GENERAL => new OperacionDocumentoEquivalente { Codigo = "10" };
        public static OperacionDocumentoEquivalente SPD_FACTURACION_NORMAL => new OperacionDocumentoEquivalente { Codigo = "601", Version = "DIAN 2.1: Documento Equivalente SPD" };
        public static OperacionDocumentoEquivalente SPD_FACTURACION_EN_SITIO => new OperacionDocumentoEquivalente { Codigo = "602", Version = "DIAN 2.1: Documento Equivalente SPD – ON SITE" };
        public static OperacionDocumentoEquivalente JUEGO_LOCALIZADO_REPORTE_DIARIO => new OperacionDocumentoEquivalente { Codigo = "321" };
        public static OperacionDocumentoEquivalente JUEGO_LOCALIZADO_REPORTE_DIARIO_FINAL => new OperacionDocumentoEquivalente { Codigo = "322" };
        public static OperacionDocumentoEquivalente ESTANDAR_ESPECTACULO_PUBLICO_ARTES_ESCENICAS => new OperacionDocumentoEquivalente { Codigo = "371" };
        public static OperacionDocumentoEquivalente MANDATO_ESPECTACULO_PUBLICO_ARTES_ESCENICAS => new OperacionDocumentoEquivalente { Codigo = "372" };
        public static OperacionDocumentoEquivalente ESTANDAR_OTROS_ESPECTACULOS_PUBLICOS => new OperacionDocumentoEquivalente { Codigo = "373" };
        public static OperacionDocumentoEquivalente MANDATO_OTROS_ESPECTACULOS_PUBLICOS => new OperacionDocumentoEquivalente { Codigo = "374" };
        public static OperacionDocumentoEquivalente COMPROBANTE_LIQUIDACION_OPERACION_BOLSA_VALORES => new OperacionDocumentoEquivalente { Codigo = "551" };
        public static OperacionDocumentoEquivalente DOCUMENTO_OPERACION_BOLSA_AGROPECUARIA_Y_OTROS => new OperacionDocumentoEquivalente { Codigo = "552" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_POS => new OperacionDocumentoEquivalente { Codigo = "20" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_BOLETA_CINE => new OperacionDocumentoEquivalente { Codigo = "25" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_BOLETA_ESPECTACULO_PUBLICO => new OperacionDocumentoEquivalente { Codigo = "27" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_DOCUMENTO_JUEGO_LOCALIZADO => new OperacionDocumentoEquivalente { Codigo = "32" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_TIQUETE_TRANSPORTE_TERRESTRE => new OperacionDocumentoEquivalente { Codigo = "35" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_DOCUMENTO_COBRO_PEAJE => new OperacionDocumentoEquivalente { Codigo = "40" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_EXTRACTO_SOCIEDAD_FINANCIERA_FONDOS => new OperacionDocumentoEquivalente { Codigo = "45" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_BILLETE_TRANSPORTE_AEREO => new OperacionDocumentoEquivalente { Codigo = "50" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_BOLSA_VALORES_AGROPECUARIA_OTROS => new OperacionDocumentoEquivalente { Codigo = "55" };
        public static OperacionDocumentoEquivalente CONTINGENCIA_SERVICIO_PUBLICO_DOMICILIARIO => new OperacionDocumentoEquivalente { Codigo = "60" };
        public static OperacionDocumentoEquivalente SS_CUDE => new OperacionDocumentoEquivalente { Codigo = "SS-CUDE" };
    }

    public class OperacionNotaCredito
    {
        public string Codigo { get; set; }

        /// <summary>
        /// Nota Crédito que referencia una factura electrónica.
        /// </summary>
        public static OperacionNotaCredito CON_REFERENCIA_FE => new OperacionNotaCredito { Codigo = "20" };
        /// <summary>
        /// Nota Crédito sin referencia a facturas*.
        /// </summary>
        public static OperacionNotaCredito SIN_REFERENCIA_FACTURA => new OperacionNotaCredito { Codigo = "22" };
        /// <summary>
        /// Nota Crédito para facturación electrónica V1 (Decreto 2242).
        /// </summary>
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static OperacionNotaCredito PARA_FE_V1 => new OperacionNotaCredito { Codigo = "23" };
        /// <summary>
        /// Nota de Ajuste para Factura Electrónica de Venta Aceptada
        /// </summary>
        public static OperacionNotaCredito PARA_FE_ACEPTADA => new OperacionNotaCredito { Codigo = "24" };
    }

    public class OperacionNotaAjusteDocumentoEquivalente
    {
        public string Codigo { get; set; }

        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_POS => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "20" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_BOLETA_CINE => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "25" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_BOLETA_ESPECTACULO_PUBLICO => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "27" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_DOCUMENTO_JUEGO_LOCALIZADO => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "321" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_TIQUETE_TRANSPORTE_TERRESTRE => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "35" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_DOCUMENTO_COBRO_PEAJE => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "40" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_EXTRACTO_SOCIEDAD_FINANCIERA_FONDOS => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "45" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_BILLETE_TRANSPORTE_AEREO => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "50" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_BOLSA_VALORES_AGROPECUARIA_OTROS => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "55" };
        public static OperacionNotaAjusteDocumentoEquivalente REFERENCIA_SERVICIO_PUBLICO_DOMICILIARIO => new OperacionNotaAjusteDocumentoEquivalente { Codigo = "60" };
    }

    public class OperacionNotaDebito
    {
        public string Codigo { get; set; }

        /// <summary>
        /// Nota Débito que referencia una factura electrónica.
        /// </summary>
        public static OperacionNotaDebito CON_REFERENCIA_FE => new OperacionNotaDebito { Codigo = "30" };
        /// <summary>
        /// Nota Débito sin referencia a facturas*.
        /// </summary>
        public static OperacionNotaDebito SIN_REFERENCIA_FACTURA => new OperacionNotaDebito { Codigo = "32" };
        /// <summary>
        /// Nota Débito para facturación electrónica V1 (Decreto 2242).
        /// </summary>
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static OperacionNotaDebito PARA_FE_V1 => new OperacionNotaDebito { Codigo = "33" };
    }


    //public enum TipoImpuesto
    //{
    //    [Description("Impuesto de Valor Agregado")]
    //    IVA = 1,

    //    [Description("Impuesto al Consumo")]
    //    IC = 2,

    //    [Description("Impuesto de Industria, Comercio y Aviso")]
    //    ICA = 3,

    //    [Description("Impuesto Nacional al Consumo")]
    //    INC = 4,

    //    [Description("Retención sobre el IVA")]
    //    ReteIVA = 5,

    //    [Description("Retención sobre Renta")]
    //    ReteRenta = 6,

    //    [Description("Retención sobre el ICA")]
    //    ReteICA = 7,

    //    [Description("Impuesto al Consumo Departamental Porcentual")]
    //    IC_Porcentual = 8,

    //    [Description("Cuota de Fomento Hortifrutícula")]
    //    FtoHorticultura = 20,

    //    [Description("Impuesto de Timbre")]
    //    Timbre = 21,

    //    [Description("Impuesto Nacional al Consumo de Bolsas Plásticas")]
    //    INCBP = 22,

    //    [Description("Impuesto Nacional del Carbono")]
    //    INCarbono = 23,

    //    [Description("Impuesto Nacional a los Combustibles")]
    //    INCombustibles = 24,

    //    [Description("Sobretasa a los combustibles")]
    //    SobretasaCombustibles = 25,

    //    [Description("Contribución minoristas (Combustibles)")]
    //    Sordicom = 26,

    //    [Description("Impuesto al Consumo de Datos")]
    //    IC_DATOS = 30,

    //    [Description("No responsable de tributo (ZY)")]
    //    NO_CAUSA = 98,

    //    [Description("Otros tributos, tasas, contribuciones y similares (ZZ)")]
    //    OTROS = 99

    //}



    public class TipoImpuesto
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        // PASAR AL MIDDLE
        private static readonly Dictionary<string, TipoImpuesto> _tiposMap = new Dictionary<string, TipoImpuesto>
        {
            {"01", new TipoImpuesto {Codigo = "01", Nombre = "IVA", Descripcion = "Impuesto sobre la Ventas"} },
            {"02", new TipoImpuesto {Codigo = "02", Nombre = "IC", Descripcion = "Impuesto al Consumo Departamental Nominal" } },
            {"03", new TipoImpuesto {Codigo = "03", Nombre = "ICA", Descripcion = "Impuesto de Industria, Comercio y Aviso" } },
            {"04", new TipoImpuesto {Codigo = "04", Nombre = "INC", Descripcion = "Impuesto Nacional al Consumo" } },
            {"05", new TipoImpuesto {Codigo = "05", Nombre = "ReteIVA", Descripcion = "Retención sobre el IVA" } },
            {"06", new TipoImpuesto {Codigo = "06", Nombre = "ReteRenta", Descripcion = "Retención sobre Renta" } },
            {"07", new TipoImpuesto {Codigo = "07", Nombre = "ReteICA", Descripcion = "Retención sobre el ICA" } },
            {"08", new TipoImpuesto {Codigo = "08", Nombre = "IC Porcentual", Descripcion = "Impuesto al Consumo Departamental Porcentual" } },
            {"20", new TipoImpuesto {Codigo = "20", Nombre = "FtoHorticultura", Descripcion = "Cuota de Fomento Hortifrutícula" } },
            {"21", new TipoImpuesto {Codigo = "21", Nombre = "Timbre", Descripcion = "Impuesto de Timbre" } },
            {"22", new TipoImpuesto {Codigo = "22", Nombre = "INC Bolsas", Descripcion = "Impuesto Nacional al Consumo de Bolsa Plástica" } },
            {"23", new TipoImpuesto {Codigo = "23", Nombre = "INCarbono", Descripcion = "Impuesto Nacional del Carbono" } },
            {"24", new TipoImpuesto {Codigo = "24", Nombre = "INCombustibles", Descripcion = "Impuesto Nacional a los Combustibles" } },
            {"25", new TipoImpuesto {Codigo = "25", Nombre = "Sobretasa Combustibles", Descripcion = "Sobretasa a los combustibles" } },
            {"26", new TipoImpuesto {Codigo = "26", Nombre = "Sordicom", Descripcion = "Contribución minoristas (Combustibles)" } },
            {"30", new TipoImpuesto {Codigo = "30", Nombre = "IC Datos", Descripcion = "Impuesto al Consumo de Datos" } },
            {"32", new TipoImpuesto {Codigo = "32", Nombre = "ICL", Descripcion = "Impuesto al Consumo de Licores" } },
            {"33", new TipoImpuesto {Codigo = "33", Nombre = "INPP", Descripcion = "Impuesto nacional productos plásticos" } },
            {"34", new TipoImpuesto {Codigo = "34", Nombre = "IBUA", Descripcion = "Impuesto a las bebidas ultraprocesadas azucaradas****" } },
            {"35", new TipoImpuesto {Codigo = "35", Nombre = "ICUI", Descripcion = "Impuesto a los productos comestibles ultraprocesados industrialmente y/o con alto contenido de azúcares añadidos, sodio o grasas saturadas" } },
            {"36", new TipoImpuesto {Codigo = "36", Nombre = "ADV", Descripcion = "AD VALOREM" } },
            {"ZY", new TipoImpuesto {Codigo = "ZY", Nombre = "No causa", Descripcion = "No responsable de tributo" } },
            {"ZZ", new TipoImpuesto {Codigo = "ZZ", Nombre = "Otros", Descripcion = "Otros tributos, tasas, contribuciones y similares" } }
        };

        // PASAR AL MIDDLE
        public static TipoImpuesto GetFromCodigo(string codigo)
        {
            try
            {
                return _tiposMap[codigo];
            }
            catch (Exception)
            {
                throw new ArgumentException("Código de TipoImpuesto no válido", nameof(codigo));
            }
        }

        // PASAR AL MIDDLE
        public static TipoImpuesto GetFromNombre(string nombre)
        {
            try
            {
                return _tiposMap.Values.FirstOrDefault(ti => ti.Nombre.Equals(nombre));
            }
            catch (Exception)
            {
                throw new ArgumentException("Código de TipoImpuesto no válido", nameof(nombre));
            }
        }

    }

    public enum TipoPersona { JURIDICA = 1, NATURAL = 2 }

    //public class TipoPersona
    //{
    //    public string Codigo { get; set; }

    //    public bool EsPersonaJuridica => Codigo == PERSONA_JURIDICA.Codigo;

    //    public bool EsPersonaNatural => Codigo == PERSONA_NATURAL.Codigo;

    //    /// <summary>
    //    /// Persona Jurídica y asimiladas.
    //    /// </summary>
    //    public static TipoPersona PERSONA_JURIDICA => new TipoPersona { Codigo = "1" };

    //    /// <summary>
    //    /// Persona Natural y asimiladas.
    //    /// </summary>
    //    public static TipoPersona PERSONA_NATURAL => new TipoPersona { Codigo = "2" };
    //}


    // Corresponde a cbc:TaxLevelCode 
    // Valor Prede
    public class RegimenFiscal
    {

        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public bool Obsoleto { get; set; } = false;

        public static RegimenFiscal Default => new RegimenFiscal() { Codigo = "R-99-PN", Descripcion = "No aplica – Otros *" };

        // public List<RegimenFiscal> list { get { return new List<RegimenFiscal>(_tiposMap.Values); } }

        public static readonly Dictionary<string, RegimenFiscal> _tiposMap = new Dictionary<string, RegimenFiscal>
        {
            { "04", new RegimenFiscal { Codigo = "04", Descripcion = "REGIMEN_SIMPLE",  Obsoleto = true } },
            { "05", new RegimenFiscal { Codigo = "05", Descripcion = "REGIMEN_ORDINARIO", Obsoleto = true } },
            { "48", new RegimenFiscal { Codigo = "48", Descripcion = "RESPONSABLE_DE_IVA", Obsoleto = true } },
            { "49", new RegimenFiscal { Codigo = "49",Descripcion = "NO_RESPONSABLE_DE_IVA", Obsoleto = true } },
            { "O-99", new RegimenFiscal { Codigo = "No aplica", Descripcion = "NO_APLICA", Obsoleto = true} },

            { "O-13", new RegimenFiscal { Codigo = "O-13", Descripcion = "GRAN_CONTRIBUYENTE" } },
            { "O-15", new RegimenFiscal { Codigo = "O-15", Descripcion = "AUTORRETENEDOR" } },
            { "O-23", new RegimenFiscal { Codigo = "O-23", Descripcion = "AGENTE_RETENCION_IVA" } },
            { "O-47", new RegimenFiscal { Codigo = "O-47", Descripcion = "REGIMEN_SIMPLE_TRIBUTACION"} },
            { "R-99-PN", new RegimenFiscal { Codigo = "R-99-PN", Descripcion = "No aplica – Otros *"} }

        };

        public static RegimenFiscal GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de RegimenFiscal no válido", nameof(codigo));
            }
        }
    }

    public class ConceptoNotaCredito
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        /// <summary>
        /// Devolución parcial de los bienes y/o no aceptación parcial del servicio
        /// </summary>
        public static ConceptoNotaCredito DEVOLUCION_PARCIAL => new ConceptoNotaCredito
        {
            Codigo = "1",
            Descripcion = "Devolución parcial de los bienes y/o no aceptación parcial del servicio"
        };

        /// <summary>
        /// Anulación de factura electrónica
        /// </summary>
        public static ConceptoNotaCredito ANULACION_TOTAL => new ConceptoNotaCredito
        {
            Codigo = "2",
            Descripcion = "Anulación de factura electrónica"
        };

        /// <summary>
        /// Rebaja o descuento parcial o total
        /// </summary>
        public static ConceptoNotaCredito REBAJA_O_DESCUENTO => new ConceptoNotaCredito
        {
            Codigo = "3",
            Descripcion = "Rebaja o descuento parcial o total"
        };

        /// <summary>
        /// Ajuste de precio
        /// </summary>
        public static ConceptoNotaCredito AJUSTE_PRECIO => new ConceptoNotaCredito
        {
            Codigo = "4",
            Descripcion = "Ajuste de precio"
        };

        /// <summary>
        /// Otros
        /// </summary>
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static ConceptoNotaCredito OTROS => new ConceptoNotaCredito
        {
            Codigo = "5",
            Descripcion = "Otros"
        };

        /// <summary>
        /// Descuento comercial por pronto pago
        /// </summary>
        public static ConceptoNotaCredito DESCUENTO_COMERCIAL_PRONTO_PAGO => new ConceptoNotaCredito
        {
            Codigo = "5",
            Descripcion = "Descuento comercial por pronto pago"
        };

        /// <summary>
        /// Descuento comercial por volumen de ventas
        /// </summary>
        public static ConceptoNotaCredito DESCUENTO_COMERCIAL_VOLUMEN_VENTAS => new ConceptoNotaCredito
        {
            Codigo = "6",
            Descripcion = "Descuento comercial por volumen de ventas"
        };
    }

    public class ConceptoNotaDebito
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        /// <summary>
        /// Intereses
        /// </summary>
        public static ConceptoNotaDebito INTERESES => new ConceptoNotaDebito
        {
            Codigo = "1",
            Descripcion = "Intereses"
        };

        /// <summary>
        /// Gastos por cobrar
        /// </summary>
        public static ConceptoNotaDebito GASTOS_COBRAR => new ConceptoNotaDebito
        {
            Codigo = "2",
            Descripcion = "Gastos por cobrar"
        };

        /// <summary>
        /// Cambio de valor
        /// </summary>
        public static ConceptoNotaDebito CAMBIO_VALOR => new ConceptoNotaDebito
        {
            Codigo = "3",
            Descripcion = "Cambio del valor"
        };

        /// <summary>
        /// Otros
        /// </summary>
        public static ConceptoNotaDebito OTROS => new ConceptoNotaDebito
        {
            Codigo = "4",
            Descripcion = "Otros"
        };
    }

    public class ConceptoNotaAjusteDocumentoEquivalente
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        /// <summary>
        /// Devolución de parte de los bienes; no aceptación de partes del servicio
        /// </summary>
        public static ConceptoNotaAjusteDocumentoEquivalente DEVOLUCION_PARCIAL => new ConceptoNotaAjusteDocumentoEquivalente
        {
            Codigo = "1",
            Descripcion = "Devolución parcial de los bienes y/o no aceptación parcial del servicio"
        };

        /// <summary>
        /// Anulación de factura electrónica
        /// </summary>
        public static ConceptoNotaAjusteDocumentoEquivalente ANULACION_TOTAL => new ConceptoNotaAjusteDocumentoEquivalente
        {
            Codigo = "2",
            Descripcion = "Anulación del documento equivalente electrónico del tiquete de máquina registradora con sistema P.O.S"
        };

        /// <summary>
        /// Rebaja o descuento parcial o total
        /// </summary>
        public static ConceptoNotaAjusteDocumentoEquivalente REBAJA_O_DESCUENTO => new ConceptoNotaAjusteDocumentoEquivalente
        {
            Codigo = "3",
            Descripcion = "Rebaja o descuento parcial o total"
        };

        /// <summary>
        /// Ajuste de precio
        /// </summary>
        public static ConceptoNotaAjusteDocumentoEquivalente AJUSTE_PRECIO => new ConceptoNotaAjusteDocumentoEquivalente
        {
            Codigo = "4",
            Descripcion = "Ajuste de precio"
        };

        /// <summary>
        /// Otros
        /// </summary>
        public static ConceptoNotaAjusteDocumentoEquivalente OTROS => new ConceptoNotaAjusteDocumentoEquivalente
        {
            Codigo = "5",
            Descripcion = "Otros"
        };
    }

    public class Moneda
    {
        public string Codigo { get; set; }

        public static Moneda PESO_COLOMBIANO => new Moneda { Codigo = "COP" };
        public static Moneda DOLAR_ESTADOUNIDENSE => new Moneda { Codigo = "USD" };
        public static Moneda EURO => new Moneda { Codigo = "EUR" };
    }

    public class Pais
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static Pais COLOMBIA => new Pais { Codigo = "CO", Nombre = "Colombia" };
    }

    public class Departamento
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static Departamento AMAZONAS => new Departamento { Codigo = "91", Nombre = "Amazonas" };
        public static Departamento ANTIOQUIA => new Departamento { Codigo = "05", Nombre = "Antioquia" };
        public static Departamento ARAUCA => new Departamento { Codigo = "81", Nombre = "Arauca" };
        public static Departamento ATLANTICO => new Departamento { Codigo = "08", Nombre = "Atlántico" };
        public static Departamento BOGOTA => new Departamento { Codigo = "11", Nombre = "Bogotá" };
        public static Departamento BOLIVAR => new Departamento { Codigo = "13", Nombre = "Bolívar" };
        public static Departamento BOYACA => new Departamento { Codigo = "15", Nombre = "Boyacá" };
        public static Departamento CALDAS => new Departamento { Codigo = "17", Nombre = "Caldas" };
        public static Departamento CAQUETA => new Departamento { Codigo = "18", Nombre = "Caquetá" };
        public static Departamento CASANARE => new Departamento { Codigo = "85", Nombre = "Casanare" };
        public static Departamento CAUCA => new Departamento { Codigo = "19", Nombre = "Cauca" };
        public static Departamento CESAR => new Departamento { Codigo = "20", Nombre = "Cesar" };
        public static Departamento CHOCO => new Departamento { Codigo = "27", Nombre = "Chocó" };
        public static Departamento CORDOBA => new Departamento { Codigo = "23", Nombre = "Córdoba" };
        public static Departamento CUNDINAMARCA => new Departamento { Codigo = "25", Nombre = "Cundinamarca" };
        public static Departamento GUAINIA => new Departamento { Codigo = "94", Nombre = "Guainía" };
        public static Departamento GUAVIARE => new Departamento { Codigo = "95", Nombre = "Guaviare" };
        public static Departamento HUILA => new Departamento { Codigo = "41", Nombre = "Huila" };
        public static Departamento LA_GUAJIRA => new Departamento { Codigo = "44", Nombre = "La Guajira" };
        public static Departamento MAGDALENA => new Departamento { Codigo = "47", Nombre = "Magdalena" };
        public static Departamento META => new Departamento { Codigo = "50", Nombre = "Meta" };
        public static Departamento NARIÑO => new Departamento { Codigo = "52", Nombre = "Nariño" };
        public static Departamento NORTE_DE_SANTANDER => new Departamento { Codigo = "54", Nombre = "Norte de Santander" };
        public static Departamento PUTUMAYO => new Departamento { Codigo = "86", Nombre = "Putumayo" };
        public static Departamento QUINDIO => new Departamento { Codigo = "63", Nombre = "Quindío" };
        public static Departamento RISARALDA => new Departamento { Codigo = "66", Nombre = "Risaralda" };
        public static Departamento SAN_ANDRES_Y_PROVIDENCIA => new Departamento { Codigo = "88", Nombre = "San Andrés y Providencia" };
        public static Departamento SANTANDER => new Departamento { Codigo = "68", Nombre = "Santander" };
        public static Departamento SUCRE => new Departamento { Codigo = "70", Nombre = "Sucre" };
        public static Departamento TOLIMA => new Departamento { Codigo = "73", Nombre = "Tolima" };
        public static Departamento VALLE_DEL_CAUCA => new Departamento { Codigo = "76", Nombre = "Valle del Cauca" };
        public static Departamento VAUPES => new Departamento { Codigo = "97", Nombre = "Vaupés" };
        public static Departamento VICHADA => new Departamento { Codigo = "99", Nombre = "Vichada" };
    }

    public class Municipio
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static Municipio ANTIOQUIA_MEDELLIN => new Municipio { Codigo = "05001", Nombre = "Medellín" };
        public static Municipio ANTIOQUIA_ABEJORRAL => new Municipio { Codigo = "05002", Nombre = "Abejorral" };
        public static Municipio ANTIOQUIA_ABRIAQUI => new Municipio { Codigo = "05004", Nombre = "Abriaquí" };
        public static Municipio ANTIOQUIA_ALEJANDRIA => new Municipio { Codigo = "05021", Nombre = "Alejandría" };
        public static Municipio ANTIOQUIA_AMAGA => new Municipio { Codigo = "05030", Nombre = "Amagá" };
        public static Municipio ANTIOQUIA_AMALFI => new Municipio { Codigo = "05031", Nombre = "Amalfi" };
        public static Municipio ANTIOQUIA_ANDES => new Municipio { Codigo = "05034", Nombre = "Andes" };
        public static Municipio ANTIOQUIA_ANGELOPOLIS => new Municipio { Codigo = "05036", Nombre = "Angelópolis" };
        public static Municipio ANTIOQUIA_ANGOSTURA => new Municipio { Codigo = "05038", Nombre = "Angostura" };
        public static Municipio ANTIOQUIA_ANORI => new Municipio { Codigo = "05040", Nombre = "Anorí" };
        public static Municipio ANTIOQUIA_SANTA_FE_DE_ANTIOQUIA => new Municipio { Codigo = "05042", Nombre = "Santa Fé De Antioquia" };
        public static Municipio ANTIOQUIA_ANZA => new Municipio { Codigo = "05044", Nombre = "Anzá" };
        public static Municipio ANTIOQUIA_APARTADO => new Municipio { Codigo = "05045", Nombre = "Apartadó" };
        public static Municipio ANTIOQUIA_ARBOLETES => new Municipio { Codigo = "05051", Nombre = "Arboletes" };
        public static Municipio ANTIOQUIA_ARGELIA => new Municipio { Codigo = "05055", Nombre = "Argelia" };
        public static Municipio ANTIOQUIA_ARMENIA => new Municipio { Codigo = "05059", Nombre = "Armenia" };
        public static Municipio ANTIOQUIA_BARBOSA => new Municipio { Codigo = "05079", Nombre = "Barbosa" };
        public static Municipio ANTIOQUIA_BELMIRA => new Municipio { Codigo = "05086", Nombre = "Belmira" };
        public static Municipio ANTIOQUIA_BELLO => new Municipio { Codigo = "05088", Nombre = "Bello" };
        public static Municipio ANTIOQUIA_BETANIA => new Municipio { Codigo = "05091", Nombre = "Betania" };
        public static Municipio ANTIOQUIA_BETULIA => new Municipio { Codigo = "05093", Nombre = "Betulia" };
        public static Municipio ANTIOQUIA_CIUDAD_BOLIVAR => new Municipio { Codigo = "05101", Nombre = "Ciudad Bolívar" };
        public static Municipio ANTIOQUIA_BRICEÑO => new Municipio { Codigo = "05107", Nombre = "Briceño" };
        public static Municipio ANTIOQUIA_BURITICA => new Municipio { Codigo = "05113", Nombre = "Buriticá" };
        public static Municipio ANTIOQUIA_CACERES => new Municipio { Codigo = "05120", Nombre = "Cáceres" };
        public static Municipio ANTIOQUIA_CAICEDO => new Municipio { Codigo = "05125", Nombre = "Caicedo" };
        public static Municipio ANTIOQUIA_CALDAS => new Municipio { Codigo = "05129", Nombre = "Caldas" };
        public static Municipio ANTIOQUIA_CAMPAMENTO => new Municipio { Codigo = "05134", Nombre = "Campamento" };
        public static Municipio ANTIOQUIA_CAÑASGORDAS => new Municipio { Codigo = "05138", Nombre = "Cañasgordas" };
        public static Municipio ANTIOQUIA_CARACOLI => new Municipio { Codigo = "05142", Nombre = "Caracolí" };
        public static Municipio ANTIOQUIA_CARAMANTA => new Municipio { Codigo = "05145", Nombre = "Caramanta" };
        public static Municipio ANTIOQUIA_CAREPA => new Municipio { Codigo = "05147", Nombre = "Carepa" };
        public static Municipio ANTIOQUIA_EL_CARMEN_DE_VIBORAL => new Municipio { Codigo = "05148", Nombre = "El Carmen De Viboral" };
        public static Municipio ANTIOQUIA_CAROLINA => new Municipio { Codigo = "05150", Nombre = "Carolina" };
        public static Municipio ANTIOQUIA_CAUCASIA => new Municipio { Codigo = "05154", Nombre = "Caucasia" };
        public static Municipio ANTIOQUIA_CHIGORODO => new Municipio { Codigo = "05172", Nombre = "Chigorodó" };
        public static Municipio ANTIOQUIA_CISNEROS => new Municipio { Codigo = "05190", Nombre = "Cisneros" };
        public static Municipio ANTIOQUIA_COCORNA => new Municipio { Codigo = "05197", Nombre = "Cocorná" };
        public static Municipio ANTIOQUIA_CONCEPCION => new Municipio { Codigo = "05206", Nombre = "Concepción" };
        public static Municipio ANTIOQUIA_CONCORDIA => new Municipio { Codigo = "05209", Nombre = "Concordia" };
        public static Municipio ANTIOQUIA_COPACABANA => new Municipio { Codigo = "05212", Nombre = "Copacabana" };
        public static Municipio ANTIOQUIA_DABEIBA => new Municipio { Codigo = "05234", Nombre = "Dabeiba" };
        public static Municipio ANTIOQUIA_DONMATIAS => new Municipio { Codigo = "05237", Nombre = "Donmatías" };
        public static Municipio ANTIOQUIA_EBEJICO => new Municipio { Codigo = "05240", Nombre = "Ebéjico" };
        public static Municipio ANTIOQUIA_EL_BAGRE => new Municipio { Codigo = "05250", Nombre = "El Bagre" };
        public static Municipio ANTIOQUIA_ENTRERRIOS => new Municipio { Codigo = "05264", Nombre = "Entrerríos" };
        public static Municipio ANTIOQUIA_ENVIGADO => new Municipio { Codigo = "05266", Nombre = "Envigado" };
        public static Municipio ANTIOQUIA_FREDONIA => new Municipio { Codigo = "05282", Nombre = "Fredonia" };
        public static Municipio ANTIOQUIA_FRONTINO => new Municipio { Codigo = "05284", Nombre = "Frontino" };
        public static Municipio ANTIOQUIA_GIRALDO => new Municipio { Codigo = "05306", Nombre = "Giraldo" };
        public static Municipio ANTIOQUIA_GIRARDOTA => new Municipio { Codigo = "05308", Nombre = "Girardota" };
        public static Municipio ANTIOQUIA_GOMEZ_PLATA => new Municipio { Codigo = "05310", Nombre = "Gómez Plata" };
        public static Municipio ANTIOQUIA_GRANADA => new Municipio { Codigo = "05313", Nombre = "Granada" };
        public static Municipio ANTIOQUIA_GUADALUPE => new Municipio { Codigo = "05315", Nombre = "Guadalupe" };
        public static Municipio ANTIOQUIA_GUARNE => new Municipio { Codigo = "05318", Nombre = "Guarne" };
        public static Municipio ANTIOQUIA_GUATAPE => new Municipio { Codigo = "05321", Nombre = "Guatapé" };
        public static Municipio ANTIOQUIA_HELICONIA => new Municipio { Codigo = "05347", Nombre = "Heliconia" };
        public static Municipio ANTIOQUIA_HISPANIA => new Municipio { Codigo = "05353", Nombre = "Hispania" };
        public static Municipio ANTIOQUIA_ITAGÜI => new Municipio { Codigo = "05360", Nombre = "Itagüí" };
        public static Municipio ANTIOQUIA_ITUANGO => new Municipio { Codigo = "05361", Nombre = "Ituango" };
        public static Municipio ANTIOQUIA_JARDIN => new Municipio { Codigo = "05364", Nombre = "Jardín" };
        public static Municipio ANTIOQUIA_JERICO => new Municipio { Codigo = "05368", Nombre = "Jericó" };
        public static Municipio ANTIOQUIA_LA_CEJA => new Municipio { Codigo = "05376", Nombre = "La Ceja" };
        public static Municipio ANTIOQUIA_LA_ESTRELLA => new Municipio { Codigo = "05380", Nombre = "La Estrella" };
        public static Municipio ANTIOQUIA_LA_PINTADA => new Municipio { Codigo = "05390", Nombre = "La Pintada" };
        public static Municipio ANTIOQUIA_LA_UNION => new Municipio { Codigo = "05400", Nombre = "La Unión" };
        public static Municipio ANTIOQUIA_LIBORINA => new Municipio { Codigo = "05411", Nombre = "Liborina" };
        public static Municipio ANTIOQUIA_MACEO => new Municipio { Codigo = "05425", Nombre = "Maceo" };
        public static Municipio ANTIOQUIA_MARINILLA => new Municipio { Codigo = "05440", Nombre = "Marinilla" };
        public static Municipio ANTIOQUIA_MONTEBELLO => new Municipio { Codigo = "05467", Nombre = "Montebello" };
        public static Municipio ANTIOQUIA_MURINDO => new Municipio { Codigo = "05475", Nombre = "Murindó" };
        public static Municipio ANTIOQUIA_MUTATA => new Municipio { Codigo = "05480", Nombre = "Mutatá" };
        public static Municipio ANTIOQUIA_NARIÑO => new Municipio { Codigo = "05483", Nombre = "Nariño" };
        public static Municipio ANTIOQUIA_NECOCLI => new Municipio { Codigo = "05490", Nombre = "Necoclí" };
        public static Municipio ANTIOQUIA_NECHI => new Municipio { Codigo = "05495", Nombre = "Nechí" };
        public static Municipio ANTIOQUIA_OLAYA => new Municipio { Codigo = "05501", Nombre = "Olaya" };
        public static Municipio ANTIOQUIA_PEÑOL => new Municipio { Codigo = "05541", Nombre = "Peñol" };
        public static Municipio ANTIOQUIA_PEQUE => new Municipio { Codigo = "05543", Nombre = "Peque" };
        public static Municipio ANTIOQUIA_PUEBLORRICO => new Municipio { Codigo = "05576", Nombre = "Pueblorrico" };
        public static Municipio ANTIOQUIA_PUERTO_BERRIO => new Municipio { Codigo = "05579", Nombre = "Puerto Berrío" };
        public static Municipio ANTIOQUIA_PUERTO_NARE => new Municipio { Codigo = "05585", Nombre = "Puerto Nare" };
        public static Municipio ANTIOQUIA_PUERTO_TRIUNFO => new Municipio { Codigo = "05591", Nombre = "Puerto Triunfo" };
        public static Municipio ANTIOQUIA_REMEDIOS => new Municipio { Codigo = "05604", Nombre = "Remedios" };
        public static Municipio ANTIOQUIA_RETIRO => new Municipio { Codigo = "05607", Nombre = "Retiro" };
        public static Municipio ANTIOQUIA_RIONEGRO => new Municipio { Codigo = "05615", Nombre = "Rionegro" };
        public static Municipio ANTIOQUIA_SABANALARGA => new Municipio { Codigo = "05628", Nombre = "Sabanalarga" };
        public static Municipio ANTIOQUIA_SABANETA => new Municipio { Codigo = "05631", Nombre = "Sabaneta" };
        public static Municipio ANTIOQUIA_SALGAR => new Municipio { Codigo = "05642", Nombre = "Salgar" };
        public static Municipio ANTIOQUIA_SAN_ANDRES_DE_CUERQUIA => new Municipio { Codigo = "05647", Nombre = "San Andrés De Cuerquía" };
        public static Municipio ANTIOQUIA_SAN_CARLOS => new Municipio { Codigo = "05649", Nombre = "San Carlos" };
        public static Municipio ANTIOQUIA_SAN_FRANCISCO => new Municipio { Codigo = "05652", Nombre = "San Francisco" };
        public static Municipio ANTIOQUIA_SAN_JERONIMO => new Municipio { Codigo = "05656", Nombre = "San Jerónimo" };
        public static Municipio ANTIOQUIA_SAN_JOSE_DE_LA_MONTAÑA => new Municipio { Codigo = "05658", Nombre = "San José De La Montaña" };
        public static Municipio ANTIOQUIA_SAN_JUAN_DE_URABA => new Municipio { Codigo = "05659", Nombre = "San Juan De Urabá" };
        public static Municipio ANTIOQUIA_SAN_LUIS => new Municipio { Codigo = "05660", Nombre = "San Luis" };
        public static Municipio ANTIOQUIA_SAN_PEDRO_DE_LOS_MILAGROS => new Municipio { Codigo = "05664", Nombre = "San Pedro De Los Milagros" };
        public static Municipio ANTIOQUIA_SAN_PEDRO_DE_URABA => new Municipio { Codigo = "05665", Nombre = "San Pedro De Urabá" };
        public static Municipio ANTIOQUIA_SAN_RAFAEL => new Municipio { Codigo = "05667", Nombre = "San Rafael" };
        public static Municipio ANTIOQUIA_SAN_ROQUE => new Municipio { Codigo = "05670", Nombre = "San Roque" };
        public static Municipio ANTIOQUIA_SAN_VICENTE_FERRER => new Municipio { Codigo = "05674", Nombre = "San Vicente Ferrer" };
        public static Municipio ANTIOQUIA_SANTA_BARBARA => new Municipio { Codigo = "05679", Nombre = "Santa Bárbara" };
        public static Municipio ANTIOQUIA_SANTA_ROSA_DE_OSOS => new Municipio { Codigo = "05686", Nombre = "Santa Rosa De Osos" };
        public static Municipio ANTIOQUIA_SANTO_DOMINGO => new Municipio { Codigo = "05690", Nombre = "Santo Domingo" };
        public static Municipio ANTIOQUIA_EL_SANTUARIO => new Municipio { Codigo = "05697", Nombre = "El Santuario" };
        public static Municipio ANTIOQUIA_SEGOVIA => new Municipio { Codigo = "05736", Nombre = "Segovia" };
        public static Municipio ANTIOQUIA_SONSON => new Municipio { Codigo = "05756", Nombre = "Sonsón" };
        public static Municipio ANTIOQUIA_SOPETRAN => new Municipio { Codigo = "05761", Nombre = "Sopetrán" };
        public static Municipio ANTIOQUIA_TAMESIS => new Municipio { Codigo = "05789", Nombre = "Támesis" };
        public static Municipio ANTIOQUIA_TARAZA => new Municipio { Codigo = "05790", Nombre = "Tarazá" };
        public static Municipio ANTIOQUIA_TARSO => new Municipio { Codigo = "05792", Nombre = "Tarso" };
        public static Municipio ANTIOQUIA_TITIRIBI => new Municipio { Codigo = "05809", Nombre = "Titiribí" };
        public static Municipio ANTIOQUIA_TOLEDO => new Municipio { Codigo = "05819", Nombre = "Toledo" };
        public static Municipio ANTIOQUIA_TURBO => new Municipio { Codigo = "05837", Nombre = "Turbo" };
        public static Municipio ANTIOQUIA_URAMITA => new Municipio { Codigo = "05842", Nombre = "Uramita" };
        public static Municipio ANTIOQUIA_URRAO => new Municipio { Codigo = "05847", Nombre = "Urrao" };
        public static Municipio ANTIOQUIA_VALDIVIA => new Municipio { Codigo = "05854", Nombre = "Valdivia" };
        public static Municipio ANTIOQUIA_VALPARAISO => new Municipio { Codigo = "05856", Nombre = "Valparaíso" };
        public static Municipio ANTIOQUIA_VEGACHI => new Municipio { Codigo = "05858", Nombre = "Vegachí" };
        public static Municipio ANTIOQUIA_VENECIA => new Municipio { Codigo = "05861", Nombre = "Venecia" };
        public static Municipio ANTIOQUIA_VIGIA_DEL_FUERTE => new Municipio { Codigo = "05873", Nombre = "Vigía Del Fuerte" };
        public static Municipio ANTIOQUIA_YALI => new Municipio { Codigo = "05885", Nombre = "Yalí" };
        public static Municipio ANTIOQUIA_YARUMAL => new Municipio { Codigo = "05887", Nombre = "Yarumal" };
        public static Municipio ANTIOQUIA_YOLOMBO => new Municipio { Codigo = "05890", Nombre = "Yolombó" };
        public static Municipio ANTIOQUIA_YONDO => new Municipio { Codigo = "05893", Nombre = "Yondó" };
        public static Municipio ANTIOQUIA_ZARAGOZA => new Municipio { Codigo = "05895", Nombre = "Zaragoza" };
        public static Municipio ATLANTICO_BARRANQUILLA => new Municipio { Codigo = "08001", Nombre = "Barranquilla" };
        public static Municipio ATLANTICO_BARANOA => new Municipio { Codigo = "08078", Nombre = "Baranoa" };
        public static Municipio ATLANTICO_CAMPO_DE_LA_CRUZ => new Municipio { Codigo = "08137", Nombre = "Campo De La Cruz" };
        public static Municipio ATLANTICO_CANDELARIA => new Municipio { Codigo = "08141", Nombre = "Candelaria" };
        public static Municipio ATLANTICO_GALAPA => new Municipio { Codigo = "08296", Nombre = "Galapa" };
        public static Municipio ATLANTICO_JUAN_DE_ACOSTA => new Municipio { Codigo = "08372", Nombre = "Juan De Acosta" };
        public static Municipio ATLANTICO_LURUACO => new Municipio { Codigo = "08421", Nombre = "Luruaco" };
        public static Municipio ATLANTICO_MALAMBO => new Municipio { Codigo = "08433", Nombre = "Malambo" };
        public static Municipio ATLANTICO_MANATI => new Municipio { Codigo = "08436", Nombre = "Manatí" };
        public static Municipio ATLANTICO_PALMAR_DE_VARELA => new Municipio { Codigo = "08520", Nombre = "Palmar De Varela" };
        public static Municipio ATLANTICO_PIOJO => new Municipio { Codigo = "08549", Nombre = "Piojó" };
        public static Municipio ATLANTICO_POLONUEVO => new Municipio { Codigo = "08558", Nombre = "Polonuevo" };
        public static Municipio ATLANTICO_PONEDERA => new Municipio { Codigo = "08560", Nombre = "Ponedera" };
        public static Municipio ATLANTICO_PUERTO_COLOMBIA => new Municipio { Codigo = "08573", Nombre = "Puerto Colombia" };
        public static Municipio ATLANTICO_REPELON => new Municipio { Codigo = "08606", Nombre = "Repelón" };
        public static Municipio ATLANTICO_SABANAGRANDE => new Municipio { Codigo = "08634", Nombre = "Sabanagrande" };
        public static Municipio ATLANTICO_SABANALARGA => new Municipio { Codigo = "08638", Nombre = "Sabanalarga" };
        public static Municipio ATLANTICO_SANTA_LUCIA => new Municipio { Codigo = "08675", Nombre = "Santa Lucía" };
        public static Municipio ATLANTICO_SANTO_TOMAS => new Municipio { Codigo = "08685", Nombre = "Santo Tomás" };
        public static Municipio ATLANTICO_SOLEDAD => new Municipio { Codigo = "08758", Nombre = "Soledad" };
        public static Municipio ATLANTICO_SUAN => new Municipio { Codigo = "08770", Nombre = "Suan" };
        public static Municipio ATLANTICO_TUBARA => new Municipio { Codigo = "08832", Nombre = "Tubará" };
        public static Municipio ATLANTICO_USIACURI => new Municipio { Codigo = "08849", Nombre = "Usiacurí" };
        public static Municipio BOGOTA_BOGOTA_DC => new Municipio { Codigo = "11001", Nombre = "Bogotá, D.C." };
        public static Municipio BOLIVAR_CARTAGENA_DE_INDIAS => new Municipio { Codigo = "13001", Nombre = "Cartagena De Indias" };
        public static Municipio BOLIVAR_ACHI => new Municipio { Codigo = "13006", Nombre = "Achí" };
        public static Municipio BOLIVAR_ALTOS_DEL_ROSARIO => new Municipio { Codigo = "13030", Nombre = "Altos Del Rosario" };
        public static Municipio BOLIVAR_ARENAL => new Municipio { Codigo = "13042", Nombre = "Arenal" };
        public static Municipio BOLIVAR_ARJONA => new Municipio { Codigo = "13052", Nombre = "Arjona" };
        public static Municipio BOLIVAR_ARROYOHONDO => new Municipio { Codigo = "13062", Nombre = "Arroyohondo" };
        public static Municipio BOLIVAR_BARRANCO_DE_LOBA => new Municipio { Codigo = "13074", Nombre = "Barranco De Loba" };
        public static Municipio BOLIVAR_CALAMAR => new Municipio { Codigo = "13140", Nombre = "Calamar" };
        public static Municipio BOLIVAR_CANTAGALLO => new Municipio { Codigo = "13160", Nombre = "Cantagallo" };
        public static Municipio BOLIVAR_CICUCO => new Municipio { Codigo = "13188", Nombre = "Cicuco" };
        public static Municipio BOLIVAR_CORDOBA => new Municipio { Codigo = "13212", Nombre = "Córdoba" };
        public static Municipio BOLIVAR_CLEMENCIA => new Municipio { Codigo = "13222", Nombre = "Clemencia" };
        public static Municipio BOLIVAR_EL_CARMEN_DE_BOLIVAR => new Municipio { Codigo = "13244", Nombre = "El Carmen De Bolívar" };
        public static Municipio BOLIVAR_EL_GUAMO => new Municipio { Codigo = "13248", Nombre = "El Guamo" };
        public static Municipio BOLIVAR_EL_PEÑON => new Municipio { Codigo = "13268", Nombre = "El Peñón" };
        public static Municipio BOLIVAR_HATILLO_DE_LOBA => new Municipio { Codigo = "13300", Nombre = "Hatillo De Loba" };
        public static Municipio BOLIVAR_MAGANGUE => new Municipio { Codigo = "13430", Nombre = "Magangué" };
        public static Municipio BOLIVAR_MAHATES => new Municipio { Codigo = "13433", Nombre = "Mahates" };
        public static Municipio BOLIVAR_MARGARITA => new Municipio { Codigo = "13440", Nombre = "Margarita" };
        public static Municipio BOLIVAR_MARIA_LA_BAJA => new Municipio { Codigo = "13442", Nombre = "María La Baja" };
        public static Municipio BOLIVAR_MONTECRISTO => new Municipio { Codigo = "13458", Nombre = "Montecristo" };
        public static Municipio BOLIVAR_MOMPOS => new Municipio { Codigo = "13468", Nombre = "Mompós" };
        public static Municipio BOLIVAR_MORALES => new Municipio { Codigo = "13473", Nombre = "Morales" };
        public static Municipio BOLIVAR_NOROSI => new Municipio { Codigo = "13490", Nombre = "Norosí" };
        public static Municipio BOLIVAR_PINILLOS => new Municipio { Codigo = "13549", Nombre = "Pinillos" };
        public static Municipio BOLIVAR_REGIDOR => new Municipio { Codigo = "13580", Nombre = "Regidor" };
        public static Municipio BOLIVAR_RIO_VIEJO => new Municipio { Codigo = "13600", Nombre = "Río Viejo" };
        public static Municipio BOLIVAR_SAN_CRISTOBAL => new Municipio { Codigo = "13620", Nombre = "San Cristóbal" };
        public static Municipio BOLIVAR_SAN_ESTANISLAO => new Municipio { Codigo = "13647", Nombre = "San Estanislao" };
        public static Municipio BOLIVAR_SAN_FERNANDO => new Municipio { Codigo = "13650", Nombre = "San Fernando" };
        public static Municipio BOLIVAR_SAN_JACINTO => new Municipio { Codigo = "13654", Nombre = "San Jacinto" };
        public static Municipio BOLIVAR_SAN_JACINTO_DEL_CAUCA => new Municipio { Codigo = "13655", Nombre = "San Jacinto Del Cauca" };
        public static Municipio BOLIVAR_SAN_JUAN_NEPOMUCENO => new Municipio { Codigo = "13657", Nombre = "San Juan Nepomuceno" };
        public static Municipio BOLIVAR_SAN_MARTIN_DE_LOBA => new Municipio { Codigo = "13667", Nombre = "San Martín De Loba" };
        public static Municipio BOLIVAR_SAN_PABLO => new Municipio { Codigo = "13670", Nombre = "San Pablo" };
        public static Municipio BOLIVAR_SANTA_CATALINA => new Municipio { Codigo = "13673", Nombre = "Santa Catalina" };
        public static Municipio BOLIVAR_SANTA_ROSA => new Municipio { Codigo = "13683", Nombre = "Santa Rosa" };
        public static Municipio BOLIVAR_SANTA_ROSA_DEL_SUR => new Municipio { Codigo = "13688", Nombre = "Santa Rosa Del Sur" };
        public static Municipio BOLIVAR_SIMITI => new Municipio { Codigo = "13744", Nombre = "Simití" };
        public static Municipio BOLIVAR_SOPLAVIENTO => new Municipio { Codigo = "13760", Nombre = "Soplaviento" };
        public static Municipio BOLIVAR_TALAIGUA_NUEVO => new Municipio { Codigo = "13780", Nombre = "Talaigua Nuevo" };
        public static Municipio BOLIVAR_TIQUISIO => new Municipio { Codigo = "13810", Nombre = "Tiquisio" };
        public static Municipio BOLIVAR_TURBACO => new Municipio { Codigo = "13836", Nombre = "Turbaco" };
        public static Municipio BOLIVAR_TURBANA => new Municipio { Codigo = "13838", Nombre = "Turbaná" };
        public static Municipio BOLIVAR_VILLANUEVA => new Municipio { Codigo = "13873", Nombre = "Villanueva" };
        public static Municipio BOLIVAR_ZAMBRANO => new Municipio { Codigo = "13894", Nombre = "Zambrano" };
        public static Municipio BOYACA_TUNJA => new Municipio { Codigo = "15001", Nombre = "Tunja" };
        public static Municipio BOYACA_ALMEIDA => new Municipio { Codigo = "15022", Nombre = "Almeida" };
        public static Municipio BOYACA_AQUITANIA => new Municipio { Codigo = "15047", Nombre = "Aquitania" };
        public static Municipio BOYACA_ARCABUCO => new Municipio { Codigo = "15051", Nombre = "Arcabuco" };
        public static Municipio BOYACA_BELEN => new Municipio { Codigo = "15087", Nombre = "Belén" };
        public static Municipio BOYACA_BERBEO => new Municipio { Codigo = "15090", Nombre = "Berbeo" };
        public static Municipio BOYACA_BETEITIVA => new Municipio { Codigo = "15092", Nombre = "Betéitiva" };
        public static Municipio BOYACA_BOAVITA => new Municipio { Codigo = "15097", Nombre = "Boavita" };
        public static Municipio BOYACA_BOYACA => new Municipio { Codigo = "15104", Nombre = "Boyacá" };
        public static Municipio BOYACA_BRICEÑO => new Municipio { Codigo = "15106", Nombre = "Briceño" };
        public static Municipio BOYACA_BUENAVISTA => new Municipio { Codigo = "15109", Nombre = "Buenavista" };
        public static Municipio BOYACA_BUSBANZA => new Municipio { Codigo = "15114", Nombre = "Busbanzá" };
        public static Municipio BOYACA_CALDAS => new Municipio { Codigo = "15131", Nombre = "Caldas" };
        public static Municipio BOYACA_CAMPOHERMOSO => new Municipio { Codigo = "15135", Nombre = "Campohermoso" };
        public static Municipio BOYACA_CERINZA => new Municipio { Codigo = "15162", Nombre = "Cerinza" };
        public static Municipio BOYACA_CHINAVITA => new Municipio { Codigo = "15172", Nombre = "Chinavita" };
        public static Municipio BOYACA_CHIQUINQUIRA => new Municipio { Codigo = "15176", Nombre = "Chiquinquirá" };
        public static Municipio BOYACA_CHISCAS => new Municipio { Codigo = "15180", Nombre = "Chiscas" };
        public static Municipio BOYACA_CHITA => new Municipio { Codigo = "15183", Nombre = "Chita" };
        public static Municipio BOYACA_CHITARAQUE => new Municipio { Codigo = "15185", Nombre = "Chitaraque" };
        public static Municipio BOYACA_CHIVATA => new Municipio { Codigo = "15187", Nombre = "Chivatá" };
        public static Municipio BOYACA_CIENEGA => new Municipio { Codigo = "15189", Nombre = "Ciénega" };
        public static Municipio BOYACA_COMBITA => new Municipio { Codigo = "15204", Nombre = "Cómbita" };
        public static Municipio BOYACA_COPER => new Municipio { Codigo = "15212", Nombre = "Coper" };
        public static Municipio BOYACA_CORRALES => new Municipio { Codigo = "15215", Nombre = "Corrales" };
        public static Municipio BOYACA_COVARACHIA => new Municipio { Codigo = "15218", Nombre = "Covarachía" };
        public static Municipio BOYACA_CUBARA => new Municipio { Codigo = "15223", Nombre = "Cubará" };
        public static Municipio BOYACA_CUCAITA => new Municipio { Codigo = "15224", Nombre = "Cucaita" };
        public static Municipio BOYACA_CUITIVA => new Municipio { Codigo = "15226", Nombre = "Cuítiva" };
        public static Municipio BOYACA_CHIQUIZA => new Municipio { Codigo = "15232", Nombre = "Chíquiza" };
        public static Municipio BOYACA_CHIVOR => new Municipio { Codigo = "15236", Nombre = "Chivor" };
        public static Municipio BOYACA_DUITAMA => new Municipio { Codigo = "15238", Nombre = "Duitama" };
        public static Municipio BOYACA_EL_COCUY => new Municipio { Codigo = "15244", Nombre = "El Cocuy" };
        public static Municipio BOYACA_EL_ESPINO => new Municipio { Codigo = "15248", Nombre = "El Espino" };
        public static Municipio BOYACA_FIRAVITOBA => new Municipio { Codigo = "15272", Nombre = "Firavitoba" };
        public static Municipio BOYACA_FLORESTA => new Municipio { Codigo = "15276", Nombre = "Floresta" };
        public static Municipio BOYACA_GACHANTIVA => new Municipio { Codigo = "15293", Nombre = "Gachantivá" };
        public static Municipio BOYACA_GAMEZA => new Municipio { Codigo = "15296", Nombre = "Gámeza" };
        public static Municipio BOYACA_GARAGOA => new Municipio { Codigo = "15299", Nombre = "Garagoa" };
        public static Municipio BOYACA_GUACAMAYAS => new Municipio { Codigo = "15317", Nombre = "Guacamayas" };
        public static Municipio BOYACA_GUATEQUE => new Municipio { Codigo = "15322", Nombre = "Guateque" };
        public static Municipio BOYACA_GUAYATA => new Municipio { Codigo = "15325", Nombre = "Guayatá" };
        public static Municipio BOYACA_GÜICAN_DE_LA_SIERRA => new Municipio { Codigo = "15332", Nombre = "Güicán De La Sierra" };
        public static Municipio BOYACA_IZA => new Municipio { Codigo = "15362", Nombre = "Iza" };
        public static Municipio BOYACA_JENESANO => new Municipio { Codigo = "15367", Nombre = "Jenesano" };
        public static Municipio BOYACA_JERICO => new Municipio { Codigo = "15368", Nombre = "Jericó" };
        public static Municipio BOYACA_LABRANZAGRANDE => new Municipio { Codigo = "15377", Nombre = "Labranzagrande" };
        public static Municipio BOYACA_LA_CAPILLA => new Municipio { Codigo = "15380", Nombre = "La Capilla" };
        public static Municipio BOYACA_LA_VICTORIA => new Municipio { Codigo = "15401", Nombre = "La Victoria" };
        public static Municipio BOYACA_LA_UVITA => new Municipio { Codigo = "15403", Nombre = "La Uvita" };
        public static Municipio BOYACA_VILLA_DE_LEYVA => new Municipio { Codigo = "15407", Nombre = "Villa De Leyva" };
        public static Municipio BOYACA_MACANAL => new Municipio { Codigo = "15425", Nombre = "Macanal" };
        public static Municipio BOYACA_MARIPI => new Municipio { Codigo = "15442", Nombre = "Maripí" };
        public static Municipio BOYACA_MIRAFLORES => new Municipio { Codigo = "15455", Nombre = "Miraflores" };
        public static Municipio BOYACA_MONGUA => new Municipio { Codigo = "15464", Nombre = "Mongua" };
        public static Municipio BOYACA_MONGUI => new Municipio { Codigo = "15466", Nombre = "Monguí" };
        public static Municipio BOYACA_MONIQUIRA => new Municipio { Codigo = "15469", Nombre = "Moniquirá" };
        public static Municipio BOYACA_MOTAVITA => new Municipio { Codigo = "15476", Nombre = "Motavita" };
        public static Municipio BOYACA_MUZO => new Municipio { Codigo = "15480", Nombre = "Muzo" };
        public static Municipio BOYACA_NOBSA => new Municipio { Codigo = "15491", Nombre = "Nobsa" };
        public static Municipio BOYACA_NUEVO_COLON => new Municipio { Codigo = "15494", Nombre = "Nuevo Colón" };
        public static Municipio BOYACA_OICATA => new Municipio { Codigo = "15500", Nombre = "Oicatá" };
        public static Municipio BOYACA_OTANCHE => new Municipio { Codigo = "15507", Nombre = "Otanche" };
        public static Municipio BOYACA_PACHAVITA => new Municipio { Codigo = "15511", Nombre = "Pachavita" };
        public static Municipio BOYACA_PAEZ => new Municipio { Codigo = "15514", Nombre = "Páez" };
        public static Municipio BOYACA_PAIPA => new Municipio { Codigo = "15516", Nombre = "Paipa" };
        public static Municipio BOYACA_PAJARITO => new Municipio { Codigo = "15518", Nombre = "Pajarito" };
        public static Municipio BOYACA_PANQUEBA => new Municipio { Codigo = "15522", Nombre = "Panqueba" };
        public static Municipio BOYACA_PAUNA => new Municipio { Codigo = "15531", Nombre = "Pauna" };
        public static Municipio BOYACA_PAYA => new Municipio { Codigo = "15533", Nombre = "Paya" };
        public static Municipio BOYACA_PAZ_DE_RIO => new Municipio { Codigo = "15537", Nombre = "Paz De Río" };
        public static Municipio BOYACA_PESCA => new Municipio { Codigo = "15542", Nombre = "Pesca" };
        public static Municipio BOYACA_PISBA => new Municipio { Codigo = "15550", Nombre = "Pisba" };
        public static Municipio BOYACA_PUERTO_BOYACA => new Municipio { Codigo = "15572", Nombre = "Puerto Boyacá" };
        public static Municipio BOYACA_QUIPAMA => new Municipio { Codigo = "15580", Nombre = "Quípama" };
        public static Municipio BOYACA_RAMIRIQUI => new Municipio { Codigo = "15599", Nombre = "Ramiriquí" };
        public static Municipio BOYACA_RAQUIRA => new Municipio { Codigo = "15600", Nombre = "Ráquira" };
        public static Municipio BOYACA_RONDON => new Municipio { Codigo = "15621", Nombre = "Rondón" };
        public static Municipio BOYACA_SABOYA => new Municipio { Codigo = "15632", Nombre = "Saboyá" };
        public static Municipio BOYACA_SACHICA => new Municipio { Codigo = "15638", Nombre = "Sáchica" };
        public static Municipio BOYACA_SAMACA => new Municipio { Codigo = "15646", Nombre = "Samacá" };
        public static Municipio BOYACA_SAN_EDUARDO => new Municipio { Codigo = "15660", Nombre = "San Eduardo" };
        public static Municipio BOYACA_SAN_JOSE_DE_PARE => new Municipio { Codigo = "15664", Nombre = "San José De Pare" };
        public static Municipio BOYACA_SAN_LUIS_DE_GACENO => new Municipio { Codigo = "15667", Nombre = "San Luis De Gaceno" };
        public static Municipio BOYACA_SAN_MATEO => new Municipio { Codigo = "15673", Nombre = "San Mateo" };
        public static Municipio BOYACA_SAN_MIGUEL_DE_SEMA => new Municipio { Codigo = "15676", Nombre = "San Miguel De Sema" };
        public static Municipio BOYACA_SAN_PABLO_DE_BORBUR => new Municipio { Codigo = "15681", Nombre = "San Pablo De Borbur" };
        public static Municipio BOYACA_SANTANA => new Municipio { Codigo = "15686", Nombre = "Santana" };
        public static Municipio BOYACA_SANTA_MARIA => new Municipio { Codigo = "15690", Nombre = "Santa María" };
        public static Municipio BOYACA_SANTA_ROSA_DE_VITERBO => new Municipio { Codigo = "15693", Nombre = "Santa Rosa De Viterbo" };
        public static Municipio BOYACA_SANTA_SOFIA => new Municipio { Codigo = "15696", Nombre = "Santa Sofía" };
        public static Municipio BOYACA_SATIVANORTE => new Municipio { Codigo = "15720", Nombre = "Sativanorte" };
        public static Municipio BOYACA_SATIVASUR => new Municipio { Codigo = "15723", Nombre = "Sativasur" };
        public static Municipio BOYACA_SIACHOQUE => new Municipio { Codigo = "15740", Nombre = "Siachoque" };
        public static Municipio BOYACA_SOATA => new Municipio { Codigo = "15753", Nombre = "Soatá" };
        public static Municipio BOYACA_SOCOTA => new Municipio { Codigo = "15755", Nombre = "Socotá" };
        public static Municipio BOYACA_SOCHA => new Municipio { Codigo = "15757", Nombre = "Socha" };
        public static Municipio BOYACA_SOGAMOSO => new Municipio { Codigo = "15759", Nombre = "Sogamoso" };
        public static Municipio BOYACA_SOMONDOCO => new Municipio { Codigo = "15761", Nombre = "Somondoco" };
        public static Municipio BOYACA_SORA => new Municipio { Codigo = "15762", Nombre = "Sora" };
        public static Municipio BOYACA_SOTAQUIRA => new Municipio { Codigo = "15763", Nombre = "Sotaquirá" };
        public static Municipio BOYACA_SORACA => new Municipio { Codigo = "15764", Nombre = "Soracá" };
        public static Municipio BOYACA_SUSACON => new Municipio { Codigo = "15774", Nombre = "Susacón" };
        public static Municipio BOYACA_SUTAMARCHAN => new Municipio { Codigo = "15776", Nombre = "Sutamarchán" };
        public static Municipio BOYACA_SUTATENZA => new Municipio { Codigo = "15778", Nombre = "Sutatenza" };
        public static Municipio BOYACA_TASCO => new Municipio { Codigo = "15790", Nombre = "Tasco" };
        public static Municipio BOYACA_TENZA => new Municipio { Codigo = "15798", Nombre = "Tenza" };
        public static Municipio BOYACA_TIBANA => new Municipio { Codigo = "15804", Nombre = "Tibaná" };
        public static Municipio BOYACA_TIBASOSA => new Municipio { Codigo = "15806", Nombre = "Tibasosa" };
        public static Municipio BOYACA_TINJACA => new Municipio { Codigo = "15808", Nombre = "Tinjacá" };
        public static Municipio BOYACA_TIPACOQUE => new Municipio { Codigo = "15810", Nombre = "Tipacoque" };
        public static Municipio BOYACA_TOCA => new Municipio { Codigo = "15814", Nombre = "Toca" };
        public static Municipio BOYACA_TOGÜI => new Municipio { Codigo = "15816", Nombre = "Togüí" };
        public static Municipio BOYACA_TOPAGA => new Municipio { Codigo = "15820", Nombre = "Tópaga" };
        public static Municipio BOYACA_TOTA => new Municipio { Codigo = "15822", Nombre = "Tota" };
        public static Municipio BOYACA_TUNUNGUA => new Municipio { Codigo = "15832", Nombre = "Tununguá" };
        public static Municipio BOYACA_TURMEQUE => new Municipio { Codigo = "15835", Nombre = "Turmequé" };
        public static Municipio BOYACA_TUTA => new Municipio { Codigo = "15837", Nombre = "Tuta" };
        public static Municipio BOYACA_TUTAZA => new Municipio { Codigo = "15839", Nombre = "Tutazá" };
        public static Municipio BOYACA_UMBITA => new Municipio { Codigo = "15842", Nombre = "Úmbita" };
        public static Municipio BOYACA_VENTAQUEMADA => new Municipio { Codigo = "15861", Nombre = "Ventaquemada" };
        public static Municipio BOYACA_VIRACACHA => new Municipio { Codigo = "15879", Nombre = "Viracachá" };
        public static Municipio BOYACA_ZETAQUIRA => new Municipio { Codigo = "15897", Nombre = "Zetaquira" };
        public static Municipio CALDAS_MANIZALES => new Municipio { Codigo = "17001", Nombre = "Manizales" };
        public static Municipio CALDAS_AGUADAS => new Municipio { Codigo = "17013", Nombre = "Aguadas" };
        public static Municipio CALDAS_ANSERMA => new Municipio { Codigo = "17042", Nombre = "Anserma" };
        public static Municipio CALDAS_ARANZAZU => new Municipio { Codigo = "17050", Nombre = "Aranzazu" };
        public static Municipio CALDAS_BELALCAZAR => new Municipio { Codigo = "17088", Nombre = "Belalcázar" };
        public static Municipio CALDAS_CHINCHINA => new Municipio { Codigo = "17174", Nombre = "Chinchiná" };
        public static Municipio CALDAS_FILADELFIA => new Municipio { Codigo = "17272", Nombre = "Filadelfia" };
        public static Municipio CALDAS_LA_DORADA => new Municipio { Codigo = "17380", Nombre = "La Dorada" };
        public static Municipio CALDAS_LA_MERCED => new Municipio { Codigo = "17388", Nombre = "La Merced" };
        public static Municipio CALDAS_MANZANARES => new Municipio { Codigo = "17433", Nombre = "Manzanares" };
        public static Municipio CALDAS_MARMATO => new Municipio { Codigo = "17442", Nombre = "Marmato" };
        public static Municipio CALDAS_MARQUETALIA => new Municipio { Codigo = "17444", Nombre = "Marquetalia" };
        public static Municipio CALDAS_MARULANDA => new Municipio { Codigo = "17446", Nombre = "Marulanda" };
        public static Municipio CALDAS_NEIRA => new Municipio { Codigo = "17486", Nombre = "Neira" };
        public static Municipio CALDAS_NORCASIA => new Municipio { Codigo = "17495", Nombre = "Norcasia" };
        public static Municipio CALDAS_PACORA => new Municipio { Codigo = "17513", Nombre = "Pácora" };
        public static Municipio CALDAS_PALESTINA => new Municipio { Codigo = "17524", Nombre = "Palestina" };
        public static Municipio CALDAS_PENSILVANIA => new Municipio { Codigo = "17541", Nombre = "Pensilvania" };
        public static Municipio CALDAS_RIOSUCIO => new Municipio { Codigo = "17614", Nombre = "Riosucio" };
        public static Municipio CALDAS_RISARALDA => new Municipio { Codigo = "17616", Nombre = "Risaralda" };
        public static Municipio CALDAS_SALAMINA => new Municipio { Codigo = "17653", Nombre = "Salamina" };
        public static Municipio CALDAS_SAMANA => new Municipio { Codigo = "17662", Nombre = "Samaná" };
        public static Municipio CALDAS_SAN_JOSE => new Municipio { Codigo = "17665", Nombre = "San José" };
        public static Municipio CALDAS_SUPIA => new Municipio { Codigo = "17777", Nombre = "Supía" };
        public static Municipio CALDAS_VICTORIA => new Municipio { Codigo = "17867", Nombre = "Victoria" };
        public static Municipio CALDAS_VILLAMARIA => new Municipio { Codigo = "17873", Nombre = "Villamaría" };
        public static Municipio CALDAS_VITERBO => new Municipio { Codigo = "17877", Nombre = "Viterbo" };
        public static Municipio CAQUETA_FLORENCIA => new Municipio { Codigo = "18001", Nombre = "Florencia" };
        public static Municipio CAQUETA_ALBANIA => new Municipio { Codigo = "18029", Nombre = "Albania" };
        public static Municipio CAQUETA_BELEN_DE_LOS_ANDAQUIES => new Municipio { Codigo = "18094", Nombre = "Belén De Los Andaquíes" };
        public static Municipio CAQUETA_CARTAGENA_DEL_CHAIRA => new Municipio { Codigo = "18150", Nombre = "Cartagena Del Chairá" };
        public static Municipio CAQUETA_CURILLO => new Municipio { Codigo = "18205", Nombre = "Curillo" };
        public static Municipio CAQUETA_EL_DONCELLO => new Municipio { Codigo = "18247", Nombre = "El Doncello" };
        public static Municipio CAQUETA_EL_PAUJIL => new Municipio { Codigo = "18256", Nombre = "El Paujíl" };
        public static Municipio CAQUETA_LA_MONTAÑITA => new Municipio { Codigo = "18410", Nombre = "La Montañita" };
        public static Municipio CAQUETA_MILAN => new Municipio { Codigo = "18460", Nombre = "Milán" };
        public static Municipio CAQUETA_MORELIA => new Municipio { Codigo = "18479", Nombre = "Morelia" };
        public static Municipio CAQUETA_PUERTO_RICO => new Municipio { Codigo = "18592", Nombre = "Puerto Rico" };
        public static Municipio CAQUETA_SAN_JOSE_DEL_FRAGUA => new Municipio { Codigo = "18610", Nombre = "San José Del Fragua" };
        public static Municipio CAQUETA_SAN_VICENTE_DEL_CAGUAN => new Municipio { Codigo = "18753", Nombre = "San Vicente Del Caguán" };
        public static Municipio CAQUETA_SOLANO => new Municipio { Codigo = "18756", Nombre = "Solano" };
        public static Municipio CAQUETA_SOLITA => new Municipio { Codigo = "18785", Nombre = "Solita" };
        public static Municipio CAQUETA_VALPARAISO => new Municipio { Codigo = "18860", Nombre = "Valparaíso" };
        public static Municipio CAUCA_POPAYAN => new Municipio { Codigo = "19001", Nombre = "Popayán" };
        public static Municipio CAUCA_ALMAGUER => new Municipio { Codigo = "19022", Nombre = "Almaguer" };
        public static Municipio CAUCA_ARGELIA => new Municipio { Codigo = "19050", Nombre = "Argelia" };
        public static Municipio CAUCA_BALBOA => new Municipio { Codigo = "19075", Nombre = "Balboa" };
        public static Municipio CAUCA_BOLIVAR => new Municipio { Codigo = "19100", Nombre = "Bolívar" };
        public static Municipio CAUCA_BUENOS_AIRES => new Municipio { Codigo = "19110", Nombre = "Buenos Aires" };
        public static Municipio CAUCA_CAJIBIO => new Municipio { Codigo = "19130", Nombre = "Cajibío" };
        public static Municipio CAUCA_CALDONO => new Municipio { Codigo = "19137", Nombre = "Caldono" };
        public static Municipio CAUCA_CALOTO => new Municipio { Codigo = "19142", Nombre = "Caloto" };
        public static Municipio CAUCA_CORINTO => new Municipio { Codigo = "19212", Nombre = "Corinto" };
        public static Municipio CAUCA_EL_TAMBO => new Municipio { Codigo = "19256", Nombre = "El Tambo" };
        public static Municipio CAUCA_FLORENCIA => new Municipio { Codigo = "19290", Nombre = "Florencia" };
        public static Municipio CAUCA_GUACHENE => new Municipio { Codigo = "19300", Nombre = "Guachené" };
        public static Municipio CAUCA_GUAPI => new Municipio { Codigo = "19318", Nombre = "Guapí" };
        public static Municipio CAUCA_INZA => new Municipio { Codigo = "19355", Nombre = "Inzá" };
        public static Municipio CAUCA_JAMBALO => new Municipio { Codigo = "19364", Nombre = "Jambaló" };
        public static Municipio CAUCA_LA_SIERRA => new Municipio { Codigo = "19392", Nombre = "La Sierra" };
        public static Municipio CAUCA_LA_VEGA => new Municipio { Codigo = "19397", Nombre = "La Vega" };
        public static Municipio CAUCA_LOPEZ_DE_MICAY => new Municipio { Codigo = "19418", Nombre = "López De Micay" };
        public static Municipio CAUCA_MERCADERES => new Municipio { Codigo = "19450", Nombre = "Mercaderes" };
        public static Municipio CAUCA_MIRANDA => new Municipio { Codigo = "19455", Nombre = "Miranda" };
        public static Municipio CAUCA_MORALES => new Municipio { Codigo = "19473", Nombre = "Morales" };
        public static Municipio CAUCA_PADILLA => new Municipio { Codigo = "19513", Nombre = "Padilla" };
        public static Municipio CAUCA_PAEZ => new Municipio { Codigo = "19517", Nombre = "Páez" };
        public static Municipio CAUCA_PATIA => new Municipio { Codigo = "19532", Nombre = "Patía" };
        public static Municipio CAUCA_PIAMONTE => new Municipio { Codigo = "19533", Nombre = "Piamonte" };
        public static Municipio CAUCA_PIENDAMO__TUNIA => new Municipio { Codigo = "19548", Nombre = "Piendamó – Tunía" };
        public static Municipio CAUCA_PUERTO_TEJADA => new Municipio { Codigo = "19573", Nombre = "Puerto Tejada" };
        public static Municipio CAUCA_PURACE => new Municipio { Codigo = "19585", Nombre = "Puracé" };
        public static Municipio CAUCA_ROSAS => new Municipio { Codigo = "19622", Nombre = "Rosas" };
        public static Municipio CAUCA_SAN_SEBASTIAN => new Municipio { Codigo = "19693", Nombre = "San Sebastián" };
        public static Municipio CAUCA_SANTANDER_DE_QUILICHAO => new Municipio { Codigo = "19698", Nombre = "Santander De Quilichao" };
        public static Municipio CAUCA_SANTA_ROSA => new Municipio { Codigo = "19701", Nombre = "Santa Rosa" };
        public static Municipio CAUCA_SILVIA => new Municipio { Codigo = "19743", Nombre = "Silvia" };
        public static Municipio CAUCA_SOTARA => new Municipio { Codigo = "19760", Nombre = "Sotara" };
        public static Municipio CAUCA_SUAREZ => new Municipio { Codigo = "19780", Nombre = "Suárez" };
        public static Municipio CAUCA_SUCRE => new Municipio { Codigo = "19785", Nombre = "Sucre" };
        public static Municipio CAUCA_TIMBIO => new Municipio { Codigo = "19807", Nombre = "Timbío" };
        public static Municipio CAUCA_TIMBIQUI => new Municipio { Codigo = "19809", Nombre = "Timbiquí" };
        public static Municipio CAUCA_TORIBIO => new Municipio { Codigo = "19821", Nombre = "Toribío" };
        public static Municipio CAUCA_TOTORO => new Municipio { Codigo = "19824", Nombre = "Totoró" };
        public static Municipio CAUCA_VILLA_RICA => new Municipio { Codigo = "19845", Nombre = "Villa Rica" };
        public static Municipio CESAR_VALLEDUPAR => new Municipio { Codigo = "20001", Nombre = "Valledupar" };
        public static Municipio CESAR_AGUACHICA => new Municipio { Codigo = "20011", Nombre = "Aguachica" };
        public static Municipio CESAR_AGUSTIN_CODAZZI => new Municipio { Codigo = "20013", Nombre = "Agustín Codazzi" };
        public static Municipio CESAR_ASTREA => new Municipio { Codigo = "20032", Nombre = "Astrea" };
        public static Municipio CESAR_BECERRIL => new Municipio { Codigo = "20045", Nombre = "Becerril" };
        public static Municipio CESAR_BOSCONIA => new Municipio { Codigo = "20060", Nombre = "Bosconia" };
        public static Municipio CESAR_CHIMICHAGUA => new Municipio { Codigo = "20175", Nombre = "Chimichagua" };
        public static Municipio CESAR_CHIRIGUANA => new Municipio { Codigo = "20178", Nombre = "Chiriguaná" };
        public static Municipio CESAR_CURUMANI => new Municipio { Codigo = "20228", Nombre = "Curumaní" };
        public static Municipio CESAR_EL_COPEY => new Municipio { Codigo = "20238", Nombre = "El Copey" };
        public static Municipio CESAR_EL_PASO => new Municipio { Codigo = "20250", Nombre = "El Paso" };
        public static Municipio CESAR_GAMARRA => new Municipio { Codigo = "20295", Nombre = "Gamarra" };
        public static Municipio CESAR_GONZALEZ => new Municipio { Codigo = "20310", Nombre = "González" };
        public static Municipio CESAR_LA_GLORIA => new Municipio { Codigo = "20383", Nombre = "La Gloria" };
        public static Municipio CESAR_LA_JAGUA_DE_IBIRICO => new Municipio { Codigo = "20400", Nombre = "La Jagua De Ibirico" };
        public static Municipio CESAR_MANAURE_BALCON_DEL_CESAR => new Municipio { Codigo = "20443", Nombre = "Manaure Balcón Del Cesar" };
        public static Municipio CESAR_PAILITAS => new Municipio { Codigo = "20517", Nombre = "Pailitas" };
        public static Municipio CESAR_PELAYA => new Municipio { Codigo = "20550", Nombre = "Pelaya" };
        public static Municipio CESAR_PUEBLO_BELLO => new Municipio { Codigo = "20570", Nombre = "Pueblo Bello" };
        public static Municipio CESAR_RIO_DE_ORO => new Municipio { Codigo = "20614", Nombre = "Río De Oro" };
        public static Municipio CESAR_LA_PAZ => new Municipio { Codigo = "20621", Nombre = "La Paz" };
        public static Municipio CESAR_SAN_ALBERTO => new Municipio { Codigo = "20710", Nombre = "San Alberto" };
        public static Municipio CESAR_SAN_DIEGO => new Municipio { Codigo = "20750", Nombre = "San Diego" };
        public static Municipio CESAR_SAN_MARTIN => new Municipio { Codigo = "20770", Nombre = "San Martín" };
        public static Municipio CESAR_TAMALAMEQUE => new Municipio { Codigo = "20787", Nombre = "Tamalameque" };
        public static Municipio CORDOBA_MONTERIA => new Municipio { Codigo = "23001", Nombre = "Montería" };
        public static Municipio CORDOBA_AYAPEL => new Municipio { Codigo = "23068", Nombre = "Ayapel" };
        public static Municipio CORDOBA_BUENAVISTA => new Municipio { Codigo = "23079", Nombre = "Buenavista" };
        public static Municipio CORDOBA_CANALETE => new Municipio { Codigo = "23090", Nombre = "Canalete" };
        public static Municipio CORDOBA_CERETE => new Municipio { Codigo = "23162", Nombre = "Cereté" };
        public static Municipio CORDOBA_CHIMA => new Municipio { Codigo = "23168", Nombre = "Chimá" };
        public static Municipio CORDOBA_CHINU => new Municipio { Codigo = "23182", Nombre = "Chinú" };
        public static Municipio CORDOBA_CIENAGA_DE_ORO => new Municipio { Codigo = "23189", Nombre = "Ciénaga De Oro" };
        public static Municipio CORDOBA_COTORRA => new Municipio { Codigo = "23300", Nombre = "Cotorra" };
        public static Municipio CORDOBA_LA_APARTADA => new Municipio { Codigo = "23350", Nombre = "La Apartada" };
        public static Municipio CORDOBA_LORICA => new Municipio { Codigo = "23417", Nombre = "Lorica" };
        public static Municipio CORDOBA_LOS_CORDOBAS => new Municipio { Codigo = "23419", Nombre = "Los Córdobas" };
        public static Municipio CORDOBA_MOMIL => new Municipio { Codigo = "23464", Nombre = "Momil" };
        public static Municipio CORDOBA_MONTELIBANO => new Municipio { Codigo = "23466", Nombre = "Montelíbano" };
        public static Municipio CORDOBA_MOÑITOS => new Municipio { Codigo = "23500", Nombre = "Moñitos" };
        public static Municipio CORDOBA_PLANETA_RICA => new Municipio { Codigo = "23555", Nombre = "Planeta Rica" };
        public static Municipio CORDOBA_PUEBLO_NUEVO => new Municipio { Codigo = "23570", Nombre = "Pueblo Nuevo" };
        public static Municipio CORDOBA_PUERTO_ESCONDIDO => new Municipio { Codigo = "23574", Nombre = "Puerto Escondido" };
        public static Municipio CORDOBA_PUERTO_LIBERTADOR => new Municipio { Codigo = "23580", Nombre = "Puerto Libertador" };
        public static Municipio CORDOBA_PURISIMA_DE_LA_CONCEPCION => new Municipio { Codigo = "23586", Nombre = "Purísima De La Concepción" };
        public static Municipio CORDOBA_SAHAGUN => new Municipio { Codigo = "23660", Nombre = "Sahagún" };
        public static Municipio CORDOBA_SAN_ANDRES_DE_SOTAVENTO => new Municipio { Codigo = "23670", Nombre = "San Andrés De Sotavento" };
        public static Municipio CORDOBA_SAN_ANTERO => new Municipio { Codigo = "23672", Nombre = "San Antero" };
        public static Municipio CORDOBA_SAN_BERNARDO_DEL_VIENTO => new Municipio { Codigo = "23675", Nombre = "San Bernardo Del Viento" };
        public static Municipio CORDOBA_SAN_CARLOS => new Municipio { Codigo = "23678", Nombre = "San Carlos" };
        public static Municipio CORDOBA_SAN_JOSE_DE_URE => new Municipio { Codigo = "23682", Nombre = "San José De Uré" };
        public static Municipio CORDOBA_SAN_PELAYO => new Municipio { Codigo = "23686", Nombre = "San Pelayo" };
        public static Municipio CORDOBA_TIERRALTA => new Municipio { Codigo = "23807", Nombre = "Tierralta" };
        public static Municipio CORDOBA_TUCHIN => new Municipio { Codigo = "23815", Nombre = "Tuchín" };
        public static Municipio CORDOBA_VALENCIA => new Municipio { Codigo = "23855", Nombre = "Valencia" };
        public static Municipio CUNDINAMARCA_AGUA_DE_DIOS => new Municipio { Codigo = "25001", Nombre = "Agua De Dios" };
        public static Municipio CUNDINAMARCA_ALBAN => new Municipio { Codigo = "25019", Nombre = "Albán" };
        public static Municipio CUNDINAMARCA_ANAPOIMA => new Municipio { Codigo = "25035", Nombre = "Anapoima" };
        public static Municipio CUNDINAMARCA_ANOLAIMA => new Municipio { Codigo = "25040", Nombre = "Anolaima" };
        public static Municipio CUNDINAMARCA_ARBELAEZ => new Municipio { Codigo = "25053", Nombre = "Arbeláez" };
        public static Municipio CUNDINAMARCA_BELTRAN => new Municipio { Codigo = "25086", Nombre = "Beltrán" };
        public static Municipio CUNDINAMARCA_BITUIMA => new Municipio { Codigo = "25095", Nombre = "Bituima" };
        public static Municipio CUNDINAMARCA_BOJACA => new Municipio { Codigo = "25099", Nombre = "Bojacá" };
        public static Municipio CUNDINAMARCA_CABRERA => new Municipio { Codigo = "25120", Nombre = "Cabrera" };
        public static Municipio CUNDINAMARCA_CACHIPAY => new Municipio { Codigo = "25123", Nombre = "Cachipay" };
        public static Municipio CUNDINAMARCA_CAJICA => new Municipio { Codigo = "25126", Nombre = "Cajicá" };
        public static Municipio CUNDINAMARCA_CAPARRAPI => new Municipio { Codigo = "25148", Nombre = "Caparrapí" };
        public static Municipio CUNDINAMARCA_CAQUEZA => new Municipio { Codigo = "25151", Nombre = "Cáqueza" };
        public static Municipio CUNDINAMARCA_CARMEN_DE_CARUPA => new Municipio { Codigo = "25154", Nombre = "Carmen De Carupa" };
        public static Municipio CUNDINAMARCA_CHAGUANI => new Municipio { Codigo = "25168", Nombre = "Chaguaní" };
        public static Municipio CUNDINAMARCA_CHIA => new Municipio { Codigo = "25175", Nombre = "Chía" };
        public static Municipio CUNDINAMARCA_CHIPAQUE => new Municipio { Codigo = "25178", Nombre = "Chipaque" };
        public static Municipio CUNDINAMARCA_CHOACHI => new Municipio { Codigo = "25181", Nombre = "Choachí" };
        public static Municipio CUNDINAMARCA_CHOCONTA => new Municipio { Codigo = "25183", Nombre = "Chocontá" };
        public static Municipio CUNDINAMARCA_COGUA => new Municipio { Codigo = "25200", Nombre = "Cogua" };
        public static Municipio CUNDINAMARCA_COTA => new Municipio { Codigo = "25214", Nombre = "Cota" };
        public static Municipio CUNDINAMARCA_CUCUNUBA => new Municipio { Codigo = "25224", Nombre = "Cucunubá" };
        public static Municipio CUNDINAMARCA_EL_COLEGIO => new Municipio { Codigo = "25245", Nombre = "El Colegio" };
        public static Municipio CUNDINAMARCA_EL_PEÑON => new Municipio { Codigo = "25258", Nombre = "El Peñón" };
        public static Municipio CUNDINAMARCA_EL_ROSAL => new Municipio { Codigo = "25260", Nombre = "El Rosal" };
        public static Municipio CUNDINAMARCA_FACATATIVA => new Municipio { Codigo = "25269", Nombre = "Facatativá" };
        public static Municipio CUNDINAMARCA_FOMEQUE => new Municipio { Codigo = "25279", Nombre = "Fómeque" };
        public static Municipio CUNDINAMARCA_FOSCA => new Municipio { Codigo = "25281", Nombre = "Fosca" };
        public static Municipio CUNDINAMARCA_FUNZA => new Municipio { Codigo = "25286", Nombre = "Funza" };
        public static Municipio CUNDINAMARCA_FUQUENE => new Municipio { Codigo = "25288", Nombre = "Fúquene" };
        public static Municipio CUNDINAMARCA_FUSAGASUGA => new Municipio { Codigo = "25290", Nombre = "Fusagasugá" };
        public static Municipio CUNDINAMARCA_GACHALA => new Municipio { Codigo = "25293", Nombre = "Gachalá" };
        public static Municipio CUNDINAMARCA_GACHANCIPA => new Municipio { Codigo = "25295", Nombre = "Gachancipá" };
        public static Municipio CUNDINAMARCA_GACHETA => new Municipio { Codigo = "25297", Nombre = "Gachetá" };
        public static Municipio CUNDINAMARCA_GAMA => new Municipio { Codigo = "25299", Nombre = "Gama" };
        public static Municipio CUNDINAMARCA_GIRARDOT => new Municipio { Codigo = "25307", Nombre = "Girardot" };
        public static Municipio CUNDINAMARCA_GRANADA => new Municipio { Codigo = "25312", Nombre = "Granada" };
        public static Municipio CUNDINAMARCA_GUACHETA => new Municipio { Codigo = "25317", Nombre = "Guachetá" };
        public static Municipio CUNDINAMARCA_GUADUAS => new Municipio { Codigo = "25320", Nombre = "Guaduas" };
        public static Municipio CUNDINAMARCA_GUASCA => new Municipio { Codigo = "25322", Nombre = "Guasca" };
        public static Municipio CUNDINAMARCA_GUATAQUI => new Municipio { Codigo = "25324", Nombre = "Guataquí" };
        public static Municipio CUNDINAMARCA_GUATAVITA => new Municipio { Codigo = "25326", Nombre = "Guatavita" };
        public static Municipio CUNDINAMARCA_GUAYABAL_DE_SIQUIMA => new Municipio { Codigo = "25328", Nombre = "Guayabal De Síquima" };
        public static Municipio CUNDINAMARCA_GUAYABETAL => new Municipio { Codigo = "25335", Nombre = "Guayabetal" };
        public static Municipio CUNDINAMARCA_GUTIERREZ => new Municipio { Codigo = "25339", Nombre = "Gutiérrez" };
        public static Municipio CUNDINAMARCA_JERUSALEN => new Municipio { Codigo = "25368", Nombre = "Jerusalén" };
        public static Municipio CUNDINAMARCA_JUNIN => new Municipio { Codigo = "25372", Nombre = "Junín" };
        public static Municipio CUNDINAMARCA_LA_CALERA => new Municipio { Codigo = "25377", Nombre = "La Calera" };
        public static Municipio CUNDINAMARCA_LA_MESA => new Municipio { Codigo = "25386", Nombre = "La Mesa" };
        public static Municipio CUNDINAMARCA_LA_PALMA => new Municipio { Codigo = "25394", Nombre = "La Palma" };
        public static Municipio CUNDINAMARCA_LA_PEÑA => new Municipio { Codigo = "25398", Nombre = "La Peña" };
        public static Municipio CUNDINAMARCA_LA_VEGA => new Municipio { Codigo = "25402", Nombre = "La Vega" };
        public static Municipio CUNDINAMARCA_LENGUAZAQUE => new Municipio { Codigo = "25407", Nombre = "Lenguazaque" };
        public static Municipio CUNDINAMARCA_MACHETA => new Municipio { Codigo = "25426", Nombre = "Machetá" };
        public static Municipio CUNDINAMARCA_MADRID => new Municipio { Codigo = "25430", Nombre = "Madrid" };
        public static Municipio CUNDINAMARCA_MANTA => new Municipio { Codigo = "25436", Nombre = "Manta" };
        public static Municipio CUNDINAMARCA_MEDINA => new Municipio { Codigo = "25438", Nombre = "Medina" };
        public static Municipio CUNDINAMARCA_MOSQUERA => new Municipio { Codigo = "25473", Nombre = "Mosquera" };
        public static Municipio CUNDINAMARCA_NARIÑO => new Municipio { Codigo = "25483", Nombre = "Nariño" };
        public static Municipio CUNDINAMARCA_NEMOCON => new Municipio { Codigo = "25486", Nombre = "Nemocón" };
        public static Municipio CUNDINAMARCA_NILO => new Municipio { Codigo = "25488", Nombre = "Nilo" };
        public static Municipio CUNDINAMARCA_NIMAIMA => new Municipio { Codigo = "25489", Nombre = "Nimaima" };
        public static Municipio CUNDINAMARCA_NOCAIMA => new Municipio { Codigo = "25491", Nombre = "Nocaima" };
        public static Municipio CUNDINAMARCA_VENECIA => new Municipio { Codigo = "25506", Nombre = "Venecia" };
        public static Municipio CUNDINAMARCA_PACHO => new Municipio { Codigo = "25513", Nombre = "Pacho" };
        public static Municipio CUNDINAMARCA_PAIME => new Municipio { Codigo = "25518", Nombre = "Paime" };
        public static Municipio CUNDINAMARCA_PANDI => new Municipio { Codigo = "25524", Nombre = "Pandi" };
        public static Municipio CUNDINAMARCA_PARATEBUENO => new Municipio { Codigo = "25530", Nombre = "Paratebueno" };
        public static Municipio CUNDINAMARCA_PASCA => new Municipio { Codigo = "25535", Nombre = "Pasca" };
        public static Municipio CUNDINAMARCA_PUERTO_SALGAR => new Municipio { Codigo = "25572", Nombre = "Puerto Salgar" };
        public static Municipio CUNDINAMARCA_PULI => new Municipio { Codigo = "25580", Nombre = "Pulí" };
        public static Municipio CUNDINAMARCA_QUEBRADANEGRA => new Municipio { Codigo = "25592", Nombre = "Quebradanegra" };
        public static Municipio CUNDINAMARCA_QUETAME => new Municipio { Codigo = "25594", Nombre = "Quetame" };
        public static Municipio CUNDINAMARCA_QUIPILE => new Municipio { Codigo = "25596", Nombre = "Quipile" };
        public static Municipio CUNDINAMARCA_APULO => new Municipio { Codigo = "25599", Nombre = "Apulo" };
        public static Municipio CUNDINAMARCA_RICAURTE => new Municipio { Codigo = "25612", Nombre = "Ricaurte" };
        public static Municipio CUNDINAMARCA_SAN_ANTONIO_DEL_TEQUENDAMA => new Municipio { Codigo = "25645", Nombre = "San Antonio Del Tequendama" };
        public static Municipio CUNDINAMARCA_SAN_BERNARDO => new Municipio { Codigo = "25649", Nombre = "San Bernardo" };
        public static Municipio CUNDINAMARCA_SAN_CAYETANO => new Municipio { Codigo = "25653", Nombre = "San Cayetano" };
        public static Municipio CUNDINAMARCA_SAN_FRANCISCO => new Municipio { Codigo = "25658", Nombre = "San Francisco" };
        public static Municipio CUNDINAMARCA_SAN_JUAN_DE_RIOSECO => new Municipio { Codigo = "25662", Nombre = "San Juan De Rioseco" };
        public static Municipio CUNDINAMARCA_SASAIMA => new Municipio { Codigo = "25718", Nombre = "Sasaima" };
        public static Municipio CUNDINAMARCA_SESQUILE => new Municipio { Codigo = "25736", Nombre = "Sesquilé" };
        public static Municipio CUNDINAMARCA_SIBATE => new Municipio { Codigo = "25740", Nombre = "Sibaté" };
        public static Municipio CUNDINAMARCA_SILVANIA => new Municipio { Codigo = "25743", Nombre = "Silvania" };
        public static Municipio CUNDINAMARCA_SIMIJACA => new Municipio { Codigo = "25745", Nombre = "Simijaca" };
        public static Municipio CUNDINAMARCA_SOACHA => new Municipio { Codigo = "25754", Nombre = "Soacha" };
        public static Municipio CUNDINAMARCA_SOPO => new Municipio { Codigo = "25758", Nombre = "Sopó" };
        public static Municipio CUNDINAMARCA_SUBACHOQUE => new Municipio { Codigo = "25769", Nombre = "Subachoque" };
        public static Municipio CUNDINAMARCA_SUESCA => new Municipio { Codigo = "25772", Nombre = "Suesca" };
        public static Municipio CUNDINAMARCA_SUPATA => new Municipio { Codigo = "25777", Nombre = "Supatá" };
        public static Municipio CUNDINAMARCA_SUSA => new Municipio { Codigo = "25779", Nombre = "Susa" };
        public static Municipio CUNDINAMARCA_SUTATAUSA => new Municipio { Codigo = "25781", Nombre = "Sutatausa" };
        public static Municipio CUNDINAMARCA_TABIO => new Municipio { Codigo = "25785", Nombre = "Tabio" };
        public static Municipio CUNDINAMARCA_TAUSA => new Municipio { Codigo = "25793", Nombre = "Tausa" };
        public static Municipio CUNDINAMARCA_TENA => new Municipio { Codigo = "25797", Nombre = "Tena" };
        public static Municipio CUNDINAMARCA_TENJO => new Municipio { Codigo = "25799", Nombre = "Tenjo" };
        public static Municipio CUNDINAMARCA_TIBACUY => new Municipio { Codigo = "25805", Nombre = "Tibacuy" };
        public static Municipio CUNDINAMARCA_TIBIRITA => new Municipio { Codigo = "25807", Nombre = "Tibirita" };
        public static Municipio CUNDINAMARCA_TOCAIMA => new Municipio { Codigo = "25815", Nombre = "Tocaima" };
        public static Municipio CUNDINAMARCA_TOCANCIPA => new Municipio { Codigo = "25817", Nombre = "Tocancipá" };
        public static Municipio CUNDINAMARCA_TOPAIPI => new Municipio { Codigo = "25823", Nombre = "Topaipí" };
        public static Municipio CUNDINAMARCA_UBALA => new Municipio { Codigo = "25839", Nombre = "Ubalá" };
        public static Municipio CUNDINAMARCA_UBAQUE => new Municipio { Codigo = "25841", Nombre = "Ubaque" };
        public static Municipio CUNDINAMARCA_VILLA_DE_SAN_DIEGO_DE_UBATE => new Municipio { Codigo = "25843", Nombre = "Villa De San Diego De Ubaté" };
        public static Municipio CUNDINAMARCA_UNE => new Municipio { Codigo = "25845", Nombre = "Une" };
        public static Municipio CUNDINAMARCA_UTICA => new Municipio { Codigo = "25851", Nombre = "Útica" };
        public static Municipio CUNDINAMARCA_VERGARA => new Municipio { Codigo = "25862", Nombre = "Vergara" };
        public static Municipio CUNDINAMARCA_VIANI => new Municipio { Codigo = "25867", Nombre = "Vianí" };
        public static Municipio CUNDINAMARCA_VILLAGOMEZ => new Municipio { Codigo = "25871", Nombre = "Villagómez" };
        public static Municipio CUNDINAMARCA_VILLAPINZON => new Municipio { Codigo = "25873", Nombre = "Villapinzón" };
        public static Municipio CUNDINAMARCA_VILLETA => new Municipio { Codigo = "25875", Nombre = "Villeta" };
        public static Municipio CUNDINAMARCA_VIOTA => new Municipio { Codigo = "25878", Nombre = "Viotá" };
        public static Municipio CUNDINAMARCA_YACOPI => new Municipio { Codigo = "25885", Nombre = "Yacopí" };
        public static Municipio CUNDINAMARCA_ZIPACON => new Municipio { Codigo = "25898", Nombre = "Zipacón" };
        public static Municipio CUNDINAMARCA_ZIPAQUIRA => new Municipio { Codigo = "25899", Nombre = "Zipaquirá" };
        public static Municipio CHOCO_QUIBDO => new Municipio { Codigo = "27001", Nombre = "Quibdó" };
        public static Municipio CHOCO_ACANDI => new Municipio { Codigo = "27006", Nombre = "Acandí" };
        public static Municipio CHOCO_ALTO_BAUDO => new Municipio { Codigo = "27025", Nombre = "Alto Baudó" };
        public static Municipio CHOCO_ATRATO => new Municipio { Codigo = "27050", Nombre = "Atrato" };
        public static Municipio CHOCO_BAGADO => new Municipio { Codigo = "27073", Nombre = "Bagadó" };
        public static Municipio CHOCO_BAHIA_SOLANO => new Municipio { Codigo = "27075", Nombre = "Bahía Solano" };
        public static Municipio CHOCO_BAJO_BAUDO => new Municipio { Codigo = "27077", Nombre = "Bajo Baudó" };
        public static Municipio CHOCO_BOJAYA => new Municipio { Codigo = "27099", Nombre = "Bojayá" };
        public static Municipio CHOCO_EL_CANTON_DEL_SAN_PABLO => new Municipio { Codigo = "27135", Nombre = "El Cantón Del San Pablo" };
        public static Municipio CHOCO_CARMEN_DEL_DARIEN => new Municipio { Codigo = "27150", Nombre = "Carmen Del Darién" };
        public static Municipio CHOCO_CERTEGUI => new Municipio { Codigo = "27160", Nombre = "Cértegui" };
        public static Municipio CHOCO_CONDOTO => new Municipio { Codigo = "27205", Nombre = "Condoto" };
        public static Municipio CHOCO_EL_CARMEN_DE_ATRATO => new Municipio { Codigo = "27245", Nombre = "El Carmen De Atrato" };
        public static Municipio CHOCO_EL_LITORAL_DEL_SAN_JUAN => new Municipio { Codigo = "27250", Nombre = "El Litoral Del San Juan" };
        public static Municipio CHOCO_ISTMINA => new Municipio { Codigo = "27361", Nombre = "Istmina" };
        public static Municipio CHOCO_JURADO => new Municipio { Codigo = "27372", Nombre = "Juradó" };
        public static Municipio CHOCO_LLORO => new Municipio { Codigo = "27413", Nombre = "Lloró" };
        public static Municipio CHOCO_MEDIO_ATRATO => new Municipio { Codigo = "27425", Nombre = "Medio Atrato" };
        public static Municipio CHOCO_MEDIO_BAUDO => new Municipio { Codigo = "27430", Nombre = "Medio Baudó" };
        public static Municipio CHOCO_MEDIO_SAN_JUAN => new Municipio { Codigo = "27450", Nombre = "Medio San Juan" };
        public static Municipio CHOCO_NOVITA => new Municipio { Codigo = "27491", Nombre = "Nóvita" };
        public static Municipio CHOCO_NUQUI => new Municipio { Codigo = "27495", Nombre = "Nuquí" };
        public static Municipio CHOCO_RIO_IRO => new Municipio { Codigo = "27580", Nombre = "Río Iró" };
        public static Municipio CHOCO_RIO_QUITO => new Municipio { Codigo = "27600", Nombre = "Río Quito" };
        public static Municipio CHOCO_RIOSUCIO => new Municipio { Codigo = "27615", Nombre = "Riosucio" };
        public static Municipio CHOCO_SAN_JOSE_DEL_PALMAR => new Municipio { Codigo = "27660", Nombre = "San José Del Palmar" };
        public static Municipio CHOCO_SIPI => new Municipio { Codigo = "27745", Nombre = "Sipí" };
        public static Municipio CHOCO_TADO => new Municipio { Codigo = "27787", Nombre = "Tadó" };
        public static Municipio CHOCO_UNGUIA => new Municipio { Codigo = "27800", Nombre = "Unguía" };
        public static Municipio CHOCO_UNION_PANAMERICANA => new Municipio { Codigo = "27810", Nombre = "Unión Panamericana" };
        public static Municipio HUILA_NEIVA => new Municipio { Codigo = "41001", Nombre = "Neiva" };
        public static Municipio HUILA_ACEVEDO => new Municipio { Codigo = "41006", Nombre = "Acevedo" };
        public static Municipio HUILA_AGRADO => new Municipio { Codigo = "41013", Nombre = "Agrado" };
        public static Municipio HUILA_AIPE => new Municipio { Codigo = "41016", Nombre = "Aipe" };
        public static Municipio HUILA_ALGECIRAS => new Municipio { Codigo = "41020", Nombre = "Algeciras" };
        public static Municipio HUILA_ALTAMIRA => new Municipio { Codigo = "41026", Nombre = "Altamira" };
        public static Municipio HUILA_BARAYA => new Municipio { Codigo = "41078", Nombre = "Baraya" };
        public static Municipio HUILA_CAMPOALEGRE => new Municipio { Codigo = "41132", Nombre = "Campoalegre" };
        public static Municipio HUILA_COLOMBIA => new Municipio { Codigo = "41206", Nombre = "Colombia" };
        public static Municipio HUILA_ELIAS => new Municipio { Codigo = "41244", Nombre = "Elías" };
        public static Municipio HUILA_GARZON => new Municipio { Codigo = "41298", Nombre = "Garzón" };
        public static Municipio HUILA_GIGANTE => new Municipio { Codigo = "41306", Nombre = "Gigante" };
        public static Municipio HUILA_GUADALUPE => new Municipio { Codigo = "41319", Nombre = "Guadalupe" };
        public static Municipio HUILA_HOBO => new Municipio { Codigo = "41349", Nombre = "Hobo" };
        public static Municipio HUILA_IQUIRA => new Municipio { Codigo = "41357", Nombre = "Íquira" };
        public static Municipio HUILA_ISNOS => new Municipio { Codigo = "41359", Nombre = "Isnos" };
        public static Municipio HUILA_LA_ARGENTINA => new Municipio { Codigo = "41378", Nombre = "La Argentina" };
        public static Municipio HUILA_LA_PLATA => new Municipio { Codigo = "41396", Nombre = "La Plata" };
        public static Municipio HUILA_NATAGA => new Municipio { Codigo = "41483", Nombre = "Nátaga" };
        public static Municipio HUILA_OPORAPA => new Municipio { Codigo = "41503", Nombre = "Oporapa" };
        public static Municipio HUILA_PAICOL => new Municipio { Codigo = "41518", Nombre = "Paicol" };
        public static Municipio HUILA_PALERMO => new Municipio { Codigo = "41524", Nombre = "Palermo" };
        public static Municipio HUILA_PALESTINA => new Municipio { Codigo = "41530", Nombre = "Palestina" };
        public static Municipio HUILA_PITAL => new Municipio { Codigo = "41548", Nombre = "Pital" };
        public static Municipio HUILA_PITALITO => new Municipio { Codigo = "41551", Nombre = "Pitalito" };
        public static Municipio HUILA_RIVERA => new Municipio { Codigo = "41615", Nombre = "Rivera" };
        public static Municipio HUILA_SALADOBLANCO => new Municipio { Codigo = "41660", Nombre = "Saladoblanco" };
        public static Municipio HUILA_SAN_AGUSTIN => new Municipio { Codigo = "41668", Nombre = "San Agustín" };
        public static Municipio HUILA_SANTA_MARIA => new Municipio { Codigo = "41676", Nombre = "Santa María" };
        public static Municipio HUILA_SUAZA => new Municipio { Codigo = "41770", Nombre = "Suaza" };
        public static Municipio HUILA_TARQUI => new Municipio { Codigo = "41791", Nombre = "Tarqui" };
        public static Municipio HUILA_TESALIA => new Municipio { Codigo = "41797", Nombre = "Tesalia" };
        public static Municipio HUILA_TELLO => new Municipio { Codigo = "41799", Nombre = "Tello" };
        public static Municipio HUILA_TERUEL => new Municipio { Codigo = "41801", Nombre = "Teruel" };
        public static Municipio HUILA_TIMANA => new Municipio { Codigo = "41807", Nombre = "Timaná" };
        public static Municipio HUILA_VILLAVIEJA => new Municipio { Codigo = "41872", Nombre = "Villavieja" };
        public static Municipio HUILA_YAGUARA => new Municipio { Codigo = "41885", Nombre = "Yaguará" };
        public static Municipio LA_GUAJIRA_RIOHACHA => new Municipio { Codigo = "44001", Nombre = "Riohacha" };
        public static Municipio LA_GUAJIRA_ALBANIA => new Municipio { Codigo = "44035", Nombre = "Albania" };
        public static Municipio LA_GUAJIRA_BARRANCAS => new Municipio { Codigo = "44078", Nombre = "Barrancas" };
        public static Municipio LA_GUAJIRA_DIBULLA => new Municipio { Codigo = "44090", Nombre = "Dibulla" };
        public static Municipio LA_GUAJIRA_DISTRACCION => new Municipio { Codigo = "44098", Nombre = "Distracción" };
        public static Municipio LA_GUAJIRA_EL_MOLINO => new Municipio { Codigo = "44110", Nombre = "El Molino" };
        public static Municipio LA_GUAJIRA_FONSECA => new Municipio { Codigo = "44279", Nombre = "Fonseca" };
        public static Municipio LA_GUAJIRA_HATONUEVO => new Municipio { Codigo = "44378", Nombre = "Hatonuevo" };
        public static Municipio LA_GUAJIRA_LA_JAGUA_DEL_PILAR => new Municipio { Codigo = "44420", Nombre = "La Jagua Del Pilar" };
        public static Municipio LA_GUAJIRA_MAICAO => new Municipio { Codigo = "44430", Nombre = "Maicao" };
        public static Municipio LA_GUAJIRA_MANAURE => new Municipio { Codigo = "44560", Nombre = "Manaure" };
        public static Municipio LA_GUAJIRA_SAN_JUAN_DEL_CESAR => new Municipio { Codigo = "44650", Nombre = "San Juan Del Cesar" };
        public static Municipio LA_GUAJIRA_URIBIA => new Municipio { Codigo = "44847", Nombre = "Uribia" };
        public static Municipio LA_GUAJIRA_URUMITA => new Municipio { Codigo = "44855", Nombre = "Urumita" };
        public static Municipio LA_GUAJIRA_VILLANUEVA => new Municipio { Codigo = "44874", Nombre = "Villanueva" };
        public static Municipio MAGDALENA_SANTA_MARTA => new Municipio { Codigo = "47001", Nombre = "Santa Marta" };
        public static Municipio MAGDALENA_ALGARROBO => new Municipio { Codigo = "47030", Nombre = "Algarrobo" };
        public static Municipio MAGDALENA_ARACATACA => new Municipio { Codigo = "47053", Nombre = "Aracataca" };
        public static Municipio MAGDALENA_ARIGUANI => new Municipio { Codigo = "47058", Nombre = "Ariguaní" };
        public static Municipio MAGDALENA_CERRO_DE_SAN_ANTONIO => new Municipio { Codigo = "47161", Nombre = "Cerro De San Antonio" };
        public static Municipio MAGDALENA_CHIVOLO => new Municipio { Codigo = "47170", Nombre = "Chivolo" };
        public static Municipio MAGDALENA_CIENAGA => new Municipio { Codigo = "47189", Nombre = "Ciénaga" };
        public static Municipio MAGDALENA_CONCORDIA => new Municipio { Codigo = "47205", Nombre = "Concordia" };
        public static Municipio MAGDALENA_EL_BANCO => new Municipio { Codigo = "47245", Nombre = "El Banco" };
        public static Municipio MAGDALENA_EL_PIÑON => new Municipio { Codigo = "47258", Nombre = "El Piñón" };
        public static Municipio MAGDALENA_EL_RETEN => new Municipio { Codigo = "47268", Nombre = "El Retén" };
        public static Municipio MAGDALENA_FUNDACION => new Municipio { Codigo = "47288", Nombre = "Fundación" };
        public static Municipio MAGDALENA_GUAMAL => new Municipio { Codigo = "47318", Nombre = "Guamal" };
        public static Municipio MAGDALENA_NUEVA_GRANADA => new Municipio { Codigo = "47460", Nombre = "Nueva Granada" };
        public static Municipio MAGDALENA_PEDRAZA => new Municipio { Codigo = "47541", Nombre = "Pedraza" };
        public static Municipio MAGDALENA_PIJIÑO_DEL_CARMEN => new Municipio { Codigo = "47545", Nombre = "Pijiño Del Carmen" };
        public static Municipio MAGDALENA_PIVIJAY => new Municipio { Codigo = "47551", Nombre = "Pivijay" };
        public static Municipio MAGDALENA_PLATO => new Municipio { Codigo = "47555", Nombre = "Plato" };
        public static Municipio MAGDALENA_PUEBLOVIEJO => new Municipio { Codigo = "47570", Nombre = "Puebloviejo" };
        public static Municipio MAGDALENA_REMOLINO => new Municipio { Codigo = "47605", Nombre = "Remolino" };
        public static Municipio MAGDALENA_SABANAS_DE_SAN_ANGEL => new Municipio { Codigo = "47660", Nombre = "Sabanas De San Ángel" };
        public static Municipio MAGDALENA_SALAMINA => new Municipio { Codigo = "47675", Nombre = "Salamina" };
        public static Municipio MAGDALENA_SAN_SEBASTIAN_DE_BUENAVISTA => new Municipio { Codigo = "47692", Nombre = "San Sebastián De Buenavista" };
        public static Municipio MAGDALENA_SAN_ZENON => new Municipio { Codigo = "47703", Nombre = "San Zenón" };
        public static Municipio MAGDALENA_SANTA_ANA => new Municipio { Codigo = "47707", Nombre = "Santa Ana" };
        public static Municipio MAGDALENA_SANTA_BARBARA_DE_PINTO => new Municipio { Codigo = "47720", Nombre = "Santa Bárbara De Pinto" };
        public static Municipio MAGDALENA_SITIONUEVO => new Municipio { Codigo = "47745", Nombre = "Sitionuevo" };
        public static Municipio MAGDALENA_TENERIFE => new Municipio { Codigo = "47798", Nombre = "Tenerife" };
        public static Municipio MAGDALENA_ZAPAYAN => new Municipio { Codigo = "47960", Nombre = "Zapayán" };
        public static Municipio MAGDALENA_ZONA_BANANERA => new Municipio { Codigo = "47980", Nombre = "Zona Bananera" };
        public static Municipio META_VILLAVICENCIO => new Municipio { Codigo = "50001", Nombre = "Villavicencio" };
        public static Municipio META_ACACIAS => new Municipio { Codigo = "50006", Nombre = "Acacías" };
        public static Municipio META_BARRANCA_DE_UPIA => new Municipio { Codigo = "50110", Nombre = "Barranca De Upía" };
        public static Municipio META_CABUYARO => new Municipio { Codigo = "50124", Nombre = "Cabuyaro" };
        public static Municipio META_CASTILLA_LA_NUEVA => new Municipio { Codigo = "50150", Nombre = "Castilla La Nueva" };
        public static Municipio META_CUBARRAL => new Municipio { Codigo = "50223", Nombre = "Cubarral" };
        public static Municipio META_CUMARAL => new Municipio { Codigo = "50226", Nombre = "Cumaral" };
        public static Municipio META_EL_CALVARIO => new Municipio { Codigo = "50245", Nombre = "El Calvario" };
        public static Municipio META_EL_CASTILLO => new Municipio { Codigo = "50251", Nombre = "El Castillo" };
        public static Municipio META_EL_DORADO => new Municipio { Codigo = "50270", Nombre = "El Dorado" };
        public static Municipio META_FUENTEDEORO => new Municipio { Codigo = "50287", Nombre = "Fuentedeoro" };
        public static Municipio META_GRANADA => new Municipio { Codigo = "50313", Nombre = "Granada" };
        public static Municipio META_GUAMAL => new Municipio { Codigo = "50318", Nombre = "Guamal" };
        public static Municipio META_MAPIRIPAN => new Municipio { Codigo = "50325", Nombre = "Mapiripán" };
        public static Municipio META_MESETAS => new Municipio { Codigo = "50330", Nombre = "Mesetas" };
        public static Municipio META_LA_MACARENA => new Municipio { Codigo = "50350", Nombre = "La Macarena" };
        public static Municipio META_URIBE => new Municipio { Codigo = "50370", Nombre = "Uribe" };
        public static Municipio META_LEJANIAS => new Municipio { Codigo = "50400", Nombre = "Lejanías" };
        public static Municipio META_PUERTO_CONCORDIA => new Municipio { Codigo = "50450", Nombre = "Puerto Concordia" };
        public static Municipio META_PUERTO_GAITAN => new Municipio { Codigo = "50568", Nombre = "Puerto Gaitán" };
        public static Municipio META_PUERTO_LOPEZ => new Municipio { Codigo = "50573", Nombre = "Puerto López" };
        public static Municipio META_PUERTO_LLERAS => new Municipio { Codigo = "50577", Nombre = "Puerto Lleras" };
        public static Municipio META_PUERTO_RICO => new Municipio { Codigo = "50590", Nombre = "Puerto Rico" };
        public static Municipio META_RESTREPO => new Municipio { Codigo = "50606", Nombre = "Restrepo" };
        public static Municipio META_SAN_CARLOS_DE_GUAROA => new Municipio { Codigo = "50680", Nombre = "San Carlos De Guaroa" };
        public static Municipio META_SAN_JUAN_DE_ARAMA => new Municipio { Codigo = "50683", Nombre = "San Juan De Arama" };
        public static Municipio META_SAN_JUANITO => new Municipio { Codigo = "50686", Nombre = "San Juanito" };
        public static Municipio META_SAN_MARTIN => new Municipio { Codigo = "50689", Nombre = "San Martín" };
        public static Municipio META_VISTAHERMOSA => new Municipio { Codigo = "50711", Nombre = "Vistahermosa" };
        public static Municipio NARIÑO_PASTO => new Municipio { Codigo = "52001", Nombre = "Pasto" };
        public static Municipio NARIÑO_ALBAN => new Municipio { Codigo = "52019", Nombre = "Albán" };
        public static Municipio NARIÑO_ALDANA => new Municipio { Codigo = "52022", Nombre = "Aldana" };
        public static Municipio NARIÑO_ANCUYA => new Municipio { Codigo = "52036", Nombre = "Ancuyá" };
        public static Municipio NARIÑO_ARBOLEDA => new Municipio { Codigo = "52051", Nombre = "Arboleda" };
        public static Municipio NARIÑO_BARBACOAS => new Municipio { Codigo = "52079", Nombre = "Barbacoas" };
        public static Municipio NARIÑO_BELEN => new Municipio { Codigo = "52083", Nombre = "Belén" };
        public static Municipio NARIÑO_BUESACO => new Municipio { Codigo = "52110", Nombre = "Buesaco" };
        public static Municipio NARIÑO_COLON => new Municipio { Codigo = "52203", Nombre = "Colón" };
        public static Municipio NARIÑO_CONSACA => new Municipio { Codigo = "52207", Nombre = "Consacá" };
        public static Municipio NARIÑO_CONTADERO => new Municipio { Codigo = "52210", Nombre = "Contadero" };
        public static Municipio NARIÑO_CORDOBA => new Municipio { Codigo = "52215", Nombre = "Córdoba" };
        public static Municipio NARIÑO_CUASPUD => new Municipio { Codigo = "52224", Nombre = "Cuaspúd" };
        public static Municipio NARIÑO_CUMBAL => new Municipio { Codigo = "52227", Nombre = "Cumbal" };
        public static Municipio NARIÑO_CUMBITARA => new Municipio { Codigo = "52233", Nombre = "Cumbitara" };
        public static Municipio NARIÑO_CHACHAGÜI => new Municipio { Codigo = "52240", Nombre = "Chachagüí" };
        public static Municipio NARIÑO_EL_CHARCO => new Municipio { Codigo = "52250", Nombre = "El Charco" };
        public static Municipio NARIÑO_EL_PEÑOL => new Municipio { Codigo = "52254", Nombre = "El Peñol" };
        public static Municipio NARIÑO_EL_ROSARIO => new Municipio { Codigo = "52256", Nombre = "El Rosario" };
        public static Municipio NARIÑO_EL_TABLON_DE_GOMEZ => new Municipio { Codigo = "52258", Nombre = "El Tablón De Gómez" };
        public static Municipio NARIÑO_EL_TAMBO => new Municipio { Codigo = "52260", Nombre = "El Tambo" };
        public static Municipio NARIÑO_FUNES => new Municipio { Codigo = "52287", Nombre = "Funes" };
        public static Municipio NARIÑO_GUACHUCAL => new Municipio { Codigo = "52317", Nombre = "Guachucal" };
        public static Municipio NARIÑO_GUAITARILLA => new Municipio { Codigo = "52320", Nombre = "Guaitarilla" };
        public static Municipio NARIÑO_GUALMATAN => new Municipio { Codigo = "52323", Nombre = "Gualmatán" };
        public static Municipio NARIÑO_ILES => new Municipio { Codigo = "52352", Nombre = "Iles" };
        public static Municipio NARIÑO_IMUES => new Municipio { Codigo = "52354", Nombre = "Imués" };
        public static Municipio NARIÑO_IPIALES => new Municipio { Codigo = "52356", Nombre = "Ipiales" };
        public static Municipio NARIÑO_LA_CRUZ => new Municipio { Codigo = "52378", Nombre = "La Cruz" };
        public static Municipio NARIÑO_LA_FLORIDA => new Municipio { Codigo = "52381", Nombre = "La Florida" };
        public static Municipio NARIÑO_LA_LLANADA => new Municipio { Codigo = "52385", Nombre = "La Llanada" };
        public static Municipio NARIÑO_LA_TOLA => new Municipio { Codigo = "52390", Nombre = "La Tola" };
        public static Municipio NARIÑO_LA_UNION => new Municipio { Codigo = "52399", Nombre = "La Unión" };
        public static Municipio NARIÑO_LEIVA => new Municipio { Codigo = "52405", Nombre = "Leiva" };
        public static Municipio NARIÑO_LINARES => new Municipio { Codigo = "52411", Nombre = "Linares" };
        public static Municipio NARIÑO_LOS_ANDES => new Municipio { Codigo = "52418", Nombre = "Los Andes" };
        public static Municipio NARIÑO_MAGÜI => new Municipio { Codigo = "52427", Nombre = "Magüí" };
        public static Municipio NARIÑO_MALLAMA => new Municipio { Codigo = "52435", Nombre = "Mallama" };
        public static Municipio NARIÑO_MOSQUERA => new Municipio { Codigo = "52473", Nombre = "Mosquera" };
        public static Municipio NARIÑO_NARIÑO => new Municipio { Codigo = "52480", Nombre = "Nariño" };
        public static Municipio NARIÑO_OLAYA_HERRERA => new Municipio { Codigo = "52490", Nombre = "Olaya Herrera" };
        public static Municipio NARIÑO_OSPINA => new Municipio { Codigo = "52506", Nombre = "Ospina" };
        public static Municipio NARIÑO_FRANCISCO_PIZARRO => new Municipio { Codigo = "52520", Nombre = "Francisco Pizarro" };
        public static Municipio NARIÑO_POLICARPA => new Municipio { Codigo = "52540", Nombre = "Policarpa" };
        public static Municipio NARIÑO_POTOSI => new Municipio { Codigo = "52560", Nombre = "Potosí" };
        public static Municipio NARIÑO_PROVIDENCIA => new Municipio { Codigo = "52565", Nombre = "Providencia" };
        public static Municipio NARIÑO_PUERRES => new Municipio { Codigo = "52573", Nombre = "Puerres" };
        public static Municipio NARIÑO_PUPIALES => new Municipio { Codigo = "52585", Nombre = "Pupiales" };
        public static Municipio NARIÑO_RICAURTE => new Municipio { Codigo = "52612", Nombre = "Ricaurte" };
        public static Municipio NARIÑO_ROBERTO_PAYAN => new Municipio { Codigo = "52621", Nombre = "Roberto Payán" };
        public static Municipio NARIÑO_SAMANIEGO => new Municipio { Codigo = "52678", Nombre = "Samaniego" };
        public static Municipio NARIÑO_SANDONA => new Municipio { Codigo = "52683", Nombre = "Sandoná" };
        public static Municipio NARIÑO_SAN_BERNARDO => new Municipio { Codigo = "52685", Nombre = "San Bernardo" };
        public static Municipio NARIÑO_SAN_LORENZO => new Municipio { Codigo = "52687", Nombre = "San Lorenzo" };
        public static Municipio NARIÑO_SAN_PABLO => new Municipio { Codigo = "52693", Nombre = "San Pablo" };
        public static Municipio NARIÑO_SAN_PEDRO_DE_CARTAGO => new Municipio { Codigo = "52694", Nombre = "San Pedro De Cartago" };
        public static Municipio NARIÑO_SANTA_BARBARA => new Municipio { Codigo = "52696", Nombre = "Santa Bárbara" };
        public static Municipio NARIÑO_SANTACRUZ => new Municipio { Codigo = "52699", Nombre = "Santacruz" };
        public static Municipio NARIÑO_SAPUYES => new Municipio { Codigo = "52720", Nombre = "Sapuyes" };
        public static Municipio NARIÑO_TAMINANGO => new Municipio { Codigo = "52786", Nombre = "Taminango" };
        public static Municipio NARIÑO_TANGUA => new Municipio { Codigo = "52788", Nombre = "Tangua" };
        public static Municipio NARIÑO_SAN_ANDRES_DE_TUMACO => new Municipio { Codigo = "52835", Nombre = "San Andrés De Tumaco" };
        public static Municipio NARIÑO_TUQUERRES => new Municipio { Codigo = "52838", Nombre = "Túquerres" };
        public static Municipio NARIÑO_YACUANQUER => new Municipio { Codigo = "52885", Nombre = "Yacuanquer" };
        public static Municipio NORTE_DE_SANTANDER_CUCUTA => new Municipio { Codigo = "54001", Nombre = "Cúcuta" };
        public static Municipio NORTE_DE_SANTANDER_ABREGO => new Municipio { Codigo = "54003", Nombre = "Ábrego" };
        public static Municipio NORTE_DE_SANTANDER_ARBOLEDAS => new Municipio { Codigo = "54051", Nombre = "Arboledas" };
        public static Municipio NORTE_DE_SANTANDER_BOCHALEMA => new Municipio { Codigo = "54099", Nombre = "Bochalema" };
        public static Municipio NORTE_DE_SANTANDER_BUCARASICA => new Municipio { Codigo = "54109", Nombre = "Bucarasica" };
        public static Municipio NORTE_DE_SANTANDER_CACOTA => new Municipio { Codigo = "54125", Nombre = "Cácota" };
        public static Municipio NORTE_DE_SANTANDER_CACHIRA => new Municipio { Codigo = "54128", Nombre = "Cáchira" };
        public static Municipio NORTE_DE_SANTANDER_CHINACOTA => new Municipio { Codigo = "54172", Nombre = "Chinácota" };
        public static Municipio NORTE_DE_SANTANDER_CHITAGA => new Municipio { Codigo = "54174", Nombre = "Chitagá" };
        public static Municipio NORTE_DE_SANTANDER_CONVENCION => new Municipio { Codigo = "54206", Nombre = "Convención" };
        public static Municipio NORTE_DE_SANTANDER_CUCUTILLA => new Municipio { Codigo = "54223", Nombre = "Cucutilla" };
        public static Municipio NORTE_DE_SANTANDER_DURANIA => new Municipio { Codigo = "54239", Nombre = "Durania" };
        public static Municipio NORTE_DE_SANTANDER_EL_CARMEN => new Municipio { Codigo = "54245", Nombre = "El Carmen" };
        public static Municipio NORTE_DE_SANTANDER_EL_TARRA => new Municipio { Codigo = "54250", Nombre = "El Tarra" };
        public static Municipio NORTE_DE_SANTANDER_EL_ZULIA => new Municipio { Codigo = "54261", Nombre = "El Zulia" };
        public static Municipio NORTE_DE_SANTANDER_GRAMALOTE => new Municipio { Codigo = "54313", Nombre = "Gramalote" };
        public static Municipio NORTE_DE_SANTANDER_HACARI => new Municipio { Codigo = "54344", Nombre = "Hacarí" };
        public static Municipio NORTE_DE_SANTANDER_HERRAN => new Municipio { Codigo = "54347", Nombre = "Herrán" };
        public static Municipio NORTE_DE_SANTANDER_LABATECA => new Municipio { Codigo = "54377", Nombre = "Labateca" };
        public static Municipio NORTE_DE_SANTANDER_LA_ESPERANZA => new Municipio { Codigo = "54385", Nombre = "La Esperanza" };
        public static Municipio NORTE_DE_SANTANDER_LA_PLAYA => new Municipio { Codigo = "54398", Nombre = "La Playa" };
        public static Municipio NORTE_DE_SANTANDER_LOS_PATIOS => new Municipio { Codigo = "54405", Nombre = "Los Patios" };
        public static Municipio NORTE_DE_SANTANDER_LOURDES => new Municipio { Codigo = "54418", Nombre = "Lourdes" };
        public static Municipio NORTE_DE_SANTANDER_MUTISCUA => new Municipio { Codigo = "54480", Nombre = "Mutiscua" };
        public static Municipio NORTE_DE_SANTANDER_OCAÑA => new Municipio { Codigo = "54498", Nombre = "Ocaña" };
        public static Municipio NORTE_DE_SANTANDER_PAMPLONA => new Municipio { Codigo = "54518", Nombre = "Pamplona" };
        public static Municipio NORTE_DE_SANTANDER_PAMPLONITA => new Municipio { Codigo = "54520", Nombre = "Pamplonita" };
        public static Municipio NORTE_DE_SANTANDER_PUERTO_SANTANDER => new Municipio { Codigo = "54553", Nombre = "Puerto Santander" };
        public static Municipio NORTE_DE_SANTANDER_RAGONVALIA => new Municipio { Codigo = "54599", Nombre = "Ragonvalia" };
        public static Municipio NORTE_DE_SANTANDER_SALAZAR => new Municipio { Codigo = "54660", Nombre = "Salazar" };
        public static Municipio NORTE_DE_SANTANDER_SAN_CALIXTO => new Municipio { Codigo = "54670", Nombre = "San Calixto" };
        public static Municipio NORTE_DE_SANTANDER_SAN_CAYETANO => new Municipio { Codigo = "54673", Nombre = "San Cayetano" };
        public static Municipio NORTE_DE_SANTANDER_SANTIAGO => new Municipio { Codigo = "54680", Nombre = "Santiago" };
        public static Municipio NORTE_DE_SANTANDER_SARDINATA => new Municipio { Codigo = "54720", Nombre = "Sardinata" };
        public static Municipio NORTE_DE_SANTANDER_SILOS => new Municipio { Codigo = "54743", Nombre = "Silos" };
        public static Municipio NORTE_DE_SANTANDER_TEORAMA => new Municipio { Codigo = "54800", Nombre = "Teorama" };
        public static Municipio NORTE_DE_SANTANDER_TIBU => new Municipio { Codigo = "54810", Nombre = "Tibú" };
        public static Municipio NORTE_DE_SANTANDER_TOLEDO => new Municipio { Codigo = "54820", Nombre = "Toledo" };
        public static Municipio NORTE_DE_SANTANDER_VILLA_CARO => new Municipio { Codigo = "54871", Nombre = "Villa Caro" };
        public static Municipio NORTE_DE_SANTANDER_VILLA_DEL_ROSARIO => new Municipio { Codigo = "54874", Nombre = "Villa Del Rosario" };
        public static Municipio QUINDIO_ARMENIA => new Municipio { Codigo = "63001", Nombre = "Armenia" };
        public static Municipio QUINDIO_BUENAVISTA => new Municipio { Codigo = "63111", Nombre = "Buenavista" };
        public static Municipio QUINDIO_CALARCA => new Municipio { Codigo = "63130", Nombre = "Calarcá" };
        public static Municipio QUINDIO_CIRCASIA => new Municipio { Codigo = "63190", Nombre = "Circasia" };
        public static Municipio QUINDIO_CORDOBA => new Municipio { Codigo = "63212", Nombre = "Córdoba" };
        public static Municipio QUINDIO_FILANDIA => new Municipio { Codigo = "63272", Nombre = "Filandia" };
        public static Municipio QUINDIO_GENOVA => new Municipio { Codigo = "63302", Nombre = "Génova" };
        public static Municipio QUINDIO_LA_TEBAIDA => new Municipio { Codigo = "63401", Nombre = "La Tebaida" };
        public static Municipio QUINDIO_MONTENEGRO => new Municipio { Codigo = "63470", Nombre = "Montenegro" };
        public static Municipio QUINDIO_PIJAO => new Municipio { Codigo = "63548", Nombre = "Pijao" };
        public static Municipio QUINDIO_QUIMBAYA => new Municipio { Codigo = "63594", Nombre = "Quimbaya" };
        public static Municipio QUINDIO_SALENTO => new Municipio { Codigo = "63690", Nombre = "Salento" };
        public static Municipio RISARALDA_PEREIRA => new Municipio { Codigo = "66001", Nombre = "Pereira" };
        public static Municipio RISARALDA_APIA => new Municipio { Codigo = "66045", Nombre = "Apía" };
        public static Municipio RISARALDA_BALBOA => new Municipio { Codigo = "66075", Nombre = "Balboa" };
        public static Municipio RISARALDA_BELEN_DE_UMBRIA => new Municipio { Codigo = "66088", Nombre = "Belén De Umbría" };
        public static Municipio RISARALDA_DOSQUEBRADAS => new Municipio { Codigo = "66170", Nombre = "Dosquebradas" };
        public static Municipio RISARALDA_GUATICA => new Municipio { Codigo = "66318", Nombre = "Guática" };
        public static Municipio RISARALDA_LA_CELIA => new Municipio { Codigo = "66383", Nombre = "La Celia" };
        public static Municipio RISARALDA_LA_VIRGINIA => new Municipio { Codigo = "66400", Nombre = "La Virginia" };
        public static Municipio RISARALDA_MARSELLA => new Municipio { Codigo = "66440", Nombre = "Marsella" };
        public static Municipio RISARALDA_MISTRATO => new Municipio { Codigo = "66456", Nombre = "Mistrató" };
        public static Municipio RISARALDA_PUEBLO_RICO => new Municipio { Codigo = "66572", Nombre = "Pueblo Rico" };
        public static Municipio RISARALDA_QUINCHIA => new Municipio { Codigo = "66594", Nombre = "Quinchía" };
        public static Municipio RISARALDA_SANTA_ROSA_DE_CABAL => new Municipio { Codigo = "66682", Nombre = "Santa Rosa De Cabal" };
        public static Municipio RISARALDA_SANTUARIO => new Municipio { Codigo = "66687", Nombre = "Santuario" };
        public static Municipio SANTANDER_BUCARAMANGA => new Municipio { Codigo = "68001", Nombre = "Bucaramanga" };
        public static Municipio SANTANDER_AGUADA => new Municipio { Codigo = "68013", Nombre = "Aguada" };
        public static Municipio SANTANDER_ALBANIA => new Municipio { Codigo = "68020", Nombre = "Albania" };
        public static Municipio SANTANDER_ARATOCA => new Municipio { Codigo = "68051", Nombre = "Aratoca" };
        public static Municipio SANTANDER_BARBOSA => new Municipio { Codigo = "68077", Nombre = "Barbosa" };
        public static Municipio SANTANDER_BARICHARA => new Municipio { Codigo = "68079", Nombre = "Barichara" };
        public static Municipio SANTANDER_BARRANCABERMEJA => new Municipio { Codigo = "68081", Nombre = "Barrancabermeja" };
        public static Municipio SANTANDER_BETULIA => new Municipio { Codigo = "68092", Nombre = "Betulia" };
        public static Municipio SANTANDER_BOLIVAR => new Municipio { Codigo = "68101", Nombre = "Bolívar" };
        public static Municipio SANTANDER_CABRERA => new Municipio { Codigo = "68121", Nombre = "Cabrera" };
        public static Municipio SANTANDER_CALIFORNIA => new Municipio { Codigo = "68132", Nombre = "California" };
        public static Municipio SANTANDER_CAPITANEJO => new Municipio { Codigo = "68147", Nombre = "Capitanejo" };
        public static Municipio SANTANDER_CARCASI => new Municipio { Codigo = "68152", Nombre = "Carcasí" };
        public static Municipio SANTANDER_CEPITA => new Municipio { Codigo = "68160", Nombre = "Cepitá" };
        public static Municipio SANTANDER_CERRITO => new Municipio { Codigo = "68162", Nombre = "Cerrito" };
        public static Municipio SANTANDER_CHARALA => new Municipio { Codigo = "68167", Nombre = "Charalá" };
        public static Municipio SANTANDER_CHARTA => new Municipio { Codigo = "68169", Nombre = "Charta" };
        public static Municipio SANTANDER_CHIMA => new Municipio { Codigo = "68176", Nombre = "Chima" };
        public static Municipio SANTANDER_CHIPATA => new Municipio { Codigo = "68179", Nombre = "Chipatá" };
        public static Municipio SANTANDER_CIMITARRA => new Municipio { Codigo = "68190", Nombre = "Cimitarra" };
        public static Municipio SANTANDER_CONCEPCION => new Municipio { Codigo = "68207", Nombre = "Concepción" };
        public static Municipio SANTANDER_CONFINES => new Municipio { Codigo = "68209", Nombre = "Confines" };
        public static Municipio SANTANDER_CONTRATACION => new Municipio { Codigo = "68211", Nombre = "Contratación" };
        public static Municipio SANTANDER_COROMORO => new Municipio { Codigo = "68217", Nombre = "Coromoro" };
        public static Municipio SANTANDER_CURITI => new Municipio { Codigo = "68229", Nombre = "Curití" };
        public static Municipio SANTANDER_EL_CARMEN_DE_CHUCURI => new Municipio { Codigo = "68235", Nombre = "El Carmen De Chucurí" };
        public static Municipio SANTANDER_EL_GUACAMAYO => new Municipio { Codigo = "68245", Nombre = "El Guacamayo" };
        public static Municipio SANTANDER_EL_PEÑON => new Municipio { Codigo = "68250", Nombre = "El Peñón" };
        public static Municipio SANTANDER_EL_PLAYON => new Municipio { Codigo = "68255", Nombre = "El Playón" };
        public static Municipio SANTANDER_ENCINO => new Municipio { Codigo = "68264", Nombre = "Encino" };
        public static Municipio SANTANDER_ENCISO => new Municipio { Codigo = "68266", Nombre = "Enciso" };
        public static Municipio SANTANDER_FLORIAN => new Municipio { Codigo = "68271", Nombre = "Florián" };
        public static Municipio SANTANDER_FLORIDABLANCA => new Municipio { Codigo = "68276", Nombre = "Floridablanca" };
        public static Municipio SANTANDER_GALAN => new Municipio { Codigo = "68296", Nombre = "Galán" };
        public static Municipio SANTANDER_GAMBITA => new Municipio { Codigo = "68298", Nombre = "Gámbita" };
        public static Municipio SANTANDER_GIRON => new Municipio { Codigo = "68307", Nombre = "Girón" };
        public static Municipio SANTANDER_GUACA => new Municipio { Codigo = "68318", Nombre = "Guaca" };
        public static Municipio SANTANDER_GUADALUPE => new Municipio { Codigo = "68320", Nombre = "Guadalupe" };
        public static Municipio SANTANDER_GUAPOTA => new Municipio { Codigo = "68322", Nombre = "Guapotá" };
        public static Municipio SANTANDER_GUAVATA => new Municipio { Codigo = "68324", Nombre = "Guavatá" };
        public static Municipio SANTANDER_GÜEPSA => new Municipio { Codigo = "68327", Nombre = "Güepsa" };
        public static Municipio SANTANDER_HATO => new Municipio { Codigo = "68344", Nombre = "Hato" };
        public static Municipio SANTANDER_JESUS_MARIA => new Municipio { Codigo = "68368", Nombre = "Jesús María" };
        public static Municipio SANTANDER_JORDAN => new Municipio { Codigo = "68370", Nombre = "Jordán" };
        public static Municipio SANTANDER_LA_BELLEZA => new Municipio { Codigo = "68377", Nombre = "La Belleza" };
        public static Municipio SANTANDER_LANDAZURI => new Municipio { Codigo = "68385", Nombre = "Landázuri" };
        public static Municipio SANTANDER_LA_PAZ => new Municipio { Codigo = "68397", Nombre = "La Paz" };
        public static Municipio SANTANDER_LEBRIJA => new Municipio { Codigo = "68406", Nombre = "Lebrija" };
        public static Municipio SANTANDER_LOS_SANTOS => new Municipio { Codigo = "68418", Nombre = "Los Santos" };
        public static Municipio SANTANDER_MACARAVITA => new Municipio { Codigo = "68425", Nombre = "Macaravita" };
        public static Municipio SANTANDER_MALAGA => new Municipio { Codigo = "68432", Nombre = "Málaga" };
        public static Municipio SANTANDER_MATANZA => new Municipio { Codigo = "68444", Nombre = "Matanza" };
        public static Municipio SANTANDER_MOGOTES => new Municipio { Codigo = "68464", Nombre = "Mogotes" };
        public static Municipio SANTANDER_MOLAGAVITA => new Municipio { Codigo = "68468", Nombre = "Molagavita" };
        public static Municipio SANTANDER_OCAMONTE => new Municipio { Codigo = "68498", Nombre = "Ocamonte" };
        public static Municipio SANTANDER_OIBA => new Municipio { Codigo = "68500", Nombre = "Oiba" };
        public static Municipio SANTANDER_ONZAGA => new Municipio { Codigo = "68502", Nombre = "Onzaga" };
        public static Municipio SANTANDER_PALMAR => new Municipio { Codigo = "68522", Nombre = "Palmar" };
        public static Municipio SANTANDER_PALMAS_DEL_SOCORRO => new Municipio { Codigo = "68524", Nombre = "Palmas Del Socorro" };
        public static Municipio SANTANDER_PARAMO => new Municipio { Codigo = "68533", Nombre = "Páramo" };
        public static Municipio SANTANDER_PIEDECUESTA => new Municipio { Codigo = "68547", Nombre = "Piedecuesta" };
        public static Municipio SANTANDER_PINCHOTE => new Municipio { Codigo = "68549", Nombre = "Pinchote" };
        public static Municipio SANTANDER_PUENTE_NACIONAL => new Municipio { Codigo = "68572", Nombre = "Puente Nacional" };
        public static Municipio SANTANDER_PUERTO_PARRA => new Municipio { Codigo = "68573", Nombre = "Puerto Parra" };
        public static Municipio SANTANDER_PUERTO_WILCHES => new Municipio { Codigo = "68575", Nombre = "Puerto Wilches" };
        public static Municipio SANTANDER_RIONEGRO => new Municipio { Codigo = "68615", Nombre = "Rionegro" };
        public static Municipio SANTANDER_SABANA_DE_TORRES => new Municipio { Codigo = "68655", Nombre = "Sabana De Torres" };
        public static Municipio SANTANDER_SAN_ANDRES => new Municipio { Codigo = "68669", Nombre = "San Andrés" };
        public static Municipio SANTANDER_SAN_BENITO => new Municipio { Codigo = "68673", Nombre = "San Benito" };
        public static Municipio SANTANDER_SAN_GIL => new Municipio { Codigo = "68679", Nombre = "San Gil" };
        public static Municipio SANTANDER_SAN_JOAQUIN => new Municipio { Codigo = "68682", Nombre = "San Joaquín" };
        public static Municipio SANTANDER_SAN_JOSE_DE_MIRANDA => new Municipio { Codigo = "68684", Nombre = "San José De Miranda" };
        public static Municipio SANTANDER_SAN_MIGUEL => new Municipio { Codigo = "68686", Nombre = "San Miguel" };
        public static Municipio SANTANDER_SAN_VICENTE_DE_CHUCURI => new Municipio { Codigo = "68689", Nombre = "San Vicente De Chucurí" };
        public static Municipio SANTANDER_SANTA_BARBARA => new Municipio { Codigo = "68705", Nombre = "Santa Bárbara" };
        public static Municipio SANTANDER_SANTA_HELENA_DEL_OPON => new Municipio { Codigo = "68720", Nombre = "Santa Helena Del Opón" };
        public static Municipio SANTANDER_SIMACOTA => new Municipio { Codigo = "68745", Nombre = "Simacota" };
        public static Municipio SANTANDER_SOCORRO => new Municipio { Codigo = "68755", Nombre = "Socorro" };
        public static Municipio SANTANDER_SUAITA => new Municipio { Codigo = "68770", Nombre = "Suaita" };
        public static Municipio SANTANDER_SUCRE => new Municipio { Codigo = "68773", Nombre = "Sucre" };
        public static Municipio SANTANDER_SURATA => new Municipio { Codigo = "68780", Nombre = "Suratá" };
        public static Municipio SANTANDER_TONA => new Municipio { Codigo = "68820", Nombre = "Tona" };
        public static Municipio SANTANDER_VALLE_DE_SAN_JOSE => new Municipio { Codigo = "68855", Nombre = "Valle De San José" };
        public static Municipio SANTANDER_VELEZ => new Municipio { Codigo = "68861", Nombre = "Vélez" };
        public static Municipio SANTANDER_VETAS => new Municipio { Codigo = "68867", Nombre = "Vetas" };
        public static Municipio SANTANDER_VILLANUEVA => new Municipio { Codigo = "68872", Nombre = "Villanueva" };
        public static Municipio SANTANDER_ZAPATOCA => new Municipio { Codigo = "68895", Nombre = "Zapatoca" };
        public static Municipio SUCRE_SINCELEJO => new Municipio { Codigo = "70001", Nombre = "Sincelejo" };
        public static Municipio SUCRE_BUENAVISTA => new Municipio { Codigo = "70110", Nombre = "Buenavista" };
        public static Municipio SUCRE_CAIMITO => new Municipio { Codigo = "70124", Nombre = "Caimito" };
        public static Municipio SUCRE_COLOSO => new Municipio { Codigo = "70204", Nombre = "Colosó" };
        public static Municipio SUCRE_COROZAL => new Municipio { Codigo = "70215", Nombre = "Corozal" };
        public static Municipio SUCRE_COVEÑAS => new Municipio { Codigo = "70221", Nombre = "Coveñas" };
        public static Municipio SUCRE_CHALAN => new Municipio { Codigo = "70230", Nombre = "Chalán" };
        public static Municipio SUCRE_EL_ROBLE => new Municipio { Codigo = "70233", Nombre = "El Roble" };
        public static Municipio SUCRE_GALERAS => new Municipio { Codigo = "70235", Nombre = "Galeras" };
        public static Municipio SUCRE_GUARANDA => new Municipio { Codigo = "70265", Nombre = "Guaranda" };
        public static Municipio SUCRE_LA_UNION => new Municipio { Codigo = "70400", Nombre = "La Unión" };
        public static Municipio SUCRE_LOS_PALMITOS => new Municipio { Codigo = "70418", Nombre = "Los Palmitos" };
        public static Municipio SUCRE_MAJAGUAL => new Municipio { Codigo = "70429", Nombre = "Majagual" };
        public static Municipio SUCRE_MORROA => new Municipio { Codigo = "70473", Nombre = "Morroa" };
        public static Municipio SUCRE_OVEJAS => new Municipio { Codigo = "70508", Nombre = "Ovejas" };
        public static Municipio SUCRE_PALMITO => new Municipio { Codigo = "70523", Nombre = "Palmito" };
        public static Municipio SUCRE_SAMPUES => new Municipio { Codigo = "70670", Nombre = "Sampués" };
        public static Municipio SUCRE_SAN_BENITO_ABAD => new Municipio { Codigo = "70678", Nombre = "San Benito Abad" };
        public static Municipio SUCRE_SAN_JUAN_DE_BETULIA => new Municipio { Codigo = "70702", Nombre = "San Juan De Betulia" };
        public static Municipio SUCRE_SAN_MARCOS => new Municipio { Codigo = "70708", Nombre = "San Marcos" };
        public static Municipio SUCRE_SAN_ONOFRE => new Municipio { Codigo = "70713", Nombre = "San Onofre" };
        public static Municipio SUCRE_SAN_PEDRO => new Municipio { Codigo = "70717", Nombre = "San Pedro" };
        public static Municipio SUCRE_SAN_LUIS_DE_SINCE => new Municipio { Codigo = "70742", Nombre = "San Luis De Sincé" };
        public static Municipio SUCRE_SUCRE => new Municipio { Codigo = "70771", Nombre = "Sucre" };
        public static Municipio SUCRE_SANTIAGO_DE_TOLU => new Municipio { Codigo = "70820", Nombre = "Santiago De Tolú" };
        public static Municipio SUCRE_TOLU_VIEJO => new Municipio { Codigo = "70823", Nombre = "Tolú Viejo" };
        public static Municipio TOLIMA_IBAGUE => new Municipio { Codigo = "73001", Nombre = "Ibagué" };
        public static Municipio TOLIMA_ALPUJARRA => new Municipio { Codigo = "73024", Nombre = "Alpujarra" };
        public static Municipio TOLIMA_ALVARADO => new Municipio { Codigo = "73026", Nombre = "Alvarado" };
        public static Municipio TOLIMA_AMBALEMA => new Municipio { Codigo = "73030", Nombre = "Ambalema" };
        public static Municipio TOLIMA_ANZOATEGUI => new Municipio { Codigo = "73043", Nombre = "Anzoátegui" };
        public static Municipio TOLIMA_ARMERO => new Municipio { Codigo = "73055", Nombre = "Armero" };
        public static Municipio TOLIMA_ATACO => new Municipio { Codigo = "73067", Nombre = "Ataco" };
        public static Municipio TOLIMA_CAJAMARCA => new Municipio { Codigo = "73124", Nombre = "Cajamarca" };
        public static Municipio TOLIMA_CARMEN_DE_APICALA => new Municipio { Codigo = "73148", Nombre = "Carmen De Apicalá" };
        public static Municipio TOLIMA_CASABIANCA => new Municipio { Codigo = "73152", Nombre = "Casabianca" };
        public static Municipio TOLIMA_CHAPARRAL => new Municipio { Codigo = "73168", Nombre = "Chaparral" };
        public static Municipio TOLIMA_COELLO => new Municipio { Codigo = "73200", Nombre = "Coello" };
        public static Municipio TOLIMA_COYAIMA => new Municipio { Codigo = "73217", Nombre = "Coyaima" };
        public static Municipio TOLIMA_CUNDAY => new Municipio { Codigo = "73226", Nombre = "Cunday" };
        public static Municipio TOLIMA_DOLORES => new Municipio { Codigo = "73236", Nombre = "Dolores" };
        public static Municipio TOLIMA_ESPINAL => new Municipio { Codigo = "73268", Nombre = "Espinal" };
        public static Municipio TOLIMA_FALAN => new Municipio { Codigo = "73270", Nombre = "Falan" };
        public static Municipio TOLIMA_FLANDES => new Municipio { Codigo = "73275", Nombre = "Flandes" };
        public static Municipio TOLIMA_FRESNO => new Municipio { Codigo = "73283", Nombre = "Fresno" };
        public static Municipio TOLIMA_GUAMO => new Municipio { Codigo = "73319", Nombre = "Guamo" };
        public static Municipio TOLIMA_HERVEO => new Municipio { Codigo = "73347", Nombre = "Herveo" };
        public static Municipio TOLIMA_HONDA => new Municipio { Codigo = "73349", Nombre = "Honda" };
        public static Municipio TOLIMA_ICONONZO => new Municipio { Codigo = "73352", Nombre = "Icononzo" };
        public static Municipio TOLIMA_LERIDA => new Municipio { Codigo = "73408", Nombre = "Lérida" };
        public static Municipio TOLIMA_LIBANO => new Municipio { Codigo = "73411", Nombre = "Líbano" };
        public static Municipio TOLIMA_SAN_SEBASTIAN_DE_MARIQUITA => new Municipio { Codigo = "73443", Nombre = "San Sebastián De Mariquita" };
        public static Municipio TOLIMA_MELGAR => new Municipio { Codigo = "73449", Nombre = "Melgar" };
        public static Municipio TOLIMA_MURILLO => new Municipio { Codigo = "73461", Nombre = "Murillo" };
        public static Municipio TOLIMA_NATAGAIMA => new Municipio { Codigo = "73483", Nombre = "Natagaima" };
        public static Municipio TOLIMA_ORTEGA => new Municipio { Codigo = "73504", Nombre = "Ortega" };
        public static Municipio TOLIMA_PALOCABILDO => new Municipio { Codigo = "73520", Nombre = "Palocabildo" };
        public static Municipio TOLIMA_PIEDRAS => new Municipio { Codigo = "73547", Nombre = "Piedras" };
        public static Municipio TOLIMA_PLANADAS => new Municipio { Codigo = "73555", Nombre = "Planadas" };
        public static Municipio TOLIMA_PRADO => new Municipio { Codigo = "73563", Nombre = "Prado" };
        public static Municipio TOLIMA_PURIFICACION => new Municipio { Codigo = "73585", Nombre = "Purificación" };
        public static Municipio TOLIMA_RIOBLANCO => new Municipio { Codigo = "73616", Nombre = "Rioblanco" };
        public static Municipio TOLIMA_RONCESVALLES => new Municipio { Codigo = "73622", Nombre = "Roncesvalles" };
        public static Municipio TOLIMA_ROVIRA => new Municipio { Codigo = "73624", Nombre = "Rovira" };
        public static Municipio TOLIMA_SALDAÑA => new Municipio { Codigo = "73671", Nombre = "Saldaña" };
        public static Municipio TOLIMA_SAN_ANTONIO => new Municipio { Codigo = "73675", Nombre = "San Antonio" };
        public static Municipio TOLIMA_SAN_LUIS => new Municipio { Codigo = "73678", Nombre = "San Luis" };
        public static Municipio TOLIMA_SANTA_ISABEL => new Municipio { Codigo = "73686", Nombre = "Santa Isabel" };
        public static Municipio TOLIMA_SUAREZ => new Municipio { Codigo = "73770", Nombre = "Suárez" };
        public static Municipio TOLIMA_VALLE_DE_SAN_JUAN => new Municipio { Codigo = "73854", Nombre = "Valle De San Juan" };
        public static Municipio TOLIMA_VENADILLO => new Municipio { Codigo = "73861", Nombre = "Venadillo" };
        public static Municipio TOLIMA_VILLAHERMOSA => new Municipio { Codigo = "73870", Nombre = "Villahermosa" };
        public static Municipio TOLIMA_VILLARRICA => new Municipio { Codigo = "73873", Nombre = "Villarrica" };
        public static Municipio VALLE_DEL_CAUCA_CALI => new Municipio { Codigo = "76001", Nombre = "Cali" };
        public static Municipio VALLE_DEL_CAUCA_ALCALA => new Municipio { Codigo = "76020", Nombre = "Alcalá" };
        public static Municipio VALLE_DEL_CAUCA_ANDALUCIA => new Municipio { Codigo = "76036", Nombre = "Andalucía" };
        public static Municipio VALLE_DE_CAUCA_ANDALUCIA => new Municipio { Codigo = "76036", Nombre = "Andalucía" };
        public static Municipio VALLE_DEL_CAUCA_ANSERMANUEVO => new Municipio { Codigo = "76041", Nombre = "Ansermanuevo" };
        public static Municipio VALLE_DEL_CAUCA_ARGELIA => new Municipio { Codigo = "76054", Nombre = "Argelia" };
        public static Municipio VALLE_DEL_CAUCA_BOLIVAR => new Municipio { Codigo = "76100", Nombre = "Bolívar" };
        public static Municipio VALLE_DEL_CAUCA_BUENAVENTURA => new Municipio { Codigo = "76109", Nombre = "Buenaventura" };
        public static Municipio VALLE_DEL_CAUCA_GUADALAJARA_DE_BUGA => new Municipio { Codigo = "76111", Nombre = "Guadalajara De Buga" };
        public static Municipio VALLE_DEL_CAUCA_BUGALAGRANDE => new Municipio { Codigo = "76113", Nombre = "Bugalagrande" };
        public static Municipio VALLE_DEL_CAUCA_CAICEDONIA => new Municipio { Codigo = "76122", Nombre = "Caicedonia" };
        public static Municipio VALLE_DEL_CAUCA_CALIMA => new Municipio { Codigo = "76126", Nombre = "Calima" };
        public static Municipio VALLE_DEL_CAUCA_CANDELARIA => new Municipio { Codigo = "76130", Nombre = "Candelaria" };
        public static Municipio VALLE_DEL_CAUCA_CARTAGO => new Municipio { Codigo = "76147", Nombre = "Cartago" };
        public static Municipio VALLE_DEL_CAUCA_DAGUA => new Municipio { Codigo = "76233", Nombre = "Dagua" };
        public static Municipio VALLE_DEL_CAUCA_EL_AGUILA => new Municipio { Codigo = "76243", Nombre = "El Águila" };
        public static Municipio VALLE_DEL_CAUCA_EL_CAIRO => new Municipio { Codigo = "76246", Nombre = "El Cairo" };
        public static Municipio VALLE_DEL_CAUCA_EL_CERRITO => new Municipio { Codigo = "76248", Nombre = "El Cerrito" };
        public static Municipio VALLE_DEL_CAUCA_EL_DOVIO => new Municipio { Codigo = "76250", Nombre = "El Dovio" };
        public static Municipio VALLE_DEL_CAUCA_FLORIDA => new Municipio { Codigo = "76275", Nombre = "Florida" };
        public static Municipio VALLE_DEL_CAUCA_GINEBRA => new Municipio { Codigo = "76306", Nombre = "Ginebra" };
        public static Municipio VALLE_DEL_CAUCA_GUACARI => new Municipio { Codigo = "76318", Nombre = "Guacarí" };
        public static Municipio VALLE_DEL_CAUCA_JAMUNDI => new Municipio { Codigo = "76364", Nombre = "Jamundí" };
        public static Municipio VALLE_DE_CAUCA_JAMUNDI => new Municipio { Codigo = "76364", Nombre = "Jamundí" };
        public static Municipio VALLE_DEL_CAUCA_LA_CUMBRE => new Municipio { Codigo = "76377", Nombre = "La Cumbre" };
        public static Municipio VALLE_DEL_CAUCA_LA_UNION => new Municipio { Codigo = "76400", Nombre = "La Unión" };
        public static Municipio VALLE_DEL_CAUCA_LA_VICTORIA => new Municipio { Codigo = "76403", Nombre = "La Victoria" };
        public static Municipio VALLE_DEL_CAUCA_OBANDO => new Municipio { Codigo = "76497", Nombre = "Obando" };
        public static Municipio VALLE_DEL_CAUCA_PALMIRA => new Municipio { Codigo = "76520", Nombre = "Palmira" };
        public static Municipio VALLE_DEL_CAUCA_PRADERA => new Municipio { Codigo = "76563", Nombre = "Pradera" };
        public static Municipio VALLE_DEL_CAUCA_RESTREPO => new Municipio { Codigo = "76606", Nombre = "Restrepo" };
        public static Municipio VALLE_DEL_CAUCA_RIOFRIO => new Municipio { Codigo = "76616", Nombre = "Riofrío" };
        public static Municipio VALLE_DEL_CAUCA_ROLDANILLO => new Municipio { Codigo = "76622", Nombre = "Roldanillo" };
        public static Municipio VALLE_DEL_CAUCA_SAN_PEDRO => new Municipio { Codigo = "76670", Nombre = "San Pedro" };
        public static Municipio VALLE_DEL_CAUCA_SEVILLA => new Municipio { Codigo = "76736", Nombre = "Sevilla" };
        public static Municipio VALLE_DEL_CAUCA_TORO => new Municipio { Codigo = "76823", Nombre = "Toro" };
        public static Municipio VALLE_DEL_CAUCA_TRUJILLO => new Municipio { Codigo = "76828", Nombre = "Trujillo" };
        public static Municipio VALLE_DEL_CAUCA_TULUA => new Municipio { Codigo = "76834", Nombre = "Tuluá" };
        public static Municipio VALLE_DEL_CAUCA_ULLOA => new Municipio { Codigo = "76845", Nombre = "Ulloa" };
        public static Municipio VALLE_DEL_CAUCA_VERSALLES => new Municipio { Codigo = "76863", Nombre = "Versalles" };
        public static Municipio VALLE_DEL_CAUCA_VIJES => new Municipio { Codigo = "76869", Nombre = "Vijes" };
        public static Municipio VALLE_DEL_CAUCA_YOTOCO => new Municipio { Codigo = "76890", Nombre = "Yotoco" };
        public static Municipio VALLE_DEL_CAUCA_YUMBO => new Municipio { Codigo = "76892", Nombre = "Yumbo" };
        public static Municipio VALLE_DEL_CAUCA_ZARZAL => new Municipio { Codigo = "76895", Nombre = "Zarzal" };
        public static Municipio ARAUCA_ARAUCA => new Municipio { Codigo = "81001", Nombre = "Arauca" };
        public static Municipio ARAUCA_ARAUQUITA => new Municipio { Codigo = "81065", Nombre = "Arauquita" };
        public static Municipio ARAUCA_CRAVO_NORTE => new Municipio { Codigo = "81220", Nombre = "Cravo Norte" };
        public static Municipio ARAUCA_FORTUL => new Municipio { Codigo = "81300", Nombre = "Fortul" };
        public static Municipio ARAUCA_PUERTO_RONDON => new Municipio { Codigo = "81591", Nombre = "Puerto Rondón" };
        public static Municipio ARAUCA_SARAVENA => new Municipio { Codigo = "81736", Nombre = "Saravena" };
        public static Municipio ARAUCA_TAME => new Municipio { Codigo = "81794", Nombre = "Tame" };
        public static Municipio CASANARE_YOPAL => new Municipio { Codigo = "85001", Nombre = "Yopal" };
        public static Municipio CASANARE_AGUAZUL => new Municipio { Codigo = "85010", Nombre = "Aguazul" };
        public static Municipio CASANARE_CHAMEZA => new Municipio { Codigo = "85015", Nombre = "Chámeza" };
        public static Municipio CASANARE_HATO_COROZAL => new Municipio { Codigo = "85125", Nombre = "Hato Corozal" };
        public static Municipio CASANARE_LA_SALINA => new Municipio { Codigo = "85136", Nombre = "La Salina" };
        public static Municipio CASANARE_MANI => new Municipio { Codigo = "85139", Nombre = "Maní" };
        public static Municipio CASANARE_MONTERREY => new Municipio { Codigo = "85162", Nombre = "Monterrey" };
        public static Municipio CASANARE_NUNCHIA => new Municipio { Codigo = "85225", Nombre = "Nunchía" };
        public static Municipio CASANARE_OROCUE => new Municipio { Codigo = "85230", Nombre = "Orocué" };
        public static Municipio CASANARE_PAZ_DE_ARIPORO => new Municipio { Codigo = "85250", Nombre = "Paz De Ariporo" };
        public static Municipio CASANARE_PORE => new Municipio { Codigo = "85263", Nombre = "Pore" };
        public static Municipio CASANARE_RECETOR => new Municipio { Codigo = "85279", Nombre = "Recetor" };
        public static Municipio CASANARE_SABANALARGA => new Municipio { Codigo = "85300", Nombre = "Sabanalarga" };
        public static Municipio CASANARE_SACAMA => new Municipio { Codigo = "85315", Nombre = "Sácama" };
        public static Municipio CASANARE_SAN_LUIS_DE_PALENQUE => new Municipio { Codigo = "85325", Nombre = "San Luis De Palenque" };
        public static Municipio CASANARE_TAMARA => new Municipio { Codigo = "85400", Nombre = "Támara" };
        public static Municipio CASANARE_TAURAMENA => new Municipio { Codigo = "85410", Nombre = "Tauramena" };
        public static Municipio CASANARE_TRINIDAD => new Municipio { Codigo = "85430", Nombre = "Trinidad" };
        public static Municipio CASANARE_VILLANUEVA => new Municipio { Codigo = "85440", Nombre = "Villanueva" };
        public static Municipio PUTUMAYO_MOCOA => new Municipio { Codigo = "86001", Nombre = "Mocoa" };
        public static Municipio PUTUMAYO_COLON => new Municipio { Codigo = "86219", Nombre = "Colón" };
        public static Municipio PUTUMAYO_ORITO => new Municipio { Codigo = "86320", Nombre = "Orito" };
        public static Municipio PUTUMAYO_PUERTO_ASIS => new Municipio { Codigo = "86568", Nombre = "Puerto Asís" };
        public static Municipio PUTUMAYO_PUERTO_CAICEDO => new Municipio { Codigo = "86569", Nombre = "Puerto Caicedo" };
        public static Municipio PUTUMAYO_PUERTO_GUZMAN => new Municipio { Codigo = "86571", Nombre = "Puerto Guzmán" };
        public static Municipio PUTUMAYO_PUERTO_LEGUIZAMO => new Municipio { Codigo = "86573", Nombre = "Puerto Leguízamo" };
        public static Municipio PUTUMAYO_SIBUNDOY => new Municipio { Codigo = "86749", Nombre = "Sibundoy" };
        public static Municipio PUTUMAYO_SAN_FRANCISCO => new Municipio { Codigo = "86755", Nombre = "San Francisco" };
        public static Municipio PUTUMAYO_SAN_MIGUEL => new Municipio { Codigo = "86757", Nombre = "San Miguel" };
        public static Municipio PUTUMAYO_SANTIAGO => new Municipio { Codigo = "86760", Nombre = "Santiago" };
        public static Municipio PUTUMAYO_VALLE_DEL_GUAMUEZ => new Municipio { Codigo = "86865", Nombre = "Valle Del Guamuez" };
        public static Municipio PUTUMAYO_VILLAGARZON => new Municipio { Codigo = "86885", Nombre = "Villagarzón" };
        public static Municipio ARCHIPIELAGO_DE_SAN_ANDRES_PROVIDENCIA_Y_SANTA_CATALINA_SAN_ANDRES => new Municipio { Codigo = "88001", Nombre = "San Andrés" };
        public static Municipio ARCHIPIELAGO_DE_SAN_ANDRES_PROVIDENCIA_Y_SANTA_CATALINA_PROVIDENCIA => new Municipio { Codigo = "88564", Nombre = "Providencia" };
        public static Municipio AMAZONAS_LETICIA => new Municipio { Codigo = "91001", Nombre = "Leticia" };
        public static Municipio AMAZONAS_EL_ENCANTO => new Municipio { Codigo = "91263", Nombre = "El Encanto" };
        public static Municipio AMAZONAS_LA_CHORRERA => new Municipio { Codigo = "91405", Nombre = "La Chorrera" };
        public static Municipio AMAZONAS_LA_PEDRERA => new Municipio { Codigo = "91407", Nombre = "La Pedrera" };
        public static Municipio AMAZONAS_LA_VICTORIA => new Municipio { Codigo = "91430", Nombre = "La Victoria" };
        public static Municipio AMAZONAS_MIRITI__PARANA => new Municipio { Codigo = "91460", Nombre = "Mirití – Paraná" };
        public static Municipio AMAZONAS_PUERTO_ALEGRIA => new Municipio { Codigo = "91530", Nombre = "Puerto Alegría" };
        public static Municipio AMAZONAS_PUERTO_ARICA => new Municipio { Codigo = "91536", Nombre = "Puerto Arica" };
        public static Municipio AMAZONAS_PUERTO_NARIÑO => new Municipio { Codigo = "91540", Nombre = "Puerto Nariño" };
        public static Municipio AMAZONAS_PUERTO_SANTANDER => new Municipio { Codigo = "91669", Nombre = "Puerto Santander" };
        public static Municipio AMAZONAS_TARAPACA => new Municipio { Codigo = "91798", Nombre = "Tarapacá" };
        public static Municipio GUAINIA_INIRIDA => new Municipio { Codigo = "94001", Nombre = "Inírida" };
        public static Municipio GUAINIA_BARRANCO_MINAS => new Municipio { Codigo = "94343", Nombre = "Barranco Minas" };
        public static Municipio GUAINIA_MAPIRIPANA => new Municipio { Codigo = "94663", Nombre = "Mapiripana" };
        public static Municipio GUAINIA_SAN_FELIPE => new Municipio { Codigo = "94883", Nombre = "San Felipe" };
        public static Municipio GUAINIA_PUERTO_COLOMBIA => new Municipio { Codigo = "94884", Nombre = "Puerto Colombia" };
        public static Municipio GUAINIA_LA_GUADALUPE => new Municipio { Codigo = "94885", Nombre = "La Guadalupe" };
        public static Municipio GUAINIA_CACAHUAL => new Municipio { Codigo = "94886", Nombre = "Cacahual" };
        public static Municipio GUAINIA_PANA_PANA => new Municipio { Codigo = "94887", Nombre = "Pana Pana" };
        public static Municipio GUAINIA_MORICHAL => new Municipio { Codigo = "94888", Nombre = "Morichal" };
        public static Municipio GUAVIARE_SAN_JOSE_DEL_GUAVIARE => new Municipio { Codigo = "95001", Nombre = "San José Del Guaviare" };
        public static Municipio GUAVIARE_CALAMAR => new Municipio { Codigo = "95015", Nombre = "Calamar" };
        public static Municipio GUAVIARE_EL_RETORNO => new Municipio { Codigo = "95025", Nombre = "El Retorno" };
        public static Municipio GUAVIARE_MIRAFLORES => new Municipio { Codigo = "95200", Nombre = "Miraflores" };
        public static Municipio VAUPES_MITU => new Municipio { Codigo = "97001", Nombre = "Mitú" };
        public static Municipio VAUPES_CARURU => new Municipio { Codigo = "97161", Nombre = "Carurú" };
        public static Municipio VAUPES_PACOA => new Municipio { Codigo = "97511", Nombre = "Pacoa" };
        public static Municipio VAUPES_TARAIRA => new Municipio { Codigo = "97666", Nombre = "Taraira" };
        public static Municipio VAUPES_PAPUNAHUA => new Municipio { Codigo = "97777", Nombre = "Papunahua" };
        public static Municipio VAUPES_YAVARATE => new Municipio { Codigo = "97889", Nombre = "Yavaraté" };
        public static Municipio VICHADA_PUERTO_CARREÑO => new Municipio { Codigo = "99001", Nombre = "Puerto Carreño" };
        public static Municipio VICHADA_LA_PRIMAVERA => new Municipio { Codigo = "99524", Nombre = "La Primavera" };
        public static Municipio VICHADA_SANTA_ROSALIA => new Municipio { Codigo = "99624", Nombre = "Santa Rosalía" };
        public static Municipio VICHADA_CUMARIBO => new Municipio { Codigo = "99773", Nombre = "Cumaribo" };
    }

    public class DireccionFisica
    {
        public Departamento Departamento { get; set; }
        public Municipio Municipio { get; set; }
        public string Linea { get; set; }
        public string CodigoPostal { get; set; } = "N/A";
        public Pais Pais { get; set; } = Pais.COLOMBIA;
    }


    public class ReferenciaDocumento
    {
        public string Numero { get; set; }
        public string UUID { get; set; }
        public AlgoritmoSeguridadUUID AlgoritmoCufe { get; set; }
        public TipoDocumentoElectronico TipoDocumento { get; set; }
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Indica si se refiere a una factura.
        /// </summary>
        public bool EsFactura
        {
            get
            {
                return (TipoDocumento.Codigo == TipoDocumentoElectronico.FACTURA_VENTA_NACIONAL.Codigo ||
                    TipoDocumento.Codigo == TipoDocumentoElectronico.FACTURA_EXPORTACION.Codigo ||
                    TipoDocumento.Codigo == TipoDocumentoElectronico.FACTURA_CONTINGENCIA_FACTURADOR.Codigo);
            }
        }

        /// <summary>
        /// Indica si se refiere a una nota de crédito.
        /// </summary>
        public bool EsNotaCredito
        {
            get
            {
                return (TipoDocumento.Codigo == TipoDocumentoElectronico.NOTA_CREDITO.Codigo);
            }
        }

        /// <summary>
        /// Indica si se refiere a una nota de débito.
        /// </summary>
        public bool EsNotaDebito
        {
            get
            {
                return (TipoDocumento.Codigo == TipoDocumentoElectronico.NOTA_DEBITO.Codigo);
            }
        }
    }

    public class ResponsabilidadFiscal
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public bool Obsoleto { get; set; }

        public static readonly Dictionary<string, ResponsabilidadFiscal> _tiposMap = new Dictionary<string, ResponsabilidadFiscal>
        {
            { "OTROS_OBLIGADO", new ResponsabilidadFiscal { Codigo = "O-99", Descripcion = "Otro tipo de obligado", Obsoleto = true } },

            { "GRAN_CONTRIBUYENTE", new ResponsabilidadFiscal { Codigo = "O-13", Descripcion = "Gran contribuyente"  } },
            { "AUTORRETENEDOR", new ResponsabilidadFiscal { Codigo = "O-15", Descripcion = "Autorretenedor"  } },
            { "AGENTE_RETENCION_IVA", new ResponsabilidadFiscal { Codigo = "O-23", Descripcion = "Agente de retención IVA"  } },
            { "REGIMEN_SIMPLE_TRIBUTACION", new ResponsabilidadFiscal { Codigo = "O-47", Descripcion = "Régimen simple de tributación" } },

            { "IMPUESTO_SOBRE_VENTAS", new ResponsabilidadFiscal { Codigo = "O-48", Descripcion = "Impuesto sobre las ventas", Obsoleto = true } },
            { "NO_RESPONSABLE_IVA", new ResponsabilidadFiscal { Codigo = "O-49", Descripcion = "No responsable de IVA", Obsoleto = true } },
            { "NO_APLICA", new ResponsabilidadFiscal { Codigo = "ZZ", Descripcion = "No aplica", Obsoleto = true } },

            { "NO_RESPONSABLE", new ResponsabilidadFiscal { Codigo = "R-99-PN", Descripcion = "No aplica – Otros" } },
        };

        public static ResponsabilidadFiscal GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de ResponsabilidadFiscal no válido", nameof(codigo));
            }
        }

    }



    // Emisor del documento Electrónico
    public class Emisor
    {
        [JsonIgnore]
        public bool EsPersonaJuridica => this.TipoPersona == TipoPersona.JURIDICA;
        [JsonIgnore]
        public bool EsPersonaNatural => this.TipoPersona == TipoPersona.NATURAL;
        public TipoPersona TipoPersona { get; set; }
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string DV { get; set; }
        /// <summary>
        /// Nombres + Apellidos si es PERSONA_NATURAL
        /// </summary>
        
        // public string Nombre { get; set; }  // Creo que Nombre y RazonSocial se pueden unificar, porque el atributo que define si es persona NATURAL o JURIDICA es Tipo Persona, pero de eso me ocuparé mas adelante
        /// <summary>
        /// Razón Social o Nombre Fiscal de la empresa, si es PERSONA_JURIDICA
        /// </summary>
        public string RazonSocial { get; set; } // Creo que este atributo se puede unifcar con Nombre, pero lo recisaré mas adelante
        public DireccionFisica DireccionFiscal { get; set; }
        public string NumeroTelefonico { get; set; }
        public string CorreoElectronico { get; set; }



        public RegimenFiscal RegimenFiscal { get; set; } = RegimenFiscal.Default; // .GetFromCodigo("R-99-PN");
        //public ResponsabilidadFiscal ResponsabilidadFiscal { get; set; } = ResponsabilidadFiscal.GetFromCodigo("REGIMEN_SIMPLE_TRIBUTACION");
        public ResponsabilidadTributaria ResponsabilidadTributaria { get; set; } = ResponsabilidadTributaria.GetFromCodigo("01");
        public string CodigoCiiu { get; set; }
        
        // Solo se usa si el emisor es PERSONA NATURAL
        public Persona Persona { get; set; }

        // INFORMACION DEL ESTABLECIMIENTO DE COMERCIO 

        public DatosComerciales DatosComerciales { get; set; }

        //public string MatriculaMercantil { get; set; } = "00000";
        public DireccionFisica DireccionComercial { get; set; }
        public string PrefijoRangoNumeracion { get; set; }


    }

    public class Proveedor
    {
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string Nombre { get; set; }
        public string RazonSocial { get; set; }
        public DireccionFisica DireccionFisica { get; set; }
        public TipoPersona TipoPersona { get; set; }
        public TipoProcedencia TipoProcedencia { get; set; } = TipoProcedencia.RESIDENTE;
        public RegimenFiscal RegimenFiscal { get; set; }
        //public ResponsabilidadFiscal ResponsabilidadFiscal { get; set; } = ResponsabilidadFiscal.GetFromCodigo("REGIMEN_SIMPLE_TRIBUTACION");
        public ResponsabilidadTributaria ResponsabilidadTributaria { get; set; } = ResponsabilidadTributaria.GetFromCodigo("ZZ");
        public string CodigoCiiu { get; set; }
        public string NumeroTelefonico { get; set; }
        public string CorreoElectronico { get; set; }
        public Persona Persona { get; set; }

    }


    /*
    public class TipoIdentificacion
    {
        public string Codigo { get; set; }

        [Obsolete("Opción obsoleta desde la versión 1.9")]
        public static TipoIdentificacion NO_OBLIGADO => new TipoIdentificacion { Codigo = "R-00-PN" };
        public static TipoIdentificacion REGISTRO_CIVIL => new TipoIdentificacion { Codigo = "11" };
        public static TipoIdentificacion TARJETA_IDENTIDAD => new TipoIdentificacion { Codigo = "12" };
        public static TipoIdentificacion CEDULA_CIUDADANIA => new TipoIdentificacion { Codigo = "13" };
        public static TipoIdentificacion TARJETA_EXTRANJERIA => new TipoIdentificacion { Codigo = "21" };
        public static TipoIdentificacion CEDULA_EXTRANJERIA => new TipoIdentificacion { Codigo = "22" };
        public static TipoIdentificacion NIT => new TipoIdentificacion { Codigo = "31" };
        public static TipoIdentificacion PASAPORTE => new TipoIdentificacion { Codigo = "41" };
        public static TipoIdentificacion DOCUMENTO_IDENTIFICACION_EXTRANJERO => new TipoIdentificacion { Codigo = "42" };
        public static TipoIdentificacion PERMISO_ESPECIAL_PERMANENCIA => new TipoIdentificacion { Codigo = "47" };
        public static TipoIdentificacion PERMISO_PROTECCION_TEMPORAL => new TipoIdentificacion { Codigo = "48" };
        public static TipoIdentificacion NIT_EXTRANJERO => new TipoIdentificacion { Codigo = "50" };
        public static TipoIdentificacion NUIP => new TipoIdentificacion { Codigo = "91" };
    }
    */

    public class TipoIdentificacion
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public string TAG { get; set; }

        private static readonly Dictionary<string, TipoIdentificacion> _tiposMap = new Dictionary<string, TipoIdentificacion>
        {
            { "R-00-PN", new TipoIdentificacion { Codigo = "R-00-PN", Descripcion = "NO OBLIGADO", TAG = "NO_OBLIGADO" } },
            { "11", new TipoIdentificacion { Codigo = "11", Descripcion = "REGISTRO CIVIL", TAG = "REGISTRO_CIVIL" } },
            { "12", new TipoIdentificacion { Codigo = "12", Descripcion = "TARJETA DE IDENTIDAD", TAG = "TARJETA_IDENTIDAD" } },
            { "13", new TipoIdentificacion { Codigo = "13", Descripcion = "CEDULA DE CIUDADANIA", TAG = "CEDULA_CIUDADANIA" } },
            { "21", new TipoIdentificacion { Codigo = "21", Descripcion = "TARJETA DE EXTRANJERIA", TAG = "TARJETA_EXTRANJERIA" } },
            { "22", new TipoIdentificacion { Codigo = "22", Descripcion = "CEDULA DE EXTRANJERIA", TAG = "CEDULA_EXTRANJERIA" } },
            { "31", new TipoIdentificacion { Codigo = "31", Descripcion = "NIT", TAG = "NIT" } },
            { "41", new TipoIdentificacion { Codigo = "41", Descripcion = "PASAPORTE",TAG = "PASAPORTE" } },
            { "42", new TipoIdentificacion { Codigo = "42", Descripcion = "DOCUMENTO DE IDENTIFICACION EXTRANJERO", TAG = "DOCUMENTO_IDENTIFICACION_EXTRANJERO" } },
            { "47", new TipoIdentificacion { Codigo = "47", Descripcion = "PERMISO ESPECIA DE PERMANENCIA", TAG = "PERMISO_ESPECIAL_PERMANENCIA" } },
            { "48", new TipoIdentificacion { Codigo = "48", Descripcion = "PERMISO ESPECIA DE PERMANENCIA", TAG = "PERMISO_PROTECION_TEMPORAL" } },
            { "50", new TipoIdentificacion { Codigo = "50", Descripcion = "NIT EXTRANJERO", TAG = "NIT_EXTRANJERO" } },
            { "91", new TipoIdentificacion { Codigo = "91", Descripcion = "NUIP", TAG = "NUIP" } }
        };

        public static TipoIdentificacion GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de TipoIdentificacion no válido", nameof(codigo));
            }
        }

    }


    public class Adquiriente
    {
        public bool EsPersonaJuridica => this.TipoPersona == TipoPersona.JURIDICA;
        public bool EsPersonaNatural => this.TipoPersona == TipoPersona.NATURAL;
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string RazonSocial { get; set; }
        public string CodigoCliente { get; set; }
        public string NombreComercial { get; set; }
        public string NumeroContrato { get; set; }
        public DireccionFisica DireccionComercial { get; set; }
        public DireccionFisica DireccionFiscal { get; set; }

        public TipoPersona TipoPersona { get; set; }
        public RegimenFiscal RegimenFiscal { get; set; } = RegimenFiscal.GetFromCodigo("R-99-PN");
        //public ResponsabilidadFiscal ResponsabilidadFiscal { get; set; } = ResponsabilidadFiscal.GetFromCodigo("REGIMEN_SIMPLE_TRIBUTACION");
        public ResponsabilidadTributaria ResponsabilidadTributaria { get; set; } = ResponsabilidadTributaria.GetFromCodigo("01");
        public string CodigoCiiu { get; set; }
        public string MatriculaMercantil { get; set; } = "00000";
        public string NumeroTelefonico { get; set; }
        public string CorreoElectronico { get; set; }
        public Persona Persona { get; set; }
    }

    public class Persona
    {
        public DocumentoIdentificacion DocumentoIdentificacion { get; set; }
        public string NumeroIdentificacion { get; set; }
        public string PrimerNombre { get; set; }
        public string SegundoNombre { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }
        /// <summary>
        /// No es igual a DireccionComercial, este campo se usa para reportar la dirección de residencia de un ABS que no tiene un establecimiento de comercio o la dorección de residencia de un comerciante que es PERSONA NATURAL (Opcional) 
        /// </summary>
        public DireccionFisica DireccionResidencia { get; set; }
    }

    public class DocumentoIdentificacion
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string TipoFiscal { get; set; }


        public static readonly Dictionary<string, DocumentoIdentificacion> _tiposMap = new Dictionary<string, DocumentoIdentificacion>
        {
            { "RC", new DocumentoIdentificacion { Codigo = "RC", Nombre = "Registro civil de nacimiento", TipoFiscal = "11" } },
            { "TI", new DocumentoIdentificacion { Codigo = "TI", Nombre = "Tarjeta de identidad", TipoFiscal = "12" }},
            { "CC", new DocumentoIdentificacion { Codigo = "CC", Nombre = "Cédula de ciudadanía", TipoFiscal = "13" }},
            { "CE", new DocumentoIdentificacion { Codigo = "CE", Nombre = "Cédula de extranjería", TipoFiscal = "22" }},
            { "PP", new DocumentoIdentificacion { Codigo = "PP", Nombre = "Pasaporte", TipoFiscal = "41" }},
            { "PE", new DocumentoIdentificacion { Codigo = "PE", Nombre = "Permiso especial de permanencia", TipoFiscal = "47" }},
            { "CN", new DocumentoIdentificacion { Codigo = "CN", Nombre = "Certificado de nacido vivo", TipoFiscal = "91" }},
            { "AS", new DocumentoIdentificacion { Codigo = "AS", Nombre = "Adulto sin identificar", TipoFiscal = "91" }},
            { "MS", new DocumentoIdentificacion { Codigo = "MS", Nombre = "Menor sin identificar", TipoFiscal = "91" }},
            { "DE", new DocumentoIdentificacion { Codigo = "DE", Nombre = "Documento extranjero", TipoFiscal = "91" }},
            { "SI", new DocumentoIdentificacion { Codigo = "SI", Nombre = "Sin identificación", TipoFiscal = "91" }},
            { "CD", new DocumentoIdentificacion { Codigo = "CD", Nombre = "Carné diplomático", TipoFiscal = "91" }},
            { "SC", new DocumentoIdentificacion { Codigo = "SC", Nombre = "Salvoconducto", TipoFiscal = "91" }}
        };

        public static DocumentoIdentificacion GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException($"Código de DocumentoIdentificacion no válido [{codigo}]", nameof(codigo));
            }
        }

        public static DocumentoIdentificacion GetFromTipoFiscal(string codigo)
        {
            try
            {
                return _tiposMap.Values.FirstOrDefault(k => k.TipoFiscal == codigo);
            }
            catch (Exception)
            {
                //throw;
                throw new ArgumentException($"Código de TipoFiscal no válido [{codigo}]", nameof(codigo));
            }
        }
        public static DocumentoIdentificacion REGISTRO_CIVIL_NACIMIENTO => new DocumentoIdentificacion { Codigo = "RC", Nombre = "Registro civil de nacimiento", TipoFiscal = "11" };
        public static DocumentoIdentificacion TARJETA_IDENTIDAD => new DocumentoIdentificacion { Codigo = "TI", Nombre = "Tarjeta de identidad", TipoFiscal = "12" };
        public static DocumentoIdentificacion CEDULA_CIUDADANIA => new DocumentoIdentificacion { Codigo = "CC", Nombre = "Cédula de ciudadanía", TipoFiscal = "13" };
        public static DocumentoIdentificacion CEDULA_EXTRANJERIA => new DocumentoIdentificacion { Codigo = "CE", Nombre = "Cédula de extranjería", TipoFiscal = "22" };
        public static DocumentoIdentificacion PASAPORTE => new DocumentoIdentificacion { Codigo = "PP", Nombre = "Pasaporte", TipoFiscal = "41" };
        public static DocumentoIdentificacion PERMISO_ESPECIAL_PERMANENCIA => new DocumentoIdentificacion { Codigo = "PE", Nombre = "Permiso especial de permanencia", TipoFiscal = "47" };
        public static DocumentoIdentificacion CRTIFICADO_NACIDO_VIVO => new DocumentoIdentificacion { Codigo = "CN", Nombre = "Certificado de nacido vivo", TipoFiscal = "91" };
        public static DocumentoIdentificacion ADULTO_SIN_IDENTIFICAR => new DocumentoIdentificacion { Codigo = "AS", Nombre = "Adulto sin identificar", TipoFiscal = "91" };
        public static DocumentoIdentificacion MENOR_SIN_IDENTIFICAR => new DocumentoIdentificacion { Codigo = "MS", Nombre = "Menor sin identificar", TipoFiscal = "91" };
        public static DocumentoIdentificacion DOCUMENTO_EXTRANJERO => new DocumentoIdentificacion { Codigo = "DE", Nombre = "Documento extranjero", TipoFiscal = "91" };
        public static DocumentoIdentificacion SIN_IDENTIFICACION => new DocumentoIdentificacion { Codigo = "SI", Nombre = "Sin identificación", TipoFiscal = "91" };
        public static DocumentoIdentificacion CARNE_DIPLOMATICO => new DocumentoIdentificacion { Codigo = "CD", Nombre = "Carné diplomático", TipoFiscal = "91" };
        public static DocumentoIdentificacion SALVOCONDUCTO => new DocumentoIdentificacion { Codigo = "SC", Nombre = "Salvoconducto", TipoFiscal = "91" };
    }

    public enum FormaPago { CONTADO = 1, CREDITO = 2 }

    //public class FormaPago
    //{
    //    public string Codigo { get; set; }

    //    public static FormaPago CONTADO => new FormaPago { Codigo = "1" };
    //    public static FormaPago CREDITO => new FormaPago { Codigo = "2" };
    //}

    public class MetodoPago
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        public static readonly Dictionary<string, MetodoPago> _tiposMap = new Dictionary<string, MetodoPago>
        {
            { "1", new MetodoPago { Codigo = "1", Descripcion = "NO_DEFINIDO" } },
            { "10", new MetodoPago { Codigo = "10", Descripcion = "EFECTIVO" } },
            { "20", new MetodoPago { Codigo = "20", Descripcion = "CHEQUE" } },
            { "30", new MetodoPago { Codigo = "30", Descripcion = "TRANSFERENCIA_CREDITO" } },
            { "31", new MetodoPago { Codigo = "31", Descripcion = "TRANSFERENCIA_DEBITO"  } },
            { "34", new MetodoPago { Codigo = "34", Descripcion = "PAGO_DEPOSITO_PREACORDADO" } },
            { "42", new MetodoPago { Codigo = "42", Descripcion = "CONSIGNACION_BANCARIA" } },
            { "48", new MetodoPago { Codigo = "48", Descripcion = "TARJETA_CREDITO" } },
            { "49", new MetodoPago { Codigo = "49", Descripcion = "TARJETA_DEBITO" } },
            { "50", new MetodoPago { Codigo = "50", Descripcion = "POSTGIRO" } },
            { "87", new MetodoPago { Codigo = "97", Descripcion = "ACUERDO_MUTUO" } },
            { "ZZZ", new MetodoPago { Codigo = "ZZZ", Descripcion = "OTRO" } }
        };

        public static MetodoPago GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException($"Código de MetodoPago no válido [{codigo}]", nameof(codigo));
            }
        }


        //public static MetodoPago NO_DEFINIDO => new MetodoPago { Codigo = "1" };
        //public static MetodoPago EFECTIVO => new MetodoPago { Codigo = "10" };
        //public static MetodoPago CHEQUE => new MetodoPago { Codigo = "20" };
        //public static MetodoPago TRANSFERENCIA_CREDITO => new MetodoPago { Codigo = "30" };
        //public static MetodoPago TRANSFERENCIA_DEBITO => new MetodoPago { Codigo = "31" };
        //public static MetodoPago PAGO_DEPOSITO_PREACORDADO => new MetodoPago { Codigo = "34" };
        //public static MetodoPago CONSIGNACION_BANCARIA => new MetodoPago { Codigo = "42" };
        //public static MetodoPago TARJETA_CREDITO => new MetodoPago { Codigo = "48" };
        //public static MetodoPago TARJETA_DEBITO => new MetodoPago { Codigo = "49" };
        //public static MetodoPago POSTGIRO => new MetodoPago { Codigo = "50" };
        //public static MetodoPago ACUERDO_MUTUO => new MetodoPago { Codigo = "97" };
        //public static MetodoPago OTRO => new MetodoPago { Codigo = "ZZZ" };

    }

    public class DetallePago
    {
        public FormaPago Forma { get; set; }
        public MetodoPago Metodo { get; set; }

        /// <summary>
        /// Si es a crédito, debe ser la fecha de vencimiento.
        /// </summary>
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Identificador (interno) del pago.
        /// </summary>
        public string Identificador { get; set; } = "NP" + DateTime.Now.Ticks.ToString();
    }

    public class CondicionEntrega
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        public static CondicionEntrega COSTO_Y_FLETE => new CondicionEntrega
        {
            Codigo = "CFR",
            Descripcion = "Costo y flete"
        };
        public static CondicionEntrega COSTO_FLETE_Y_SEGURO => new CondicionEntrega
        {
            Codigo = "CIF",
            Descripcion = "Costo, flete y seguro"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega TRANSPORTE_Y_SEGURO_PAGADO => new CondicionEntrega
        {
            Codigo = "CIP",
            Descripcion = "Transporte y seguro pagado"
        };
        public static CondicionEntrega TRANSPORTE_Y_SEGURO_PAGADO_HASTA => new CondicionEntrega
        {
            Codigo = "CIP",
            Descripcion = "Transporte y Seguro Pagados hasta"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega TRANSPORTE_PAGADO => new CondicionEntrega
        {
            Codigo = "CPT",
            Descripcion = "Transporte pagado"
        };
        public static CondicionEntrega TRANSPORTE_PAGADO_HASTA => new CondicionEntrega
        {
            Codigo = "CPT",
            Descripcion = "Transporte Pagado Hasta"
        };
        public static CondicionEntrega ENTREGADO_EN_DETERMINADO_LUGAR => new CondicionEntrega
        {
            Codigo = "DAP",
            Descripcion = "Entregado en determinado lugar"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega ENTREGADO_EN_TERMINAL => new CondicionEntrega
        {
            Codigo = "DAT",
            Descripcion = "Entregado en terminal"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega ENTREGADO_CON_ARANCELES_PAGADOS => new CondicionEntrega
        {
            Codigo = "DDP",
            Descripcion = "Entregado con aranceles pagados"
        };
        public static CondicionEntrega ENTREGADO_CON_PAGOS_DE_DERECHOS => new CondicionEntrega
        {
            Codigo = "DDP",
            Descripcion = "Entregado con Pago de Derechos"
        };
        public static CondicionEntrega ENTREGADO_LUGAR_DESCARGA => new CondicionEntrega
        {
            Codigo = "DPU",
            Descripcion = "Entregada en Lugar Descargada"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega EN_TIENDA => new CondicionEntrega
        {
            Codigo = "EXW",
            Descripcion = "En tienda"
        };
        public static CondicionEntrega EN_FABRICA => new CondicionEntrega
        {
            Codigo = "EXW",
            Descripcion = "En Fábrica"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega GRATIS_JUNTO_AL_BARCO => new CondicionEntrega
        {
            Codigo = "FAS",
            Descripcion = "Gratis junto al barco"
        };
        public static CondicionEntrega FRANCO_COSTADO_DEL_BUQUE => new CondicionEntrega
        {
            Codigo = "FAS",
            Descripcion = "Franco al costado del buque"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega TRANSPORTISTA_GRATUITO => new CondicionEntrega
        {
            Codigo = "FCA",
            Descripcion = "Transportista gratuito"
        };
        public static CondicionEntrega FRANCO_TRANSPORTISTA => new CondicionEntrega
        {
            Codigo = "FCA",
            Descripcion = "Franco transportista"
        };
        [Obsolete("Obsoleto desde la versión 1.9", false)]
        public static CondicionEntrega GRATUITO_A_BORDO => new CondicionEntrega
        {
            Codigo = "FOB",
            Descripcion = "Gratuito a bordo"
        };
        public static CondicionEntrega FRANCO_A_BORDO => new CondicionEntrega
        {
            Codigo = "FOB",
            Descripcion = "Franco a bordo"
        };
    }

    public class TerminosEntrega
    {
        public string TerminosEspeciales { get; set; }
        public CondicionEntrega Condicion { get; set; }
    }

    public class Cargo
    {
        public string Razon { get; set; }
        public decimal Porcentaje { get; set; }
        public decimal Monto { get; set; }
        public decimal Base { get; set; }
    }

    public class TipoDescuento
    {
        public string Codigo { get; set; }

        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento IMPUESTO_ASUMIDO => new TipoDescuento { Codigo = "00" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento PAGUE_UNO_LLEVE_OTRO => new TipoDescuento { Codigo = "01" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento CONTRACTUAL => new TipoDescuento { Codigo = "02" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento PRONTO_PAGO => new TipoDescuento { Codigo = "03" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento ENVIO_GRATIS => new TipoDescuento { Codigo = "04" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento ESPECIFICO_POR_INVENTARIO => new TipoDescuento { Codigo = "05" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento MONTO_DE_COMPRAS => new TipoDescuento { Codigo = "06" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento TEMPORADA => new TipoDescuento { Codigo = "07" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento ACTUALIZACION_PRODUCTOS_SERVICIOS => new TipoDescuento { Codigo = "08" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento GENERAL => new TipoDescuento { Codigo = "09" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento VOLUMEN => new TipoDescuento { Codigo = "10" };
        [Obsolete("Opción obsoleta desde la versión 1.8", false)]
        public static TipoDescuento OTROS => new TipoDescuento { Codigo = "11" };

        /// <summary>
        /// Descuento no condicionado es para los descuentos a nivel de línea.
        /// </summary>
        public static TipoDescuento NO_CONDICIONADO => new TipoDescuento { Codigo = "00" };
        /// <summary>
        /// Descuento condicionado, son los descuentos a nivel de pie de factura.
        /// </summary>
        public static TipoDescuento CONDICIONADO => new TipoDescuento { Codigo = "01" };
    }

    public class Descuento
    {
        public string Razon { get; set; }
        public decimal Porcentaje { get; set; }
        public decimal Monto { get; set; }
        public decimal Base { get; set; }
        public TipoDescuento Tipo { get; set; } = TipoDescuento.NO_CONDICIONADO;
    }

    public class TasaCambio
    {
        /// <summary>
        /// Moneda del documento.
        /// </summary>
        public Moneda Origen { get; set; }

        /// <summary>
        /// Debe ser peso colombiano.
        /// </summary>
        public Moneda Destino { get; set; } = Moneda.PESO_COLOMBIANO;

        /// <summary>
        /// Tasa de cambio aplicada.
        /// </summary>
        public decimal Cambio { get; set; }

        /// <summary>
        /// Fecha de la tasa.
        /// </summary>
        public DateTime Fecha { get; set; }
    }

    public class Impuesto
    {
        /// <summary>
        /// Tipo de impuesto.
        /// </summary>
        public TipoImpuesto Tipo { get; set; }

        /// <summary>
        /// Porcentaje del impuesto.
        /// </summary>
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// Indicar cuando Porcentaje y Base imponible sean igual a 0.
        /// </summary>
        public decimal PorUnidad { get; set; }

        /// <summary>
        /// Unidad de medida para el impuesto.
        /// </summary>
        public UnidadMedida UnidadMedida { get; set; }

        /// <summary>
        /// Valor para la unidad de medida.
        /// </summary>
        public decimal ValorUnidadMedida { get; set; }

        /// <summary>
        /// Base imponible del impuesto.
        /// </summary>
        public decimal BaseImponible { get; set; }

        /// <summary>
        /// Monto total de importe.
        /// </summary>
        public decimal Importe { get; set; }

        /// <summary>
        /// Indica si se trata de un impuesto retenido (ReteIVA, ReteICA o ReteFuente).
        /// </summary>
        public bool Retenido
        {
            get
            {
                //return (Tipo == TipoImpuesto.ReteIVA || Tipo == TipoImpuesto.ReteICA || Tipo == TipoImpuesto.ReteRenta);
                return (Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteIVA").Codigo || Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteICA").Codigo || Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteRenta").Codigo);
            }
        }
    }

    public class ResumenImpuesto
    {
        /// <summary>
        /// Tipo de impuesto.
        /// </summary>
        public TipoImpuesto Tipo { get; set; }

        /// <summary>
        /// Suma de los elementos Importe de la colección Detalles.
        /// </summary>
        public decimal Importe { get; set; }

        /// <summary>
        /// Detalles del impuesto.
        /// Indicar un elemento por cada porcentaje aplicado del impuesto.
        /// </summary>
        public List<Impuesto> Detalles { get; set; } = new List<Impuesto>();

        /// <summary>
        /// Indica si se trata de un impuesto retenido (ReteIVA, ReteICA o ReteFuente).
        /// </summary>
        public bool Retenido
        {
            get
            {
                return (Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteIVA").Codigo || Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteICA").Codigo || Tipo.Codigo == TipoImpuesto.GetFromNombre("ReteRenta").Codigo);
            }
        }
    }

    public class Anticipo
    {
        /// <summary>
        /// Identificador (interno) del anticipo.
        /// Consecutivo iniciado en 1.
        /// </summary>
        [Obsolete("Consecutivo iniciado en 1.", false)]
        public string Identificador { get; set; }

        /// <summary>
        /// Valor del anticipo.
        /// </summary>
        public decimal Valor { get; set; }

        /// <summary>
        /// Fecha en la cual el pago fue realizado.
        /// </summary>
        public DateTime FechaPago { get; set; }

        /// <summary>
        /// Fecha en la cual el pago fue recibido.
        /// </summary>
        public DateTime FechaRecepcionPago { get; set; }

        /// <summary>
        /// Instrucciones relativas al pago.
        /// </summary>
        public string Instrucciones { get; set; }

        /// <summary>
        /// Concepto de anticipo.
        /// Requerido en Sector Salud.
        /// </summary>
        public ConceptoAnticipo ConceptoAnticipo { get; set; }
    }

    public class ConceptoAnticipo
    {
        public string Codigo { get; set; }

        public static ConceptoAnticipo SS_COPAGO => new ConceptoAnticipo { Codigo = "01" };
        public static ConceptoAnticipo SS_CUOTA_MODERADORA => new ConceptoAnticipo { Codigo = "02" };
        public static ConceptoAnticipo SS_PAGOS_COMPARTIDOS_PLANES_VOLUNTARIOS_SALUD => new ConceptoAnticipo { Codigo = "03" };
        public static ConceptoAnticipo SS_ANTICIPO => new ConceptoAnticipo { Codigo = "04" };
        public static ConceptoAnticipo SS_NO_APLICA => new ConceptoAnticipo { Codigo = "05" };
    }

    public class Totales
    {
        /// <summary>
        /// Total valor bruto, suma de los valores brutos de las líneas.
        /// </summary>
        public decimal ValorBruto { get; set; }

        /// <summary>
        ///  Base imponible para el cálculo de los tributos.
        /// </summary>
        public decimal BaseImponible { get; set; }

        /// <summary>
        /// Total de Valor Bruto más tributos.
        /// </summary>
        public decimal ValorBrutoConImpuestos { get; set; }

        /// <summary>
        /// Suma de todos los descuentos globales aplicados al documento.
        /// </summary>
        public decimal Descuentos { get; set; }

        /// <summary>
        /// Suma de todos los cargos globales aplicados al documento.
        /// </summary>
        public decimal Cargos { get; set; }

        /// <summary>
        /// Anticipo total del documento.
        /// </summary>
        public decimal Anticipo { get; set; }

        /// <summary>
        /// Valor total del documento.
        /// Suma de Valor Bruto más tributos - Valor del Descuento Total + Valor del Cargo Total.
        /// </summary>
        public decimal TotalPagable { get; set; }
    }

    public class CodigoPrecioReal
    {
        public string Valor { get; set; }

        public static CodigoPrecioReal VALOR_COMERCIAL => new CodigoPrecioReal { Valor = "01" };
        public static CodigoPrecioReal VALOR_EN_INVENTARIOS => new CodigoPrecioReal { Valor = "02" };
        public static CodigoPrecioReal OTROS => new CodigoPrecioReal { Valor = "03" };
    }

    public class TipoCodigoProducto
    {
        public string Identificador { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static readonly Dictionary<string, TipoCodigoProducto> _tiposMap = new Dictionary<string, TipoCodigoProducto>
        {
            { "UNSPSC", new TipoCodigoProducto { Identificador = "001", Codigo = "10", Nombre = "UNSPSC" } },
            { "GTIN", new TipoCodigoProducto { Identificador = "010", Codigo = "9", Nombre = "GTIN" } },
            { "PARTIDAS_ARANCELARIAS", new TipoCodigoProducto { Identificador = "020", Codigo = "195", Nombre = "Partida Arancelarias" } },
            { "ESTANDAR_INTERNO", new TipoCodigoProducto { Identificador = "999", Codigo = "", Nombre = "Estándar de adopción del contribuyente" } },
        };

        public static TipoCodigoProducto GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de ResponsabilidadTributaria no válido", nameof(codigo));
            }
        }



        /// <summary>
        /// United Nations Standard Products and Services Code
        /// </summary>
        //public static TipoCodigoProducto UNSPSC => new TipoCodigoProducto
        //{
        //    Identificador = "001",
        //    Codigo = "10",
        //    Nombre = "UNSPSC"
        //};

        /// <summary>
        /// Números Globales de Identificación de Productos
        /// </summary>
        //public static TipoCodigoProducto GTIN => new TipoCodigoProducto
        //{
        //    Identificador = "010",
        //    Codigo = "9",
        //    Nombre = "GTIN"
        //};

        /// <summary>
        /// Partida Arancelarias
        /// </summary>
        //public static TipoCodigoProducto PARTIDAS_ARANCELARIAS => new TipoCodigoProducto
        //{
        //    Identificador = "020",
        //    Codigo = "195",
        //    Nombre = "Partida Arancelarias"
        //};

        /// <summary>
        /// Estándar de adopción del contribuyente
        /// </summary>
        //public static TipoCodigoProducto ESTANDAR_INTERNO => new TipoCodigoProducto
        //{
        //    Identificador = "999",
        //    Codigo = "",
        //    Nombre = "Estándar de adopción del contribuyente"
        //};
    }

    public class CodigoProducto
    {
        public TipoCodigoProducto Tipo { get; set; } = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO");
        public string Valor { get; set; }
    }

    public class Linea
    {
        /// <summary>
        /// Cantidad de ítems.
        /// </summary>
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Código del producto. Opcional.
        /// </summary>
        public CodigoProducto CodigoProducto { get; set; }

        /// <summary>
        /// Unidad de medida del ítem.
        /// </summary>
        public UnidadMedida UnidadMedida { get; set; } = UnidadMedida.CADA;

        /// <summary>
        /// Nombre del producto o servicio.
        /// </summary>
        public string Descripcion { get; set; }

        /// <summary>
        /// Campo para descripción adicional.
        /// </summary>
        public string DescripcionComplementaria { get; set; }

        /// <summary>
        /// Obligatorio si es factura de exportación.
        /// </summary>
        public string Marca { get; set; } = "UNKNOW_BRAND";

        /// <summary>
        /// Referencia o Modelo del producto. Obligatorio si es factura de exportación.
        /// </summary>
        public string Referencia { get; set; } = "UNKNOW_MODEL";

        /// <summary>
        /// Precio unitario (a cobrar) sin impuestos.
        /// </summary>
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// Obligatorio cuando Costo total es igual a 0.
        /// </summary>
        public decimal PrecioUnitarioReal { get; set; }

        /// <summary>
        /// Código de referencia para Precio unitario real.
        /// Obligatorio cuando Costo total es igual a 0.
        /// </summary>
        public CodigoPrecioReal CodigoPrecioReal { get; set; } = CodigoPrecioReal.OTROS;

        /// <summary>
        /// (Cantidad * PrecioUnitarioAntesDeIva) + Cargos - Descuentos
        /// </summary>
        public decimal BaseGeneral { get; set; }

        /// <summary>
        /// Descuentos detallados.
        /// </summary>
        public List<Descuento> Descuentos { get; set; } = new List<Descuento>();
        //public Collection<Descuento> Descuentos { get; private set; } = new Collection<Descuento>();

        /// <summary>
        /// Cargos detallados.
        /// </summary>
        public List<Cargo> Cargos { get; set; } = new List<Cargo>();

        /// <summary>
        /// Detalles de impuesto.
        /// </summary>
        public List<Impuesto> Impuestos { get; set; } = new List<Impuesto>();

        /// <summary>
        /// NIT del mandante. Opcional en facturas tipo Mandato.
        /// </summary>
        public string NitMandante { get; set; }

        /// <summary>
        /// Anotaciones opcionales. Obligatorio para facturas AIU.
        /// </summary>
        public string Anotacion { get; set; }

        /// <summary>
        /// Obligatorio cuando se informe el tipo de operación MANDATOS (TipoIdentificadorMandato) o TRANSPORTE (TipoIdentificadorTransporte).
        /// </summary>
        public string IdentificadorOperacion { get; set; }

        /// <summary>
        /// Lista de valores correspondientes al Sector Transporte de carga.
        /// </summary>
        public RemesaTransporte RemesaTransporte { get; set; }

        public PeriodoFacturacion PeriodoFacturacionLine { get; set; }

        public bool Excluido { get; set; } = false;

    }

    public class RemesaTransporte
    {
        public string Numero { get; set; }
        public string Consecutivo { get; set; }
        public decimal Valor { get; set; }
        public decimal Cantidad { get; set; }
        public UnidadMedida UnidadMedida { get; set; }
    }

    public class ParteEvento
    {
        public TipoPersona TipoPersona { get; set; } = TipoPersona.JURIDICA;
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string Nombre { get; set; }
        public ResponsabilidadTributaria ResponsabilidadTributaria { get; set; } = ResponsabilidadTributaria.GetFromCodigo("01");
    }

    public class ConceptoEventoRechazo
    {
        public string Codigo { get; set; }

        /// <summary>
        /// Documento con inconsistencias
        /// </summary>
        public static ConceptoEventoRechazo INCONSISTENCIAS_DOCUMENTO => new ConceptoEventoRechazo { Codigo = "01" };

        /// <summary>
        /// Mercancía no entregada totalmente
        /// </summary>
        public static ConceptoEventoRechazo MERCANCIA_NO_ENTREGADA_TOTALMENTE => new ConceptoEventoRechazo { Codigo = "02" };

        /// <summary>
        /// Mercancía no entregada parcialmente
        /// </summary>
        public static ConceptoEventoRechazo MERCANCIA_NO_ENTREGADA_PARCIALMENTE => new ConceptoEventoRechazo { Codigo = "03" };

        /// <summary>
        /// Servicio no prestado
        /// </summary>
        public static ConceptoEventoRechazo SERVICIO_NO_PRESTADO => new ConceptoEventoRechazo { Codigo = "04" };
    }


    public class PeriodoNomina
    {
        /// <summary>
        /// Fecha de Ingreso del trabajador a la empresa.
        /// </summary>
        public DateTime FechaIngreso { get; set; }

        /// <summary>
        /// Fecha de Retiro del trabajador a la empresa.
        /// </summary>
        public DateTime? FechaRetiro { get; set; }

        /// <summary>
        /// Fecha de Inicio del período de liquidación del documento.
        /// </summary>
        public DateTime FechaLiquidacionInicio { get; set; }

        /// <summary>
        /// Fecha de Fin del período de liquidación del documento.
        /// </summary>
        public DateTime FechaLiquidacionFin { get; set; }

        /// <summary>
        /// Tiempo que lleva el trabajador laboranod en la empresa.
        /// </summary>
        public TiempoLaborado TiempoLaborado { get; set; }
    }

    public class TiempoLaborado
    {
        /// <summary>
        /// 1 año = 360 días
        /// </summary>
        public int Anios { get; set; } = 0;

        /// <summary>
        /// 1 mes = 30 días
        /// </summary>
        public int Meses { get; set; } = 0;

        /// <summary>
        /// 1 día = 1
        /// </summary>
        public int Dias { get; set; } = 0;

        public int CalcularDias() => (360 * Anios) + (30 * Meses) + Dias;
    }

    public class ProveedorSoftware
    {
        public string RazonSocial { get; set; }
        public string PrimerNombre { get; set; }
        public string OtrosNombres { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }
        public string Nit { get; set; }
        public string SoftwareIdentificador { get; set; }
        public string SoftwarePin { get; set; }
    }

    public class InformacionGeneralNominaIndividual
    {
        /// <summary>
        /// Fecha de generación del documento.
        /// </summary>
        public DateTime FechaGeneracion { get; set; }

        public DireccionFisica LugarGeneracion { get; set; }

        public Moneda Moneda { get; set; } = Moneda.PESO_COLOMBIANO;

        /// <summary>
        /// Corresponde a la tasa de cambio de la moneda utilizada en el documento en el campo Moneda a Pesos Colombianos.
        /// </summary>
        public decimal TRM { get; set; } = 1;

        public TipoPeriodoNomina PeriodoNomina { get; set; }
    }

    public class TipoPeriodoNomina
    {
        public string Codigo { get; set; }

        public static TipoPeriodoNomina SEMANAL => new TipoPeriodoNomina { Codigo = "1" };
        public static TipoPeriodoNomina DECENAL => new TipoPeriodoNomina { Codigo = "2" };
        public static TipoPeriodoNomina CATORCENAL => new TipoPeriodoNomina { Codigo = "3" };
        public static TipoPeriodoNomina QUINCENAL => new TipoPeriodoNomina { Codigo = "4" };
        public static TipoPeriodoNomina MENSUAL => new TipoPeriodoNomina { Codigo = "5" };
        public static TipoPeriodoNomina OTRO => new TipoPeriodoNomina { Codigo = "6" };
    }

    public class Empleador
    {
        public string RazonSocial { get; set; }
        public string PrimerNombre { get; set; }
        public string OtrosNombres { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }
        public string Nit { get; set; }
        public DireccionFisica Ubicacion { get; set; }
    }

    public class Trabajador
    {
        public TipoTrabajador Tipo { get; set; }
        public SubTipoTrabajador SubTipo { get; set; }

        /// <summary>
        /// Si el trabajador desarrolló durante el presente período alguna de las actividades descritas en el Decreto 2090 de 2003, o la norma que lo modifique, adicione o sustituya.
        /// </summary>
        public bool AltoRiesgoPension { get; set; }

        public TipoIdentificacion TipoDocumento { get; set; }
        public string NumeroDocumento { get; set; }
        public string PrimerNombre { get; set; }
        public string OtrosNombres { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }

        public DireccionFisica LugarTrabajo { get; set; }

        /// <summary>
        /// Si el trabajador tiene un salario integral, el cual es de tipo de renumeración que incluye todos los conceptos que puedan constituir salario en un solo monto o pago y
        /// que sea superior a 10 SMLMV más un 30% correspondiente a factor prestacional.
        /// </summary>
        public bool SalarioIntegral { get; set; }

        public TipoContrato TipoContrato { get; set; }

        /// <summary>
        /// Corresponde al valor que el empleador paga de forma periódica al trabajador como contraprestación por el trabajo realizado, este puede ser fijo o variable de acuerdo a 
        /// la unidad de tiempo en que las partes hayan acordado el pago, teniendo como base el día o la hora trabajada.
        /// </summary>
        public decimal Sueldo { get; set; }

        /// <summary>
        /// Opcional. Código interno del trabajador.
        /// </summary>
        public string Codigo { get; set; }
    }

    public class TipoContrato
    {
        public string Codigo { get; set; }

        public static TipoContrato TERMINO_FIJO => new TipoContrato { Codigo = "1" };
        public static TipoContrato TERMINO_INDEFINIDO => new TipoContrato { Codigo = "2" };
        public static TipoContrato OBRA_LABOR => new TipoContrato { Codigo = "3" };
        public static TipoContrato APRENDIZAJE => new TipoContrato { Codigo = "4" };
        public static TipoContrato PRACTICAS_PASANTIAS => new TipoContrato { Codigo = "5" };
    }

    public class TipoTrabajador
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        public static TipoTrabajador DEPENDIENTE => new TipoTrabajador { Codigo = "01", Descripcion = "Dependiente" };
        public static TipoTrabajador SERVICIO_DOMESTICO => new TipoTrabajador { Codigo = "02", Descripcion = "Servicio domestico" };
        public static TipoTrabajador MADRE_COMUNITARIA => new TipoTrabajador { Codigo = "04", Descripcion = "Madre comunitaria" };
        public static TipoTrabajador APRENDIZ_SENA_ETAPA_LECTIVA => new TipoTrabajador { Codigo = "12", Descripcion = "Aprendices del SENA en etapa lectiva" };
        public static TipoTrabajador FUNCIONARIO_SIN_TOPE_MAXIMO_IBC => new TipoTrabajador { Codigo = "18", Descripcion = "Funcionarios públicos sin tope máximo de ibc" };
        public static TipoTrabajador APRENDIZ_SENA_ETAPA_PRODUCTIVA => new TipoTrabajador { Codigo = "19", Descripcion = "Aprendices del SENA en etapa productiva" };
        public static TipoTrabajador ESTUDIANTE_POSTGRADO_SALUD => new TipoTrabajador { Codigo = "21", Descripcion = "Estudiantes de postgrado en salud" };
        public static TipoTrabajador PROFESOR_ESTABLECIMIENTO_PARTICULAR => new TipoTrabajador { Codigo = "22", Descripcion = "Profesor de establecimiento particular" };
        public static TipoTrabajador ESTUDIANTE_APORTE_SOLO_RIESGO_LABORAL => new TipoTrabajador { Codigo = "23", Descripcion = "Estudiantes aportes solo riesgos laborales" };
        public static TipoTrabajador DEPENDIENTE_ENTIDAD_UNIVERSIDAD_PUBLICA_REGIMEN_ESPECIAL_SALUD => new TipoTrabajador { Codigo = "30", Descripcion = "Dependiente entidades o universidades públicas con régimen especial en salud" };
        public static TipoTrabajador COOPERADO_O_PRE_COOPERATIVA_TRABAJO_ASOCIADO => new TipoTrabajador { Codigo = "31", Descripcion = "Cooperados o pre-cooperativas de trabajo asociado" };
        public static TipoTrabajador DEPENDIENTE_ENTIDAD_BENEFICIARIA_SISTEMA_GENERAL_PARTICIPACIONES => new TipoTrabajador { Codigo = "47", Descripcion = "Trabajador dependiente de entidad beneficiaria del sistema general de participaciones ‐ aportes patronales" };
        public static TipoTrabajador TRABAJADOR_TIEMPO_PARCIAL => new TipoTrabajador { Codigo = "51", Descripcion = "Trabajador de tiempo parcial" };
        public static TipoTrabajador PRE_PENSIONADO_ENTIDAD_LIQUIDACION => new TipoTrabajador { Codigo = "54", Descripcion = "Pre-pensionado de entidad en liquidación" };
        public static TipoTrabajador PRE_PENSIONADO_APORTE_VOLUNTARIO_SALUD => new TipoTrabajador { Codigo = "56", Descripcion = "Pre-pensionado con aporte voluntario a salud" };
        public static TipoTrabajador ESTUDIANTE_PRACTICAS_LABORALES_SECTOR_PUBLICO => new TipoTrabajador { Codigo = "58", Descripcion = "Estudiantes de prácticas laborales en el sector público" };
    }

    public class SubTipoTrabajador
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        public static SubTipoTrabajador NO_APLICA => new SubTipoTrabajador { Codigo = "00", Descripcion = "No aplica" };
        public static SubTipoTrabajador DEPENDIENTE_PENSIONADO_VEJEZ_ACTIVO => new SubTipoTrabajador { Codigo = "01", Descripcion = "Dependiente pensionado por vejez activo" };
    }

    public class CuentaBancaria
    {
        public string NombreBanco { get; set; }
        public string TipoCuenta { get; set; }
        public string NumeroCuenta { get; set; }
    }

    public class Devengados
    {
        public DevengadoResumen Resumen { get; set; }
        public DevengadoTransporte Transporte { get; set; }

        public Collection<DevengadoTransporte> Transportes { get; set; } = new Collection<DevengadoTransporte>();

        public Collection<HoraExtra> HorasExtraDiurnas { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasExtraNocturnas { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasRecargoNocturno { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasExtraDiurnasDominicalesFestivos { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasRecargoDiurnoDominicalesFestivos { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasExtraNocturnasDominicalesFestivos { get; set; } = new Collection<HoraExtra>();
        public Collection<HoraExtra> HorasRecargoNocturnoDominicalesFestivos { get; set; } = new Collection<HoraExtra>();

        public Collection<VacacionComun> VacacionesComunes { get; set; } = new Collection<VacacionComun>();
        public Collection<VacacionCompensada> VacacionesCompensadas { get; set; } = new Collection<VacacionCompensada>();

        public Primas Primas { get; set; }
        public Cesantias Cesantias { get; set; }

        public Collection<Incapacidad> Incapacidades { get; set; } = new Collection<Incapacidad>();

        public Collection<LicenciaMaternidadPaternidad> LicenciasMaternidadPaternidad { get; set; } = new Collection<LicenciaMaternidadPaternidad>();
        public Collection<LicenciaRenumerada> LicenciasRenumeradas { get; set; } = new Collection<LicenciaRenumerada>();
        public Collection<LicenciaNoRenumerada> LicenciasNoRenumeradas { get; set; } = new Collection<LicenciaNoRenumerada>();

        public Collection<Bonificacion> Bonificaciones { get; set; } = new Collection<Bonificacion>();
        public Collection<Auxilio> Auxilios { get; set; } = new Collection<Auxilio>();
        public Collection<HuelgaLegal> HuelgasLegales { get; set; } = new Collection<HuelgaLegal>();
        public Collection<OtroConceptoSalarial> OtrosConceptos { get; set; } = new Collection<OtroConceptoSalarial>();
        public Collection<Compensacion> Compensaciones { get; set; } = new Collection<Compensacion>();
        public Collection<BonoEPCTV> BonosEPCTV { get; set; } = new Collection<BonoEPCTV>();
        public Collection<decimal> Comisiones { get; set; } = new Collection<decimal>();
        public Collection<decimal> PagosTerceros { get; set; } = new Collection<decimal>();
        public Collection<decimal> Anticipos { get; set; } = new Collection<decimal>();

        /// <summary>
        /// De conformidad con lo previsto en el artículo 230 del Código Sustantivo del Trabajo, o la norma que lo modifique, adicione o sustituya, corresponde al valor que el 
        /// empleador dispone para suministrar la dotación de sus trabajadores.
        /// </summary>
        public decimal Dotacion { get; set; }

        /// <summary>
        /// Corresponde al valor no salarial que el patrocinador paga de forma mensual como ayuda o apoyo economía al aprendiz o practicante universitario durante su etapa lectiva y fase practica.
        /// </summary>
        public decimal ApoyoSostenimiento { get; set; }

        public decimal Teletrabajo { get; set; }
        public decimal BonificacionRetiro { get; set; }
        public decimal Indemnizacion { get; set; }
        public decimal Reintegro { get; set; }
    }

    public class Deducciones
    {
        public DeduccionSalud Salud { get; set; }
        public FondoPension FondoPension { get; set; }
        public FondoSeguridadPensional FondoSeguridadPensional { get; set; }

        public Collection<Sindicato> Sindicatos { get; set; } = new Collection<Sindicato>();
        public Collection<Sancion> Sanciones { get; set; } = new Collection<Sancion>();
        public Collection<Libranza> Libranzas { get; set; } = new Collection<Libranza>();
        public Collection<decimal> PagosTerceros { get; set; } = new Collection<decimal>();
        public Collection<decimal> Anticipos { get; set; } = new Collection<decimal>();
        public Collection<decimal> OtrasDeducciones { get; set; } = new Collection<decimal>();

        public decimal PensionVoluntaria { get; set; }
        public decimal RetencionFuente { get; set; }
        public decimal AhorroFomentoConstruccion { get; set; }
        public decimal Cooperativa { get; set; }
        public decimal EmbargoFiscal { get; set; }
        public decimal PlanesComplementarios { get; set; }
        public decimal Educacion { get; set; }
        public decimal Reintegro { get; set; }
        public decimal Deuda { get; set; }
    }

    public class DeduccionSalud
    {
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// El trabajador debe estar afiliado al sistema de salud. La cotización por salud que corresponde al 12.5% de la base del aporte, se hace en conjunto con la empresa. Ésta última 
        /// aporta el 8.5%, y el empleado debe aportar el 4% restante. Ese 4% es el valor que se debe descontar (deducir) del total devengado a cargo del empleado.
        /// </summary>
        public decimal Deduccion { get; set; }
    }

    public class FondoPension
    {
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// El trabajador también debe estar afiliado al sistema de pensiones. La cotización por pensión está a cargo tanto de la empresa como del empleado. Del total del aporte (16%), 
        /// la empresa aporta el 75% (12%) y el trabajador aporta el restante 25% (4%). Como el trabajador debe aportar un 4% por concepto de pensión, este valor se le descuenta (deduce) 
        /// del valor devengado en el respectivo periodo (mesoquincena).
        /// </summary>
        public decimal Deduccion { get; set; }
    }

    public class FondoSeguridadPensional
    {
        public decimal? Porcentaje { get; set; }

        /// <summary>
        /// Todo trabajador que devengue un sueldo que sea igual o superior a 4 salarios mininos, debe aportar un 1% al Fondo de solidaridad pensional.
        /// </summary>
        public decimal? Deduccion { get; set; }

        public decimal? PorcentajeSubsistencia { get; set; }

        /// <summary>
        /// Valor Pagado correspondiente a Fondo de Subsistencia por parte del trabajador.
        /// </summary>
        public decimal? DeduccionSubsistencia { get; set; }
    }

    public class Sindicato
    {
        public decimal Porcentaje { get; set; }
        public decimal Deduccion { get; set; }
    }

    public class Sancion
    {
        public decimal SancionPublica { get; set; }
        public decimal SancionPrivada { get; set; }
    }

    public class Libranza
    {
        public string Descripcion { get; set; }

        /// <summary>
        /// La cuota que el empleado deba pagar a una entidad financiera, para la amortización de un crédito que le haya sido otorgado por libranza.
        /// </summary>
        public decimal Deduccion { get; set; }
    }

    public class DevengadoResumen
    {
        /// <summary>
        /// Cantidad de dias laborados durante el Periodo de Pago.
        /// </summary>
        public int DiasTrabajados { get; set; }

        /// <summary>
        /// Valor Base o Sueldo del trabajador según lo estipulado en su contrato. Corresponde al Sueldo Trabajado por los días laborados.
        /// </summary>
        public decimal SueldoTrabajado { get; set; }
    }

    public class DevengadoTransporte
    {
        /// <summary>
        /// Valor de Auxilio de Transporte que recibe el trabajador por ley, según aplique.
        /// </summary>
        public decimal? AuxilioTransporte { get; set; }

        /// <summary>
        /// Valor de Viaticos, Manutención y Alojamiento de carácter Salarial.
        /// </summary>
        public decimal? ViaticoManutencionAlojamiento { get; set; }

        /// <summary>
        /// Valor de Viaticos, Manutención y Alojamiento de carácter No Salarial.
        /// </summary>
        public decimal? ViaticoManutencionAlojamientoNoSalarial { get; set; }
    }

    public class HoraExtra
    {
        /// <summary>
        /// Fecha y Hora de inicio de Hora Extra
        /// </summary>
        public DateTime? HoraInicio { get; set; }

        /// <summary>
        /// Fecha y Hora de fin de Hora Extra
        /// </summary>
        public DateTime? HoraFin { get; set; }

        /// <summary>
        /// Cantidad de Horas Extra
        /// </summary>
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Porcentaje al cual corresponde el calculo de 1 hora Extra
        /// </summary>
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// Es el valor pagado por el tiempo que se trabaja adicional a la jornada legal o pactada contractualmente
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class VacacionComun
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        /// <summary>
        /// Número de días que el trabajador estuvo inactivo durante el mes por vacaciones.
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Corresponde al valor pagado al trabajador, por el descanso remunerado que tiene derecho por haber trabajado un determinado tiempo. (Vacaciones SI disfrutadas)
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class VacacionCompensada
    {
        /// <summary>
        /// Número de días que el trabajador estuvo activo durante el mes sin disfrutar sus vacaciones. (Vacaciones NO disfrutadas)
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Corresponde al valor pagado al trabajador, por el descanso remunerado que no disfrutó y que tiene derecho por haber trabajado un determinado tiempo. (Vacaciones NO disfrutadas)
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class Primas
    {
        /// <summary>
        /// Cantidad de días trabajados para cálculo de Pago de Corte de Prima
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Valor Pagado por Prima Legal con respecto a Cantidad de Días
        /// </summary>
        public decimal Pago { get; set; }

        /// <summary>
        /// Valor Pagado por Prima No Salarial
        /// </summary>
        public decimal? PagoNoSalarial { get; set; }
    }

    public class Cesantias
    {
        /// <summary>
        /// Valor Pagado por Cesantías
        /// </summary>
        public decimal Pago { get; set; }

        /// <summary>
        /// Porcentaje de Interes de Cesantías
        /// </summary>
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// Valor Pagado por Intereses de Cesantías
        /// </summary>
        public decimal PagoIntereses { get; set; }
    }

    public class Incapacidad
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
        public TipoIncapacidad TipoIncapacidad { get; set; }
        public decimal Pago { get; set; }
    }

    public class LicenciaMaternidadPaternidad
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        /// <summary>
        /// Número de días que el trabajador o aprendiz efectivamente estuvo inactivo por licencia de maternidad o paternidad.
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Valor pagado al trabajador del descanso remunerado que la ley confiere por el nacimiento de un hijo, y que es reconocido y pagado por la EPS a la que está 
        /// afiliado el padre o la madre, o en su defecto por el empleador.
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class LicenciaRenumerada
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
        public decimal Pago { get; set; }
    }

    public class LicenciaNoRenumerada
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
    }

    public class Bonificacion
    {
        public decimal? BonificacionSalarial { get; set; }
        public decimal? BonificacionNoSalarial { get; set; }
    }

    public class Auxilio
    {
        public decimal? AuxilioSalarial { get; set; }
        public decimal? AuxilioNoSalarial { get; set; }
    }

    public class HuelgaLegal
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
    }

    public class OtroConceptoSalarial
    {
        public string Descripcion { get; set; }
        public decimal? ConceptoSalarial { get; set; }
        public decimal? ConceptoNoSalarial { get; set; }
    }

    public class Compensacion
    {
        /// <summary>
        /// Suma de dinero definido en el régimen de compensaciones como retribución mensual recibido por el asociado por la ejecución de su actividad material o inmaterial, la cual 
        /// se fija teniendo en cuenta el tipo de labor desempeñada, el rendimiento o la productividad y la cantidad de trabajo aportado. El monto de la compensación ordinaria podrá ser
        /// una suma básica igual para todos los asociados (Ordinaria).
        /// </summary>
        public decimal CompensacionOrdinaria { get; set; }

        /// <summary>
        /// Los demás pagos adicionales a la Compensación Ordinaria que recibe el asociado como retribución por su trabajo, definidos en el régimen de compensaciones (Extraordinaria).
        /// </summary>
        public decimal CompensacionExtraordinaria { get; set; }
    }

    /// <summary>
    /// Bonos Electronicos o de Papel de Servicio, Cheques, Tarjetas, Vales, etc.
    /// </summary>
    public class BonoEPCTV
    {
        public decimal? PagoSalarial { get; set; }
        public decimal? PagoNoSalarial { get; set; }
        public decimal? PagoAlimentacionSalarial { get; set; }
        public decimal? PagoAlimentacionNoSalarial { get; set; }
    }

    public class TipoHoraExtraRecargo
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public decimal Porcentaje { get; set; }

        public decimal PorcentajeCalculo
        {
            get { return 1 + (Porcentaje / 100); }
        }

        public static TipoHoraExtraRecargo HORA_EXTRA_DIURNA => new TipoHoraExtraRecargo { Codigo = "1", Descripcion = "Hora Extra Diurna", Porcentaje = 25.00m };
        public static TipoHoraExtraRecargo HORA_EXTRA_NOCTURNA => new TipoHoraExtraRecargo { Codigo = "2", Descripcion = "Hora Extra Nocturna", Porcentaje = 75.00m };
        public static TipoHoraExtraRecargo HORA_RECARGO_NOCTURNO => new TipoHoraExtraRecargo { Codigo = "3", Descripcion = "Hora Recargo Nocturno", Porcentaje = 35.00m };
        public static TipoHoraExtraRecargo HORA_EXTRA_DIURNA_DOMINICAL_FESTIVOS => new TipoHoraExtraRecargo { Codigo = "4", Descripcion = "Hora Extra Diurna Dominical y Festivos", Porcentaje = 100.00m };
        public static TipoHoraExtraRecargo HORA_RECARGO_DIURNO_DOMINICAL_FESTIVOS => new TipoHoraExtraRecargo { Codigo = "5", Descripcion = "Hora Recargo Diurno Dominical y Festivos", Porcentaje = 75.00m };
        public static TipoHoraExtraRecargo HORA_EXTRA_NOCTURNA_DOMINICAL_FESTIVOS => new TipoHoraExtraRecargo { Codigo = "6", Descripcion = "Hora Extra Nocturno Dominical y Festivos", Porcentaje = 150.00m };
        public static TipoHoraExtraRecargo HORA_RECARGO_NOCTURNO_DOMINICAL_FESTIVOS => new TipoHoraExtraRecargo { Codigo = "7", Descripcion = "Hora Recargo Nocturno Dominical y Festivos", Porcentaje = 110.00m };
    }

    public class TipoIncapacidad
    {
        public string Codigo { get; set; }

        public static TipoIncapacidad COMUN => new TipoIncapacidad { Codigo = "1" };
        public static TipoIncapacidad PROFESIONAL => new TipoIncapacidad { Codigo = "2" };
        public static TipoIncapacidad LABORAL => new TipoIncapacidad { Codigo = "3" };
    }

    public class TotalesNominaIndividual
    {
        public decimal TotalDevengados { get; set; }
        public decimal TotalDeducciones { get; set; }
        public decimal ValorTotal { get; set; }
        public decimal Redondeo { get; set; } = 0;
    }

    public class TipoAjusteNominaIndividual
    {
        public string Codigo { get; set; }

        public static TipoAjusteNominaIndividual REEMPLAZAR => new TipoAjusteNominaIndividual { Codigo = "1" };
        public static TipoAjusteNominaIndividual ELIMINAR => new TipoAjusteNominaIndividual { Codigo = "2" };
    }

    public class ReferenciaNominaIndividual
    {
        public string Numero { get; set; }
        public string Cune { get; set; }
        public DateTime FechaGeneracion { get; set; }
    }

    public enum TipoProcedencia { RESIDENTE = 10, NO_RESIDENTE = 11 }

    //public class TipoProcedencia
    //{
    //    public string Codigo { get; set; }

    //    public static TipoProcedencia RESIDENTE => new TipoProcedencia { Codigo = "10" };
    //    public static TipoProcedencia NO_RESIDENTE => new TipoProcedencia { Codigo = "11" };
    //}





    public class ResponsabilidadTributaria
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static readonly Dictionary<string, ResponsabilidadTributaria> _tiposMap = new Dictionary<string, ResponsabilidadTributaria>
        {
            { "01", new ResponsabilidadTributaria { Codigo = "01", Nombre = "IVA" } },
            { "04", new ResponsabilidadTributaria { Codigo = "04", Nombre = "INC" } },
            { "ZA", new ResponsabilidadTributaria { Codigo = "ZA", Nombre = "IVA e INC" } },
            { "ZZ", new ResponsabilidadTributaria { Codigo = "ZZ", Nombre = "No aplica" } },
        };

        public static ResponsabilidadTributaria GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de ResponsabilidadTributaria no válido", nameof(codigo));
            }
        }


    }

    public class TipoIdentificadorMandato
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static TipoIdentificadorMandato BIENES_SERVICIOS_INGRESO_PROPIO => new TipoIdentificadorMandato
        {
            Codigo = "0",
            Nombre = "B/S ingreso propio"
        };
        public static TipoIdentificadorMandato BIENES_SERVICIOS_INGRESOS_RECIBIDOS_PARA_TERCEROS => new TipoIdentificadorMandato
        {
            Codigo = "1",
            Nombre = "B/S Ingresos Recibidos para Terceros"
        };
    }

    public class TipoIdentificadorTransporte
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        public static TipoIdentificadorTransporte SERVICIO_ADICIONAL => new TipoIdentificadorTransporte
        {
            Codigo = "0",
            Nombre = "Servicio adicional"
        };
        public static TipoIdentificadorTransporte REMESA_TRANSPORTE_REGISTRADA_RNCD => new TipoIdentificadorTransporte
        {
            Codigo = "1",
            Nombre = "Remesa de transporte registrada en el RNDC"
        };
    }

    public class ReceptorBienes
    {
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Cargo { get; set; }
        public string Departamento { get; set; }
    }

    public class GeneracionTransmision
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }

        public static GeneracionTransmision POR_OPERACION => new GeneracionTransmision
        {
            Codigo = "1",
            Descripcion = "Por operación"
        };
        public static GeneracionTransmision ACUMULADO_SEMANAL => new GeneracionTransmision
        {
            Codigo = "2",
            Descripcion = "Acumulado semanal"
        };
    }

    public class FormaAdquisicion
    {
        public GeneracionTransmision GeneracionTransmision { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class ConceptoNotaAjusteDocumentoSoporte
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }


        public List<ConceptoNotaAjusteDocumentoSoporte> Lista { get { return new List<ConceptoNotaAjusteDocumentoSoporte>(_tiposMap.Values); } }

        public static readonly Dictionary<string, ConceptoNotaAjusteDocumentoSoporte> _tiposMap = new Dictionary<string, ConceptoNotaAjusteDocumentoSoporte>
        {
            { "DPBS", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "1", Descripcion = "Devolución parcial de los bienes y/o no aceptación parcial del servicio" } },
            { "ADS", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "2", Descripcion = "Anulación del documento soporte en adquisiciones efectuadas a sujetos no obligados a expedir factura de venta o documento equivalente" } },
            { "RDPT", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "3", Descripcion = "Rebaja o descuento parcial o total" } },
            { "AP", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "4", Descripcion = "Ajuste de precio" } },
            { "OTRO", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "5", Descripcion = "OTRO" } }
        };

        public static ConceptoNotaAjusteDocumentoSoporte GetFromCodigo(string codigo)
        {
            if (_tiposMap.ContainsKey(codigo))
            {
                return _tiposMap[codigo];
            }
            else
            {
                throw new ArgumentException("Código de RegimenFiscal no válido", nameof(codigo));
            }
        }


        public static ConceptoNotaAjusteDocumentoSoporte DEVOLUCION_PARCIAL => new ConceptoNotaAjusteDocumentoSoporte
        {
            Codigo = "1",
            Descripcion = "Devolución parcial de los bienes y/o no aceptación parcial del servicio"
        };
        public static ConceptoNotaAjusteDocumentoSoporte ANULACION_TOTAL => new ConceptoNotaAjusteDocumentoSoporte
        {
            Codigo = "2",
            Descripcion = "Anulación del documento soporte en adquisiciones efectuadas a sujetos no obligados a expedir factura de venta o documento equivalente"
        };
        public static ConceptoNotaAjusteDocumentoSoporte REBAJA_O_DESCUENTO => new ConceptoNotaAjusteDocumentoSoporte
        {
            Codigo = "3",
            Descripcion = "Rebaja o descuento parcial o total"
        };
        public static ConceptoNotaAjusteDocumentoSoporte AJUSTE_PRECIO => new ConceptoNotaAjusteDocumentoSoporte
        {
            Codigo = "4",
            Descripcion = "Ajuste de precio"
        };
        public static ConceptoNotaAjusteDocumentoSoporte OTROS => new ConceptoNotaAjusteDocumentoSoporte
        {
            Codigo = "5",
            Descripcion = "Otros"
        };
    }


    // PASAR AL MIDDLE
    /// <summary>
    /// Esta información proviente de la tabla Contribuyentes
    /// </summary>
    public class DatosFiscales
    {
        public DatosFiscales() { }
        public DatosFiscales(string razon_social, string regimen_fiscal = "R-99-PN", string responsabilidad_tributaria = "ZZ")
        {
            this.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");
            this.TipoPersona = TipoPersona.JURIDICA;
            this.RazonSocial = razon_social;

            // PENDIENTE VALIDACIONES 
            this.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
            this.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo(responsabilidad_tributaria);
        }


        // PASAR ALMIDDLE
        public DatosFiscales(string tipo_identificacion, string primer_nombre, string segundo_nombre, string primer_apellido, string segundo_apellido, string regimen_fiscal = "R-99-PN", string responsabilidad_tributaria = "ZZ")
        {
            try
            {
                this.TipoIdentificacion = TipoIdentificacion.GetFromCodigo(tipo_identificacion);
            }
            catch (Exception)
            {
                throw;
            }

            this.TipoPersona = TipoPersona.NATURAL;
            this.PrimerNombre = primer_nombre;
            this.SegundoNombre = segundo_nombre;
            this.PrimerApellido = primer_apellido;
            this.SegundoApellido = segundo_apellido;

            // PENDIENTE VALIDACIONES 
            this.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
            this.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo(responsabilidad_tributaria);
        }

        public TipoPersona TipoPersona { get; set; }
        public TipoIdentificacion TipoIdentificacion { get; set; }
        public string Identificacion { get; set; } = string.Empty;
        public string DV { get; set; } = string.Empty;
        public string PrimerNombre { get; set; } = string.Empty;
        public string SegundoNombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string SegundoApellido { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public DireccionFisica Direccion { get; set; } = new DireccionFisica();
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public RegimenFiscal RegimenFiscal { get; set; } 
        public ResponsabilidadTributaria ResponsabilidadTributaria { get; set; }


        public string NombreRazonsocial()
        {
            if (TipoPersona == TipoPersona.NATURAL)
                return $"{PrimerNombre.Trim()} {SegundoNombre.Trim()} {PrimerApellido.Trim()} {SegundoApellido.Trim()}".Replace("  ", " ").Replace("  ", " ");
            else
                return RazonSocial;
        }
    }


    // PASAR ALMIDDLE
    /// <summary>
    /// Esta información proviene de la tabla Terceros 
    /// </summary>
    public class DatosComerciales
    {
        public DatosComerciales() { }

        /// <summary>
        /// Para reportar datos de un establecimiento de comercio, bien sea del Emisor o del Receptor del documento electrónico
        /// </summary>
        /// <param name="codigo_cliente"></param>
        /// <param name="nombre_comercial"></param>
        /// <param name="matricula_mercantil">Se refiere a la matrícula mercantil del establecimiento</param>
        /// <param name="telefono">Numero telefónico del Establecimiento de comercio</param>
        /// <param name="email">Correo ELectrónico del establecomiento de comercio (A este correo se enviará el documento electrónico)</param>
        /// <param name="direccion">Dirección fpisica del Establecimiento de COmercio</param>
        public DatosComerciales(string codigo_cliente, string nombre_comercial, string matricula_mercantil, string telefono, string email, DireccionFisica direccion)
        {
            this.CodigoCliente = codigo_cliente;
            this.NombreComercial = nombre_comercial;
            this.MatriculaMercantil = matricula_mercantil;
            this.Telefono = telefono;
            this.Email = email;
            this.Direccion = direccion;
        }

        public string CodigoCliente { get; set; } = string.Empty;
        public string NombreComercial { get; set; } = string.Empty;
        public string MatriculaMercantil { get; set; } = string.Empty;

        public DireccionFisica Direccion { get; set; } = new DireccionFisica();
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }



    public class OrdenPedido : OrdenCompra
    {
    }
    // *************************************************************************

}
