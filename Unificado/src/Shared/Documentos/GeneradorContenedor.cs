using System;
using System.Collections.Generic;
using System.Text;

using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;

namespace eFacturacionColombia_NETCore.Documentos
{
    public class GeneradorContenedor
    {
        public AttachedDocumentType AttachedDocument { get; private set; }

        protected ReferenciaDocumento Referencia;

        public GeneradorContenedor(AmbienteDestino ambiente)
        {
            AttachedDocument = new AttachedDocumentType
            {
                UBLExtensions = new UBLExtensionType[]
                {
                    new UBLExtensionType { ExtensionContent = new ExtensionContentType { } }
                },
                UBLVersionID = new UBLVersionIDType { Value = "UBL 2.1" },
                CustomizationID = new CustomizationIDType { Value = "Documentos adjuntos" },
                ProfileID = new ProfileIDType { Value = "Factura Electrónica de Venta" },
                ProfileExecutionID = new ProfileExecutionIDType { Value = ((int)ambiente).ToString()},
                ID = new IDType { Value = DateTime.Now.ToFileTimeUtc().ToString() },
                IssueDate = new IssueDateType { Value = DateTime.Now },
                IssueTime = new IssueTimeType { Value = DateTime.Now.ToString("HH:mm:ss") + "-05:00" },
                DocumentType = new DocumentTypeType { Value = "Contenedor de Factura Electrónica" }
            };
        }


        public GeneradorContenedor ConFecha(DateTime fecha)
        {
            AttachedDocument.IssueDate = new IssueDateType { Value = fecha };
            AttachedDocument.IssueTime = new IssueTimeType { Value = fecha.ToString("HH:mm:ss") + "-05:00" };

            return this;
        }

        public GeneradorContenedor ConNumero(string numero)
        {
            AttachedDocument.ID = new IDType { Value = numero };

            return this;
        }

        [Obsolete("Usar método ConRespuesta(bytesRespuesta, codigo, fecha)", false)]
        public GeneradorContenedor ConRespuesta(ApplicationResponseType ar)
        {
            var xmlApplicationResponse = ar.SerializeXmlDocument().ToXmlString();

            AttachedDocument.ParentDocumentLineReference = new LineReferenceType[]
            {
                new LineReferenceType
                {
                    LineID = new LineIDType { Value = "1"},
                    DocumentReference = new DocumentReferenceType
                    {
                        ID = new IDType { Value = ar.DocumentResponse[0].DocumentReference[0].ID.Value },
                        UUID = new UUIDType
                        {
                            schemeName = ar.DocumentResponse[0].DocumentReference[0].UUID.schemeName,
                            Value = ar.DocumentResponse[0].DocumentReference[0].UUID.Value
                        },
                        IssueDate = new IssueDateType { Value = ar.IssueDate.Value },
                        DocumentType = new DocumentTypeType { Value = "ApplicationResponse"},
                        Attachment = new AttachmentType
                        {
                            ExternalReference = new ExternalReferenceType
                            {
                                MimeCode = new MimeCodeType { Value = "text/xml" },
                                EncodingCode = new EncodingCodeType { Value = "UTF-8"},
                                Description = new DescriptionType[]
                                {
                                    new DescriptionType { CDataValue = new CDataType { Value = xmlApplicationResponse, UseCData = true } }
                                }
                            }
                        },
                        ResultOfVerification = new ResultOfVerificationType
                        {
                            ValidatorID = new ValidatorIDType { Value = "Unidad Especial Dirección de Impuestos y Aduanas Nacionales"},
                            ValidationResultCode = new ValidationResultCodeType { Value = ar.DocumentResponse[0].Response.ResponseCode.Value },
                            ValidationDate = new ValidationDateType { Value = ar.IssueDate.Value },
                            ValidationTime = new ValidationTimeType { Value = ar.IssueTime.Value }
                        }
                    }
                }
            };

            return this;
        }

