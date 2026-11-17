using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace eFacturacionColombia_NETCore.Documentos.Extensiones
{
    public class ExtensionSectorSalud : BaseExtensionSector
    {
        public override string ElementName => "CustomTagGeneral";

        public ExtensionSectorSalud()
        {
            CustomTagGeneral = new CustomTagGeneralType
            {
                Names = new[]
                {
                    "Responsable",
                    "Tipo, identificador:año del acto administrativo"
                },
                Values = new[]
                {
                    "url www.minsalud.gov.co",
                    "Resolución 2275:2023"
                },
                Interoperabilidad = new InteroperabilidadType
                {
                    Group = new GroupType
                    {
                        SchemeName = "Sector Salud",
                        Collection = new GroupCollectionType[] { }
                    }
                }
            };
        }

        public ExtensionSectorSalud ConInformacion(Informacion informacion)
        {
            var collection = CustomTagGeneral.Interoperabilidad.Group.Collection?.ToList() ?? new List<GroupCollectionType>();

            var item = new GroupCollectionType
            {
                SchemeName = "Usuario",
                AdditionalInformation = new AdditionalInformationType[]
                {
                    new AdditionalInformationType
                    {
                        Name = "CODIGO_PRESTADOR",
                        Value = informacion.CodigoPrestador
                    },
                    new AdditionalInformationType
                    {
                        Name = "MODALIDAD_PAGO",
                        Value = informacion.ModalidadPago.Nombre,
                        SchemeName = "salud_modalidad_pago.gc",
                        SchemeId = informacion.ModalidadPago.Codigo
                    },
                    new AdditionalInformationType
                    {
                        Name = "COBERTURA_PLAN_BENEFICIOS",
                        Value = informacion.CoberturaPlanBeneficios.Nombre,
                        SchemeName = "salud_cobertura.gc",
                        SchemeId = informacion.CoberturaPlanBeneficios.Codigo
                    },
                    new AdditionalInformationType
                    {
                        Name = "NUMERO_CONTRATO",
                        Value = informacion.NumeroContrato
                    },
                    new AdditionalInformationType
                    {
                        Name = "NUMERO_POLIZA",
                        Value = informacion.NumeroPoliza
                    }
                }
            };

            collection.Add(item);

            CustomTagGeneral.Interoperabilidad.Group.Collection = collection.ToArray();

            return this;
        }

        public ExtensionSectorSalud ConInteroperabilidadPT(InteroperabilidadPT interoperabilidad)
        {
            var parametrosDescarga = new Collection<ParametroArgumentoType>();
            foreach (var parametro in interoperabilidad.DescargaAdjuntos.ParametrosArgumentos)
                parametrosDescarga.Add(new ParametroArgumentoType { Name = parametro.Nombre, Value = parametro.Valor });

            var parametrosEntrega = new Collection<ParametroArgumentoType>();
            foreach (var parametro in interoperabilidad.EntregaDocumento.ParametrosArgumentos)
                parametrosEntrega.Add(new ParametroArgumentoType { Name = parametro.Nombre, Value = parametro.Valor });

            CustomTagGeneral.Interoperabilidad.InteroperabilidadPT = new InteroperabilidadPTType
            {
                UrlDescargaAdjuntos = new UrlDescargaAdjuntosType
                {
                    Url = interoperabilidad.DescargaAdjuntos.Url,
                    ParametrosArgumentos = parametrosDescarga.ToArray()
                },
                EntregaDocumento = new EntregaDocumentoType
                {
                    WebService = interoperabilidad.EntregaDocumento.UrlWebservice,
                    ParametrosArgumentos = parametrosEntrega.ToArray()
                }
            };

            return this;
        }

        public override XmlElement SerializeXmlElement()
        {
            var serializer = new XmlSerializer(typeof(CustomTagGeneralType),
                 new XmlAttributeOverrides(), new Type[] { }, new XmlRootAttribute { ElementName = ElementName, Namespace = RootNamespace }, "");

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
                    serializer.Serialize(xw, CustomTagGeneral);

                    var doc = new XmlDocument();
                    doc.LoadXml(sw.ToString());

                    doc.DocumentElement.RemoveAllAttributes();

                    return doc.DocumentElement;
                }
            }
        }

        public class Informacion
        {
            public string CodigoPrestador { get; set; }
            public ModalidadPago ModalidadPago { get; set; }
            public CoberturaPlanBeneficios CoberturaPlanBeneficios { get; set; }
            public string NumeroContrato { get; set; }
            public string NumeroPoliza { get; set; }
        }

        public class ModalidadPago
        {
            public string Codigo { get; set; }
            public string Nombre { get; set; }

            public static ModalidadPago PAGO_INDIVIDUAL_POR_CASO => new ModalidadPago
            {
                Codigo = "01",
                Nombre = "Pago individual por caso / Conjunto integral de atenciones / Paquete / Canasta"
            };
            public static ModalidadPago PAGO_GLOBAL_PROSPECTIVO => new ModalidadPago
            {
                Codigo = "02",
                Nombre = "Pago global prospectivo"
            };
            public static ModalidadPago PAGO_POR_CAPITACON => new ModalidadPago
            {
                Codigo = "03",
                Nombre = "Pago por capitación"
            };
            public static ModalidadPago PAGO_POR_EVENTO => new ModalidadPago
            {
                Codigo = "04",
                Nombre = "Pago por evento"
            };
            public static ModalidadPago OTRA_MODALIDAD_ESPECIFICA => new ModalidadPago
            {
                Codigo = "05",
                Nombre = "Otra modalidad (específica)"
            };
        }

        public class CoberturaPlanBeneficios
        {
            public string Codigo { get; set; }
            public string Nombre { get; set; }

            public static CoberturaPlanBeneficios PLAN_BENEFICIOS_SALUD_FINANCIADO_UPC => new CoberturaPlanBeneficios
            {
                Codigo = "01",
                Nombre = "Plan de beneficios en salud financiado con UPC"
            };
            public static CoberturaPlanBeneficios PRESUPUESTO_MAXIMO => new CoberturaPlanBeneficios
            {
                Codigo = "02",
                Nombre = "Presupuesto máximo"
            };
            public static CoberturaPlanBeneficios PRIMA_EPS_EOC_NO_ASEGURADOS_SOAT => new CoberturaPlanBeneficios
            {
                Codigo = "03",
                Nombre = "Prima EPS / EOC, no asegurados SOAT"
            };
            public static CoberturaPlanBeneficios COBERTURA_POLIZA_SOAT => new CoberturaPlanBeneficios
            {
                Codigo = "04",
                Nombre = "Cobertura Póliza SOAT "
            };
            public static CoberturaPlanBeneficios COBERTURA_ARL => new CoberturaPlanBeneficios
            {
                Codigo = "05",
                Nombre = "Cobertura ARL"
            };
            public static CoberturaPlanBeneficios COBERTURA_ADRES => new CoberturaPlanBeneficios
            {
                Codigo = "06",
                Nombre = "Cobertura ADRES"
            };
            public static CoberturaPlanBeneficios COBERTURA_SALUD_PUBLICA => new CoberturaPlanBeneficios
            {
                Codigo = "07",
                Nombre = "Cobertura Salud Pública"
            };
            public static CoberturaPlanBeneficios COBERTURA_ENTIDAD_TERRITORIAL => new CoberturaPlanBeneficios
            {
                Codigo = "08",
                Nombre = "Cobertura entidad territorial, recursos de oferta"
            };
            public static CoberturaPlanBeneficios URGENCIAS_POBLACION_MIGRANTE => new CoberturaPlanBeneficios
            {
                Codigo = "09",
                Nombre = "Urgencias población migrante"
            };
            public static CoberturaPlanBeneficios PLAN_COMPLEMENTARIO_SALUD => new CoberturaPlanBeneficios
            {
                Codigo = "10",
                Nombre = "Plan complementario en salud"
            };
            public static CoberturaPlanBeneficios PLAN_MEDICINA_PREPAGADA => new CoberturaPlanBeneficios
            {
                Codigo = "11",
                Nombre = "Plan medicina prepagada"
            };
            public static CoberturaPlanBeneficios OTRAS_POLIZAS_SALUD => new CoberturaPlanBeneficios
            {
                Codigo = "12",
                Nombre = "Otras pólizas en salud"
            };
            public static CoberturaPlanBeneficios COBERTURA_REGIMEN_ESPECIAL_O_EXCEPCION => new CoberturaPlanBeneficios
            {
                Codigo = "13",
                Nombre = "Cobertura Régimen Especial o Excepción"
            };
            public static CoberturaPlanBeneficios COBERTURA_FONDO_NACIONAL_PERSONAS_PRIVADAS_LIBERTAD => new CoberturaPlanBeneficios
            {
                Codigo = "14",
                Nombre = "Cobertura Fondo Nacional de Salud de las Personas Privadas de la Libertad"
            };
            public static CoberturaPlanBeneficios PARTICULAR => new CoberturaPlanBeneficios
            {
                Codigo = "15",
                Nombre = "Particular"
            };
        }

        public class InteroperabilidadPT
        {
            public DescargaAdjuntos DescargaAdjuntos { get; set; }
            public EntregaDocumento EntregaDocumento { get; set; }
        }

        public class DescargaAdjuntos
        {
            public string Url { get; set; }
            public Collection<ParametroArgumento> ParametrosArgumentos { get; set; } = new Collection<ParametroArgumento>();
        }

        public class EntregaDocumento
        {
            public string UrlWebservice { get; set; }
            public Collection<ParametroArgumento> ParametrosArgumentos { get; set; } = new Collection<ParametroArgumento>();
        }

        public class ParametroArgumento
        {
            public string Nombre { get; set; }
            public string Valor { get; set; }
        }
    }
}