using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;

namespace eFacturacionColombia_NETCore.Documentos
{
    public class GeneradorEvento
    {
        public ApplicationResponseType ApplicationResponse { get; private set; }

        public GeneradorEvento(ApplicationResponseType applicationResponse)
        {
            ApplicationResponse = applicationResponse;
        }

        public GeneradorEvento(AmbienteDestino ambiente, ReferenciaDocumento referencia)
        {
            ApplicationResponse = new ApplicationResponseType();
            ApplicationResponse.UBLVersionID = new UBLVersionIDType { Value = "UBL 2.1" };
            ApplicationResponse.CustomizationID = new CustomizationIDType { Value = "1" };
            ApplicationResponse.ProfileID = new ProfileIDType { Value = "DIAN 2.1: ApplicationResponse de la Factura Electrónica de Venta" };
            ApplicationResponse.ProfileExecutionID = new ProfileExecutionIDType { Value = ((int)ambiente).ToString() };

            ApplicationResponse.UBLExtensions = new UBLExtensionType[]
            {
                new UBLExtensionType { ExtensionContent = new ExtensionContentType { }  },
                new UBLExtensionType { ExtensionContent = new ExtensionContentType { }  }
            };

            ApplicationResponse.ID = new IDType { Value = referencia.Numero + "_" + new Random().Next(10000, 99999) };

            ApplicationResponse.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = "Unidad Especial Dirección de Impuestos y Aduanas Nacionales" },
                        CompanyID = new CompanyIDType
                        {
                            schemeName = "31",
                            schemeID = NitHelper.CalcularDigitoVerificador("800197268").ToString(), // 4
                            Value = "800197268" // nit dian
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = "01" },
                            Name = new NameType1 { Value = "IVA" }
                        }
                    }
                }
            };

            ApplicationResponse.DocumentResponse = new DocumentResponseType[]
            {
                new DocumentResponseType
                {
                    DocumentReference = new DocumentReferenceType[]
                    {
                        new DocumentReferenceType
                        {
                            ID = new IDType { Value = referencia.Numero },
                            UUID =  new UUIDType
                            {
                                schemeName = referencia.AlgoritmoCufe.Codigo,
                                Value = referencia.UUID
                            },
                            DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo }
                        }
                    }
                }
            };
        }


        public GeneradorEvento ConFecha(DateTime fecha)
        {
            ApplicationResponse.IssueDate = new IssueDateType { Value = fecha };

            ApplicationResponse.IssueTime = new IssueTimeType { Value = fecha.ToString("HH:mm:ss") + "-05:00" };

            return this;
        }

        public GeneradorEvento ConNumero(string numero)
        {
            ApplicationResponse.ID = new IDType { Value = numero };

            return this;
        }

        public GeneradorEvento ConExtensionDian(ExtensionDian extension)
        {
            var dianExtensions = new DianExtensionsType
            {
                InvoiceSource = new CountryType
                {
                    IdentificationCode = new IdentificationCodeType
                    {
                        listAgencyID = "6",
                        listAgencyName = "United Nations Economic Commission for Europe",
                        listSchemeURI = "urn:oasis:names:specification:ubl:codelist:gc:CountryIdentificationCode-2.1",
                        Value = extension.PaisOrigen.Codigo
                    }
                },
                SoftwareProvider = new SoftwareProvider
                {
                    ProviderID = new coID2Type
                    {
                        schemeAgencyID = "195",
                        schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                        schemeName = "31",
                        schemeID = NitHelper.CalcularDigitoVerificador(extension.SoftwareProveedorNit).ToString(),
                        Value = extension.SoftwareProveedorNit
                    },
                    SoftwareID = new IdentifierType1
                    {
                        schemeAgencyID = "195",
                        schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                        Value = extension.SoftwareIdentificador
                    }
                },
                SoftwareSecurityCode = new IdentifierType1
                {
                    schemeAgencyID = "195",
                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                    Value = CryptographyHelper.Sha384(extension.SoftwareIdentificador + extension.SoftwarePin + ApplicationResponse.ID.Value)
                },
                AuthorizationProvider = new AuthorizationProviderType
                {
                    AuthorizationProviderID = new IdentifierType1
                    {
                        schemeAgencyID = "195",
                        schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                        schemeName = "31",
                        schemeID = NitHelper.CalcularDigitoVerificador("800197268").ToString(), // 4
                        Value = "800197268" // nit dian
                    }
                },
                QRCode = new QRCodeType { Value = "{{QR_CODE_VALUE}}" }
            };

            ApplicationResponse.UBLExtensions[0] = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType { Any = dianExtensions.SerializeXmlElement() }
            };

            return this;
        }

        public GeneradorEvento ConRegistrante(ParteEvento parte)
        {
            ApplicationResponse.SenderParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = parte.Nombre },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                            schemeName = parte.TipoIdentificacion.Codigo,
                            schemeID = NitHelper.CalcularDigitoVerificador(parte.Identificacion).ToString(), // dv
                            schemeVersionID = ((int)parte.TipoPersona).ToString(),
                            Value = parte.Identificacion
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = parte.ResponsabilidadTributaria.Codigo },
                            Name = new NameType1 { Value = parte.ResponsabilidadTributaria.Nombre }
                        }
                    }
                }
            };

            return this;
        }

        public GeneradorEvento ConReceptor(ParteEvento parte)
        {
            ApplicationResponse.ReceiverParty = new PartyType
            {
                PartyTaxScheme = new PartyTaxSchemeType[]
                {
                    new PartyTaxSchemeType
                    {
                        RegistrationName = new RegistrationNameType { Value = parte.Nombre },
                        CompanyID = new CompanyIDType
                        {
                            schemeAgencyID = "195",
                            schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                            schemeName = parte.TipoIdentificacion.Codigo,
                            schemeID = NitHelper.CalcularDigitoVerificador(parte.Identificacion).ToString(), // dv
                            schemeVersionID = ((int)parte.TipoPersona).ToString(),
                            Value = parte.Identificacion
                        },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = parte.ResponsabilidadTributaria.Codigo },
                            Name = new NameType1 { Value = parte.ResponsabilidadTributaria.Nombre }
                        }
                    }
                }
            };

            return this;
        }

        public GeneradorEvento ParaAcuseRecibo(ReceptorBienes receptor = null)
        {
            if (receptor == null)
            {
                receptor = new ReceptorBienes
                {
                    TipoIdentificacion = new TipoIdentificacion
                    {
                        Codigo = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.schemeName
                    },
                    Identificacion = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.Value,
                    Nombres = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].RegistrationName.Value,
                    Apellidos = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].RegistrationName.Value,
                    Cargo = "N/A",
                    Departamento = "N/A"
                };
            }

            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType { Value = "030" },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = "Acuse de recibo de Factura Electrónica de Venta" }
                }
            };

            ApplicationResponse.DocumentResponse[0].IssuerParty = new PartyType
            {
                Person = new PersonType[]
                {
                    new PersonType
                    {
                        ID = new IDType
                        {
                            schemeName = receptor.TipoIdentificacion.Codigo,
                            schemeID = NitHelper.CalcularDigitoVerificador(receptor.Identificacion).ToString(), // dv
                            Value = receptor.Identificacion
                        },
                        FirstName = new FirstNameType { Value = receptor.Nombres },
                        FamilyName = new FamilyNameType { Value = receptor.Apellidos },
                        JobTitle = new JobTitleType { Value  = receptor.Cargo },
                        OrganizationDepartment = new OrganizationDepartmentType { Value = receptor.Departamento }
                    }
                }
            };

            return this;
        }

        public GeneradorEvento ParaRechazoDocumento(ConceptoEventoRechazo concepto = null)
        {
            concepto = concepto ?? ConceptoEventoRechazo.INCONSISTENCIAS_DOCUMENTO;

            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType
                {
                    Value = "031",
                    listID = concepto.Codigo
                },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = "Reclamo de la Factura Electrónica de Venta" }
                }
            };

            return this;
        }

        public GeneradorEvento ParaRecepcionBienes(ReceptorBienes receptor = null)
        {
            if (receptor == null)
            {
                receptor = new ReceptorBienes
                {
                    TipoIdentificacion = new TipoIdentificacion
                    {
                        Codigo = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.schemeName
                    },
                    Identificacion = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.Value,
                    Nombres = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].RegistrationName.Value,
                    Apellidos = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].RegistrationName.Value,
                    Cargo = "N/A",
                    Departamento = "N/A"
                };
            }

            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType { Value = "032" },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = "Recibo del bien y/o prestación del servicio" }
                }
            };

            ApplicationResponse.DocumentResponse[0].IssuerParty = new PartyType
            {
                Person = new PersonType[]
                {
                    new PersonType
                    {
                        ID = new IDType
                        {
                            schemeName = receptor.TipoIdentificacion.Codigo,
                            schemeID = NitHelper.CalcularDigitoVerificador(receptor.Identificacion).ToString(), // dv
                            Value = receptor.Identificacion
                        },
                        FirstName = new FirstNameType { Value = receptor.Nombres },
                        FamilyName = new FamilyNameType { Value = receptor.Apellidos },
                        JobTitle = new JobTitleType { Value  = receptor.Cargo },
                        OrganizationDepartment = new OrganizationDepartmentType { Value = receptor.Departamento }
                    }
                }
            };

            return this;
        }

        public GeneradorEvento ParaAceptacionDocumento()
        {
            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType { Value = "033" },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = "Aceptación expresa" }
                }
            };

            return this;
        }

        public GeneradorEvento ParaAceptacionExpresa() => ParaAceptacionDocumento();

        public GeneradorEvento ParaAceptacionTacita()
        {
            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType { Value = "034" },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = "Aceptación Tácita" }
                }
            };

            return this;
        }

        public GeneradorEvento ParaEvento(string codigo, string descripcion, string subcodigo = null)
        {
            ApplicationResponse.DocumentResponse[0].Response = new ResponseType
            {
                ResponseCode = new ResponseCodeType { Value = codigo, listID = subcodigo },
                Description = new DescriptionType[]
                {
                    new DescriptionType { Value = descripcion }
                }
            };

            return this;
        }

        public GeneradorEvento AsignarCUDE(string pinSoftware = null)
        {
            ApplicationResponse.UUID = new UUIDType
            {
                schemeID = ApplicationResponse.ProfileExecutionID.Value,
                schemeName = AlgoritmoSeguridadUUID.CUDE_SHA384.Codigo,
                Value = CrearCUDE(ApplicationResponse, pinSoftware)
            };

            var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + ApplicationResponse.DocumentResponse[0].DocumentReference[0].UUID.Value;
            if (ApplicationResponse.ProfileExecutionID.Value == ((int)AmbienteDestino.PRUEBAS).ToString())
                qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + ApplicationResponse.DocumentResponse[0].DocumentReference[0].UUID.Value;

            if (ApplicationResponse.UBLExtensions != null &&
                ApplicationResponse.UBLExtensions.Length >= 1)
            {
                var ublExtension = ApplicationResponse.UBLExtensions[0];
                ublExtension.ExtensionContent.Any.InnerXml = ublExtension.ExtensionContent.Any.InnerXml.Replace("{{QR_CODE_VALUE}}", qrCodeValue);
            }

            return this;
        }

        public ApplicationResponseType Obtener()
        {
            return ApplicationResponse;
        }


        public static string CrearCUDE(ApplicationResponseType applicationResponse, string pinSoftware = null)
        {
            var NumDE = applicationResponse.ID.Value;

            var FecEmi = applicationResponse.IssueDate.Value.ToString("yyyy-MM-dd");

            var HorEmi = applicationResponse.IssueTime.Value;

            var NitFE = applicationResponse.SenderParty.PartyTaxScheme[0].CompanyID.Value;

            var DocAdq = applicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.Value;

            var ResCode = applicationResponse.DocumentResponse[0].Response.ResponseCode.Value;

            var ID = applicationResponse.DocumentResponse[0].DocumentReference[0].ID.Value;

            var DocType = applicationResponse.DocumentResponse[0].DocumentReference[0].DocumentTypeCode.Value;

            string cadena = NumDE + FecEmi + HorEmi + NitFE + DocAdq + ResCode + ID + DocType + pinSoftware;

            using (var sha384Hash = SHA384.Create())
            {
                var sourceBytes = Encoding.UTF8.GetBytes(cadena);
                var hashBytes = sha384Hash.ComputeHash(sourceBytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);

                return hash.ToLower();
            }
        }
    }
}