        [Obsolete("Usar métodos ConEmisor(emisor), ConAdquiriente(adquiriente) y ConDocumento(bytesDocumento, referencia).", false)]
        public GeneradorContenedor ConDocumento(InvoiceType invoice, byte[] bytesFirmados = null)
        {
            AttachedDocument.ParentDocumentID = new ParentDocumentIDType { Value = invoice.ID.Value };

            AttachedDocument.SenderParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = invoice.AccountingSupplierParty.Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = invoice.AccountingCustomerParty[0].Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.Attachment = new AttachmentType
            {
                ExternalReference = new ExternalReferenceType
                {
                    MimeCode = new MimeCodeType { Value = "text/xml" },
                    EncodingCode = new EncodingCodeType { Value = "UTF-8" },
                    Description = new DescriptionType[]
                    {
                        new DescriptionType { CDataValue = new CDataType { Value = (bytesFirmados != null) ? Encoding.UTF8.GetString(bytesFirmados) : invoice.SerializeXmlDocument().ToXmlString(), UseCData = true } }
                    }
                }
            };

            return this;
        }

        [Obsolete("Usar métodos ConEmisor(emisor), ConAdquiriente(adquiriente) y ConDocumento(bytesDocumento, referencia).", false)]
        public GeneradorContenedor ConDocumento(CreditNoteType creditNote, byte[] bytesFirmados = null)
        {
            AttachedDocument.ParentDocumentID = new ParentDocumentIDType { Value = creditNote.ID.Value };

            AttachedDocument.SenderParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = creditNote.AccountingSupplierParty.Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = creditNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = creditNote.AccountingCustomerParty[0].Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = creditNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.Attachment = new AttachmentType
            {
                ExternalReference = new ExternalReferenceType
                {
                    MimeCode = new MimeCodeType { Value = "text/xml" },
                    EncodingCode = new EncodingCodeType { Value = "UTF-8" },
                    Description = new DescriptionType[]
                    {
                        new DescriptionType { CDataValue = new CDataType { Value = (bytesFirmados != null) ? Encoding.UTF8.GetString(bytesFirmados) : creditNote.SerializeXmlDocument().ToXmlString(), UseCData = true } }
                    }
                }
            };

            return this;
        }

