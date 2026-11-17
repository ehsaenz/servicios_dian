using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public abstract class BaseExtensionSector
    {
        public abstract string ElementName { get; }

        public string RootNamespace { get; set; } = "";

        public CustomTagGeneralType CustomTagGeneral { get; set; }

        public abstract XmlElement SerializeXmlElement();
    }
}