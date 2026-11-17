using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionFabricanteSoftware : ExtensionDocumentoEquivalente
    {
        public class Informacion
        {
            public string NombreApellido { get; set; }
            public string RazonSocial { get; set; }
            public string NombreSoftware { get; set; }
        }

        private Informacion informacion;

        public void ConInformacion(Informacion informacion)
        {
            this.informacion = informacion;
        }

        public override XmlElement GetExtensionContent()
        {
            var docXml = new XmlDocument();
            docXml.LoadXml($@"<FabricanteSoftware>
    <InformacionDelFabricanteDelSoftware>
	    <Name>NombreApellido</Name>
	    <Value>{informacion.NombreApellido}</Value>
        <Name>RazonSocial</Name>
	    <Value>{informacion.RazonSocial}</Value>
        <Name>NombreSoftware</Name>
	    <Value>{informacion.NombreSoftware}</Value>
    </InformacionDelFabricanteDelSoftware>
</FabricanteSoftware>");

            docXml.DocumentElement.SetAttribute("xmlns", RootNamespace);

            var newXml = new XmlDocument();
            newXml.LoadXml(docXml.OuterXml);

            return newXml.DocumentElement;
        }
    }
}
