using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public abstract class ExtensionDocumentoEquivalente
    {
        public string RootNamespace { get; set; } = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        public abstract XmlElement GetExtensionContent();
    }
}
