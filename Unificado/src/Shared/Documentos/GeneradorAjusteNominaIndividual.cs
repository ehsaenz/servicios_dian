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
    public class GeneradorAjusteNominaIndividual
    {
        public NominaIndividualDeAjusteType AjusteNominaIndividual { get; private set; }

        public GeneradorAjusteNominaIndividual(NominaIndividualDeAjusteType ajusteNomina)
        {
            AjusteNominaIndividual = ajusteNomina;
        }

        public GeneradorAjusteNominaIndividual(AmbienteDestino ambiente, TipoAjusteNominaIndividual tipo, ReferenciaNominaIndividual referencia)
        {
            AjusteNominaIndividual = new NominaIndividualDeAjusteType
            {
                UBLExtensions = new UBLExtensionType[]
                {
                    new UBLExtensionType { ExtensionContent = new ExtensionContentType { } }
                },
                TipoNota = tipo.Codigo,
                Reemplazar = (tipo.Codigo != TipoAjusteNominaIndividual.REEMPLAZAR.Codigo) ? null : new ReemplazarType
                {
                    ReemplazandoPredecesor = new ReemplazarReemplazandoPredecesorType
                    {
                        NumeroPred = referencia.Numero,
                        CUNEPred = referencia.Cune,
                        FechaGenPred = referencia.FechaGeneracion
                    },
                    InformacionGeneral = new ReemplazarInformacionGeneralType
                    {
                        Version = "V1.0: Nota de Ajuste de Documento Soporte de Pago de Nómina Electrónica",
                        Ambiente = ((int)ambiente).ToString(),
                        TipoXML = TipoDocumentoElectronico.AJUSTE_NOMINA_INDIVIDUAL.Codigo
                    },
                    Redondeo = 0
                },
                Eliminar = (tipo.Codigo == TipoAjusteNominaIndividual.REEMPLAZAR.Codigo) ? null : new EliminarType
                {
                    EliminandoPredecesor = new EliminarEliminandoPredecesorType
                    {
                        NumeroPred = referencia.Numero,
                        CUNEPred = referencia.Cune,
                        FechaGenPred = referencia.FechaGeneracion
                    },
                    InformacionGeneral = new EliminarInformacionGeneralType
                    {
                        Version = "V1.0: Nota de Ajuste de Documento Soporte de Pago de Nómina Electrónica",
                        Ambiente = ((int)ambiente).ToString(),
                        TipoXML = TipoDocumentoElectronico.AJUSTE_NOMINA_INDIVIDUAL.Codigo
                    }
                }
            };
        }

        public GeneradorAjusteNominaIndividual ConInformacionGeneral(InformacionGeneralNominaIndividual informacion)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.LugarGeneracionXML = new ReemplazarLugarGeneracionXMLType
                {
                    Pais = informacion.LugarGeneracion.Pais.Codigo,
                    DepartamentoEstado = informacion.LugarGeneracion.Departamento.Codigo,
                    MunicipioCiudad = informacion.LugarGeneracion.Municipio.Codigo,
                    Idioma = "es"
                };

                AjusteNominaIndividual.Reemplazar.InformacionGeneral.FechaGen = informacion.FechaGeneracion;
                AjusteNominaIndividual.Reemplazar.InformacionGeneral.HoraGen = informacion.FechaGeneracion.ToString("HH:mm:ss") + "-05:00";

                AjusteNominaIndividual.Reemplazar.InformacionGeneral.TipoMoneda = informacion.Moneda.Codigo;
                AjusteNominaIndividual.Reemplazar.InformacionGeneral.TRM = informacion.TRM;

                AjusteNominaIndividual.Reemplazar.InformacionGeneral.PeriodoNomina = informacion.PeriodoNomina.Codigo;
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                AjusteNominaIndividual.Eliminar.LugarGeneracionXML = new EliminarLugarGeneracionXMLType
                {
                    Pais = informacion.LugarGeneracion.Pais.Codigo,
                    DepartamentoEstado = informacion.LugarGeneracion.Departamento.Codigo,
                    MunicipioCiudad = informacion.LugarGeneracion.Municipio.Codigo,
                    Idioma = "es"
                };

                AjusteNominaIndividual.Eliminar.InformacionGeneral.FechaGen = informacion.FechaGeneracion;
                AjusteNominaIndividual.Eliminar.InformacionGeneral.HoraGen = informacion.FechaGeneracion.ToString("HH:mm:ss") + "-05:00";
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConPeriodo(PeriodoNomina periodo)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.Periodo = new ReemplazarPeriodoType
                {
                    FechaIngreso = periodo.FechaIngreso,
                    FechaRetiro = periodo.FechaRetiro?.ToString("yyyy-MM-dd"),
                    FechaLiquidacionInicio = periodo.FechaLiquidacionInicio,
                    FechaLiquidacionFin = periodo.FechaLiquidacionFin,
                    TiempoLaborado = periodo.TiempoLaborado.CalcularDias().ToString(),
                    FechaGen = AjusteNominaIndividual.Reemplazar.InformacionGeneral.FechaGen
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConNumero(string prefijo, long consecutivo)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.NumeroSecuenciaXML = new ReemplazarNumeroSecuenciaXMLType
                {
                    Prefijo = prefijo,
                    Consecutivo = consecutivo,
                    Numero = prefijo + consecutivo
                };
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                AjusteNominaIndividual.Eliminar.NumeroSecuenciaXML = new EliminarNumeroSecuenciaXMLType
                {
                    Prefijo = prefijo,
                    Consecutivo = consecutivo,
                    Numero = prefijo + consecutivo
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConProveedorSoftware(ProveedorSoftware proveedor)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.ProveedorXML = new ReemplazarProveedorXMLType
                {
                    RazonSocial = proveedor.RazonSocial,
                    PrimerApellido = proveedor.PrimerApellido,
                    SegundoApellido = proveedor.SegundoApellido,
                    PrimerNombre = proveedor.PrimerNombre,
                    OtrosNombres = proveedor.OtrosNombres,
                    NIT = proveedor.Nit,
                    DV = NitHelper.CalcularDV(proveedor.Nit).ToString(),
                    SoftwareID = proveedor.SoftwareIdentificador,
                    SoftwareSC = CryptographyHelper.Sha384(proveedor.SoftwareIdentificador + proveedor.SoftwarePin + AjusteNominaIndividual.Reemplazar.NumeroSecuenciaXML.Numero)
                };
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                AjusteNominaIndividual.Eliminar.ProveedorXML = new EliminarProveedorXMLType
                {
                    RazonSocial = proveedor.RazonSocial,
                    PrimerApellido = proveedor.PrimerApellido,
                    SegundoApellido = proveedor.SegundoApellido,
                    PrimerNombre = proveedor.PrimerNombre,
                    OtrosNombres = proveedor.OtrosNombres,
                    NIT = proveedor.Nit,
                    DV = NitHelper.CalcularDV(proveedor.Nit).ToString(),
                    SoftwareID = proveedor.SoftwareIdentificador,
                    SoftwareSC = CryptographyHelper.Sha384(proveedor.SoftwareIdentificador + proveedor.SoftwarePin + AjusteNominaIndividual.Eliminar.NumeroSecuenciaXML.Numero)
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual AgregarNota(string observacion)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                var notas = AjusteNominaIndividual.Reemplazar.Notas?.ToList() ?? new List<string>();

                notas.Add(observacion);

                AjusteNominaIndividual.Reemplazar.Notas = notas.ToArray();
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                var notas = AjusteNominaIndividual.Eliminar.Notas?.ToList() ?? new List<string>();

                notas.Add(observacion);

                AjusteNominaIndividual.Eliminar.Notas = notas.ToArray();
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConEmpleador(Empleador empleador)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.Empleador = new ReemplazarEmpleadorType
                {
                    RazonSocial = empleador.RazonSocial,
                    PrimerApellido = empleador.PrimerApellido,
                    SegundoApellido = empleador.SegundoApellido,
                    PrimerNombre = empleador.PrimerNombre,
                    OtrosNombres = empleador.OtrosNombres,
                    NIT = empleador.Nit,
                    DV = NitHelper.CalcularDV(empleador.Nit).ToString(),
                    Pais = empleador.Ubicacion.Pais.Codigo,
                    DepartamentoEstado = empleador.Ubicacion.Departamento.Codigo,
                    MunicipioCiudad = empleador.Ubicacion.Municipio.Codigo,
                    Direccion = empleador.Ubicacion.Linea
                };
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                AjusteNominaIndividual.Eliminar.Empleador = new EliminarEmpleadorType
                {
                    RazonSocial = empleador.RazonSocial,
                    PrimerApellido = empleador.PrimerApellido,
                    SegundoApellido = empleador.SegundoApellido,
                    PrimerNombre = empleador.PrimerNombre,
                    OtrosNombres = empleador.OtrosNombres,
                    NIT = empleador.Nit,
                    DV = NitHelper.CalcularDV(empleador.Nit).ToString(),
                    Pais = empleador.Ubicacion.Pais.Codigo,
                    DepartamentoEstado = empleador.Ubicacion.Departamento.Codigo,
                    MunicipioCiudad = empleador.Ubicacion.Municipio.Codigo,
                    Direccion = empleador.Ubicacion.Linea
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConTrabajador(Trabajador trabajador)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.Trabajador = new ReemplazarTrabajadorType
                {
                    TipoTrabajador = trabajador.Tipo.Codigo,
                    SubTipoTrabajador = trabajador.SubTipo.Codigo,
                    AltoRiesgoPension = trabajador.AltoRiesgoPension,
                    TipoDocumento = trabajador.TipoDocumento.Codigo,
                    NumeroDocumento = trabajador.NumeroDocumento,
                    PrimerApellido = trabajador.PrimerApellido,
                    SegundoApellido = trabajador.SegundoApellido,
                    PrimerNombre = trabajador.PrimerNombre,
                    OtrosNombres = trabajador.OtrosNombres,
                    LugarTrabajoPais = trabajador.LugarTrabajo.Pais.Codigo,
                    LugarTrabajoDepartamentoEstado = trabajador.LugarTrabajo.Departamento.Codigo,
                    LugarTrabajoMunicipioCiudad = trabajador.LugarTrabajo.Municipio.Codigo,
                    LugarTrabajoDireccion = trabajador.LugarTrabajo.Linea,
                    SalarioIntegral = trabajador.SalarioIntegral,
                    TipoContrato = trabajador.TipoContrato.Codigo,
                    Sueldo = trabajador.Sueldo,
                    CodigoTrabajador = trabajador.Codigo
                };

                AjusteNominaIndividual.Reemplazar.NumeroSecuenciaXML.CodigoTrabajador = trabajador.Codigo;
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConPago(DetallePago pago, CuentaBancaria cuentaBancaria = null)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.Pago = new ReemplazarPagoType
                {
                    Forma = ((int)pago.Forma).ToString(),
                    Metodo = pago.Metodo.Codigo,
                    Banco = cuentaBancaria?.NombreBanco,
                    TipoCuenta = cuentaBancaria?.TipoCuenta,
                    NumeroCuenta = cuentaBancaria?.NumeroCuenta
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual AgregarFechaPago(DateTime fecha)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                var fechas = AjusteNominaIndividual.Reemplazar.FechasPagos?.ToList() ?? new List<DateTime>();

                fechas.Add(fecha);

                AjusteNominaIndividual.Reemplazar.FechasPagos = fechas.ToArray();
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConDevengados(Devengados devengados)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                var transporte = new List<ReemplazarDevengadosTransporteType>();
                if (devengados.Transporte != null)
                {
                    transporte.Add(new ReemplazarDevengadosTransporteType
                    {
                        AuxilioTransporte = devengados.Transporte.AuxilioTransporte?.ToString(),
                        ViaticoManuAlojS = devengados.Transporte.ViaticoManutencionAlojamiento?.ToString(),
                        ViaticoManuAlojNS = devengados.Transporte.ViaticoManutencionAlojamientoNoSalarial?.ToString()
                    });
                }

                foreach (var item in devengados.Transportes)
                {
                    transporte.Add(new ReemplazarDevengadosTransporteType
                    {
                        AuxilioTransporte = item.AuxilioTransporte?.ToString(),
                        ViaticoManuAlojS = item.ViaticoManutencionAlojamiento?.ToString(),
                        ViaticoManuAlojNS = item.ViaticoManutencionAlojamientoNoSalarial?.ToString()
                    });
                }

                var heds = new List<ReemplazarDevengadosHEDType>();
                foreach (var hed in devengados.HorasExtraDiurnas)
                    heds.Add(new ReemplazarDevengadosHEDType { HoraInicio = hed.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hed.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hed.Cantidad, Porcentaje = hed.Porcentaje, Pago = hed.Pago });

                var hens = new List<ReemplazarDevengadosHENType>();
                foreach (var hen in devengados.HorasExtraNocturnas)
                    hens.Add(new ReemplazarDevengadosHENType { HoraInicio = hen.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hen.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hen.Cantidad, Porcentaje = hen.Porcentaje, Pago = hen.Pago });

                var hrns = new List<ReemplazarDevengadosHRNType>();
                foreach (var hrn in devengados.HorasRecargoNocturno)
                    hrns.Add(new ReemplazarDevengadosHRNType { HoraInicio = hrn.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrn.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrn.Cantidad, Porcentaje = hrn.Porcentaje, Pago = hrn.Pago });

                var heddfs = new List<ReemplazarDevengadosHEDDFType>();
                foreach (var heddf in devengados.HorasExtraDiurnasDominicalesFestivos)
                    heddfs.Add(new ReemplazarDevengadosHEDDFType { HoraInicio = heddf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = heddf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = heddf.Cantidad, Porcentaje = heddf.Porcentaje, Pago = heddf.Pago });

                var hrddfs = new List<ReemplazarDevengadosHRDDFType>();
                foreach (var hrddf in devengados.HorasRecargoDiurnoDominicalesFestivos)
                    hrddfs.Add(new ReemplazarDevengadosHRDDFType { HoraInicio = hrddf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrddf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrddf.Cantidad, Porcentaje = hrddf.Porcentaje, Pago = hrddf.Pago });

                var hendfs = new List<ReemplazarDevengadosHENDFType>();
                foreach (var hendf in devengados.HorasExtraNocturnasDominicalesFestivos)
                    hendfs.Add(new ReemplazarDevengadosHENDFType { HoraInicio = hendf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hendf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hendf.Cantidad, Porcentaje = hendf.Porcentaje, Pago = hendf.Pago });

                var hrndfs = new List<ReemplazarDevengadosHRNDFType>();
                foreach (var hrndf in devengados.HorasRecargoNocturnoDominicalesFestivos)
                    hrndfs.Add(new ReemplazarDevengadosHRNDFType { HoraInicio = hrndf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrndf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrndf.Cantidad, Porcentaje = hrndf.Porcentaje, Pago = hrndf.Pago });

                var vacacionesComunes = new List<ReemplazarDevengadosVacacionesVacacionesComunesType>();
                foreach (var vacacion in devengados.VacacionesComunes)
                    vacacionesComunes.Add(new ReemplazarDevengadosVacacionesVacacionesComunesType { FechaInicio = vacacion.FechaInicio, FechaFin = vacacion.FechaFin, Cantidad = vacacion.CantidadDias, Pago = vacacion.Pago });

                var vacacionesCompensadas = new List<ReemplazarDevengadosVacacionesVacacionesCompensadasType>();
                foreach (var vacacion in devengados.VacacionesCompensadas)
                    vacacionesCompensadas.Add(new ReemplazarDevengadosVacacionesVacacionesCompensadasType { Cantidad = vacacion.CantidadDias, Pago = vacacion.Pago });

                var incapacidades = new List<ReemplazarDevengadosIncapacidadType>();
                foreach (var incapacidad in devengados.Incapacidades)
                    incapacidades.Add(new ReemplazarDevengadosIncapacidadType { FechaInicio = incapacidad.FechaInicio, FechaFin = incapacidad.FechaFin, Cantidad = incapacidad.CantidadDias, Tipo = incapacidad.TipoIncapacidad.Codigo, Pago = incapacidad.Pago });

                var licenciasMP = new List<ReemplazarDevengadosLicenciasLicenciaMPType>();
                foreach (var licencia in devengados.LicenciasMaternidadPaternidad)
                    licenciasMP.Add(new ReemplazarDevengadosLicenciasLicenciaMPType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias, Pago = licencia.Pago });

                var licenciasR = new List<ReemplazarDevengadosLicenciasLicenciaRType>();
                foreach (var licencia in devengados.LicenciasRenumeradas)
                    licenciasR.Add(new ReemplazarDevengadosLicenciasLicenciaRType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias, Pago = licencia.Pago });

                var licenciasNR = new List<ReemplazarDevengadosLicenciasLicenciaNRType>();
                foreach (var licencia in devengados.LicenciasNoRenumeradas)
                    licenciasNR.Add(new ReemplazarDevengadosLicenciasLicenciaNRType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias });

                var bonificaciones = new List<ReemplazarDevengadosBonificacionType>();
                foreach (var bonificacion in devengados.Bonificaciones)
                    bonificaciones.Add(new ReemplazarDevengadosBonificacionType { BonificacionS = bonificacion.BonificacionSalarial?.ToString(), BonificacionNS = bonificacion.BonificacionNoSalarial?.ToString() });

                var auxilios = new List<ReemplazarDevengadosAuxilioType>();
                foreach (var auxilio in devengados.Auxilios)
                    auxilios.Add(new ReemplazarDevengadosAuxilioType { AuxilioS = auxilio.AuxilioSalarial?.ToString(), AuxilioNS = auxilio.AuxilioNoSalarial?.ToString() });

                var huelgas = new List<ReemplazarDevengadosHuelgaLegalType>();
                foreach (var huelga in devengados.HuelgasLegales)
                    huelgas.Add(new ReemplazarDevengadosHuelgaLegalType { FechaInicio = huelga.FechaInicio, FechaFin = huelga.FechaFin, Cantidad = huelga.CantidadDias });

                var otrosConceptos = new List<ReemplazarDevengadosOtroConceptoType>();
                foreach (var otro in devengados.OtrosConceptos)
                    otrosConceptos.Add(new ReemplazarDevengadosOtroConceptoType { DescripcionConcepto = otro.Descripcion, ConceptoS = otro.ConceptoSalarial?.ToString(), ConceptoNS = otro.ConceptoNoSalarial?.ToString() });

                var compensaciones = new List<ReemplazarDevengadosCompensacionType>();
                foreach (var compensacion in devengados.Compensaciones)
                    compensaciones.Add(new ReemplazarDevengadosCompensacionType { CompensacionO = compensacion.CompensacionOrdinaria, CompensacionE = compensacion.CompensacionExtraordinaria });

                var bonos = new List<ReemplazarDevengadosBonoEPCTVType>();
                foreach (var bono in devengados.BonosEPCTV)
                    bonos.Add(new ReemplazarDevengadosBonoEPCTVType { PagoS = bono.PagoSalarial?.ToString(), PagoNS = bono.PagoNoSalarial?.ToString(), PagoAlimentacionS = bono.PagoAlimentacionSalarial?.ToString(), PagoAlimentacionNS = bono.PagoAlimentacionNoSalarial?.ToString() });

                AjusteNominaIndividual.Reemplazar.Devengados = new ReemplazarDevengadosType
                {
                    Basico = new ReemplazarDevengadosBasicoType
                    {
                        DiasTrabajados = devengados.Resumen.DiasTrabajados,
                        SueldoTrabajado = devengados.Resumen.SueldoTrabajado
                    },
                    Transporte = transporte.ToArray(),
                    HEDs = heds.ToArray(),
                    HENs = hens.ToArray(),
                    HRNs = hrns.ToArray(),
                    HEDDFs = heddfs.ToArray(),
                    HRDDFs = hrddfs.ToArray(),
                    HENDFs = hendfs.ToArray(),
                    HRNDFs = hrndfs.ToArray(),
                    Vacaciones = new ReemplazarDevengadosVacacionesType
                    {
                        VacacionesComunes = vacacionesComunes.ToArray(),
                        VacacionesCompensadas = vacacionesCompensadas.ToArray()
                    },
                    Primas = (devengados.Primas == null) ? null : new ReemplazarDevengadosPrimasType
                    {
                        Cantidad = devengados.Primas.CantidadDias,
                        Pago = devengados.Primas.Pago,
                        PagoNS = devengados.Primas.PagoNoSalarial?.ToString()
                    },
                    Cesantias = (devengados.Cesantias == null) ? null : new ReemplazarDevengadosCesantiasType
                    {
                        Pago = devengados.Cesantias.Pago,
                        Porcentaje = devengados.Cesantias.Porcentaje,
                        PagoIntereses = devengados.Cesantias.PagoIntereses
                    },
                    Incapacidades = incapacidades.ToArray(),
                    Licencias = new ReemplazarDevengadosLicenciasType
                    {
                        LicenciaMP = licenciasMP.ToArray(),
                        LicenciaR = licenciasR.ToArray(),
                        LicenciaNR = licenciasNR.ToArray()
                    },
                    Bonificaciones = bonificaciones.ToArray(),
                    Auxilios = auxilios.ToArray(),
                    HuelgasLegales = huelgas.ToArray(),
                    OtrosConceptos = otrosConceptos.ToArray(),
                    Compensaciones = compensaciones.ToArray(),
                    BonoEPCTVs = bonos.ToArray(),
                    Comisiones = devengados.Comisiones.ToArray(),
                    PagosTerceros = devengados.PagosTerceros.ToArray(),
                    Anticipos = devengados.Anticipos.ToArray(),
                    Dotacion = devengados.Dotacion,
                    ApoyoSost = devengados.ApoyoSostenimiento,
                    Teletrabajo = devengados.Teletrabajo,
                    BonifRetiro = devengados.BonificacionRetiro,
                    Indemnizacion = devengados.Indemnizacion,
                    Reintegro = devengados.Reintegro
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConDeducciones(Deducciones deducciones)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                var sindicatos = new List<ReemplazarDeduccionesSindicatoType>();
                foreach (var deduccion in deducciones.Sindicatos)
                    sindicatos.Add(new ReemplazarDeduccionesSindicatoType { Porcentaje = deduccion.Porcentaje, Deduccion = deduccion.Deduccion });

                var sanciones = new List<ReemplazarDeduccionesSancionType>();
                foreach (var sancion in deducciones.Sanciones)
                    sanciones.Add(new ReemplazarDeduccionesSancionType { SancionPublic = sancion.SancionPublica, SancionPriv = sancion.SancionPrivada });

                var libranzas = new List<ReemplazarDeduccionesLibranzaType>();
                foreach (var libranza in deducciones.Libranzas)
                    libranzas.Add(new ReemplazarDeduccionesLibranzaType { Descripcion = libranza.Descripcion, Deduccion = libranza.Deduccion });

                AjusteNominaIndividual.Reemplazar.Deducciones = new ReemplazarDeduccionesType
                {
                    Salud = (deducciones.Salud == null) ? null : new ReemplazarDeduccionesSaludType
                    {
                        Porcentaje = deducciones.Salud.Porcentaje,
                        Deduccion = deducciones.Salud.Deduccion
                    },
                    FondoPension = (deducciones.FondoPension == null) ? null : new ReemplazarDeduccionesFondoPensionType
                    {
                        Porcentaje = deducciones.FondoPension.Porcentaje,
                        Deduccion = deducciones.FondoPension.Deduccion
                    },
                    FondoSP = (deducciones.FondoSeguridadPensional == null) ? null : new ReemplazarDeduccionesFondoSPType
                    {
                        Porcentaje = deducciones.FondoSeguridadPensional.Porcentaje?.ToString(),
                        DeduccionSP = deducciones.FondoSeguridadPensional.Deduccion?.ToString(),
                        PorcentajeSub = deducciones.FondoSeguridadPensional.PorcentajeSubsistencia?.ToString(),
                        DeduccionSub = deducciones.FondoSeguridadPensional.DeduccionSubsistencia?.ToString()
                    },
                    Sindicatos = sindicatos.ToArray(),
                    Sanciones = sanciones.ToArray(),
                    Libranzas = libranzas.ToArray(),
                    PagosTerceros = deducciones.PagosTerceros.ToArray(),
                    Anticipos = deducciones.Anticipos.ToArray(),
                    OtrasDeducciones = deducciones.OtrasDeducciones.ToArray(),
                    PensionVoluntaria = deducciones.PensionVoluntaria,
                    RetencionFuente = deducciones.RetencionFuente,
                    AFC = deducciones.AhorroFomentoConstruccion,
                    Cooperativa = deducciones.Cooperativa,
                    EmbargoFiscal = deducciones.EmbargoFiscal,
                    PlanComplementarios = deducciones.PlanesComplementarios,
                    Educacion = deducciones.Educacion,
                    Reintegro = deducciones.Reintegro,
                    Deuda = deducciones.Deuda
                };
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual ConTotales(TotalesNominaIndividual totales)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.DevengadosTotal = totales.TotalDevengados;
                AjusteNominaIndividual.Reemplazar.DeduccionesTotal = totales.TotalDeducciones;
                AjusteNominaIndividual.Reemplazar.ComprobanteTotal = totales.ValorTotal;

                AjusteNominaIndividual.Reemplazar.Redondeo = totales.Redondeo;
            }

            return this;
        }

        public GeneradorAjusteNominaIndividual AsignarCUNE(string softwarePin)
        {
            if (AjusteNominaIndividual.Reemplazar != null)
            {
                AjusteNominaIndividual.Reemplazar.InformacionGeneral.CUNE = CalcularCUNE(AjusteNominaIndividual, softwarePin);
                AjusteNominaIndividual.Reemplazar.InformacionGeneral.EncripCUNE = AlgoritmoSeguridadUUID.CUNE_SHA384.Codigo;

                var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + AjusteNominaIndividual.Reemplazar.InformacionGeneral.CUNE;
                if (AjusteNominaIndividual.Reemplazar.InformacionGeneral.Ambiente == ((int)AmbienteDestino.PRUEBAS).ToString())
                    qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + AjusteNominaIndividual.Reemplazar.InformacionGeneral.CUNE;

                AjusteNominaIndividual.Reemplazar.CodigoQR = qrCodeValue;
            }
            else if (AjusteNominaIndividual.Eliminar != null)
            {
                AjusteNominaIndividual.Eliminar.InformacionGeneral.CUNE = CalcularCUNE(AjusteNominaIndividual, softwarePin);
                AjusteNominaIndividual.Eliminar.InformacionGeneral.EncripCUNE = AlgoritmoSeguridadUUID.CUNE_SHA384.Codigo;

                var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + AjusteNominaIndividual.Eliminar.InformacionGeneral.CUNE;
                if (AjusteNominaIndividual.Eliminar.InformacionGeneral.Ambiente == ((int)AmbienteDestino.PRUEBAS).ToString())
                    qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + AjusteNominaIndividual.Eliminar.InformacionGeneral.CUNE;

                AjusteNominaIndividual.Eliminar.CodigoQR = qrCodeValue;
            }

            return this;
        }


        public static string CalcularCUNE(NominaIndividualDeAjusteType ajusteNomina, string softwarePin)
        {
            var NumNIAE = "";
            var FecNIAE = "";
            var HorNIAE = "";
            var ValDev = "";
            var ValDed = "";
            var ValTol = "";
            var NitNIAE = "";
            var DocEmp = "";
            var TipoXML = "";
            var TipAmb = "";

            if (ajusteNomina.Reemplazar != null)
            {
                NumNIAE = ajusteNomina.Reemplazar.NumeroSecuenciaXML.Numero;
                FecNIAE = ajusteNomina.Reemplazar.InformacionGeneral.FechaGen.ToString("yyyy-MM-dd");
                HorNIAE = ajusteNomina.Reemplazar.InformacionGeneral.HoraGen;
                ValDev = ajusteNomina.Reemplazar.DevengadosTotal.ToString("0.00").Replace(",", ".");
                ValDed = ajusteNomina.Reemplazar.DeduccionesTotal.ToString("0.00").Replace(",", ".");
                ValTol = ajusteNomina.Reemplazar.ComprobanteTotal.ToString("0.00").Replace(",", ".");
                NitNIAE = ajusteNomina.Reemplazar.Empleador.NIT;
                DocEmp = ajusteNomina.Reemplazar.Trabajador.NumeroDocumento;
                TipoXML = ajusteNomina.Reemplazar.InformacionGeneral.TipoXML;
                TipAmb = ajusteNomina.Reemplazar.InformacionGeneral.Ambiente;
            }
            else if (ajusteNomina.Eliminar != null)
            {
                NumNIAE = ajusteNomina.Eliminar.NumeroSecuenciaXML.Numero;
                FecNIAE = ajusteNomina.Eliminar.InformacionGeneral.FechaGen.ToString("yyyy-MM-dd");
                HorNIAE = ajusteNomina.Eliminar.InformacionGeneral.HoraGen;
                ValDev = "0.00";
                ValDed = "0.00";
                ValTol = "0.00";
                NitNIAE = ajusteNomina.Eliminar.Empleador.NIT;
                DocEmp = "0";
                TipoXML = ajusteNomina.Eliminar.InformacionGeneral.TipoXML;
                TipAmb = ajusteNomina.Eliminar.InformacionGeneral.Ambiente;
            }

            string cadena = NumNIAE + FecNIAE + HorNIAE + ValDev + ValDed + ValTol + NitNIAE + DocEmp + TipoXML + softwarePin + TipAmb;

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