using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionSectorCarnico: BaseExtensionSector
    {
        public override string ElementName => "CustomTagGeneral";

        public ExtensionSectorCarnico()
        {
            CustomTagGeneral = new CustomTagGeneralType
            {
                Interoperabilidad = new InteroperabilidadType
                {
                    Group = new GroupType
                    {
                        SchemeName = "Fedegán",
                        Collection = new GroupCollectionType[]
                        {
                            new GroupCollectionType
                            {
                                SchemeName = "Proveedor",
                                AdditionalInformation = new AdditionalInformationType[] { }
                            }
                        }
                    }
                }
            };
        }

        public ExtensionSectorCarnico ConInformacion(Informacion informacion)
        {
            CustomTagGeneral.Interoperabilidad.Group.Collection[0].AdditionalInformation = new AdditionalInformationType[]
            {
                new AdditionalInformationType
                {
                    Name = "NitProveedorCarnes",
                    Value = informacion.NitProveedorCarnes
                }
            };

            return this;
        }

        public override XmlElement SerializeXmlElement()
        {
            var serializer = new XmlSerializer(typeof(CustomTagGeneralType),
                new XmlAttributeOverrides(), new Type[] { }, new XmlRootAttribute { ElementName = ElementName, Namespace = RootNamespace }, "");

            var namespaces = new XmlSerializerNamespaces();

            using (var sw = new StringWriter())
            {
                var settings = new XmlWriterSettings
                {
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace,
                    Indent = true
                };

                using (var xw = XmlWriter.Create(sw, settings))
                {
                    serializer.Serialize(xw, CustomTagGeneral, namespaces);

                    var doc = new XmlDocument();
                    doc.LoadXml(sw.ToString());

                    doc.DocumentElement.RemoveAllAttributes();

                    return doc.DocumentElement;
                }
            }
        }

        public class Informacion
        {
            public string NitProveedorCarnes { get; set; }
        }
    }
}