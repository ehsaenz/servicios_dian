using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using NominaElectronica.Enums;
using NominaElectronica.MetaDatos;

namespace DIAN.Nomina.Servicios
{
    /// <summary>
    /// Construye la nómina individual a partir del cuerpo que envía el ERP (<see cref="MetaDatosNEI"/>). Es el mapeo de la API
    /// anterior (<c>NominaIndividual.GenNominaIndividual</c>) sin base de datos, sin estado global y sin leer la hora del
    /// servidor: la fecha de generación llega por parámetro (hora de Colombia).
    /// </summary>
    public static class ConstructorNomina
    {
        public static GeneradorNominaIndividual ConstruirIndividual(MetaDatosNEI nom, SoftwareConfig software, AmbienteDestino ambiente, bool habilitacion, DateTime ahora)
        {
            DireccionFisica DIRECCION_LABORAL = new DireccionFisica
            {
                Departamento = new Departamento { Codigo = nom.Empleador.LocacionComercial.CodigoDepartamento, Nombre = nom.Empleador.LocacionComercial.NombreDepartamento },
                Municipio = new Municipio { Codigo = nom.Empleador.LocacionComercial.CodigoMunicipio, Nombre = nom.Empleador.LocacionComercial.NombreMunicipio },
                Pais = new Pais { Codigo = nom.Empleador.LocacionComercial.CodigoPais, Nombre = nom.Empleador.LocacionComercial.NombrePais },
                Linea = nom.Empleador.LocacionComercial.Direccion
            };

            var informacion = new InformacionGeneralNominaIndividual
            {
                FechaGeneracion = ahora,
                LugarGeneracion = DIRECCION_LABORAL,
                Moneda = new Moneda { Codigo = nom.InformacionGeneral.Moneda },
                TRM = nom.InformacionGeneral.TRM,
                PeriodoNomina = new TipoPeriodoNomina { Codigo = nom.InformacionGeneral.CodigoTipoPeriodo }   //new MetaDatos.TipoPeriodoNomina.MENSUAL
            };

            var periodo = new PeriodoNomina
            {
                FechaIngreso = nom.PeriodoNomina.FechaIngreso,
                FechaRetiro = nom.PeriodoNomina.FechaRetiro == (DateTime)default ? null : nom.PeriodoNomina.FechaRetiro,
                FechaLiquidacionInicio = nom.PeriodoNomina.FechaLiquidacionInicio,
                FechaLiquidacionFin = nom.PeriodoNomina.FechaLiquidacionFin,
                TiempoLaborado = new TiempoLaborado
                {
                    Dias = nom.PeriodoNomina.DiasLaborados
                }
            };

            var proveedor = new ProveedorSoftware
            {
                RazonSocial = nom.Empleador.Nombre_o_RazonSocial,
                Nit = nom.Empleador.NIT,
                SoftwareIdentificador = software.Identificador,
                SoftwarePin = software.PIN
            };



            var empleador = new Empleador
            {
                RazonSocial = nom.Empleador.Nombre_o_RazonSocial,
                Nit = nom.Empleador.NIT,
                Ubicacion = DIRECCION_LABORAL
            };

            decimal salarioMensual = Decimal.Round(nom.InformacionLaboral.SalarioBasico, 2);
            var salarioHora = decimal.Round(salarioMensual / 240, 2);

            var trabajador = new Trabajador
            {
                Codigo = nom.Empleado.Codigo,
                Tipo = new TipoTrabajador { Codigo = nom.InformacionLaboral.TipoTrabajadorCod, Descripcion = nom.InformacionLaboral.TipoTrabajadorDesc },  //TipoTrabajador.PROFESOR_ESTABLECIMIENTO_PARTICULAR,
                SubTipo = new SubTipoTrabajador { Codigo = nom.InformacionLaboral.SubTipoTrabajadorCod, Descripcion = nom.InformacionLaboral.SubTipoTrabajadorDesc },
                AltoRiesgoPension = nom.InformacionLaboral.AltoRiesgoPension,
                PrimerNombre = nom.Empleado.PrimerNombre,
                OtrosNombres = nom.Empleado.SegundoNombre == "" ? null : nom.Empleado.SegundoNombre,
                PrimerApellido = nom.Empleado.PrimerApellido,
                SegundoApellido = nom.Empleado.SegundoApellido,
                TipoDocumento = new TipoIdentificacion { Codigo = nom.Empleado.TipoIdDian },
                NumeroDocumento = nom.Empleado.NIT,
                TipoContrato = new TipoContrato { Codigo = nom.InformacionLaboral.TipoContrato }, // TipoContrato.TERMINO_FIJO
                SalarioIntegral = nom.InformacionLaboral.SalarioIntegral,
                Sueldo = salarioMensual,
                LugarTrabajo = DIRECCION_LABORAL
            };


            var pago = new DetallePago
            {
                Fecha = ahora,
                Forma = nom.InformacionPago.FormaPago == "2" ? FormaPago.CREDITO : FormaPago.CONTADO, // la nómina siempre es de contado ("1")
                Metodo = new MetodoPago { Codigo = nom.InformacionPago.MetodoPago } // "42" MetodoPago.CONSIGNACION_BANCARIA
            };

            var DiasLaborados = periodo.TiempoLaborado.CalcularDias();

            var devengados = new Devengados
            {
                Resumen = new DevengadoResumen
                {
                    DiasTrabajados = nom.InformacionDevengados.DiasLaborados, /*periodo.TiempoLaborado.CalcularDias()*/
                    SueldoTrabajado = Decimal.Round(nom.InformacionDevengados.SueldoDevengado, 2) // nom.InformacionLaboral.Salario / 30 * DiasLaborados
                }
            };

            if (nom.InformacionDevengados.Primas != null)
            {
                primas p = nom.InformacionDevengados.Primas;
                if(p.Pago > 0) devengados.Primas = new Primas { CantidadDias = p.CantidadDias, Pago = p.Pago, PagoNoSalarial = p.PagoNoSalarial };
            }

            if (nom.InformacionDevengados.VacacionesComunes != null)
            {
                vacaciones_comunes v = nom.InformacionDevengados.VacacionesComunes;
                if(v.Pago > 0)
                {
                    devengados.VacacionesComunes = new Collection<VacacionComun>();
                    devengados.VacacionesComunes.Add(new VacacionComun() { CantidadDias = v.CantidadDias, FechaInicio = v.FechaInicio, FechaFin = v.FechaFin, Pago = v.Pago });
                }
            }

            if (nom.InformacionDevengados.VacacionesCompensadas != null)
            {
                var v = nom.InformacionDevengados.VacacionesCompensadas;
                if (v.Pago > 0)
                {
                    devengados.VacacionesCompensadas = new Collection<VacacionCompensada>();
                    devengados.VacacionesCompensadas.Add(new VacacionCompensada() { CantidadDias = v.CantidadDias, Pago = v.Pago });
                }
            }


            if (nom.InformacionDevengados.Cesantias != null)
            {
                var c = nom.InformacionDevengados.Cesantias;
                devengados.Cesantias = new Cesantias { Pago = c.Pago, Porcentaje = c.Porcentaje, PagoIntereses = c.PagoIntereses };
            }

            decimal auxilioTransporte = nom.InformacionLaboral.AuxilioTransporte ? decimal.Truncate(nom.InformacionDevengados.ValorAuxilioTransporte) : 0;
            if (auxilioTransporte > 0)
            {
                devengados.Transporte = new DevengadoTransporte { AuxilioTransporte = auxilioTransporte  };
            }

            /*
            if (AMBIENTE_PRUEBAS)
            {
                devengados.Auxilios.Add(new Auxilio { AuxilioSalarial = 20000});
                devengados.Auxilios.Add(new Auxilio { AuxilioNoSalarial = 25000 });

                devengados.BonosEPCTV.Add(new BonoEPCTV { PagoAlimentacionSalarial = 50000 });
                devengados.BonosEPCTV.Add(new BonoEPCTV { PagoAlimentacionNoSalarial = 55000 });

                devengados.BonosEPCTV.Add(new BonoEPCTV {  PagoSalarial = 80000  });
                devengados.BonosEPCTV.Add(new BonoEPCTV { PagoNoSalarial = 85000 });

                devengados.Bonificaciones.Add(new Bonificacion { BonificacionSalarial = 100000 });
                devengados.Bonificaciones.Add(new Bonificacion { BonificacionNoSalarial = 100000 });

            }
            */

            foreach(var otroIng in nom.OtrosIngresos)
            {
                switch (otroIng.Concepto)
                {
                    case enmConceptoIngreso.BONO_ALIMENTACION:
                        if (otroIng.Tipo == enmTipoIngreso.CONSTITUTIVO_SALARIO)
                            devengados.BonosEPCTV.Add(new BonoEPCTV { PagoAlimentacionSalarial = otroIng.Valor });
                        else
                            devengados.BonosEPCTV.Add(new BonoEPCTV { PagoAlimentacionNoSalarial = otroIng.Valor });
                        break;
                    case enmConceptoIngreso.BONO_GRAL:
                        if (otroIng.Tipo == enmTipoIngreso.CONSTITUTIVO_SALARIO)
                            devengados.BonosEPCTV.Add(new BonoEPCTV { PagoSalarial = otroIng.Valor });
                        else
                            devengados.BonosEPCTV.Add(new BonoEPCTV { PagoNoSalarial = otroIng.Valor });
                        break;
                    case enmConceptoIngreso.BONIFICACION:
                        if (otroIng.Tipo == enmTipoIngreso.CONSTITUTIVO_SALARIO)
                            devengados.Bonificaciones.Add(new Bonificacion { BonificacionSalarial = otroIng.Valor });
                        else
                            devengados.Bonificaciones.Add(new Bonificacion { BonificacionNoSalarial = otroIng.Valor });
                        break;
                    case enmConceptoIngreso.AUXILIO:
                        if (otroIng.Tipo == enmTipoIngreso.CONSTITUTIVO_SALARIO)
                            devengados.Auxilios.Add(new FacturacionColombia_Metadatos.Auxilio { AuxilioSalarial = otroIng.Valor });
                        else
                            devengados.Auxilios.Add(new FacturacionColombia_Metadatos.Auxilio { AuxilioNoSalarial = otroIng.Valor });
                        break;
                    case enmConceptoIngreso.COMISION:
                        devengados.Comisiones.Add(otroIng.Valor);
                        break;
                }
            }


            foreach (HoraExtraLab hel in nom.HorasExtraDiurnas)
            {
                devengados.HorasExtraDiurnas.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasExtraNocturnas)
            {
                devengados.HorasExtraNocturnas.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasExtraDiurnasDominicalesFestivos)
            {
                devengados.HorasExtraDiurnasDominicalesFestivos.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA_DOMINICAL_FESTIVOS").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA_DOMINICAL_FESTIVOS").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasRecargoDiurnoDominicalesFestivos)
            {
                devengados.HorasRecargoDiurnoDominicalesFestivos.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_RECARGO_DIURNO_DOMINICAL_FESTIVOS").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_RECARGO_DIURNO_DOMINICAL_FESTIVOS").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasExtraNocturnasDominicalesFestivos)
            {
                devengados.HorasExtraNocturnasDominicalesFestivos.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA_DOMINICAL_FESTIVOS").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA_DOMINICAL_FESTIVOS").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasRecargoNocturnoDominicalesFestivos)
            {
                devengados.HorasRecargoNocturnoDominicalesFestivos.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_RECARGO_NOCTURNO_DOMINICAL_FESTIVOS").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_RECARGO_NOCTURNO_DOMINICAL_FESTIVOS").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }


            decimal totalDevCS = 0;
            decimal totalDevNCS = 0;
            decimal totalDevengados = 0;

            decimal ValorSueldo = nom.InformacionDevengados.SueldoDevengado;
            totalDevCS += ValorSueldo;
            totalDevengados += ValorSueldo;

            decimal ValorAT = devengados.Transporte?.AuxilioTransporte ?? 0;
            totalDevNCS += ValorAT;
            totalDevengados += ValorAT;

            decimal ValorHED = devengados.HorasExtraDiurnas?.Sum(h => h.Pago) ?? 0;
            totalDevCS += ValorHED;
            totalDevengados += ValorHED;

            decimal ValorHEDDF = devengados.HorasExtraDiurnasDominicalesFestivos?.Sum(h => h.Pago) ?? 0;
            totalDevCS += ValorHEDDF;
            totalDevengados += ValorHEDDF;


            // ************************ Corrección 2022-05-16 ************************
            // ***********************  AUXILIOS *************************** 
            decimal ValorAS = devengados.Auxilios?.Sum(a => a.AuxilioSalarial) ?? 0;
            totalDevCS += ValorAS; // Auxilio Constitutivo de Salario
            totalDevengados += ValorAS;

            decimal ValorANS = devengados.Auxilios?.Sum(a => a.AuxilioNoSalarial) ?? 0;
            totalDevNCS += ValorANS; // Auxilio NO Constitutivo de Salario
            totalDevengados += ValorANS;

            // **** BONOS Electronicos, Papel, Cheques, Tarjetas, Vales  **** 
            decimal ValorBAS = devengados.BonosEPCTV?.Sum(bas => bas.PagoAlimentacionSalarial) ?? 0;
            totalDevCS += ValorBAS; // Bono Alimentacion Salarial
            totalDevengados += ValorBAS;

            decimal ValorBANS = devengados.BonosEPCTV?.Sum(bans => bans.PagoAlimentacionNoSalarial) ?? 0;
            totalDevNCS += ValorBANS;  // Bono Alimentacion NO Salarial
            totalDevengados += ValorBANS;

            decimal ValorBS = devengados.BonosEPCTV?.Sum(bps => bps.PagoSalarial) ?? 0;
            totalDevCS += ValorBS;  // Bono Salarial
            totalDevengados += ValorBS;

            decimal ValorBNS = devengados.BonosEPCTV?.Sum(bpns => bpns.PagoNoSalarial) ?? 0;
            totalDevNCS += ValorBNS;  // Bono NO Salarial
            totalDevengados += ValorBNS;

            decimal ValorBSE = devengados.Bonificaciones?.Sum(bs => bs.BonificacionSalarial) ?? 0;
            totalDevCS += ValorBSE; // Bonificacion Salarial Efectivo
            totalDevengados += ValorBS;

            decimal ValorBNSE = devengados.Bonificaciones?.Sum(bns => bns.BonificacionNoSalarial) ?? 0;
            totalDevNCS += ValorBNSE;  // Bonificacion NO Salarial Efectivo
            totalDevengados += ValorBNSE;

            decimal ValorCMS = devengados.Comisiones?.Sum() ?? 0;
            totalDevCS += ValorCMS; // Comisiones Siempre Constitutivas de Salario
            totalDevengados += ValorCMS;

            // ***********************************************************************

            if (nom.InformacionDevengados.Primas != null)
            {
                decimal ValorPrimasOrd = devengados.Primas?.Pago ?? 0;
                totalDevengados += ValorPrimasOrd;

                decimal ValorPrimasExtra = devengados.Primas?.PagoNoSalarial ?? 0;
                totalDevengados += ValorPrimasExtra;
            }

            if (nom.InformacionDevengados.VacacionesComunes != null)
            {
                decimal ValorVCM= nom.InformacionDevengados.VacacionesComunes.Pago;
                totalDevCS += ValorVCM;
                totalDevengados += ValorVCM;
            }

            if (nom.InformacionDevengados.Cesantias != null)
            {
                decimal ValorCST= nom.InformacionDevengados.Cesantias.Pago;
                totalDevNCS += ValorCST;
                totalDevengados += ValorCST;

                decimal ValorICST = nom.InformacionDevengados.Cesantias.PagoIntereses;
                totalDevNCS += ValorICST;
                totalDevengados += ValorICST;

            }

            var deducciones = new Deducciones
            {
                // PENDIENTE REVISAR SI ESTAS DEDUCCIONES SE APLICAN SOBRE EL DEVENGADO SALARAIAL O SOBRE EL SUELDO DEVENGADO
                Salud = new DeduccionSalud
                {
                    Porcentaje = 4,
                    Deduccion = decimal.Round((totalDevCS / 100) * 4, 2)
                },
                FondoPension = new FondoPension
                {
                    Porcentaje = 4,
                    Deduccion = decimal.Round((totalDevCS / 100) * 4, 2)
                    // salarioMensual
                },
                /*FondoSeguridadPensional = new FondoSeguridadPensional
                {
                    Porcentaje = 1,
                    Deduccion = decimal.Round((salarioMensual / 100) * 1, 2)
                }*/
            };


            decimal totalDeducciones = 0;
            totalDeducciones += deducciones.Salud?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoPension?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoSeguridadPensional?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoSeguridadPensional?.DeduccionSubsistencia ?? 0;

            // etc ...

            var totales = new TotalesNominaIndividual
            {
                TotalDevengados = totalDevengados,
                TotalDeducciones = totalDeducciones,
                ValorTotal = totalDevengados - totalDeducciones 
            };

            CuentaBancaria CtaBanco = null;

            if (nom.InformacionPago.MetodoPago == "42")
            {
                CtaBanco = new CuentaBancaria
                {
                    NombreBanco = nom.CuentaBanco.NombreBanco,
                    TipoCuenta = nom.CuentaBanco.TipoCuenta,
                    NumeroCuenta = nom.CuentaBanco.NumeroCuenta
                };
            }

            // CuentaBancaria CtaBanco = new CuentaBancaria { NombreBanco = cuentaBancaria.NombreBanco, NumeroCuenta = cuentaBancaria.NumeroCuenta, TipoCuenta = cuentaBancaria.TipoCuenta };

            var generador = new GeneradorNominaIndividual(ambiente)
                .ConInformacionGeneral(informacion)
                .ConPeriodo(periodo)
                .ConNumero(nom.InformacionEnvio.Prefijo, nom.InformacionEnvio.Numero)
                .ConProveedorSoftware(proveedor)
                .ConEmpleador(empleador)
                .ConTrabajador(trabajador)
                .ConPago(pago, CtaBanco)
                .AgregarFechaPago(pago.Fecha)
                .ConDevengados(devengados)
                .ConDeducciones(deducciones)
                .ConTotales(totales)
                .AsignarCUNE(software.PIN);

            List<string> notas = nom.Notas?.ToList() ?? new List<string> { "Este documento fué generado por MatrixSQL-Nómina" + (habilitacion ? " en AMBIENTE DE PRUEBAS, por tanto no es válido" : " en ambiente de Producción") };

            foreach (string n in notas)
            {
                generador.AgregarNota(n);
            }
            

            return generador;
        }

        /// <summary>
        /// Construye el ajuste de nómina (reemplazar o eliminar una nómina ya enviada). Es el mapeo de la API anterior
        /// (<c>GenNominaIndividualAjuste</c>); el número lleva la letra del tipo de ajuste ("R" o "E") después del prefijo.
        /// </summary>
        public static GeneradorAjusteNominaIndividual ConstruirAjuste(MetaDatosNEI nom, ReferenciaNominaIndividual ref_nom, SoftwareConfig software, AmbienteDestino ambiente, bool habilitacion, DateTime ahora)
        {
            

            DireccionFisica DIRECCION_LABORAL = new DireccionFisica
            {
                Departamento = new Departamento { Codigo = nom.Empleador.LocacionComercial.CodigoDepartamento, Nombre = nom.Empleador.LocacionComercial.NombreDepartamento },
                Municipio = new Municipio { Codigo = nom.Empleador.LocacionComercial.CodigoMunicipio, Nombre = nom.Empleador.LocacionComercial.NombreMunicipio },
                Pais = new Pais { Codigo = nom.Empleador.LocacionComercial.CodigoPais, Nombre = nom.Empleador.LocacionComercial.NombrePais },
                Linea = nom.Empleador.LocacionComercial.Direccion
            };

            

            var informacion = new InformacionGeneralNominaIndividual
            {
                FechaGeneracion = ahora,
                LugarGeneracion = DIRECCION_LABORAL,
                Moneda = new Moneda { Codigo = nom.InformacionGeneral.Moneda },
                TRM = nom.InformacionGeneral.TRM,
                PeriodoNomina = new TipoPeriodoNomina { Codigo = nom.InformacionGeneral.CodigoTipoPeriodo }   //new MetaDatos.TipoPeriodoNomina.MENSUAL
            };

            var periodo = new PeriodoNomina
            {
                FechaIngreso = nom.PeriodoNomina.FechaIngreso,
                FechaRetiro = nom.PeriodoNomina.FechaRetiro == (DateTime) default ? null : nom.PeriodoNomina.FechaRetiro,
                FechaLiquidacionInicio = nom.PeriodoNomina.FechaLiquidacionInicio,
                FechaLiquidacionFin = nom.PeriodoNomina.FechaLiquidacionFin,
                TiempoLaborado = new TiempoLaborado
                {
                    Dias = nom.PeriodoNomina.DiasLaborados
                }
            };

            var proveedor = new ProveedorSoftware
            {
                RazonSocial = nom.Empleador.Nombre_o_RazonSocial,
                Nit = nom.Empleador.NIT,
                SoftwareIdentificador = software.Identificador,
                SoftwarePin = software.PIN
            };

            var empleador = new Empleador
            {
                RazonSocial = nom.Empleador.Nombre_o_RazonSocial,
                Nit = nom.Empleador.NIT,
                Ubicacion = DIRECCION_LABORAL
            };

            decimal salarioMensual = Decimal.Round(nom.InformacionLaboral.SalarioBasico, 2);
            var salarioHora = decimal.Round(salarioMensual / 240, 2);

            var trabajador = new Trabajador
            {
                Codigo = nom.Empleado.Codigo,
                Tipo = new TipoTrabajador { Codigo = nom.InformacionLaboral.TipoTrabajadorCod, Descripcion = nom.InformacionLaboral.TipoTrabajadorDesc },  //TipoTrabajador.PROFESOR_ESTABLECIMIENTO_PARTICULAR,
                SubTipo = new SubTipoTrabajador { Codigo = nom.InformacionLaboral.SubTipoTrabajadorCod, Descripcion = nom.InformacionLaboral.SubTipoTrabajadorDesc },
                AltoRiesgoPension = nom.InformacionLaboral.AltoRiesgoPension,
                PrimerNombre = nom.Empleado.PrimerNombre,
                OtrosNombres = nom.Empleado.SegundoNombre == "" ? null : nom.Empleado.SegundoNombre,
                PrimerApellido = nom.Empleado.PrimerApellido,
                SegundoApellido = nom.Empleado.SegundoApellido,
                TipoDocumento = new TipoIdentificacion { Codigo = nom.Empleado.TipoIdDian },
                NumeroDocumento = nom.Empleado.NIT,
                TipoContrato = new TipoContrato { Codigo = nom.InformacionLaboral.TipoContrato }, // TipoContrato.TERMINO_FIJO
                SalarioIntegral = nom.InformacionLaboral.SalarioIntegral,
                Sueldo = Decimal.Round(nom.InformacionLaboral.SalarioBasico, 2),
                LugarTrabajo = DIRECCION_LABORAL
            };


            var pago = new DetallePago
            {
                Fecha = ahora,
                Forma = nom.InformacionPago.FormaPago == "2" ? FormaPago.CREDITO : FormaPago.CONTADO,
                Metodo = new MetodoPago { Codigo = nom.InformacionPago.MetodoPago } // "42" MetodoPago.CONSIGNACION_BANCARIA
            };

            CuentaBancaria cuentaBancaria = new CuentaBancaria
            {
                NombreBanco = nom.CuentaBanco.NombreBanco,
                TipoCuenta = nom.CuentaBanco.TipoCuenta,
                NumeroCuenta = nom.CuentaBanco.NumeroCuenta
            };

            var DiasLaborados = periodo.TiempoLaborado.CalcularDias();

            var devengados = new Devengados
            {
                Resumen = new DevengadoResumen
                {
                    DiasTrabajados = nom.InformacionDevengados.DiasLaborados, /*periodo.TiempoLaborado.CalcularDias()*/
                    SueldoTrabajado = Decimal.Round(nom.InformacionDevengados.SueldoDevengado, 2) // nom.InformacionLaboral.Salario / 30 * DiasLaborados
                }
            };

            decimal auxilioTransporte = nom.InformacionLaboral.AuxilioTransporte ? decimal.Truncate(nom.InformacionDevengados.ValorAuxilioTransporte) : 0;
            if (auxilioTransporte > 0)
            {
                devengados.Transporte = new DevengadoTransporte { AuxilioTransporte = auxilioTransporte };
            }

            foreach (HoraExtraLab hel in nom.HorasExtraDiurnas)
            {
                devengados.HorasExtraDiurnas.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_DIURNA").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            foreach (HoraExtraLab hel in nom.HorasExtraNocturnas)
            {
                devengados.HorasExtraNocturnas.Add(
                    new HoraExtra
                    {
                        HoraInicio = hel.HoraInicio,
                        HoraFin = hel.HoraFin,
                        Cantidad = hel.Cantidad,
                        Porcentaje = maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA").Porcentaje,
                        Pago = decimal.Round(salarioHora * maestro_tipos_hora_extra.GetTipoHoraExtraRecargo("HORA_EXTRA_NOCTURNA").PorcentajeCalculo * hel.Cantidad, 2)
                    });
            }

            var deducciones = new Deducciones
            {
                Salud = new DeduccionSalud
                {
                    Porcentaje = 4,
                    Deduccion = decimal.Round((nom.InformacionDevengados.SueldoDevengado / 100) * 4, 2)
                },
                FondoPension = new FondoPension
                {
                    Porcentaje = 4,
                    Deduccion = decimal.Round((nom.InformacionDevengados.SueldoDevengado / 100) * 4, 2)
                    // salarioMensual
                },
                /*FondoSeguridadPensional = new FondoSeguridadPensional
                {
                    Porcentaje = 1,
                    Deduccion = decimal.Round((salarioMensual / 100) * 1, 2)
                }*/
            };


            decimal totalDevCS = 0;
            decimal totalDevNCS = 0;
            decimal totalDevengados = 0;

            decimal ValorSueldo = nom.InformacionDevengados.SueldoDevengado;
            totalDevCS += ValorSueldo;
            totalDevengados += ValorSueldo;

            decimal ValorAT = devengados.Transporte?.AuxilioTransporte ?? 0;
            totalDevNCS += ValorAT;
            totalDevengados += ValorAT; // nom.InformacionLaboral.AuxilioTransporte ? nom.InformacionDevengados.ValorAuxilioTransporte : 0; //  devengados.Transporte.AuxilioTransporte ?? 0;

            decimal ValorHED = devengados.HorasExtraDiurnas?.Sum(h => h.Pago) ?? 0;
            totalDevCS += ValorHED;
            totalDevengados += ValorHED;

            decimal ValorHEDDF = devengados.HorasExtraDiurnasDominicalesFestivos?.Sum(h => h.Pago) ?? 0;
            totalDevCS += ValorHEDDF;
            totalDevengados += ValorHEDDF; 

            // etc ...

            decimal totalDeducciones = 0;
            totalDeducciones += deducciones.Salud?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoPension?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoSeguridadPensional?.Deduccion ?? 0;
            totalDeducciones += deducciones.FondoSeguridadPensional?.DeduccionSubsistencia ?? 0;

            // etc ...

            var totales = new TotalesNominaIndividual
            {
                TotalDevengados = totalDevengados,
                TotalDeducciones = totalDeducciones,
                ValorTotal = totalDevengados - totalDeducciones
            };


            //NominaIndividualType nomina;

            // ReferenciaNominaIndividual ref_nom;

            //switch (nom.InformacionEnvio.TipoEnvio)
            //{
            //    case enmTipoEnvio.Eliminar:
            //    case enmTipoEnvio.Reemplazar:
            //        //ref_nom = new ReferenciaNominaIndividual { Numero = nom.DocumentoReferencia.Numero, FechaGeneracion = nom.DocumentoReferencia.FechaGeneracion, Cune = nom.DocumentoReferencia.Cune };
            //        //numeroAjusteNomina = ref_nom.Numero;
            //        //cuneAjusteNomina = ref_nom.Cune;
            //        break;
            //    default:
            //        resp.Errores.Add(new MtxError { Ambito = enmAmbito.BackEnd, Codigo = "0001", Descripcion = "Los únicos valores aceptados en Nomina de Ajuste son: ELIMIENAR y REEPLAZAR" });
            //        responseDian.statusCode = "101";
            //        responseDian.statusMessage = $"El valor del campo [InformacionEnvio.TipoEnvio] = {nom.InformacionEnvio.TipoEnvio.ToString()} No es válido";
            //        return responseDian; // { Ok = false };
            //}


            GeneradorAjusteNominaIndividual genAjuste = null; ;
            NominaIndividualDeAjusteType ajusteNomina;

            if (nom.InformacionEnvio.TipoEnvio == enmTipoEnvio.Eliminar)
            {
                genAjuste = new GeneradorAjusteNominaIndividual(ambiente, TipoAjusteNominaIndividual.ELIMINAR, ref_nom)
                .ConInformacionGeneral(informacion)
                .ConPeriodo(periodo)
                .ConNumero(nom.InformacionEnvio.Prefijo + "E", nom.InformacionEnvio.Numero)
                .ConProveedorSoftware(proveedor)
                //.AgregarNota("Observación 1")
                //.AgregarNota("Observación 2")
                //.AgregarNota("Observación 3")
                .ConEmpleador(empleador)
                .ConTrabajador(trabajador)
                .ConPago(pago, cuentaBancaria)
                .AgregarFechaPago(pago.Fecha)
                .AsignarCUNE(software.PIN);

            }
            else if (nom.InformacionEnvio.TipoEnvio == enmTipoEnvio.Reemplazar)
            {
                genAjuste = new GeneradorAjusteNominaIndividual(ambiente, TipoAjusteNominaIndividual.REEMPLAZAR, ref_nom)
                    .ConInformacionGeneral(informacion)
                    .ConPeriodo(periodo)
                    .ConNumero(nom.InformacionEnvio.Prefijo + "R", nom.InformacionEnvio.Numero)
                    .ConProveedorSoftware(proveedor)
                    //.AgregarNota("Observación 1")
                    //.AgregarNota("Observación 2")
                    //.AgregarNota("Observación 3")
                    .ConEmpleador(empleador)
                    .ConTrabajador(trabajador)
                    .ConPago(pago, cuentaBancaria)
                    .AgregarFechaPago(pago.Fecha)
                    .ConDevengados(devengados) // en tipo REEMPLAZAR
                    .ConDeducciones(deducciones) // en tipo REEMPLAZAR
                    .ConTotales(totales) // en tipo REEMPLAZAR
                    .AsignarCUNE(software.PIN);
            }
            else
            {
                throw new ArgumentException($"El valor del campo [InformacionEnvio.TipoEnvio] = {nom.InformacionEnvio.TipoEnvio.ToString()} No es válido");
            }

            List<string> notas = nom.Notas?.ToList() ?? new List<string> { "Primer envío" };

            foreach (string n in notas)
            {
                genAjuste.AgregarNota(n);
                //genAjuste.AgregarNota("Nómina enviada con error");
            }


            ajusteNomina = genAjuste.AjusteNominaIndividual;
            if (nom.InformacionEnvio.TipoEnvio == enmTipoEnvio.Reemplazar)
                nom.InformacionEnvio.CUNE = ajusteNomina.Reemplazar.InformacionGeneral.CUNE;
            else
                nom.InformacionEnvio.CUNE = ajusteNomina.Eliminar.InformacionGeneral.CUNE;

            return genAjuste;
        }
    }
}
