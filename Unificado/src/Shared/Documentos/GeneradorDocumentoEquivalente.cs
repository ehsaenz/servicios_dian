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
    public class GeneradorDocumentoEquivalente
    {
        private string _stringQR = "";

        public InvoiceType Invoice { get; private set; }

        public GeneradorDocumentoEquivalente(AmbienteDestino ambiente, TipoDocumentoEquivalente tipo)
        {
            Invoice = new InvoiceType();
            Invoice.UBLVersionID = new UBLVersionIDType { Value = "UBL 2.1" };
            Invoice.CustomizationID = new CustomizationIDType { Value = OperacionDocumentoEquivalente.GENERAL.Codigo };
            Invoice.ProfileID = new ProfileIDType { Value = string.IsNullOrEmpty(tipo.Version) ? "DIAN 2.1: Documento Equivalente" : tipo.Version };
            Invoice.ProfileExecutionID = new ProfileExecutionIDType { Value = ((int)ambiente).ToString() };
            Invoice.UBLExtensions = new UBLExtensionType[]
            {
                new UBLExtensionType { ExtensionContent = new ExtensionContentType { }  },
                new UBLExtensionType { ExtensionContent = new ExtensionContentType { }  }
            };

            Invoice.InvoiceTypeCode = new InvoiceTypeCodeType { Value = tipo.Codigo };
        }

        public GeneradorDocumentoEquivalente(InvoiceType invoice)
        {
            Invoice = invoice;
        }


        public GeneradorDocumentoEquivalente ConOperacion(OperacionDocumentoEquivalente operacion)
        {
            Invoice.CustomizationID = new CustomizationIDType { Value = operacion.Codigo };

            if (!string.IsNullOrEmpty(operacion.Version))
                Invoice.ProfileID = new ProfileIDType { Value = operacion.Version };

            return this;
        }

        public GeneradorDocumentoEquivalente ConNumero(string numero)
        {
            Invoice.ID = new IDType { Value = numero };

            return this;
        }

        public GeneradorDocumentoEquivalente ConFecha(DateTime fecha)
        {
            Invoice.IssueDate = new IssueDateType { Value = fecha };

            Invoice.IssueTime = new IssueTimeType { Value = fecha.ToString("HH:mm:ss") + "-05:00" };

            return this;
        }

        public GeneradorDocumentoEquivalente ConVencimiento(DateTime fecha)
        {
            Invoice.DueDate = new DueDateType { Value = fecha };

            return this;
        }

        public GeneradorDocumentoEquivalente ConNota(string texto)
        {
            Invoice.Note = new NoteType[] { new NoteType { Value = texto } };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarNota(string texto)
        {
            var notes = Invoice.Note?.ToList() ?? new List<NoteType>();

            var note = new NoteType { Value = texto };

            notes.Add(note);

            Invoice.Note = notes.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente ConMoneda(Moneda moneda)
        {
            Invoice.DocumentCurrencyCode = new DocumentCurrencyCodeType { Value = moneda.Codigo };

            return this;
        }

        public GeneradorDocumentoEquivalente ConPeriodoFacturacion(PeriodoFacturacion periodo)
        {
            Invoice.InvoicePeriod = new PeriodType[]
            {
                new PeriodType
                {
                    StartDate = new StartDateType { Value = periodo.FechaInicio },
                    StartTime = new StartTimeType { Value = periodo.FechaInicio.ToString("HH:mm:ss") + "-05:00" },
                    EndDate = new EndDateType { Value = periodo.FechaFin },
                    EndTime = new EndTimeType { Value = periodo.FechaFin.ToString("HH:mm:ss") + "-05:00" }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente ConOrdenCompra(OrdenCompra orden)
        {
            Invoice.OrderReference = new OrderReferenceType
            {
                ID = new IDType { Value = orden.Numero },
                IssueDate = new IssueDateType { Value = orden.Fecha }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente ConReferencia(ReferenciaDocumento referencia)
        {
            if (referencia.TipoDocumento.Codigo == TipoDocumentoElectronico.FACTURA_CONTINGENCIA_FACTURADOR.Codigo)
            {
                Invoice.AdditionalDocumentReference = new DocumentReferenceType[]
                {
                    new DocumentReferenceType
                    {
                        ID = new IDType { Value = referencia.Numero },
                        UUID = new UUIDType
                        {
                            schemeName = referencia.AlgoritmoCufe.Codigo,
                            Value = referencia.UUID
                        },
                        IssueDate = new IssueDateType { Value = referencia.Fecha },
                        DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo }
                    }
                };
            }
            else
            {
                Invoice.BillingReference = new BillingReferenceType[]
                {
                    new BillingReferenceType
                    {
                        InvoiceDocumentReference = !referencia.EsFactura ? null : new DocumentReferenceType
                        {
                            ID = new IDType { Value = referencia.Numero },
                            UUID = new UUIDType
                            {
                                schemeName = referencia.AlgoritmoCufe.Codigo,
                                Value = referencia.UUID
                            },
                            DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                            IssueDate = new IssueDateType { Value = referencia.Fecha }
                        },
                        CreditNoteDocumentReference = !referencia.EsNotaCredito ? null : new DocumentReferenceType
                        {
                            ID = new IDType { Value = referencia.Numero },
                            UUID = new UUIDType
                            {
                                schemeName = referencia.AlgoritmoCufe.Codigo,
                                Value = referencia.UUID
                            },
                            DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                            IssueDate = new IssueDateType { Value = referencia.Fecha }
                        },
                        DebitNoteDocumentReference = !referencia.EsNotaDebito ? null : new DocumentReferenceType
                        {
                            ID = new IDType { Value = referencia.Numero },
                            UUID = new UUIDType
                            {
                                schemeName = referencia.AlgoritmoCufe.Codigo,
                                Value = referencia.UUID
                            },
                            DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                            IssueDate = new IssueDateType { Value = referencia.Fecha }
                        }
                    }
                };
            }

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarReferencia(ReferenciaDocumento referencia)
        {
            if (referencia.TipoDocumento.Codigo == TipoDocumentoElectronico.FACTURA_CONTINGENCIA_FACTURADOR.Codigo)
            {
                var referencias = Invoice.AdditionalDocumentReference?.ToList() ?? new List<DocumentReferenceType>();

                referencias.Add(new DocumentReferenceType
                {
                    ID = new IDType { Value = referencia.Numero },
                    UUID = new UUIDType
                    {
                        schemeName = referencia.AlgoritmoCufe.Codigo,
                        Value = referencia.UUID
                    },
                    IssueDate = new IssueDateType { Value = referencia.Fecha },
                    DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo }
                });

                Invoice.AdditionalDocumentReference = referencias.ToArray();
            }
            else
            {
                var referencias = Invoice.BillingReference?.ToList() ?? new List<BillingReferenceType>();

                referencias.Add(new BillingReferenceType
                {
                    InvoiceDocumentReference = !referencia.EsFactura ? null : new DocumentReferenceType
                    {
                        ID = new IDType { Value = referencia.Numero },
                        UUID = new UUIDType
                        {
                            schemeName = referencia.AlgoritmoCufe.Codigo,
                            Value = referencia.UUID
                        },
                        DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                        IssueDate = new IssueDateType { Value = referencia.Fecha }
                    },
                    CreditNoteDocumentReference = !referencia.EsNotaCredito ? null : new DocumentReferenceType
                    {
                        ID = new IDType { Value = referencia.Numero },
                        UUID = new UUIDType
                        {
                            schemeName = referencia.AlgoritmoCufe.Codigo,
                            Value = referencia.UUID
                        },
                        DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                        IssueDate = new IssueDateType { Value = referencia.Fecha }
                    },
                    DebitNoteDocumentReference = !referencia.EsNotaDebito ? null : new DocumentReferenceType
                    {
                        ID = new IDType { Value = referencia.Numero },
                        UUID = new UUIDType
                        {
                            schemeName = referencia.AlgoritmoCufe.Codigo,
                            Value = referencia.UUID
                        },
                        DocumentTypeCode = new DocumentTypeCodeType { Value = referencia.TipoDocumento.Codigo },
                        IssueDate = new IssueDateType { Value = referencia.Fecha }
                    }
                });

                Invoice.BillingReference = referencias.ToArray();
            }

            return this;
        }

        public GeneradorDocumentoEquivalente ConExtensionDian(ExtensionDian extension)
        {
            var dianExtensions = new DianExtensionsType
            {
                InvoiceControl = new InvoiceControl
                {
                    InvoiceAuthorization = new NumericType1 { Value = extension.RangoNumeracion.NumeroResolucion },
                    AuthorizationPeriod = new PeriodType
                    {
                        StartDate = new StartDateType { Value = extension.RangoNumeracion.VigenciaDesde },
                        EndDate = new EndDateType { Value = extension.RangoNumeracion.VigenciaHasta }
                    },
                    AuthorizedInvoices = new AuthrorizedInvoices
                    {
                        Prefix = extension.RangoNumeracion.Prefijo,
                        From = extension.RangoNumeracion.Desde,
                        To = extension.RangoNumeracion.Hasta
                    }
                },
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
                    Value = CryptographyHelper.Sha384(extension.SoftwareIdentificador + extension.SoftwarePin + Invoice.ID.Value    )
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

            Invoice.UBLExtensions[0] = new UBLExtensionType
            {
                ExtensionContent = new ExtensionContentType { Any = dianExtensions.SerializeXmlElement() }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente ConEmisor(Emisor emisor)
        {
            Invoice.AccountingSupplierParty = new SupplierPartyType
            {
                AdditionalAccountID = new AdditionalAccountIDType[]
                {
                    new AdditionalAccountIDType
                    {
                        schemeAgencyID = "195",
                        Value = ((int)emisor.TipoPersona).ToString()
                    }
                },
                Party = new PartyType
                {
                    IndustryClassificationCode = string.IsNullOrEmpty(emisor.CodigoCiiu) ? null : new IndustryClassificationCodeType
                    {
                        Value = emisor.CodigoCiiu
                    },
                    PartyName = new PartyNameType[]
                    {
                        new PartyNameType
                        {
                            Name = new NameType1 { Value = emisor.DatosComerciales.NombreComercial }
                        }
                    },
                    PhysicalLocation = new LocationType1
                    {
                        Address = new AddressType
                        {
                            ID = (emisor.DireccionFiscal.Municipio != null) ?
                                new IDType { Value = emisor.DireccionFiscal.Municipio.Codigo } : null,
                            CityName = (emisor.DireccionFiscal.Municipio != null) ?
                                new CityNameType { Value = emisor.DireccionFiscal.Municipio.Nombre } : null,
                            CountrySubentity = (emisor.DireccionFiscal.Departamento != null) ?
                                new CountrySubentityType { Value = emisor.DireccionFiscal.Departamento.Nombre } : null,
                            CountrySubentityCode = (emisor.DireccionFiscal.Departamento != null) ?
                                new CountrySubentityCodeType { Value = emisor.DireccionFiscal.Departamento.Codigo } : null,
                            // La regla DEAJ73 pide el código postal del emisor, de la tabla del anexo. No se emite el valor
                            // por defecto "N/A" de DireccionFisica porque no es un código válido.
                            PostalZone = (string.IsNullOrWhiteSpace(emisor.DireccionFiscal.CodigoPostal) || emisor.DireccionFiscal.CodigoPostal == "N/A") ?
                                null : new PostalZoneType { Value = emisor.DireccionFiscal.CodigoPostal },
                            AddressLine = new AddressLineType[]
                            {
                                new AddressLineType { Line =  new LineType{ Value = emisor.DireccionFiscal.Linea } }
                            },
                            Country = new CountryType
                            {
                                IdentificationCode = new IdentificationCodeType { Value = emisor.DireccionFiscal.Pais.Codigo },
                                Name = new NameType1
                                {
                                    languageID = "es",
                                    Value = emisor.DireccionFiscal.Pais.Nombre
                                }
                            }
                        }
                    },
                    PartyTaxScheme = new PartyTaxSchemeType[]
                    {
                        new PartyTaxSchemeType
                        {
                            RegistrationName = new RegistrationNameType { Value = emisor.DatosComerciales.NombreComercial },
                            CompanyID = new CompanyIDType
                            {
                                schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                schemeAgencyID = "195",
                                schemeName = "31", // nit
                                schemeID = NitHelper.CalcularDigitoVerificador(emisor.Identificacion).ToString(), // dv
                                Value = emisor.Identificacion
                            },
                            RegistrationAddress = new AddressType
                            {
                                ID = (emisor.DireccionFiscal.Municipio != null) ?
                                    new IDType { Value = emisor.DireccionFiscal.Municipio.Codigo } : null,
                                CityName = (emisor.DireccionFiscal.Municipio != null) ?
                                    new CityNameType { Value = emisor.DireccionFiscal.Municipio.Nombre } : null,
                                CountrySubentity = (emisor.DireccionFiscal.Departamento != null) ?
                                    new CountrySubentityType { Value = emisor.DireccionFiscal.Departamento.Nombre } : null,
                                CountrySubentityCode = (emisor.DireccionFiscal.Departamento != null) ?
                                    new CountrySubentityCodeType { Value = emisor.DireccionFiscal.Departamento.Codigo } : null,
                                AddressLine = new AddressLineType[]
                                {
                                    new AddressLineType { Line =  new LineType{ Value = emisor.DireccionFiscal.Linea } }
                                },
                                Country = new CountryType
                                {
                                    IdentificationCode = new IdentificationCodeType { Value = emisor.DireccionFiscal.Pais.Codigo },
                                    Name = new NameType1
                                    {
                                        languageID = "es",
                                        Value = emisor.DireccionFiscal.Pais.Nombre
                                    }
                                }
                            },
                            TaxLevelCode = new TaxLevelCodeType
                            {
                                Value = emisor.RegimenFiscal?.Codigo ?? "R-99-PN",
                                listName = emisor.RegimenFiscal?.Descripcion ?? "No aplica – Otros *"
                            },
                            TaxScheme = new TaxSchemeType
                            {
                                ID = new IDType { Value = emisor.ResponsabilidadTributaria.Codigo },
                                Name = new NameType1 { Value = emisor.ResponsabilidadTributaria.Nombre }
                            }
                        }
                    },
                    PartyLegalEntity = new PartyLegalEntityType[]
                    {
                        new PartyLegalEntityType
                        {
                            RegistrationName = new RegistrationNameType { Value = emisor.DatosComerciales.NombreComercial },
                            CompanyID = new CompanyIDType
                            {
                                schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                schemeAgencyID = "195",
                                schemeName = "31", // nit
                                schemeID = NitHelper.CalcularDigitoVerificador(emisor.Identificacion).ToString(), // dv
                                Value = emisor.Identificacion
                            },
                            CorporateRegistrationScheme = new CorporateRegistrationSchemeType
                            {
                                ID = new IDType { Value = emisor.PrefijoRangoNumeracion },
                                Name = new NameType1 { Value = emisor.DatosComerciales.MatriculaMercantil }
                            }
                        }
                    },
                    Contact = (emisor.NumeroTelefonico == null && emisor.CorreoElectronico == null) ? null : new ContactType
                    {
                        Telephone = new TelephoneType { Value = emisor.NumeroTelefonico },
                        ElectronicMail = new ElectronicMailType { Value = emisor.CorreoElectronico }
                    }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente ConAdquiriente(Adquiriente adquiriente)
        {
            Invoice.AccountingCustomerParty = new CustomerPartyType[]
            {
                new CustomerPartyType
                {
                    CustomerAssignedAccountID = !string.IsNullOrEmpty(adquiriente.NumeroContrato) ? new CustomerAssignedAccountIDType
                    {
                        Value = adquiriente.NumeroContrato
                    } : null,
                    AdditionalAccountID = new AdditionalAccountIDType[]
                    {
                        new AdditionalAccountIDType
                        {
                            schemeAgencyID = "195",
                            Value = ((int)adquiriente.TipoPersona).ToString()
                        }
                    },
                    Party = new PartyType
                    {
                        PartyIdentification = adquiriente.EsPersonaJuridica ? null : new PartyIdentificationType[]
                        {
                            new PartyIdentificationType
                            {
                                ID = new IDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                }
                            }
                        },
                        PartyName = new PartyNameType[]
                        {
                            new PartyNameType
                            {
                                Name = new NameType1 { Value = adquiriente.NombreComercial }
                            }
                        },
                        PhysicalLocation = new LocationType1
                        {
                            Address = new AddressType
                            {
                                ID = (adquiriente.DireccionComercial.Municipio != null) ?
                                    new IDType { Value = adquiriente.DireccionComercial.Municipio.Codigo } : null,
                                CityName = (adquiriente.DireccionComercial.Municipio != null) ?
                                    new CityNameType { Value = adquiriente.DireccionComercial.Municipio.Nombre } : null,
                                CountrySubentity = (adquiriente.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityType { Value = adquiriente.DireccionComercial.Departamento.Nombre } : null,
                                CountrySubentityCode = (adquiriente.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityCodeType { Value = adquiriente.DireccionComercial.Departamento.Codigo } : null,
                                AddressLine = new AddressLineType[]
                                {
                                    new AddressLineType { Line =  new LineType{ Value = adquiriente.DireccionComercial.Linea } }
                                },
                                Country = new CountryType
                                {
                                    IdentificationCode = new IdentificationCodeType { Value = adquiriente.DireccionComercial.Pais.Codigo },
                                    Name = new NameType1
                                    {
                                        languageID = "es",
                                        Value = adquiriente.DireccionComercial.Pais.Nombre
                                    }
                                }
                            }
                        },
                        PartyTaxScheme = new PartyTaxSchemeType[]
                        {
                            new PartyTaxSchemeType
                            {
                                RegistrationName = new RegistrationNameType { Value = adquiriente.RazonSocial ?? adquiriente.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                },
                                TaxLevelCode = new TaxLevelCodeType
                                {
                                    listName = adquiriente.RegimenFiscal?.Descripcion ?? "No aplica – Otros *",
                                    Value = adquiriente.RegimenFiscal?.Codigo ?? "R-99-PN"
                                },
                                TaxScheme = new TaxSchemeType
                                {
                                    ID = new IDType { Value = adquiriente.ResponsabilidadTributaria.Codigo },
                                    Name = new NameType1 { Value = adquiriente.ResponsabilidadTributaria.Nombre }
                                }
                            }
                        },
                        PartyLegalEntity = new PartyLegalEntityType[]
                        {
                            new PartyLegalEntityType
                            {
                                RegistrationName = new RegistrationNameType { Value = adquiriente.RazonSocial ?? adquiriente.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                },
                                CorporateRegistrationScheme = new CorporateRegistrationSchemeType
                                {
                                    Name = new NameType1 { Value = adquiriente.MatriculaMercantil }
                                },
                                ShareholderParty = new ShareholderPartyType[]
                                {
                                    new ShareholderPartyType
                                    {
                                        PartecipationPercent = new PartecipationPercentType { Value = 100 }
                                    }
                                }
                            }
                        },
                        Contact = (adquiriente.NumeroTelefonico == null && adquiriente.CorreoElectronico == null) ? null : new ContactType
                        {
                            Telephone = new TelephoneType { Value = adquiriente.NumeroTelefonico },
                            ElectronicMail = new ElectronicMailType { Value = adquiriente.CorreoElectronico }
                        }
                    }
                }
            };


            return this;
        }

        public GeneradorDocumentoEquivalente AgregarAdquiriente(Adquiriente adquiriente, decimal porcentajeParticipacion)
        {
            var accountingCustomerList = Invoice.AccountingCustomerParty?.ToList() ?? new List<CustomerPartyType>();

            bool agregarAccountingCustomer = true;
            for (int x = 0; x < accountingCustomerList.Count; x++)
            {
                if (accountingCustomerList[x].Party.PartyTaxScheme[0].CompanyID.Value == adquiriente.Identificacion)
                {
                    accountingCustomerList[x].Party.PartyLegalEntity[0].ShareholderParty[0].PartecipationPercent.Value = porcentajeParticipacion;
                    agregarAccountingCustomer = false;
                }
            }

            if (agregarAccountingCustomer)
            {
                var customerParty = new CustomerPartyType
                {
                    CustomerAssignedAccountID = !string.IsNullOrEmpty(adquiriente.NumeroContrato) ? new CustomerAssignedAccountIDType
                    {
                        Value = adquiriente.NumeroContrato
                    } : null,
                    AdditionalAccountID = new AdditionalAccountIDType[]
                    {
                        new AdditionalAccountIDType
                        {
                            schemeAgencyID = "195",
                            Value = ((int)adquiriente.TipoPersona).ToString()
                        }
                    },
                    Party = new PartyType
                    {
                        PartyIdentification = adquiriente.EsPersonaJuridica ? null : new PartyIdentificationType[]
                        {
                            new PartyIdentificationType
                            {
                                ID = new IDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                }
                            }
                        },
                        PartyName = new PartyNameType[]
                        {
                            new PartyNameType
                            {
                                Name = new NameType1 { Value = adquiriente.NombreComercial }
                            }
                        },
                        PhysicalLocation = new LocationType1
                        {
                            Address = new AddressType
                            {
                                ID = (adquiriente.DireccionComercial.Municipio != null) ?
                                    new IDType { Value = adquiriente.DireccionComercial.Municipio.Codigo } : null,
                                CityName = (adquiriente.DireccionComercial.Municipio != null) ?
                                    new CityNameType { Value = adquiriente.DireccionComercial.Municipio.Nombre } : null,
                                CountrySubentity = (adquiriente.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityType { Value = adquiriente.DireccionComercial.Departamento.Nombre } : null,
                                CountrySubentityCode = (adquiriente.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityCodeType { Value = adquiriente.DireccionComercial.Departamento.Codigo } : null,
                                AddressLine = new AddressLineType[]
                                {
                                    new AddressLineType { Line =  new LineType{ Value = adquiriente.DireccionComercial.Linea } }
                                },
                                Country = new CountryType
                                {
                                    IdentificationCode = new IdentificationCodeType { Value = adquiriente.DireccionComercial.Pais.Codigo },
                                    Name = new NameType1
                                    {
                                        languageID = "es",
                                        Value = adquiriente.DireccionComercial.Pais.Nombre
                                    }
                                }
                            }
                        },
                        PartyTaxScheme = new PartyTaxSchemeType[]
                        {
                            new PartyTaxSchemeType
                            {
                                RegistrationName = new RegistrationNameType { Value = adquiriente.RazonSocial ?? adquiriente.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                },
                                TaxLevelCode = new TaxLevelCodeType
                                {
                                    listName = adquiriente.RegimenFiscal?.Descripcion ?? "No aplica – Otros *",
                                    Value = adquiriente.RegimenFiscal?.Codigo
                                },
                                TaxScheme = new TaxSchemeType
                                {
                                    ID = new IDType { Value = adquiriente.ResponsabilidadTributaria.Codigo },
                                    Name = new NameType1 { Value = adquiriente.ResponsabilidadTributaria.Nombre }
                                }
                            }
                        },
                        PartyLegalEntity = new PartyLegalEntityType[]
                        {
                            new PartyLegalEntityType
                            {
                                RegistrationName = new RegistrationNameType { Value = adquiriente.RazonSocial ?? adquiriente.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = adquiriente.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(adquiriente.Identificacion).ToString(), // dv
                                    Value = adquiriente.Identificacion
                                },
                                CorporateRegistrationScheme = new CorporateRegistrationSchemeType
                                {
                                    Name = new NameType1 { Value = adquiriente.MatriculaMercantil }
                                },
                                ShareholderParty = new ShareholderPartyType[]
                                {
                                    new ShareholderPartyType
                                    {
                                        PartecipationPercent = new PartecipationPercentType { Value = porcentajeParticipacion }
                                    }
                                }
                            }
                        },
                        Contact = (adquiriente.NumeroTelefonico == null && adquiriente.CorreoElectronico == null) ? null : new ContactType
                        {
                            Telephone = new TelephoneType { Value = adquiriente.NumeroTelefonico },
                            ElectronicMail = new ElectronicMailType { Value = adquiriente.CorreoElectronico }
                        }
                    }
                };

                accountingCustomerList.Add(customerParty);
            }

            Invoice.AccountingCustomerParty = accountingCustomerList.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente ParaConsumidorFinal()
        {
            Invoice.AccountingCustomerParty = new CustomerPartyType[]
            {
                new CustomerPartyType
                {
                    AdditionalAccountID = new AdditionalAccountIDType[]
                    {
                        new AdditionalAccountIDType
                        {
                            schemeAgencyID = "195",
                            Value = ((int)TipoPersona.JURIDICA).ToString()
                        }
                    },
                    Party = new PartyType
                    {
                        PartyIdentification = new PartyIdentificationType[]
                        {
                            new PartyIdentificationType
                            {
                                ID = new IDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = "13",
                                    schemeID = NitHelper.CalcularDigitoVerificador("222222222222").ToString(),
                                    Value = "222222222222"
                                }
                            }
                        },
                        PartyName = new PartyNameType[]
                        {
                            new PartyNameType
                            {
                                Name = new NameType1 { Value = "Consumidor Final" }
                            }
                        },
                        PartyTaxScheme = new PartyTaxSchemeType[]
                        {
                            new PartyTaxSchemeType
                            {
                                RegistrationName = new RegistrationNameType { Value = "Consumidor Final" },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = "13",
                                    schemeID = NitHelper.CalcularDigitoVerificador("222222222222").ToString(),
                                    Value = "222222222222"
                                },
                                TaxScheme = new TaxSchemeType
                                {
                                    ID = new IDType { Value = "ZZ" },
                                    Name = new NameType1 { Value = "No aplica" }
                                }
                            }
                        }
                    }
                }
            };


            return this;
        }

        public GeneradorDocumentoEquivalente ConDetallesEntrega(DetallesEntrega entrega)
        {
            Invoice.Delivery = new DeliveryType[]
            {
                new DeliveryType
                {
                    ActualDeliveryDate = new ActualDeliveryDateType { Value = entrega.Fecha },
                    ActualDeliveryTime = new ActualDeliveryTimeType { Value = entrega.Fecha.ToString("HH:mm:ss") + "-05:00" },
                    DeliveryAddress = new AddressType
                    {
                        ID = (entrega.Direccion.Municipio != null) ?
                            new IDType { Value = entrega.Direccion.Municipio.Codigo } : null,
                        CityName = (entrega.Direccion.Municipio != null) ?
                            new CityNameType { Value = entrega.Direccion.Municipio.Nombre } : null,
                        CountrySubentity = (entrega.Direccion.Departamento != null) ?
                            new CountrySubentityType { Value = entrega.Direccion.Departamento.Nombre } : null,
                        CountrySubentityCode = (entrega.Direccion.Departamento != null) ?
                            new CountrySubentityCodeType { Value = entrega.Direccion.Departamento.Codigo } : null,
                        AddressLine = new AddressLineType[]
                        {
                            new AddressLineType { Line =  new LineType{ Value = entrega.Direccion.Linea } }
                        },
                        Country = new CountryType
                        {
                            IdentificationCode = new IdentificationCodeType { Value = entrega.Direccion.Pais.Codigo },
                            Name = new NameType1
                            {
                                languageID = "es",
                                Value = entrega.Direccion.Pais.Nombre
                            }
                        }
                    },
                    DeliveryParty = (entrega.Transportador == null) ? null : new PartyType
                    {
                        PartyIdentification = entrega.Transportador.EsPersonaJuridica ? null : new PartyIdentificationType[]
                        {
                            new PartyIdentificationType
                            {
                                ID = new IDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = entrega.Transportador.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(entrega.Transportador.Identificacion).ToString(), // dv
                                    Value = entrega.Transportador.Identificacion
                                }
                            }
                        },
                        PartyName = new PartyNameType[]
                        {
                            new PartyNameType
                            {
                                Name = new NameType1 { Value = entrega.Transportador.NombreComercial }
                            }
                        },
                        PhysicalLocation = new LocationType1
                        {
                            Address = new AddressType
                            {
                                ID = (entrega.Transportador.DireccionComercial.Municipio != null) ?
                                    new IDType { Value = entrega.Transportador.DireccionComercial.Municipio.Codigo } : null,
                                CityName = (entrega.Transportador.DireccionComercial.Municipio != null) ?
                                    new CityNameType { Value = entrega.Transportador.DireccionComercial.Municipio.Nombre } : null,
                                CountrySubentity = (entrega.Transportador.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityType { Value = entrega.Transportador.DireccionComercial.Departamento.Nombre } : null,
                                CountrySubentityCode = (entrega.Transportador.DireccionComercial.Departamento != null) ?
                                    new CountrySubentityCodeType { Value = entrega.Transportador.DireccionComercial.Departamento.Codigo } : null,
                                AddressLine = new AddressLineType[]
                                {
                                    new AddressLineType { Line =  new LineType{ Value = entrega.Transportador.DireccionComercial.Linea } }
                                },
                                Country = new CountryType
                                {
                                    IdentificationCode = new IdentificationCodeType { Value = entrega.Transportador.DireccionComercial.Pais.Codigo },
                                    Name = new NameType1
                                    {
                                        languageID = "es",
                                        Value = entrega.Transportador.DireccionComercial.Pais.Nombre
                                    }
                                }
                            }
                        },
                        PartyTaxScheme = new PartyTaxSchemeType[]
                        {
                            new PartyTaxSchemeType
                            {
                                RegistrationName = new RegistrationNameType { Value = entrega.Transportador.RazonSocial ?? entrega.Transportador.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = entrega.Transportador.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(entrega.Transportador.Identificacion).ToString(), // dv
                                    Value = entrega.Transportador.Identificacion
                                },
                                TaxLevelCode = new TaxLevelCodeType
                                {
                                    listName = entrega.Transportador.RegimenFiscal?.Descripcion ?? "No aplica – Otros *",
                                    Value = entrega.Transportador.RegimenFiscal?.Codigo ?? "R-99-PN"
                                },
                                TaxScheme = new TaxSchemeType
                                {
                                    ID = new IDType { Value = entrega.Transportador.ResponsabilidadTributaria.Codigo },
                                    Name = new NameType1 { Value = entrega.Transportador.ResponsabilidadTributaria.Nombre }
                                }
                            }
                        },
                        PartyLegalEntity = new PartyLegalEntityType[]
                        {
                            new PartyLegalEntityType
                            {
                                RegistrationName = new RegistrationNameType { Value = entrega.Transportador.RazonSocial ?? entrega.Transportador.NombreComercial },
                                CompanyID = new CompanyIDType
                                {
                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                    schemeAgencyID = "195",
                                    schemeName = entrega.Transportador.TipoIdentificacion.Codigo,
                                    schemeID = NitHelper.CalcularDigitoVerificador(entrega.Transportador.Identificacion).ToString(), // dv
                                    Value = entrega.Transportador.Identificacion
                                },
                                CorporateRegistrationScheme = new CorporateRegistrationSchemeType
                                {
                                    Name = new NameType1 { Value = entrega.Transportador.MatriculaMercantil }
                                }
                            }
                        },
                        Contact = (entrega.Transportador.NumeroTelefonico == null && entrega.Transportador.CorreoElectronico == null) ? null : new ContactType
                        {
                            Telephone = new TelephoneType { Value = entrega.Transportador.NumeroTelefonico },
                            ElectronicMail = new ElectronicMailType { Value = entrega.Transportador.CorreoElectronico }
                        }
                    }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente ConAnticipo(Anticipo anticipo)
        {
            Invoice.PrepaidPayment = new PaymentType[]
            {
                new PaymentType
                {
                    ID = new IDType { schemeID = anticipo.ConceptoAnticipo?.Codigo, Value = "1" },
                    PaidAmount = new PaidAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = anticipo.Valor
                    },
                    PaidDate = new PaidDateType { Value = anticipo.FechaPago },
                    PaidTime = new PaidTimeType { Value = anticipo.FechaPago.ToString("HH:mm:ss") + "-05:00" },
                    ReceivedDate = new ReceivedDateType { Value = anticipo.FechaRecepcionPago },
                    InstructionID = new InstructionIDType { Value = anticipo.Instrucciones }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarAnticipo(Anticipo anticipo)
        {
            var prepaidPayments = (Invoice.PrepaidPayment != null) ? Invoice.PrepaidPayment.ToList() : new List<PaymentType>();

            var prepaidPayment = new PaymentType
            {
                ID = new IDType { schemeID = anticipo.ConceptoAnticipo?.Codigo, Value = (prepaidPayments.Count + 1).ToString() },
                PaidAmount = new PaidAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = anticipo.Valor
                },
                PaidDate = new PaidDateType { Value = anticipo.FechaPago },
                PaidTime = new PaidTimeType { Value = anticipo.FechaPago.ToString("HH:mm:ss") + "-05:00" },
                ReceivedDate = new ReceivedDateType { Value = anticipo.FechaRecepcionPago },
                InstructionID = new InstructionIDType { Value = anticipo.Instrucciones }
            };

            prepaidPayments.Add(prepaidPayment);

            Invoice.PrepaidPayment = prepaidPayments.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente ConDetallePago(DetallePago pago)
        {
            Invoice.PaymentMeans = new PaymentMeansType[]
            {
                new PaymentMeansType
                {
                    ID = new IDType { Value = ((int)pago.Forma).ToString() },
                    PaymentMeansCode = new PaymentMeansCodeType { Value = pago.Metodo.Codigo },
                    PaymentDueDate = new PaymentDueDateType { Value = pago.Fecha },
                    PaymentID = new PaymentIDType[]
                    {
                        new PaymentIDType { Value = pago.Identificador }
                    }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarDetallePago(DetallePago pago)
        {
            var paymenMeans = (Invoice.PaymentMeans != null) ? Invoice.PaymentMeans.ToList() : new List<PaymentMeansType>();

            var payment = new PaymentMeansType
            {
                ID = new IDType { Value = ((int)pago.Forma).ToString() },
                PaymentMeansCode = new PaymentMeansCodeType { Value = pago.Metodo.Codigo },
                PaymentDueDate = new PaymentDueDateType { Value = pago.Fecha },
                PaymentID = new PaymentIDType[]
                {
                    new PaymentIDType { Value = pago.Identificador }
                }
            };

            paymenMeans.Add(payment);

            Invoice.PaymentMeans = paymenMeans.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente ConTerminosEntrega(TerminosEntrega terminos)
        {
            Invoice.DeliveryTerms = new DeliveryTermsType
            {
                SpecialTerms = new SpecialTermsType[]
                {
                    new SpecialTermsType { Value = terminos.TerminosEspeciales }
                },
                LossRiskResponsibilityCode = new LossRiskResponsibilityCodeType { Value = terminos.Condicion.Codigo },
                LossRisk = new LossRiskType[]
                {
                    new LossRiskType { Value = terminos.Condicion.Descripcion }
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarCargo(Cargo cargo)
        {
            var list = (Invoice.AllowanceCharge != null) ? Invoice.AllowanceCharge.ToList() : new List<AllowanceChargeType>();

            var charge = new AllowanceChargeType
            {
                ID = new IDType { Value = (list.Count + 1).ToString() },
                ChargeIndicator = new ChargeIndicatorType { Value = true },
                AllowanceChargeReason = new AllowanceChargeReasonType[]
                {
                    new AllowanceChargeReasonType { Value = cargo.Razon }
                },
                MultiplierFactorNumeric = (cargo.Porcentaje <= 0M && cargo.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = cargo.Porcentaje },
                Amount = new AmountType2
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = cargo.Monto
                },
                BaseAmount = (cargo.Base <= 0M) ? null : new BaseAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = cargo.Base
                }
            };

            list.Add(charge);

            Invoice.AllowanceCharge = list.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarDescuento(Descuento descuento)
        {
            var list = (Invoice.AllowanceCharge != null) ? Invoice.AllowanceCharge.ToList() : new List<AllowanceChargeType>();

            var allowance = new AllowanceChargeType
            {
                ID = new IDType { Value = (list.Count + 1).ToString() },
                ChargeIndicator = new ChargeIndicatorType { Value = false },
                AllowanceChargeReasonCode = new AllowanceChargeReasonCodeType { Value = descuento.Tipo.Codigo },
                AllowanceChargeReason = new AllowanceChargeReasonType[]
                {
                    new AllowanceChargeReasonType { Value = descuento.Razon }
                },
                MultiplierFactorNumeric = (descuento.Porcentaje <= 0M && descuento.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = descuento.Porcentaje },
                Amount = new AmountType2
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = descuento.Monto
                },
                BaseAmount = (descuento.Base <= 0M) ? null : new BaseAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = descuento.Base
                }
            };

            list.Add(allowance);

            Invoice.AllowanceCharge = list.ToArray();

            return this;
        }

        public GeneradorDocumentoEquivalente ConTasaCambio(TasaCambio tasa)
        {
            Invoice.PaymentExchangeRate = new ExchangeRateType
            {
                SourceCurrencyCode = new SourceCurrencyCodeType { Value = tasa.Origen.Codigo },
                SourceCurrencyBaseRate = new SourceCurrencyBaseRateType { Value = 1.00M },
                TargetCurrencyCode = new TargetCurrencyCodeType { Value = tasa.Destino.Codigo },
                TargetCurrencyBaseRate = new TargetCurrencyBaseRateType { Value = 1.00M },
                CalculationRate = new CalculationRateType { Value = tasa.Cambio },
                Date = new DateType1 { Value = tasa.Fecha }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarResumenImpuesto(ResumenImpuesto resumen)
        {
            if (resumen.Retenido)
            {
                AgregarResumenImpuestoRetenido(resumen);
            }
            else
            {
                AgregarResumenImpuestoNormal(resumen);
            }

            return this;
        }

        protected void AgregarResumenImpuestoNormal(ResumenImpuesto resumen)
        {
            var list = (Invoice.TaxTotal != null) ? Invoice.TaxTotal.ToList() : new List<TaxTotalType>();

            var taxTotal = new TaxTotalType
            {
                TaxAmount = new TaxAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = resumen.Importe.RedondearDian()
                },
                RoundingAmount = new RoundingAmountType
                {
                    Value = 0.00M,
                    currencyID = Invoice.DocumentCurrencyCode.Value
                }
            };

            var details = new List<TaxSubtotalType>();

            foreach (var impuesto in resumen.Detalles)
            {
                var taxSubtotal = new TaxSubtotalType
                {
                    TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.BaseImponible.RedondearDian()
                    },
                    TaxAmount = new TaxAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.Importe.RedondearDian()
                    },
                    BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                    {
                        unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                        Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1M
                    },
                    PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.PorUnidad
                    },
                    TaxCategory = new TaxCategoryType
                    {
                        Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje.RedondearDian() },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = resumen.Tipo.Codigo },
                            Name = new NameType1 { Value = resumen.Tipo.Nombre }
                        }
                    }
                };

                details.Add(taxSubtotal);
            }

            if (details.Count == 0)
            {
                var taxSubtotal = new TaxSubtotalType
                {
                    TaxableAmount = new TaxableAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = 0.00M
                    },
                    TaxAmount = new TaxAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = 0.00M
                    },
                    TaxCategory = new TaxCategoryType
                    {
                        Percent = new PercentType1 { Value = 0.00M },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = resumen.Tipo.Codigo },
                            Name = new NameType1 { Value = resumen.Tipo.Nombre }
                        }
                    }
                };

                details.Add(taxSubtotal);
            }

            taxTotal.TaxSubtotal = details.ToArray();

            list.Add(taxTotal);

            Invoice.TaxTotal = list.ToArray();
        }

        protected void AgregarResumenImpuestoRetenido(ResumenImpuesto resumen)
        {
            var list = (Invoice.WithholdingTaxTotal != null) ? Invoice.WithholdingTaxTotal.ToList() : new List<TaxTotalType>();

            var withholdingTaxTotal = new TaxTotalType
            {
                TaxAmount = new TaxAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = resumen.Importe.RedondearDian()
                },
                RoundingAmount = new RoundingAmountType
                {
                    Value = 0.00M,
                    currencyID = Invoice.DocumentCurrencyCode.Value
                }
            };

            var details = new List<TaxSubtotalType>();

            foreach (var impuesto in resumen.Detalles)
            {
                var taxSubtotal = new TaxSubtotalType
                {
                    TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.BaseImponible.RedondearDian()
                    },
                    TaxAmount = new TaxAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.Importe.RedondearDian()
                    },
                    BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                    {
                        unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                        Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1M
                    },
                    PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = impuesto.PorUnidad
                    },
                    TaxCategory = new TaxCategoryType
                    {
                        Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = resumen.Tipo.Codigo },
                            Name = new NameType1 { Value = resumen.Tipo.Nombre }
                        }
                    }
                };

                details.Add(taxSubtotal);
            }

            if (details.Count == 0)
            {
                var taxSubtotal = new TaxSubtotalType
                {
                    TaxableAmount = new TaxableAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = 0.00M
                    },
                    TaxAmount = new TaxAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = 0.00M
                    },
                    TaxCategory = new TaxCategoryType
                    {
                        Percent = new PercentType1 { Value = 0M },
                        TaxScheme = new TaxSchemeType
                        {
                            ID = new IDType { Value = resumen.Tipo.Codigo },
                            Name = new NameType1 { Value = resumen.Tipo.Nombre }
                        }
                    }
                };

                details.Add(taxSubtotal);
            }

            withholdingTaxTotal.TaxSubtotal = details.ToArray();

            list.Add(withholdingTaxTotal);

            Invoice.WithholdingTaxTotal = list.ToArray();
        }

        public GeneradorDocumentoEquivalente ConTotales(Totales totales)
        {
            Invoice.LegalMonetaryTotal = new MonetaryTotalType
            {
                LineExtensionAmount = new LineExtensionAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.ValorBruto.RedondearDian()
                },
                TaxExclusiveAmount = new TaxExclusiveAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.BaseImponible.RedondearDian()
                },
                TaxInclusiveAmount = new TaxInclusiveAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.ValorBrutoConImpuestos.RedondearDian()
                },
                AllowanceTotalAmount = (totales.Descuentos <= 0M) ? null : new AllowanceTotalAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.Descuentos.RedondearDian()
                },
                ChargeTotalAmount = (totales.Cargos <= 0M) ? null : new ChargeTotalAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.Cargos.RedondearDian()
                },
                PrepaidAmount = new PrepaidAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.Anticipo.RedondearDian()
                },
                PayableAmount = new PayableAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = totales.TotalPagable.RedondearDian()
                }
            };

            return this;
        }

        public GeneradorDocumentoEquivalente AgregarLinea(Linea linea)
        {
            var list = (Invoice.InvoiceLine != null) ? Invoice.InvoiceLine.ToList() : new List<InvoiceLineType>();

            var line = new InvoiceLineType
            {
                ID = new IDType
                {
                    Value = (list.Count + 1).ToString(),
                    schemeID = linea.IdentificadorOperacion
                },
                Note = (linea.Anotacion == null) ? null : new NoteType[]
                {
                    new NoteType { Value = linea.Anotacion }
                },
                InvoicedQuantity = new InvoicedQuantityType
                {
                    unitCode = (linea.Impuestos.Count > 0 && linea.Impuestos[0].PorUnidad > 0M) ? "NIU" : linea.UnidadMedida.Codigo,
                    Value = linea.Cantidad.RedondearDian()
                },
                LineExtensionAmount = new LineExtensionAmountType
                {
                    currencyID = Invoice.DocumentCurrencyCode.Value,
                    Value = linea.BaseGeneral.RedondearDian()
                },
                FreeOfChargeIndicator = new FreeOfChargeIndicatorType { Value = (linea.BaseGeneral <= 0M) },
                PricingReference = (linea.BaseGeneral > 0) ? null : new PricingReferenceType
                {
                    AlternativeConditionPrice = new PriceType[]
                    {
                        new PriceType
                        {
                            PriceAmount = new PriceAmountType
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = linea.PrecioUnitarioReal
                            },
                            PriceTypeCode = new PriceTypeCodeType { Value = "01" }
                        }
                    }
                },
                Item = new ItemType
                {
                    Description = new DescriptionType[] { new DescriptionType { Value = linea.Descripcion } },
                    BrandName = (Invoice.InvoiceTypeCode.Value != ((int)TipoFactura.FACTURA_EXPORTACION).ToString("00")) ? null : new BrandNameType[]
                    {
                        new BrandNameType { Value = linea.Marca }
                    }, 
                    ModelName = (Invoice.InvoiceTypeCode.Value != ((int)TipoFactura.FACTURA_EXPORTACION).ToString("00")) ? null : new ModelNameType[]
                    {
                        new ModelNameType { Value = linea.Referencia }
                    },
                    StandardItemIdentification = (linea.CodigoProducto == null) ? null : new ItemIdentificationType
                    {
                        ID = new IDType
                        {
                            schemeID = linea.CodigoProducto.Tipo.Identificador,
                            schemeName = linea.CodigoProducto.Tipo.Nombre,
                            schemeAgencyID = linea.CodigoProducto.Tipo.Codigo,
                            Value = linea.CodigoProducto.Valor
                        }
                    },
                    InformationContentProviderParty = (linea.NitMandante == null) ? null : new PartyType
                    {
                        PowerOfAttorney = new PowerOfAttorneyType[]
                        {
                            new PowerOfAttorneyType
                            {
                                AgentParty = new PartyType
                                {
                                    PartyIdentification = new PartyIdentificationType[]
                                    {
                                        new PartyIdentificationType
                                        {
                                            ID = new IDType
                                            {
                                                Value = linea.NitMandante,
                                                schemeAgencyID = "195",
                                                schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                                schemeID = NitHelper.CalcularDV(linea.NitMandante).ToString(),
                                                schemeName = "31"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Price = new PriceType
                {
                    PriceAmount = new PriceAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = linea.PrecioUnitario.RedondearDian()
                    },
                    BaseQuantity = new BaseQuantityType
                    {
                        unitCode = (linea.Impuestos.Count > 0 && linea.Impuestos[0].PorUnidad > 0M) ? "NIU" : linea.UnidadMedida.Codigo,
                        Value = linea.Cantidad.RedondearDian()
                    }
                }
            };

            var allowanceChargeList = new List<AllowanceChargeType>();
            if (linea.Cargos.Count > 0)
            {
                foreach (var cargo in linea.Cargos)
                {
                    var charge = new AllowanceChargeType
                    {
                        ID = new IDType { Value = (allowanceChargeList.Count + 1).ToString() },
                        ChargeIndicator = new ChargeIndicatorType { Value = true },
                        AllowanceChargeReason = new AllowanceChargeReasonType[]
                        {
                            new AllowanceChargeReasonType { Value = cargo.Razon }
                        },
                        MultiplierFactorNumeric = (cargo.Porcentaje <= 0M && cargo.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = cargo.Porcentaje },
                        Amount = new AmountType2
                        {
                            currencyID = Invoice.DocumentCurrencyCode.Value,
                            Value = cargo.Monto.RedondearDian()
                        },
                        BaseAmount = (cargo.Base <= 0M) ? null : new BaseAmountType
                        {
                            currencyID = Invoice.DocumentCurrencyCode.Value,
                            Value = cargo.Base.RedondearDian()
                        }
                    };

                    allowanceChargeList.Add(charge);
                }
            }
            if (linea.Descuentos.Count > 0)
            {
                foreach (var descuento in linea.Descuentos)
                {
                    var allowance = new AllowanceChargeType
                    {
                        ID = new IDType { Value = (allowanceChargeList.Count + 1).ToString() },
                        ChargeIndicator = new ChargeIndicatorType { Value = false },
                        AllowanceChargeReasonCode = new AllowanceChargeReasonCodeType { Value = descuento.Tipo.Codigo },
                        AllowanceChargeReason = new AllowanceChargeReasonType[]
                        {
                            new AllowanceChargeReasonType { Value = descuento.Razon }
                        },
                        MultiplierFactorNumeric = (descuento.Porcentaje <= 0M && descuento.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = descuento.Porcentaje },
                        Amount = new AmountType2
                        {
                            currencyID = Invoice.DocumentCurrencyCode.Value,
                            Value = descuento.Monto.RedondearDian()
                        },
                        BaseAmount = (descuento.Base <= 0M) ? null : new BaseAmountType
                        {
                            currencyID = Invoice.DocumentCurrencyCode.Value,
                            Value = descuento.Base.RedondearDian()
                        }
                    };

                    allowanceChargeList.Add(allowance);
                }
            }
            line.AllowanceCharge = allowanceChargeList.ToArray();

            if (linea.Impuestos.Count > 0)
            {
                var listTaxtTotal = new List<TaxTotalType>();
                var listWithholdingTaxTotal = new List<TaxTotalType>();

                foreach (var impuesto in linea.Impuestos)
                {
                    if (impuesto.Retenido == false)
                    {
                        var taxTotal = new TaxTotalType
                        {
                            TaxAmount = new TaxAmountType
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = impuesto.Importe.RedondearDian()
                            },
                            RoundingAmount = new RoundingAmountType
                            {
                                Value = 0.00M,
                                currencyID = Invoice.DocumentCurrencyCode.Value
                            },
                            TaxSubtotal = new TaxSubtotalType[]
                            {
                                new TaxSubtotalType
                                {
                                    TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.BaseImponible.RedondearDian()
                                    },
                                    TaxAmount = new TaxAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.Importe.RedondearDian()
                                    },
                                    BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                                    {
                                        unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                                        Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1M
                                    },
                                    PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.PorUnidad
                                    },
                                    TaxCategory = new TaxCategoryType
                                    {
                                        Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje },
                                        TaxScheme = new TaxSchemeType
                                        {
                                            ID = new IDType { Value = impuesto.Tipo.Codigo },
                                            Name = new NameType1 { Value = impuesto.Tipo.Nombre }
                                        }
                                    }
                                }
                            }
                        };

                        listTaxtTotal.Add(taxTotal);
                    }
                    else
                    {
                        var withholdingTaxTotal = new TaxTotalType
                        {
                            TaxAmount = new TaxAmountType
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = impuesto.Importe.RedondearDian()
                            },
                            RoundingAmount = new RoundingAmountType
                            {
                                Value = 0.00M,
                                currencyID = Invoice.DocumentCurrencyCode.Value
                            },
                            TaxSubtotal = new TaxSubtotalType[]
                            {
                                new TaxSubtotalType
                                {
                                    TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.BaseImponible.RedondearDian()
                                    },
                                    TaxAmount = new TaxAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.Importe.RedondearDian()
                                    },
                                    BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                                    {
                                        unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                                        Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1M
                                    },
                                    PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                                    {
                                        currencyID = Invoice.DocumentCurrencyCode.Value,
                                        Value = impuesto.PorUnidad
                                    },
                                    TaxCategory = new TaxCategoryType
                                    {
                                        Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje },
                                        TaxScheme = new TaxSchemeType
                                        {
                                            ID = new IDType { Value = impuesto.Tipo.Codigo },
                                            Name = new NameType1 { Value = impuesto.Tipo.Nombre }
                                        }
                                    }
                                }
                            }
                        };

                        listWithholdingTaxTotal.Add(withholdingTaxTotal);
                    }
                }

                line.TaxTotal = listTaxtTotal.ToArray();
                line.WithholdingTaxTotal = listWithholdingTaxTotal.ToArray();
            }

            if (!string.IsNullOrEmpty(linea.DescripcionComplementaria))
            {
                var descriptions = line.Item.Description.ToList();

                var additionalDescription = new DescriptionType
                {
                    Value = linea.DescripcionComplementaria
                };

                descriptions.Add(additionalDescription);

                line.Item.Description = descriptions.ToArray();
            }

            list.Add(line);

            Invoice.InvoiceLine = list.ToArray();

            Invoice.LineCountNumeric = new LineCountNumericType { Value = list.Count };

            return this;
        }


        public GeneradorDocumentoEquivalente AgregarLineas(List<Linea> lineas)
        {

            var list = (Invoice.InvoiceLine != null) ? Invoice.InvoiceLine.ToList() : new List<InvoiceLineType>();


            foreach (var linea in lineas)
            {

                var line = new InvoiceLineType
                {
                    ID = new IDType
                    {
                        Value = (list.Count + 1).ToString(),
                        schemeID = linea.IdentificadorOperacion
                    },
                    Note = (linea.Anotacion == null) ? null : new NoteType[]
                    {
                        new NoteType { Value = linea.Anotacion }
                    },
                    InvoicedQuantity = new InvoicedQuantityType
                    {
                        unitCode = (linea.Impuestos.Count > 0 && linea.Impuestos[0].PorUnidad > 0M) ? "NIU" : linea.UnidadMedida.Codigo,
                        Value = linea.Cantidad.RedondearDian()
                    },
                    LineExtensionAmount = new LineExtensionAmountType
                    {
                        currencyID = Invoice.DocumentCurrencyCode.Value,
                        Value = linea.BaseGeneral.RedondearDian()
                    },
                    FreeOfChargeIndicator = new FreeOfChargeIndicatorType { Value = (linea.BaseGeneral <= 0M) },
                    PricingReference = (linea.BaseGeneral > 0) ? null : new PricingReferenceType
                    {
                        AlternativeConditionPrice = new PriceType[]
                        {
                            new PriceType
                            {
                                PriceAmount = new PriceAmountType
                                {
                                    currencyID = Invoice.DocumentCurrencyCode.Value,
                                    Value = linea.PrecioUnitarioReal.RedondearDian()
                                },
                                PriceTypeCode = new PriceTypeCodeType { Value = "01" }
                            }
                        }
                    },
                    Item = new ItemType
                    {
                        Description = new DescriptionType[] { new DescriptionType { Value = linea.Descripcion } },
                        BrandName = (Invoice.InvoiceTypeCode.Value != ((int)TipoFactura.FACTURA_EXPORTACION).ToString("00")) ? null : new BrandNameType[]
                        {
                            new BrandNameType { Value = linea.Marca }
                        },
                        ModelName = (Invoice.InvoiceTypeCode.Value != ((int)TipoFactura.FACTURA_EXPORTACION).ToString("00")) ? null : new ModelNameType[]
                        {
                            new ModelNameType { Value = linea.Referencia }
                        },
                        StandardItemIdentification = (linea.CodigoProducto == null) ? null : new ItemIdentificationType
                        {
                            ID = new IDType
                            {
                                schemeID = linea.CodigoProducto.Tipo.Identificador,
                                schemeName = linea.CodigoProducto.Tipo.Nombre,
                                schemeAgencyID = linea.CodigoProducto.Tipo.Codigo,
                                Value = linea.CodigoProducto.Valor
                            }
                        },
                        InformationContentProviderParty = (linea.NitMandante == null) ? null : new PartyType
                        {
                            PowerOfAttorney = new PowerOfAttorneyType[]
                            {
                                new PowerOfAttorneyType
                                {
                                    AgentParty = new PartyType
                                    {
                                        PartyIdentification = new PartyIdentificationType[]
                                        {
                                            new PartyIdentificationType
                                            {
                                                ID = new IDType
                                                {
                                                    Value = linea.NitMandante,
                                                    schemeAgencyID = "195",
                                                    schemeAgencyName = "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)",
                                                    schemeID = NitHelper.CalcularDV(linea.NitMandante).ToString(),
                                                    schemeName = "31"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Price = new PriceType
                    {
                        PriceAmount = new PriceAmountType
                        {
                            currencyID = Invoice.DocumentCurrencyCode.Value,
                            Value = linea.PrecioUnitario.RedondearDian()
                        },
                        BaseQuantity = new BaseQuantityType
                        {
                            unitCode = (linea.Impuestos.Count > 0 && linea.Impuestos[0].PorUnidad > 0M) ? "NIU" : linea.UnidadMedida.Codigo,
                            Value = linea.Cantidad.RedondearDian()
                        }
                    }
                };

                var allowanceChargeList = new List<AllowanceChargeType>();
                if (linea.Cargos?.Count > 0)
                {
                    foreach (var cargo in linea.Cargos)
                    {
                        var charge = new AllowanceChargeType
                        {
                            ID = new IDType { Value = (allowanceChargeList.Count + 1).ToString() },
                            ChargeIndicator = new ChargeIndicatorType { Value = true },
                            AllowanceChargeReason = new AllowanceChargeReasonType[]
                            {
                                new AllowanceChargeReasonType { Value = cargo.Razon }
                            },
                            MultiplierFactorNumeric = (cargo.Porcentaje <= 0M && cargo.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = cargo.Porcentaje },
                            Amount = new AmountType2
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = cargo.Monto
                            },
                            BaseAmount = (cargo.Base <= 0M) ? null : new BaseAmountType
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = cargo.Base
                            }
                        };

                        allowanceChargeList.Add(charge);
                    }
                }
                if (linea.Descuentos?.Count > 0)
                {
                    foreach (var descuento in linea.Descuentos)
                    {
                        var allowance = new AllowanceChargeType
                        {
                            ID = new IDType { Value = (allowanceChargeList.Count + 1).ToString() },
                            ChargeIndicator = new ChargeIndicatorType { Value = false },
                            AllowanceChargeReasonCode = new AllowanceChargeReasonCodeType { Value = descuento.Tipo.Codigo },
                            AllowanceChargeReason = new AllowanceChargeReasonType[]
                            {
                                new AllowanceChargeReasonType { Value = descuento.Razon }
                            },
                            MultiplierFactorNumeric = (descuento.Porcentaje <= 0M && descuento.Base <= 0M) ? null : new MultiplierFactorNumericType { Value = descuento.Porcentaje },
                            Amount = new AmountType2
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = descuento.Monto
                            },
                            BaseAmount = (descuento.Base <= 0M) ? null : new BaseAmountType
                            {
                                currencyID = Invoice.DocumentCurrencyCode.Value,
                                Value = descuento.Base
                            }
                        };

                        allowanceChargeList.Add(allowance);
                    }
                }
                line.AllowanceCharge = allowanceChargeList.ToArray();

                if (linea.Impuestos.Count > 0)
                {
                    var listTaxtTotal = new List<TaxTotalType>();
                    var listWithholdingTaxTotal = new List<TaxTotalType>();

                    foreach (var impuesto in linea.Impuestos)
                    {
                        if (impuesto.Retenido == false)
                        {
                            var taxTotal = new TaxTotalType
                            {
                                TaxAmount = new TaxAmountType
                                {
                                    currencyID = Invoice.DocumentCurrencyCode.Value,
                                    Value = impuesto.Importe.RedondearDian()
                                },
                                RoundingAmount = new RoundingAmountType
                                {
                                    Value = 0.00M,
                                    currencyID = Invoice.DocumentCurrencyCode.Value
                                },
                                TaxSubtotal = new TaxSubtotalType[]
                                {
                                    new TaxSubtotalType
                                    {
                                        TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.BaseImponible.RedondearDian()
                                        },
                                        TaxAmount = new TaxAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.Importe.RedondearDian()
                                        },
                                        BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                                        {
                                            unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                                            Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1M
                                        },
                                        PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.PorUnidad
                                        },
                                        TaxCategory = new TaxCategoryType
                                        {
                                            Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje.RedondearDian() },
                                            TaxScheme = new TaxSchemeType
                                            {
                                                ID = new IDType { Value = impuesto.Tipo.Codigo },
                                                Name = new NameType1 { Value = impuesto.Tipo.Nombre }
                                            }
                                        }
                                    }
                                }
                            };

                            listTaxtTotal.Add(taxTotal);
                        }
                        else
                        {
                            var withholdingTaxTotal = new TaxTotalType
                            {
                                TaxAmount = new TaxAmountType
                                {
                                    currencyID = Invoice.DocumentCurrencyCode.Value,
                                    Value = impuesto.Importe.RedondearDian()
                                },
                                RoundingAmount = new RoundingAmountType
                                {
                                    Value = 0.00M,
                                    currencyID = Invoice.DocumentCurrencyCode.Value
                                },
                                TaxSubtotal = new TaxSubtotalType[]
                                {
                                    new TaxSubtotalType
                                    {
                                        TaxableAmount = (impuesto.BaseImponible <= 0M) ? null : new TaxableAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.BaseImponible.RedondearDian()
                                        },
                                        TaxAmount = new TaxAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.Importe.RedondearDian()
                                        },
                                        BaseUnitMeasure = (impuesto.PorUnidad <= 0M) ? null : new BaseUnitMeasureType
                                        {
                                            unitCode = (impuesto.UnidadMedida != null) ? impuesto.UnidadMedida.Codigo : "NIU",
                                            Value = (impuesto.ValorUnidadMedida > 0M) ? impuesto.ValorUnidadMedida : 1.00M
                                        },
                                        PerUnitAmount = (impuesto.PorUnidad <= 0M) ? null : new PerUnitAmountType
                                        {
                                            currencyID = Invoice.DocumentCurrencyCode.Value,
                                            Value = impuesto.PorUnidad.RedondearDian()
                                        },
                                        TaxCategory = new TaxCategoryType
                                        {
                                            Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje.RedondearDian() },
                                            TaxScheme = new TaxSchemeType
                                            {
                                                ID = new IDType { Value = impuesto.Tipo.Codigo },
                                                Name = new NameType1 { Value = impuesto.Tipo.Nombre }
                                            }
                                        }
                                    }
                                }
                            };

                            listWithholdingTaxTotal.Add(withholdingTaxTotal);
                        }
                    }

                    line.TaxTotal = listTaxtTotal.ToArray();
                    line.WithholdingTaxTotal = listWithholdingTaxTotal.ToArray();
                }

                if (!string.IsNullOrEmpty(linea.DescripcionComplementaria))
                {
                    var descriptions = line.Item.Description.ToList();

                    var additionalDescription = new DescriptionType
                    {
                        Value = linea.DescripcionComplementaria
                    };

                    descriptions.Add(additionalDescription);

                    line.Item.Description = descriptions.ToArray();
                }

                list.Add(line);

            }

            Invoice.InvoiceLine = list.ToArray();

            Invoice.LineCountNumeric = new LineCountNumericType { Value = list.Count };

            return this;
        }



        public GeneradorDocumentoEquivalente AsignarCUDE(string pinSoftware)
        {
            var algoritmo = AlgoritmoSeguridadUUID.CUDE_SHA384;

            Invoice.UUID = new UUIDType
            {
                schemeID = Invoice.ProfileExecutionID.Value,
                schemeName = algoritmo.Codigo,
                Value = CrearCUDE(Invoice, pinSoftware)
            };

            //var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + Invoice.UUID.Value;
            //if (Invoice.ProfileExecutionID.Value == ((int)AmbienteDestino.PRUEBAS).ToString())
            //    qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + Invoice.UUID.Value;

            if (Invoice.UBLExtensions != null && Invoice.UBLExtensions.Length >= 1)
            {
                _stringQR = GenerarTextoQR();
                var ublExtension = Invoice.UBLExtensions[0];
                ublExtension.ExtensionContent.Any.InnerXml = ublExtension.ExtensionContent.Any.InnerXml.Replace("{{QR_CODE_VALUE}}", _stringQR);
            }

            return this;
        }

        public string GenerarTextoQR()
        {
            var NumFac = Invoice.ID.Value;
            var FecFac = Invoice.IssueDate.Value.ToString("yyyy-MM-dd");
            var HorFac = Invoice.IssueDate.Value.ToString("HH:mm:ss") + "-05:00";
            var NitFac = Invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.Value;
            var DocAdq = Invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.Value;
            var ValFac = Invoice.LegalMonetaryTotal.LineExtensionAmount.Value.ToString("0.00");
            var ValIva = 0M;
            foreach (var tax in Invoice.TaxTotal ?? new TaxTotalType[] { })
            {
                if (tax.TaxSubtotal[0].TaxCategory.TaxScheme.ID.Value == "01")
                    ValIva += tax.TaxAmount.Value;
            }
            var StrValIva = ValIva.ToString("0.00");
            var StrValTolFac = Invoice.LegalMonetaryTotal.PayableAmount.Value.ToString("0.00");
            var UUID = Invoice.UUID.Value;

            var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + Invoice.UUID.Value;
            if (Invoice.ProfileExecutionID.Value == ((int)AmbienteDestino.PRUEBAS).ToString())
                qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + Invoice.UUID.Value;

            string ret = "NumDE: " + NumFac + "\r\n" +
                "FecDE: " + FecFac + "\r\n" +
                //"HorDE: " + HorFac + "\r\n" +
                "NumEmisor: " + NitFac + "\r\n" +
                "DocAdquiriente: " + DocAdq + "\r\n" +
                "ValDE: " + ValFac + "\r\n" +
                "ValIva: " + StrValIva + "\r\n" +
                "ValTolDE: " + StrValTolFac + "\r\n" +
                "CUDE: " + UUID + "\r\n" +
                "QRCode: " + qrCodeValue;

            return ret;
        }

        public string StringQR { get { return _stringQR; } }

        public InvoiceType Obtener()
        {
            return Invoice;
        }


        public static string CrearCUDE(InvoiceType invoice, string pinSoftware)
        {
            var NumFac = invoice.ID.Value;
            var FecFac = invoice.IssueDate.Value.ToString("yyyy-MM-dd");
            var HorFac = invoice.IssueTime.Value;
            var ValBru = invoice.LegalMonetaryTotal.LineExtensionAmount.Value.ToString("0.00").Replace(",", ".");
            var CodImp1 = "01";
            var ValImp1 = 0M;
            if (invoice.TaxTotal != null)
            {
                foreach (var tax in invoice.TaxTotal)
                {
                    if (tax.TaxSubtotal[0].TaxCategory.TaxScheme.ID.Value == "01")
                        ValImp1 += tax.TaxAmount.Value;
                }
            }
            var StrValImp1 = ValImp1.ToString("0.00").Replace(",", ".");
            var CodImp2 = "04";
            var ValImp2 = 0M;
            if (invoice.TaxTotal != null)
            {
                foreach (var tax in invoice.TaxTotal)
                {
                    if (tax.TaxSubtotal[0].TaxCategory.TaxScheme.ID.Value == "04")
                        ValImp2 += tax.TaxAmount.Value;
                }
            }
            var StrValImp2 = ValImp2.ToString("0.00").Replace(",", ".");
            var CodImp3 = "03";
            var ValImp3 = 0M;
            if (invoice.TaxTotal != null)
            {
                foreach (var tax in invoice.TaxTotal)
                {
                    if (tax.TaxSubtotal[0].TaxCategory.TaxScheme.ID.Value == "03")
                        ValImp3 += tax.TaxAmount.Value;
                }
            }
            var StrValImp3 = ValImp3.ToString("0.00").Replace(",", ".");
            var ValPag = invoice.LegalMonetaryTotal.PayableAmount.Value.ToString("0.00").Replace(",", ".");
            var NitOFE = invoice.AccountingSupplierParty.Party.PartyTaxScheme[0].CompanyID.Value;
            var NumAdq = invoice.AccountingCustomerParty[0].Party.PartyTaxScheme[0].CompanyID.Value;
            var tipAmb = invoice.ProfileExecutionID.Value;

            string cadena = NumFac + FecFac + HorFac + ValBru + CodImp1 + StrValImp1 + CodImp2 +
                StrValImp2 + CodImp3 + StrValImp3 + ValPag + NitOFE + NumAdq + pinSoftware + tipAmb;

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