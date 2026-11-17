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
    public class ExtensionExportacion : BaseExtensionSector
    {
        public override string ElementName => "CustomTagGeneral";

        public ExtensionExportacion()
        {
            CustomTagGeneral = new CustomTagGeneralExportacionType
            {
                Interoperabilidad = new InteroperabilidadType
                {
                    Group = new GroupType
                    {
                        SchemeName = "Exportación",
                        Collection = new GroupCollectionType[] { }
                    }
                }
            };
        }

        public ExtensionExportacion ConInformacion(Informacion informacion)
        {
            CustomTagGeneral.Interoperabilidad.Group.Collection = new GroupCollectionType[]
            {
                new GroupCollectionType
                {
                    SchemeName = "DATOS ADICIONALES",
                    AdditionalInformation = new AdditionalInformationType[]
                    {
                        new AdditionalInformationType
                        {
                            Name = "Responsable/Encargado",
                            Value = informacion.ResponsableEncargado,
                        },
                        new AdditionalInformationType
                        {
                            Name = "Lugar de Salida",
                            Value = informacion.LugarSalida
                        },
                        new AdditionalInformationType
                        {
                            Name = "Medio de transporte",
                            Value = informacion.MedioTransporte
                        },
                        new AdditionalInformationType
                        {
                            Name = "Tipo de Doc. de transporte",
                            Value = informacion.TipoDocumentoTransporte
                        },
                        new AdditionalInformationType
                        {
                            Name = "N° de Doc. de transporte",
                            Value = informacion.NumeroDocumentoTransporte
                        },
                        new AdditionalInformationType
                        {
                            Name = "Transportadora o Tramitadora",
                            Value = informacion.TransportadoraTramitadora
                        },
                        new AdditionalInformationType
                        {
                            Name = "País de Origen de la M/cia",
                            Value = informacion.PaisOrigen
                        },
                        new AdditionalInformationType
                        {
                            Name = "Destino",
                            Value = informacion.Destino
                        },
                        new AdditionalInformationType
                        {
                            Name = "Términos de pago",
                            Value = informacion.TerminosPago
                        },
                        new AdditionalInformationType
                        {
                            Name = "Seguro",
                            Value = informacion.Seguro
                        },
                        new AdditionalInformationType
                        {
                            Name = "Observaciones",
                            Value = informacion.Observaciones
                        }
                    }
                }
            };

            return this;
        }

        public ExtensionExportacion ConTotales(Totales totales)
        {
            var customTagExportacion = CustomTagGeneral as CustomTagGeneralExportacionType;

            customTagExportacion.TotalesCop = new TotalesCopType
            {
                FctConvCop = totales.Conversion,
                MonedaCop = totales.Moneda,
                SubTotalCop = totales.SubTotal,
                DescuentoDetalleCop = totales.DescuentoDetalle,
                RecargoDetalleCop = totales.RecargoDetalle,
                TotalBrutoFacturaCop = totales.TotalBrutoFactura,
                TotIvaCop = totales.TotalIva,
                TotIncCop = totales.TotalInc,
                TotBolCop = totales.TotalBolsas,
                ImpOtroCop = totales.OtrosImpuestos,
                MntImpCop = totales.MontoImpuestos,
                TotalNetoFacturaCop = totales.TotalNetoFactura,
                MntDctoCop = totales.MontoDescuento,
                MntRcgoCop = totales.MontoRecargo,
                VlrPagarCop = totales.ValorPagar,
                ReteFueCop = totales.ReteFuente,
                ReteIvaCop = totales.ReteIva,
                ReteIcaCop = totales.ReteIca,
                TotAnticiposCop = totales.TotAnticipos,
            };

            return this;
        }

        public override XmlElement SerializeXmlElement()
        {
            var serializer = new XmlSerializer(typeof(CustomTagGeneralExportacionType),
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
            public string ResponsableEncargado { get; set; }
            public string LugarSalida { get; set; }
            public string MedioTransporte { get; set; }
            public string TipoDocumentoTransporte { get; set; }
            public string NumeroDocumentoTransporte { get; set; }
            public string TransportadoraTramitadora { get; set; }
            public string PaisOrigen { get; set; }
            public string Destino { get; set; }
            public string TerminosPago { get; set; }
            public string Seguro { get; set; }
            public string Observaciones { get; set; }
        }

        public class Totales
        {
            public decimal Conversion { get; set; }
            public string Moneda { get; set; }
            public decimal SubTotal { get; set; }
            public decimal DescuentoDetalle { get; set; }
            public decimal RecargoDetalle { get; set; }
            public decimal TotalBrutoFactura { get; set; }
            public decimal TotalIva { get; set; }
            public decimal TotalInc { get; set; }
            public decimal TotalBolsas { get; set; }
            public decimal OtrosImpuestos { get; set; }
            public decimal MontoImpuestos { get; set; }
            public decimal TotalNetoFactura { get; set; }
            public decimal MontoDescuento { get; set; }
            public decimal MontoRecargo { get; set; }
            public decimal ValorPagar { get; set; }
            public decimal ReteFuente { get; set; }
            public decimal ReteIva { get; set; }
            public decimal ReteIca { get; set; }
            public decimal TotAnticipos { get; set; }
        }
    }
}