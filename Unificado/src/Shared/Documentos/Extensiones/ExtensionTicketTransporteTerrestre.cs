using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionTicketTransporteTerrestre : ExtensionDocumentoEquivalente
    {
        public class Informacion
        {
            public string ModoTransporte { get; set; }
            public string IdMedioTransporte { get; set; }
            public string MedioDeTransporte { get; set; }
            public string LugarOrigen { get; set; }
            public string LugarDestino { get; set; }
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
            docXml.LoadXml($@"<InformacionAdicional>
    <InformacionTicket>
	    <Name>ModoTransporte</Name>
	    <Value>{informacion.ModoTransporte}</Value>
        <Name>IDMediodeTransporte</Name>
	    <Value>{informacion.IdMedioTransporte}</Value>
        <Name>Mediodetransporte</Name>
	    <Value>{informacion.MedioDeTransporte}</Value>
        <Name>LugardeOrigen</Name>
	    <Value>{informacion.LugarOrigen}</Value>
        <Name>LugardeDestino</Name>
	    <Value>{informacion.LugarDestino}</Value>
    </InformacionTicket>
</InformacionAdicional>");

            return docXml.DocumentElement;
        }
    }
}
