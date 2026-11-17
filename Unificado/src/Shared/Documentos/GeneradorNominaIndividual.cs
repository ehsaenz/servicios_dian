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
    public class GeneradorNominaIndividual
    {
        public NominaIndividualType NominaIndividual { get; private set; }

        public GeneradorNominaIndividual(NominaIndividualType nomina)
        {
            NominaIndividual = nomina;
        }

        public GeneradorNominaIndividual(AmbienteDestino ambiente)
        {
            NominaIndividual = new NominaIndividualType
            {
                UBLExtensions = new UBLExtensionType[]
                {
                    new UBLExtensionType { ExtensionContent = new ExtensionContentType { }  }
                },
                Novedad = new NovedadType
                {
                    CUNENov = "",
                    Value = false
                },
                InformacionGeneral = new InformacionGeneralType
                {
                    Version = "V1.0: Documento Soporte de Pago de Nómina Electrónica",
                    Ambiente = ((int)ambiente).ToString(),
                    TipoXML = TipoDocumentoElectronico.NOMINA_INDIVIDUAL.Codigo
                },
                Redondeo = 0
            };
        }

        public GeneradorNominaIndividual ConInformacionGeneral(InformacionGeneralNominaIndividual informacion)
        {
            NominaIndividual.LugarGeneracionXML = new LugarGeneracionXMLType
            {
                Pais = informacion.LugarGeneracion.Pais.Codigo,
                DepartamentoEstado = informacion.LugarGeneracion.Departamento.Codigo,
                MunicipioCiudad = informacion.LugarGeneracion.Municipio.Codigo,
                Idioma = "es"
            };

            NominaIndividual.InformacionGeneral.FechaGen = informacion.FechaGeneracion;
            NominaIndividual.InformacionGeneral.HoraGen = informacion.FechaGeneracion.ToString("HH:mm:ss") + "-05:00";

            NominaIndividual.InformacionGeneral.TipoMoneda = informacion.Moneda.Codigo;
            NominaIndividual.InformacionGeneral.TRM = informacion.TRM;

            NominaIndividual.InformacionGeneral.PeriodoNomina = informacion.PeriodoNomina.Codigo;

            return this;
        }

        public GeneradorNominaIndividual ConPeriodo(PeriodoNomina periodo)
        {
            NominaIndividual.Periodo = new PeriodoType
            {
                FechaIngreso = periodo.FechaIngreso,
                FechaRetiro = periodo.FechaRetiro?.ToString("yyyy-MM-dd"),
                FechaLiquidacionInicio = periodo.FechaLiquidacionInicio,
                FechaLiquidacionFin = periodo.FechaLiquidacionFin,
                TiempoLaborado = periodo.TiempoLaborado.CalcularDias().ToString(),
                FechaGen = NominaIndividual.InformacionGeneral.FechaGen
            };

            return this;
        }

        public GeneradorNominaIndividual ConNumero(string prefijo, long consecutivo)
        {
            NominaIndividual.NumeroSecuenciaXML = new NumeroSecuenciaXMLType
            {
                Prefijo = prefijo,
                Consecutivo = consecutivo,
                Numero = prefijo + consecutivo
            };

            return this;
        }

        public GeneradorNominaIndividual ConProveedorSoftware(ProveedorSoftware proveedor)
        {
            NominaIndividual.ProveedorXML = new ProveedorXMLType
            {
                RazonSocial = proveedor.RazonSocial,
                PrimerApellido = proveedor.PrimerApellido,
                SegundoApellido = proveedor.SegundoApellido,
                PrimerNombre = proveedor.PrimerNombre,
                OtrosNombres = proveedor.OtrosNombres,
                NIT = proveedor.Nit,
                DV = NitHelper.CalcularDV(proveedor.Nit).ToString(),
                SoftwareID = proveedor.SoftwareIdentificador,
                SoftwareSC = CryptographyHelper.Sha384(proveedor.SoftwareIdentificador + proveedor.SoftwarePin + NominaIndividual.NumeroSecuenciaXML.Numero)
            };

            return this;
        }

        public GeneradorNominaIndividual AgregarNota(string observacion)
        {
            var notas = NominaIndividual.Notas?.ToList() ?? new List<string>();

            notas.Add(observacion);

            NominaIndividual.Notas = notas.ToArray();

            return this;
        }

        public GeneradorNominaIndividual ConEmpleador(Empleador empleador)
        {
            NominaIndividual.Empleador = new EmpleadorType
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

            return this;
        }

        public GeneradorNominaIndividual ConTrabajador(Trabajador trabajador)
        {
            NominaIndividual.Trabajador = new TrabajadorType
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

            NominaIndividual.NumeroSecuenciaXML.CodigoTrabajador = trabajador.Codigo;

            return this;
        }

        public GeneradorNominaIndividual ConPago(DetallePago pago, CuentaBancaria cuentaBancaria = null)
        {
            NominaIndividual.Pago = new PagoType
            {
                Forma = ((int)pago.Forma).ToString(),
                Metodo = pago.Metodo.Codigo,
                Banco = cuentaBancaria?.NombreBanco,
                TipoCuenta = cuentaBancaria?.TipoCuenta,
                NumeroCuenta = cuentaBancaria?.NumeroCuenta
            };

            return this;
        }

        public GeneradorNominaIndividual AgregarFechaPago(DateTime fecha)
        {
            var fechas = NominaIndividual.FechasPagos?.ToList() ?? new List<DateTime>();

            fechas.Add(fecha);

            NominaIndividual.FechasPagos = fechas.ToArray();

            return this;
        }

        public GeneradorNominaIndividual ConDevengados(Devengados devengados)
        {
            var transporte = new List<DevengadosTransporteType>();
            if (devengados.Transporte != null)
            {
                transporte.Add(new DevengadosTransporteType
                {
                    AuxilioTransporte = devengados.Transporte.AuxilioTransporte?.ToString(),
                    ViaticoManuAlojS = devengados.Transporte.ViaticoManutencionAlojamiento?.ToString(),
                    ViaticoManuAlojNS = devengados.Transporte.ViaticoManutencionAlojamientoNoSalarial?.ToString()
                });
            }

            foreach (var item in devengados.Transportes)
            {
                transporte.Add(new DevengadosTransporteType
                {
                    AuxilioTransporte = item.AuxilioTransporte?.ToString(),
                    ViaticoManuAlojS = item.ViaticoManutencionAlojamiento?.ToString(),
                    ViaticoManuAlojNS = item.ViaticoManutencionAlojamientoNoSalarial?.ToString()
                });
            }

            var heds = new List<DevengadosHEDType>();
            foreach (var hed in devengados.HorasExtraDiurnas)
                heds.Add(new DevengadosHEDType { HoraInicio = hed.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hed.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hed.Cantidad, Porcentaje = hed.Porcentaje, Pago = hed.Pago });

            var hens = new List<DevengadosHENType>();
            foreach (var hen in devengados.HorasExtraNocturnas)
                hens.Add(new DevengadosHENType { HoraInicio = hen.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hen.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hen.Cantidad, Porcentaje = hen.Porcentaje, Pago = hen.Pago });

            var hrns = new List<DevengadosHRNType>();
            foreach (var hrn in devengados.HorasRecargoNocturno)
                hrns.Add(new DevengadosHRNType { HoraInicio = hrn.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrn.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrn.Cantidad, Porcentaje = hrn.Porcentaje, Pago = hrn.Pago });

            var heddfs = new List<DevengadosHEDDFType>();
            foreach (var heddf in devengados.HorasExtraDiurnasDominicalesFestivos)
                heddfs.Add(new DevengadosHEDDFType { HoraInicio = heddf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = heddf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = heddf.Cantidad, Porcentaje = heddf.Porcentaje, Pago = heddf.Pago });

            var hrddfs = new List<DevengadosHRDDFType>();
            foreach (var hrddf in devengados.HorasRecargoDiurnoDominicalesFestivos)
                hrddfs.Add(new DevengadosHRDDFType { HoraInicio = hrddf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrddf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrddf.Cantidad, Porcentaje = hrddf.Porcentaje, Pago = hrddf.Pago });

            var hendfs = new List<DevengadosHENDFType>();
            foreach (var hendf in devengados.HorasExtraNocturnasDominicalesFestivos)
                hendfs.Add(new DevengadosHENDFType { HoraInicio = hendf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hendf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hendf.Cantidad, Porcentaje = hendf.Porcentaje, Pago = hendf.Pago });

            var hrndfs = new List<DevengadosHRNDFType>();
            foreach (var hrndf in devengados.HorasRecargoNocturnoDominicalesFestivos)
                hrndfs.Add(new DevengadosHRNDFType { HoraInicio = hrndf.HoraInicio?.ToString("yyyy-MM-dd'T'HH:mm:ss"), HoraFin = hrndf.HoraFin?.ToString("yyyy-MM-dd'T'HH:mm:ss"), Cantidad = hrndf.Cantidad, Porcentaje = hrndf.Porcentaje, Pago = hrndf.Pago });

            var vacacionesComunes = new List<DevengadosVacacionesVacacionesComunesType>();
            foreach (var vacacion in devengados.VacacionesComunes)
                vacacionesComunes.Add(new DevengadosVacacionesVacacionesComunesType { FechaInicio = vacacion.FechaInicio, FechaFin = vacacion.FechaFin, Cantidad = vacacion.CantidadDias, Pago = vacacion.Pago });

            var vacacionesCompensadas = new List<DevengadosVacacionesVacacionesCompensadasType>();
            foreach (var vacacion in devengados.VacacionesCompensadas)
                vacacionesCompensadas.Add(new DevengadosVacacionesVacacionesCompensadasType { Cantidad = vacacion.CantidadDias, Pago = vacacion.Pago });

            var incapacidades = new List<DevengadosIncapacidadType>();
            foreach (var incapacidad in devengados.Incapacidades)
                incapacidades.Add(new DevengadosIncapacidadType { FechaInicio = incapacidad.FechaInicio, FechaFin = incapacidad.FechaFin, Cantidad = incapacidad.CantidadDias, Tipo = incapacidad.TipoIncapacidad.Codigo, Pago = incapacidad.Pago });

            var licenciasMP = new List<DevengadosLicenciasLicenciaMPType>();
            foreach (var licencia in devengados.LicenciasMaternidadPaternidad)
                licenciasMP.Add(new DevengadosLicenciasLicenciaMPType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias, Pago = licencia.Pago });

            var licenciasR = new List<DevengadosLicenciasLicenciaRType>();
            foreach (var licencia in devengados.LicenciasRenumeradas)
                licenciasR.Add(new DevengadosLicenciasLicenciaRType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias, Pago = licencia.Pago });

            var licenciasNR = new List<DevengadosLicenciasLicenciaNRType>();
            foreach (var licencia in devengados.LicenciasNoRenumeradas)
                licenciasNR.Add(new DevengadosLicenciasLicenciaNRType { FechaInicio = licencia.FechaInicio, FechaFin = licencia.FechaFin, Cantidad = licencia.CantidadDias });

            var bonificaciones = new List<DevengadosBonificacionType>();
            foreach (var bonificacion in devengados.Bonificaciones)
                bonificaciones.Add(new DevengadosBonificacionType { BonificacionS = bonificacion.BonificacionSalarial?.ToString(), BonificacionNS = bonificacion.BonificacionNoSalarial?.ToString() });

            var auxilios = new List<DevengadosAuxilioType>();
            foreach (var auxilio in devengados.Auxilios)
                auxilios.Add(new DevengadosAuxilioType { AuxilioS = auxilio.AuxilioSalarial?.ToString(), AuxilioNS = auxilio.AuxilioNoSalarial?.ToString() });

            var huelgas = new List<DevengadosHuelgaLegalType>();
            foreach (var huelga in devengados.HuelgasLegales)
                huelgas.Add(new DevengadosHuelgaLegalType { FechaInicio = huelga.FechaInicio, FechaFin = huelga.FechaFin, Cantidad = huelga.CantidadDias });

            var otrosConceptos = new List<DevengadosOtroConceptoType>();
            foreach (var otro in devengados.OtrosConceptos)
                otrosConceptos.Add(new DevengadosOtroConceptoType { DescripcionConcepto = otro.Descripcion, ConceptoS = otro.ConceptoSalarial?.ToString(), ConceptoNS = otro.ConceptoNoSalarial?.ToString() });

            var compensaciones = new List<DevengadosCompensacionType>();
            foreach (var compensacion in devengados.Compensaciones)
                compensaciones.Add(new DevengadosCompensacionType { CompensacionO = compensacion.CompensacionOrdinaria, CompensacionE = compensacion.CompensacionExtraordinaria });

            var bonos = new List<DevengadosBonoEPCTVType>();
            foreach (var bono in devengados.BonosEPCTV)
                bonos.Add(new DevengadosBonoEPCTVType { PagoS = bono.PagoSalarial?.ToString(), PagoNS = bono.PagoNoSalarial?.ToString(), PagoAlimentacionS = bono.PagoAlimentacionSalarial?.ToString(), PagoAlimentacionNS = bono.PagoAlimentacionNoSalarial?.ToString() });

            NominaIndividual.Devengados = new DevengadosType
            {
                Basico = new DevengadosBasicoType
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
                Vacaciones = new DevengadosVacacionesType
                {
                    VacacionesComunes = vacacionesComunes.ToArray(),
                    VacacionesCompensadas = vacacionesCompensadas.ToArray()
                },
                Primas = (devengados.Primas == null) ? null : new DevengadosPrimasType
                {
                    Cantidad = devengados.Primas.CantidadDias,
                    Pago = devengados.Primas.Pago,
                    PagoNS = devengados.Primas.PagoNoSalarial?.ToString()
                },
                Cesantias = (devengados.Cesantias == null) ? null : new DevengadosCesantiasType
                {
                    Pago = devengados.Cesantias.Pago,
                    Porcentaje = devengados.Cesantias.Porcentaje,
                    PagoIntereses = devengados.Cesantias.PagoIntereses
                },
                Incapacidades = incapacidades.ToArray(),
                Licencias = new DevengadosLicenciasType
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

            return this;
        }

        public GeneradorNominaIndividual ConDeducciones(Deducciones deducciones)
        {
            var sindicatos = new List<DeduccionesSindicatoType>();
            foreach (var deduccion in deducciones.Sindicatos)
                sindicatos.Add(new DeduccionesSindicatoType { Porcentaje = deduccion.Porcentaje, Deduccion = deduccion.Deduccion });

            var sanciones = new List<DeduccionesSancionType>();
            foreach (var sancion in deducciones.Sanciones)
                sanciones.Add(new DeduccionesSancionType { SancionPublic = sancion.SancionPublica, SancionPriv = sancion.SancionPrivada });

            var libranzas = new List<DeduccionesLibranzaType>();
            foreach (var libranza in deducciones.Libranzas)
                libranzas.Add(new DeduccionesLibranzaType { Descripcion = libranza.Descripcion, Deduccion = libranza.Deduccion });

            NominaIndividual.Deducciones = new DeduccionesType
            {
                Salud = (deducciones.Salud == null) ? null : new DeduccionesSaludType
                {
                    Porcentaje = deducciones.Salud.Porcentaje,
                    Deduccion = deducciones.Salud.Deduccion
                },
                FondoPension = (deducciones.FondoPension == null) ? null : new DeduccionesFondoPensionType
                {
                    Porcentaje = deducciones.FondoPension.Porcentaje,
                    Deduccion = deducciones.FondoPension.Deduccion
                },
                FondoSP = (deducciones.FondoSeguridadPensional == null) ? null : new DeduccionesFondoSPType
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

            return this;
        }

        public GeneradorNominaIndividual ConTotales(TotalesNominaIndividual totales)
        {
            NominaIndividual.DevengadosTotal = totales.TotalDevengados;
            NominaIndividual.DeduccionesTotal = totales.TotalDeducciones;
            NominaIndividual.ComprobanteTotal = totales.ValorTotal;

            NominaIndividual.Redondeo = totales.Redondeo;

            return this;
        }

        public GeneradorNominaIndividual AsignarCUNE(string softwarePin)
        {
            NominaIndividual.InformacionGeneral.CUNE = CalcularCUNE(NominaIndividual, softwarePin);
            NominaIndividual.InformacionGeneral.EncripCUNE = AlgoritmoSeguridadUUID.CUNE_SHA384.Codigo;

            var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + NominaIndividual.InformacionGeneral.CUNE;
            if (NominaIndividual.InformacionGeneral.Ambiente == ((int)AmbienteDestino.PRUEBAS).ToString())
                qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + NominaIndividual.InformacionGeneral.CUNE;

            NominaIndividual.CodigoQR = qrCodeValue;

            return this;
        }

        public string GenerarTextoQR()
        {
            var NumNIE = NominaIndividual.NumeroSecuenciaXML.Numero;
            var FecNIE = NominaIndividual.InformacionGeneral.FechaGen.ToString("yyyy-MM-dd");
            var HorNIE = NominaIndividual.InformacionGeneral.HoraGen;
            var NitNIE = NominaIndividual.Empleador.NIT;
            var DocEmp = NominaIndividual.Trabajador.NumeroDocumento;
            var ValDev = NominaIndividual.DevengadosTotal.ToString("0.00");
            var ValDed = NominaIndividual.DeduccionesTotal.ToString("0.00");
            var ValTol = NominaIndividual.ComprobanteTotal.ToString("0.00");
            var CUNE = NominaIndividual.InformacionGeneral.CUNE;

            var qrCodeValue = "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=" + NominaIndividual.InformacionGeneral.CUNE;
            if (NominaIndividual.InformacionGeneral.Ambiente == ((int)AmbienteDestino.PRUEBAS).ToString())
                qrCodeValue = "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=" + NominaIndividual.InformacionGeneral.CUNE;

            string ret = "NumNIE: " + NumNIE + "\r\n" +
                "FecNIE: " + FecNIE + "\r\n" +
                "HorNIE: " + HorNIE + "\r\n" +
                "NitNIE: " + NitNIE + "\r\n" +
                "DocEmp: " + DocEmp + "\r\n" +
                "ValDev: " + ValDev + "\r\n" +
                "ValDed: " + ValDed + "\r\n" +
                "ValTol: " + ValTol + "\r\n" +
                "CUNE: " + CUNE + "\r\n" +
                "QRCode: " + qrCodeValue;

            return ret;
        }


        public static string CalcularCUNE(NominaIndividualType nomina, string softwarePin)
        {
            var NumNIE = nomina.NumeroSecuenciaXML.Numero;
            var FecNIE = nomina.InformacionGeneral.FechaGen.ToString("yyyy-MM-dd");
            var HorNIE = nomina.InformacionGeneral.HoraGen;
            var ValDev = nomina.DevengadosTotal.ToString("0.00").Replace(",", ".");
            var ValDed = nomina.DeduccionesTotal.ToString("0.00").Replace(",", ".");
            var ValTol = nomina.ComprobanteTotal.ToString("0.00").Replace(",", ".");
            var NitNIE = nomina.Empleador.NIT;
            var DocEmp = nomina.Trabajador.NumeroDocumento;
            var TipoXML = nomina.InformacionGeneral.TipoXML;
            var TipAmb = nomina.InformacionGeneral.Ambiente;

            string cadena = NumNIE + FecNIE + HorNIE + ValDev + ValDed + ValTol + NitNIE + DocEmp + TipoXML + softwarePin + TipAmb;

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