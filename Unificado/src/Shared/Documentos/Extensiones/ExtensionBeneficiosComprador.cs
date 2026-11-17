using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionBeneficiosComprador : ExtensionDocumentoEquivalente
    {
        public class Informacion
        {
            public string CodigoComprador { get; set; }
            public string NombresApellidos { get; set; }
            public string PuntosAcumulados { get; set; }
        }

        private Informacion informacion;

        public void ConInformacion(Informacion informacion)
        {
            this.informacion = informacion;
        }

        public override XmlElement GetExtensionContent()
        {
            var docXml = new XmlDocument();
            docXml.LoadXml($@"<BeneficiosComprador>
    <InformacionBeneficiosComprador>
	    <Name>Codigo</Name>
	    <Value>{informacion.CodigoComprador}</Value>
        <Name>NombresApellidos</Name>
	    <Value>{informacion.NombresApellidos}</Value>
        <Name>Puntos</Name>
	    <Value>{informacion.PuntosAcumulados}</Value>
    </InformacionBeneficiosComprador>
</BeneficiosComprador>");

            docXml.DocumentElement.SetAttribute("xmlns", RootNamespace);

            var newXml = new XmlDocument();
            newXml.LoadXml(docXml.OuterXml);

            return newXml.DocumentElement;
        }
    }
}
