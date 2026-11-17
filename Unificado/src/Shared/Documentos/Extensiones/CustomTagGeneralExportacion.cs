using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class CustomTagGeneralExportacionType: CustomTagGeneralType
    {
        public TotalesCopType TotalesCop { get; set; }
    }

    public class TotalesCopType
    {
        [XmlElementAttribute("FctConvCop")]
        public decimal FctConvCop { get; set; }

        [XmlElementAttribute("MonedaCop")]
        public string MonedaCop { get; set; }

        [XmlElementAttribute("SubTotalCop")]
        public decimal SubTotalCop { get; set; }

        [XmlElementAttribute("DescuentoDetalleCop")]
        public decimal DescuentoDetalleCop { get; set; }

        [XmlElementAttribute("RecargoDetalleCop")]
        public decimal RecargoDetalleCop { get; set; }

        [XmlElementAttribute("TotalBrutoFacturaCop")]
        public decimal TotalBrutoFacturaCop { get; set; }

        [XmlElementAttribute("TotIvaCop")]
        public decimal TotIvaCop { get; set; }

        [XmlElementAttribute("TotIncCop")]
        public decimal TotIncCop { get; set; }

        [XmlElementAttribute("TotBolCop")]
        public decimal TotBolCop { get; set; }

        [XmlElementAttribute("ImpOtroCop")]
        public decimal ImpOtroCop { get; set; }

        [XmlElementAttribute("MntImpCop")]
        public decimal MntImpCop { get; set; }

        [XmlElementAttribute("TotalNetoFacturaCop")]
        public decimal TotalNetoFacturaCop { get; set; }

        [XmlElementAttribute("MntDctoCop")]
        public decimal MntDctoCop { get; set; }

        [XmlElementAttribute("MntRcgoCop")]
        public decimal MntRcgoCop { get; set; }

        [XmlElementAttribute("VlrPagarCop")]
        public decimal VlrPagarCop { get; set; }

        [XmlElementAttribute("ReteFueCop")]
        public decimal ReteFueCop { get; set; }

        [XmlElementAttribute("ReteIvaCop")]
        public decimal ReteIvaCop { get; set; }

        [XmlElementAttribute("ReteIcaCop")]
        public decimal ReteIcaCop { get; set; }

        [XmlElementAttribute("TotAnticiposCop")]
        public decimal TotAnticiposCop { get; set; }
    }
}
