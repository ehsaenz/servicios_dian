using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class CustomTagGeneralType
    {
        [XmlElementAttribute("Name")]
        public string[] Names { get; set; }

        [XmlElementAttribute("Value")]
        public string[] Values { get; set; }

        public InteroperabilidadType Interoperabilidad { get; set; }
    }

    public class InteroperabilidadType
    {
        public GroupType Group { get; set; }

        [XmlElementAttribute("InteroperabilidadPT")]
        public InteroperabilidadPTType InteroperabilidadPT { get; set; }
    }

    public class GroupType
    {
        [XmlAttributeAttribute("schemeName")]
        public string SchemeName { get; set; }

        [XmlElementAttribute("Collection")]
        public GroupCollectionType[] Collection { get; set; }
    }

    public class GroupCollectionType
    {
        [XmlAttributeAttribute("schemeName")]
        public string SchemeName { get; set; }

        [XmlElementAttribute("AdditionalInformation")]
        public AdditionalInformationType[] AdditionalInformation { get; set; }
    }

    public class AdditionalInformationType
    {
        [XmlAttributeAttribute("schemeName")]
        public string SchemeName { get; set; }

        [XmlAttributeAttribute("schemeID")]
        public string SchemeId { get; set; }

        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class InteroperabilidadPTType
    {
        [XmlElementAttribute("URLDescargaAdjuntos")]
        public UrlDescargaAdjuntosType UrlDescargaAdjuntos { get; set; }

        public EntregaDocumentoType EntregaDocumento { get; set; }
    }

    public class UrlDescargaAdjuntosType
    {
        [XmlElementAttribute("URL")]
        public string Url { get; set; }

        [XmlArrayItemAttribute("ParametroArgumento")]
        public ParametroArgumentoType[] ParametrosArgumentos { get; set; }
    }

    public class ParametroArgumentoType
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class EntregaDocumentoType
    {
        [XmlElementAttribute("WS")]
        public string WebService { get; set; }

        [XmlArrayItemAttribute("ParametroArgumento")]
        public ParametroArgumentoType[] ParametrosArgumentos { get; set; }
    }
}