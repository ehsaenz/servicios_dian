using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionCajaVenta : ExtensionDocumentoEquivalente
    {
        public class Informacion
        {
            public string PlacaCaja { get; set; }
            public string UbicacionCaja { get; set; }
            public string NombreCajero { get; set; }
            public string TipoCaja { get; set; }
            public string CodigoVenta { get; set; }
            public decimal SubTotal { get; set; }
        }

        private Informacion informacion;

        public void ConInformacion(Informacion informacion)
        {
            this.informacion = informacion;
        }

        public override XmlElement GetExtensionContent()
        {
            var docXml = new XmlDocument();
            docXml.LoadXml($@"<PuntoVenta>
    <InformacionCajaVenta>
	    <Name>PlacaCaja</Name>
	    <Value>{informacion.PlacaCaja}</Value>
        <Name>UbicaciónCaja</Name>
	    <Value>{informacion.UbicacionCaja}</Value>
        <Name>Cajero</Name>
	    <Value>{informacion.NombreCajero}</Value>
        <Name>TipoCaja</Name>
	    <Value>{informacion.TipoCaja}</Value>
        <Name>CódigoVenta</Name>
	    <Value>{informacion.CodigoVenta}</Value>
        <Name>SubTotal</Name>
	    <Value>{informacion.SubTotal}</Value>
    </InformacionCajaVenta>
</PuntoVenta>");

            docXml.DocumentElement.SetAttribute("xmlns", RootNamespace);

            var newXml = new XmlDocument();
            newXml.LoadXml(docXml.OuterXml);

            return newXml.DocumentElement;
        }
    }
}