        [Obsolete("Usar métodos ConEmisor(emisor), ConAdquiriente(adquiriente) y ConDocumento(bytesDocumento, referencia).", false)]
        public GeneradorContenedor ConDocumento(DebitNoteType debitNote, byte[] bytesFirmados = null)
        {
            AttachedDocument.ParentDocumentID = new ParentDocumentIDType { Value = debitNote.ID.Value };

            AttachedDocument.SenderParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = debitNote.AccountingSupplierParty.Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = debitNote.AccountingSupplierParty.Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = debitNote.AccountingCustomerParty[0].Party.PartyName[0].Name.Value },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeID = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeID,
                            schemeName = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.schemeName,
                            Value = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.Value
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.listName,
                            Value = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxLevelCode.Value
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.ID.Value },
                            Name = new NameType1 { Value = debitNote.AccountingCustomerParty[0].Party.PartyTaxScheme[0].TaxScheme.Name.Value }
                        }
                    }
                }
            };

            AttachedDocument.Attachment = new AttachmentType
            {
                ExternalReference = new ExternalReferenceType
                {
                    MimeCode = new MimeCodeType { Value = "text/xml" },
                    EncodingCode = new EncodingCodeType { Value = "UTF-8" },
                    Description = new DescriptionType[]
                    {
                        new DescriptionType { CDataValue = new CDataType { Value = (bytesFirmados != null) ? Encoding.UTF8.GetString(bytesFirmados) : debitNote.SerializeXmlDocument().ToXmlString(), UseCData = true } }
                    }
                }
            };

            return this;
        }

        public GeneradorContenedor ConEmisor(Emisor emisor)
        {
            AttachedDocument.SenderParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = emisor.RazonSocial },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                            schemeID = NitHelper.CalcularDV(emisor.Identificacion).ToString(),
                            schemeName = "31",
                            Value = emisor.Identificacion
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = emisor.RegimenFiscal.Descripcion,
                            Value = emisor.RegimenFiscal.Codigo
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = emisor.ResponsabilidadTributaria.Codigo },
                            Name = new NameType1 { Value = emisor.ResponsabilidadTributaria.Nombre }
                        }
                    }
                }
            };

            return this;
        }

        public GeneradorContenedor ConAdquiriente(Adquiriente adquiriente)
        {
            AttachedDocument.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = adquiriente.NombreComercial },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                            schemeID = NitHelper.CalcularDV(adquiriente.Identificacion).ToString(),
                            schemeName = adquiriente.TipoIdentificacion.Codigo,
                            Value = adquiriente.Identificacion
                        },
                        TaxLevelCode = new TaxLevelCodeType
                        {
                            listName = adquiriente.RegimenFiscal.Codigo,
                            Value = adquiriente.RegimenFiscal.Codigo
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = adquiriente.ResponsabilidadTributaria.Codigo },
                            Name = new NameType1 { Value = adquiriente.ResponsabilidadTributaria.Nombre }
                        }
                    }
                }
            };

            return this;
        }

        public GeneradorContenedor ConDocumento(byte[] bytesDocumento, ReferenciaDocumento referencia)
        {
            AttachedDocument.ParentDocumentID = new ParentDocumentIDType { Value = referencia.Numero };

            AttachedDocument.Attachment = new AttachmentType
            {
                ExternalReference = new ExternalReferenceType
                {
                    MimeCode = new MimeCodeType { Value = "text/xml" },
                    EncodingCode = new EncodingCodeType { Value = "UTF-8" },
                    Description = new DescriptionType[]
                    {
                        new DescriptionType { CDataValue = new CDataType { Value = Encoding.UTF8.GetString(bytesDocumento), UseCData = true } }
                    }
                }
            };

            Referencia = referencia;

            return this;
        }

        public GeneradorContenedor ConRespuesta(byte[] bytesRespuesta, string codigo, DateTime fecha)
        {
            AttachedDocument.ParentDocumentLineReference = new LineReferenceType[]
            {
                new LineReferenceType
                {
                    LineID = new LineIDType { Value = "1"},
                    DocumentReference = new DocumentReferenceType
                    {
                        ID = new IDType { Value = Referencia.Numero },
                        UUID = new UUIDType
                        {
                            schemeName = Referencia.AlgoritmoCufe.Codigo,
                            Value = Referencia.UUID
                        },
                        IssueDate = new IssueDateType { Value = fecha },
                        DocumentType = new DocumentTypeType { Value = "ApplicationResponse"},
                        Attachment = new AttachmentType
                        {
                            ExternalReference = new ExternalReferenceType
                            {
                                MimeCode = new MimeCodeType { Value = "text/xml" },
                                EncodingCode = new EncodingCodeType { Value = "UTF-8"},
                                Description = new DescriptionType[]
                                {
                                    new DescriptionType { CDataValue = new CDataType { Value = Encoding.UTF8.GetString(bytesRespuesta), UseCData = true } }
                                }
                            }
                        },
                        ResultOfVerification = new ResultOfVerificationType
                        {
                            ValidatorID = new ValidatorIDType { Value = "Unidad Especial Dirección de Impuestos y Aduanas Nacionales"},
                            ValidationResultCode = new ValidationResultCodeType { Value = codigo },
                            ValidationDate = new ValidationDateType { Value = fecha },
                            ValidationTime = new ValidationTimeType { Value = fecha.ToString("HH:mm:ss") + "-05:00" }
                        }
                    }
                }
            };

            return this;
        }

        public AttachedDocumentType Obtener()
        {
            return AttachedDocument;
        }
    }
}