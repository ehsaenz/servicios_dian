using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using BasicLibraryExt;
using NominaElectronica.Enums;

namespace NominaElectronica.MetaDatos
{

    public class MetaDatosNEI
    {
        private List<Error> errors = new List<Error>();
        private enmTipoDocElec tipoDocElec;
        private enmTipoEnvio tipoEnvio;

        public MetaDatosNEI() 
        {
            tipoDocElec = enmTipoDocElec.NominaElectronicaIndividual;
            tipoEnvio = enmTipoEnvio.Normal;
        }

        /// <summary>
        /// Esta sobrecarga es para NominaIndividual de Ajuste
        /// </summary>
        /// <param name="tipo_envio">Normal/Eliminar/Reemplazar</param>
        /// <param name="cune_documento_ref">CUNE del documento que desea Eliminar/Reemplazar</param>
        /// <param name="fecha_gen_documento_ref">Fecha de generación del documento que desea Eliminar/Reemplazar</param>
        /// <param name="numero_documento_ref">Prefijo + Numero del documento que desea Eliminar/Reemplazar</param>
        public MetaDatosNEI(enmTipoEnvio tipo_envio, string cune_documento_ref, DateTime fecha_gen_documento_ref, string numero_documento_ref)
        {
            tipoDocElec = enmTipoDocElec.NominaElectronicaIndividualAjuste;
            tipoEnvio = tipo_envio;
            if (tipo_envio == enmTipoEnvio.Normal)
            {
                throw new MetadatosValorNoPermitdoException($"El tipo de envío [{tipoEnvio.ToString()}] no está permitido para docuentos de tipo [{tipoDocElec}]");
                //if (cune_documento_ref != null) errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "1001", Descripcion = "Si [TipoEnvio] es [Normal] [cune_documento_ref] debería ser [null]" });
            }
            else
            {
                var sb = new StringBuilder();
                //if (cune_documento_ref == null) errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "1001", Descripcion = "Si [TipoEnvio] es [Eliminar/Reemplazar] [cune_documento_ref] no puede ser [null]" });
                if (cune_documento_ref == null || cune_documento_ref == "")
                    sb.AppendLine($"Si [TipoEnvio] es [{tipoEnvio.ToString()}] [cune_documento_ref] no puede ser [null]");
                if (numero_documento_ref == null || numero_documento_ref == "")
                    sb.AppendLine($"Si [TipoEnvio] es [{tipoEnvio.ToString()}] [numero_documento_ref] no puede ser [null]");
                if (fecha_gen_documento_ref == default(DateTime))
                    sb.AppendLine($"Si [TipoEnvio] es [{tipoEnvio.ToString()}] [fecha_gen_documento_ref] no puede ser [null]");
                if(sb.ToString() == "")
                    DocumentoReferencia = new documento_referencia { Cune = cune_documento_ref, FechaGeneracion = fecha_gen_documento_ref, Numero = numero_documento_ref };
                else
                    throw new MetadatosValorNoPermitdoException(sb.ToString());

            }
        }


        public List<string> Notas;

        public documento_referencia DocumentoReferencia { get; set; }

        public informacion_envio InformacionEnvio { get; set; }
        public parametros_legales_nomina ParametrosLegalesNomina { get; set; }
        public persona Empleador { get; set; }
        public persona Empleado { get; set; }
        public cuenta_banco CuentaBanco { get; set; }
        public informacion_laboral InformacionLaboral { get; set; }
        public informacion_devevngados InformacionDevengados { get; set; }
        public periodo_nomina PeriodoNomina { get; set; }
        public informacion_general InformacionGeneral { get; set; }
        public informacion_pago InformacionPago { get; set; }


        public Collection<HoraExtraLab> HorasExtraDiurnas { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasExtraNocturnas { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasRecargoNocturno { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasExtraDiurnasDominicalesFestivos { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasRecargoDiurnoDominicalesFestivos { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasExtraNocturnasDominicalesFestivos { get; set; } = new Collection<HoraExtraLab>();
        public Collection<HoraExtraLab> HorasRecargoNocturnoDominicalesFestivos { get; set; } = new Collection<HoraExtraLab>();

        public Collection<OtroIngreso> OtrosIngresos { get; set; } = new Collection<OtroIngreso>();

        #region METODOS

        /// <summary>
        /// Obtiene últimos parametros legales para el año solicitado
        /// Este método falla si se quieren obtener los parametros que estaban vigentes en una fecha específica
        /// </summary>
        /// <param name="AAAA"></param>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        [Obsolete]
        public parametros_legales GetParametrosLegales(string AAAA, string apiKey)
        {
            //var url = $"http://26.153.35.249:8061/api/ParametrosNomina/{AÑO}";
            var url = $"https://services.matrixsql.com:41390/api/ParametrosNomina/GetByYear/{AAAA}";
            return ParametrosLegales(url, apiKey, AAAA, false);
        }

        /// <summary>
        /// Obtiene últimos parametros legales que se encontraban vigentes a una fecha específica 
        /// </summary>
        /// <param name="fecha_vigencia"></param>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        public parametros_legales GetParametrosLegales(DateTime fecha_vigencia, string apiKey)
        {
            var string_fecha = fecha_vigencia.ToString("yyyy-MM-dd");
            var url = $"https://services.matrixsql.com:41390/api/ParametrosNomina/GetByDate/{string_fecha}";
            return ParametrosLegales(url, apiKey, string_fecha, true);
        }


        private parametros_legales ParametrosLegales(string url, string apiKey, string vigencia, bool EsFecha = true)
        {
            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Accept = "application/json";

                if (apiKey != "")
                    request.Headers.Add("ApiKey", apiKey);

                response = (HttpWebResponse)request.GetResponse();
                StreamReader sr = new StreamReader(response.GetResponseStream());
                string responseBody = sr.ReadToEnd();
                if (responseBody.Trim() == "")
                {
                    if (EsFecha)
                        throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"No se encontraron parametros legales para la fecha [" + vigencia + "]\",\"Errors\":[]}");
                    else
                        throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"No se encontraron parametros legales para el año [" + vigencia + "]\",\"Errors\":[]}");
                }
                parametros_legales ni = responseBody.ToObject<parametros_legales>();  //  MtxJson.Utils.JsonDeserializar<parametros_legales>(responseBody);
                return ni;

            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"" + e.Message + "\",\"Errors\":[]}");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        throw new Exception(streamReader.ReadToEnd());
                    }
                }
            }

        }




        public string GetParametrosLegalesStr(string AÑO)
        {
            HttpWebResponse response = null;
            try
            {
                var url = $"http://26.153.35.249:8061/api/ParametrosNomina/{AÑO}";

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("ApiKey", DianModelDoc.Servicios.SecretosLocales.Obtener("Legado.Unificado/src/Nomina/MetaDatosNE/MetaDatosNEI.cs::TokenHttp.177"));
                response = (HttpWebResponse)request.GetResponse();
                StreamReader sr = new StreamReader(response.GetResponseStream());
                string responseBody = sr.ReadToEnd();
                if (responseBody.Trim() == "")
                    throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"No se encontraron paraetros legales para el año [" + AÑO + "]\",\"Errors\":[]}");
                return responseBody;

            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"" + e.Message + "\",\"Errors\":[]}");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        throw new Exception(streamReader.ReadToEnd());
                    }
                }
            }
        }


        public string GetParametrosNomina(DateTime FechaLiquidacion)
        {
            HttpWebResponse response = null;
            try
            {
                //var url = $"http://26.153.35.249:8061/api/ParametrosNomina/{AÑO}";
                var url = $"https://services.matrixsql.com:41390/api/ParametrosNomina/GetByDate/{FechaLiquidacion.ToString("yyyy-MM-dd")}";         
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Headers.Add("ApiKey", DianModelDoc.Servicios.SecretosLocales.Obtener("Legado.Unificado/src/Nomina/MetaDatosNE/MetaDatosNEI.cs::TokenHttp.214"));

                response = (HttpWebResponse)request.GetResponse();
                StreamReader sr = new StreamReader(response.GetResponseStream());
                string responseBody = sr.ReadToEnd();
                if (responseBody.Trim() == "")
                    throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"No se encontraron paraetros legales para la fecha [" + FechaLiquidacion + "]\",\"Errors\":[]}");
                return responseBody;

            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw new Exception("{\"statusCodeAPI\":\"0000\",\"statusMessageAPI\":\"" + e.Message + "\",\"Errors\":[]}");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        throw new Exception(streamReader.ReadToEnd());
                    }
                }
            }
        }



        public void SetInformacionEnvio(string prefijo, long numero)
        {
            InformacionEnvio = new informacion_envio { Prefijo = prefijo, Numero = numero, TipoDocumentoElectronico = tipoDocElec, TipoEnvio = tipoEnvio };
        }


        /// <summary>
        /// Usar cuando el empleador es persona Natural
        /// </summary>
        /// <param name="cedula"></param>
        /// <param name="dv"></param>
        /// <param name="primer_nombre"></param>
        /// <param name="segundo_nombre"></param>
        /// <param name="primer_apellido"></param>
        /// <param name="segundo_apellido"></param>
        /// <param name="email"></param>
        /// <param name="direccion_principal"></param>
        /// <param name="direccion_sucursal"></param>
        public void SetEmpleadorPersonaNatural(string cedula, string dv, string primer_nombre, string segundo_nombre, string primer_apellido, string segundo_apellido, string email, LocacionFisica direccion_principal, LocacionFisica direccion_sucursal)
        {
            Empleador = new persona() { Rol = enmRolPersona.Empleador, TipoPersona = "NATURAL", NIT = cedula, DV = dv, PrimerNombre = primer_nombre, SegundoNombre = segundo_nombre, PrimerApellido = primer_apellido, SegundoApellido = segundo_apellido, EmailContacto = email, LocacionPrincipal = direccion_principal, LocacionComercial = direccion_sucursal };
        }

        [System.Obsolete("Este método no contiene el parámetro [email] por lo cual no debe usarse")]
        public void SetEmpleadorPersonaNatural(string cedula, string dv, string primer_nombre, string segundo_nombre, string primer_apellido, string segundo_apellido, LocacionFisica direccion_principal, LocacionFisica direccion_sucursal)
        {
            Empleador = new persona() { Rol = enmRolPersona.Empleador, TipoPersona = "NATURAL", NIT = cedula, DV = dv, PrimerNombre = primer_nombre, SegundoNombre = segundo_nombre, PrimerApellido = primer_apellido, SegundoApellido = segundo_apellido, LocacionPrincipal = direccion_principal, LocacionComercial = direccion_sucursal };
        }


        /// <summary>
        /// Usar cuando el empleador es persona jurídica
        /// </summary>
        /// <param name="nit"></param>
        /// <param name="dv"></param>
        /// <param name="razon_social"></param>
        /// <param name="nombrenombre_sucursal"></param>
        /// <param name="email"></param>
        /// <param name="direccion_principal"></param>
        /// <param name="direccion_laboral"></param>
        public void SetEmpleadorPersonaJuridica(string nit, string dv, string razon_social, string nombrenombre_sucursal, string email, LocacionFisica direccion_principal, LocacionFisica direccion_laboral)
        {
            Empleador = new persona() { Rol = enmRolPersona.Empleador, TipoPersona = "JURIDICA", NIT = nit, DV = dv, RazonSocial = razon_social, NombreComercial = nombrenombre_sucursal, EmailContacto = email, LocacionPrincipal = direccion_principal, LocacionComercial = direccion_laboral };
        }

        [System.Obsolete("Este método no contiene el parámetro [email] por lo cual no debe usarse")]
        public void SetEmpleadorPersonaJuridica(string nit, string dv, string razon_social, string Nombrenombre_sucursal, LocacionFisica DireccionPrincipal, LocacionFisica DireccionLaboral)
        {
            Empleador = new persona() { Rol = enmRolPersona.Empleador, TipoPersona = "JURIDICA", NIT = nit, DV = dv, RazonSocial = razon_social, NombreComercial = Nombrenombre_sucursal, LocacionPrincipal = DireccionPrincipal, LocacionComercial = DireccionLaboral };
        }


        /// <summary>
        /// Setear datos del empleado
        /// </summary>
        /// <param name="cedula"></param>
        /// <param name="primer_nombre"></param>
        /// <param name="segundo_nombre"></param>
        /// <param name="primer_apellido"></param>
        /// <param name="segundo_apellido"></param>
        /// <param name="email"></param>
        /// <param name="direcion_vivienda"></param>
        /// <param name="direccion_laboral"></param>
        public void SetEmpleado(string codigo, string cedula, string id_tipo_persona, string primer_nombre, string segundo_nombre, string primer_apellido, string segundo_apellido, string email, LocacionFisica direcion_vivienda, LocacionFisica direccion_laboral)
        {
            Empleado = new persona() { Rol = enmRolPersona.Empleado, TipoPersona = "NATURAL", TipoIdDian = id_tipo_persona, Codigo = codigo, NIT = cedula, PrimerNombre = primer_nombre, SegundoNombre = segundo_nombre, PrimerApellido = primer_apellido, SegundoApellido = segundo_apellido, EmailContacto = email,  LocacionPrincipal = direcion_vivienda, LocacionComercial = direccion_laboral };
        }

        [System.Obsolete("Este método no contiene el parámetro [email] por lo cual no debe usarse")]
        public void SetEmpleado(string codigo, string cedula, string id_tipo_persona, string primer_nombre, string segundo_nombre, string primer_apellido, string segundo_apellido, LocacionFisica direcion_vivienda, LocacionFisica direccion_laboral)
        {
            Empleado = new persona() { Rol = enmRolPersona.Empleado, TipoPersona = "NATURAL", TipoIdDian = id_tipo_persona, Codigo = codigo, NIT = cedula, PrimerNombre = primer_nombre, SegundoNombre = segundo_nombre, PrimerApellido = primer_apellido, SegundoApellido = segundo_apellido, LocacionPrincipal = direcion_vivienda, LocacionComercial = direccion_laboral };
        }


        /// <summary>
        /// Datos de la cuenta
        /// </summary>
        /// <param name="nombre_banco"></param>
        /// <param name="tipo_cuenta"></param>
        /// <param name="numero_cuenta"></param>
        public void SetCuenta(string nombre_banco, string tipo_cuenta, string numero_cuenta)
        {
            CuentaBanco = new cuenta_banco { NombreBanco = nombre_banco, TipoCuenta = tipo_cuenta, NumeroCuenta = numero_cuenta };
        }

        public void SetParametrosLegales(int AÑO, decimal SalarioMinimoLegalVigente, decimal AuxilioTransporteLegalVigente, int HorasSemanaJornadaLaboral)
        {
            ParametrosLegalesNomina = new parametros_legales_nomina { AAAA = AÑO, SalarioMinimo = SalarioMinimoLegalVigente, AuxilioTransporte = AuxilioTransporteLegalVigente, HorasSemanales = HorasSemanaJornadaLaboral };
        }

        public void SetParametrosLegales(parametros_legales ParametrosLegales)
        {
            ParametrosLegalesNomina = new parametros_legales_nomina(ParametrosLegales);
        }




        public void SetInformacionGeneral(string moneda, decimal TRM, string periodo_nomina_cod)
        {
            InformacionGeneral = new informacion_general { Moneda = moneda, TRM = TRM, CodigoTipoPeriodo = periodo_nomina_cod }; //new parametros_legales() { AAAA = 2022, SMLV = 1000000, ATLV = 117172, HSJL = 48 }
        }

        public void SetInformacionGeneral(string moneda, decimal TRM, string periodo_nomina_cod, parametros_legales parametrosLegales)
        {
            InformacionGeneral = new informacion_general { Moneda = moneda, TRM = TRM, CodigoTipoPeriodo = periodo_nomina_cod}; //new parametros_legales() { AAAA = 2022, SMLV = 1000000, ATLV = 117172, HSJL = 48 }
            ParametrosLegalesNomina = new parametros_legales_nomina(parametrosLegales);
        }



        /// <summary>
        /// Información lalboral
        /// </summary>
        /// <param name="salario"></param>
        /// <param name="fecha_ingreso"></param>
        /// <param name="tipo_contrato"></param>
        /// <param name="tipo_trabajador_cod"></param>
        /// <param name="sub_tipo_trabajador_Desc"></param>

        [System.Obsolete("Este método no contiene el parámetro [contrato_id] por lo cual no debe usarse")]
        public void SetInformacionLaboral(decimal salario, bool auxilio_transporte, DateTime fecha_ingreso, string tipo_contrato, string tipo_trabajador_cod, string tipo_trabajador_Desc, string sub_tipo_trabajador_cod, string sub_tipo_trabajador_desc)
        {
            InformacionLaboral = new informacion_laboral { SalarioBasico = salario, AuxilioTransporte = auxilio_transporte, FechaIngreso = fecha_ingreso, TipoContrato = tipo_contrato, TipoTrabajadorCod = tipo_trabajador_cod, TipoTrabajadorDesc = tipo_trabajador_Desc, SubTipoTrabajadorCod = sub_tipo_trabajador_cod, SubTipoTrabajadorDesc = sub_tipo_trabajador_desc };
            
        }


        /// <summary>
        /// Hace referencia a información contractual vigente surante el período de nómina
        /// </summary>
        /// <param name="salario"></param>
        /// <param name="auxilio_transporte"></param>
        /// <param name="fecha_ingreso"></param>
        /// <param name="tipo_contrato"></param>
        /// <param name="contrato_id"></param>
        /// <param name="tipo_trabajador_cod"></param>
        /// <param name="tipo_trabajador_Desc"></param>
        /// <param name="sub_tipo_trabajador_cod"></param>
        /// <param name="sub_tipo_trabajador_desc"></param>
        public void SetInformacionLaboral(decimal salario, bool auxilio_transporte, DateTime fecha_ingreso, string tipo_contrato, int contrato_id, string tipo_trabajador_cod, string tipo_trabajador_Desc, string sub_tipo_trabajador_cod, string sub_tipo_trabajador_desc)
        {
            InformacionLaboral = new informacion_laboral { SalarioBasico = salario, AuxilioTransporte = auxilio_transporte, FechaIngreso = fecha_ingreso, TipoContrato = tipo_contrato, ContratoID = contrato_id, TipoTrabajadorCod = tipo_trabajador_cod, TipoTrabajadorDesc = tipo_trabajador_Desc, SubTipoTrabajadorCod = sub_tipo_trabajador_cod, SubTipoTrabajadorDesc = sub_tipo_trabajador_desc };
        }


        public void SetPeriodoNomina(DateTime fecha_ingrreso, DateTime? fecha_retiro, DateTime fecha_liquidacion_inicio, DateTime fecha_liquidacion_fin, int dias_laborados)
        {
            PeriodoNomina = new periodo_nomina { FechaIngreso = fecha_ingrreso, FechaRetiro = fecha_retiro, FechaLiquidacionInicio = fecha_liquidacion_inicio, FechaLiquidacionFin = fecha_liquidacion_fin, DiasLaborados = dias_laborados };
        }

        public informacion_devevngados SetInformacionDevengados(decimal salario_devengado, int dias_laborados, decimal valor_auxilio_transporte, int dias_auxilio_transporte)
        {
            InformacionDevengados = new informacion_devevngados { SueldoDevengado = salario_devengado, DiasLaborados = dias_laborados, ValorAuxilioTransporte = valor_auxilio_transporte, DiasAuxilioTransporte = dias_auxilio_transporte };
            return InformacionDevengados;
        }



        public void SetInformacionPago(string forma_pago, string metodo_pago)
        {
            InformacionPago = new informacion_pago { FormaPago = forma_pago, MetodoPago = metodo_pago };
        }


        public void AddOtroIngreso(enmConceptoIngreso conceptoIngreso, enmTipoIngreso tipoIngreso, decimal valor)
        {
            OtrosIngresos.Add( new OtroIngreso { Concepto = conceptoIngreso, Tipo =   tipoIngreso, Valor = valor });
        }



        public RespMethod Validar()
        {
            RespMethod resp = new RespMethod();

            int AAAA = PeriodoNomina.FechaLiquidacionInicio.Year;

            if (ParametrosLegalesNomina.SalarioMinimo == 0) errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"No se ha definido el salario minimo para el año {AAAA}" });
            if (ParametrosLegalesNomina.AuxilioTransporte == 0) errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"No se ha definido el auxilio de transporte para el año {AAAA}" });

            if (InformacionLaboral.AuxilioTransporte)
            {
                if (InformacionLaboral.SalarioBasico >= (ParametrosLegalesNomina.SalarioMinimo * 2))
                {
                    // Creo que se debe cambiar este error por un warning
                    errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = "El empleado no tiene derecho a Auxilio de transporte, porque su salario mensual es mayor o igual a 2 SMLV" });
                }

            }
            else
            {
                if(InformacionDevengados.ValorAuxilioTransporte > 0)
                    errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"En [Información Laboral] se indica que éste empleado no tiene derecho a Auxilio de transporte, sin embargo, en [Información Devengados] se le reporta pago por {InformacionDevengados.ValorAuxilioTransporte.ToString("$ ##,###.#0")}" });
            }

            if(InformacionDevengados.DiasAuxilioTransporte > InformacionDevengados.DiasLaborados)
                errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"El número de dias para Auxilio de transporte [{InformacionDevengados.DiasAuxilioTransporte}] no puede ser superior al numero de días laborados [{InformacionDevengados.DiasLaborados}]" });

            if (InformacionDevengados.SueldoDevengado != (InformacionLaboral.SalarioBasico / 30 * InformacionDevengados.DiasLaborados))
                errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"El sueldo devengado [{InformacionDevengados.SueldoDevengado}] no coincide con (SALARIO_BASICO / 30 * DIAS_LABORADOS)" });

            if (InformacionDevengados.ValorAuxilioTransporte != (ParametrosLegalesNomina.AuxilioTransporte / 30 * InformacionDevengados.DiasAuxilioTransporte))
                errors.Add(new Error { Source = ErrorSource.FrontEnd, Codigo = "PREV001", Descripcion = $"El valor del axilio de transporte [{InformacionDevengados.ValorAuxilioTransporte}] no coincide con (AUXILIO_TRANSPORTE / 30 * DIAS_AUXILIO_TRANSPORTE)" });

            Empleador.Validacion(ref errors);
            Empleado.Validacion(ref errors);

            if (errors.Count == 0)
                resp.Ok = true;
            else 
            {
                resp.Ok = false;
                resp.Obj = (string)"Hay errores en la información suministrada, por favor revise los errores antes de enviar a la DIAN";
            }
 
            resp.Errores = errors;
            
           return resp;
        }

        public MetaDatosNEI AgregarNota(string Observacion)
        {
            var notas = Notas?.ToList() ?? new List<string>();
            notas.Add(Observacion);
            return this;
        }

        #endregion

    }


    public class OtroIngreso
    {
        public enmConceptoIngreso Concepto { get; set; }
        public enmTipoIngreso Tipo { get; set; }
        public decimal Valor { get; set; }
        
    }


    /// <summary>
    /// Información que identifica cada documento de nómina electrónica o nómina de ajuste enviados a la DIAN 
    /// </summary>
    public class informacion_envio
    {
        public enmTipoDocElec TipoDocumentoElectronico { get; set; }
        public enmTipoEnvio TipoEnvio { get; set; }
        public string Prefijo { get; set; }
        public long Numero { get; set; }
        public DateTime FechaEnvio { get; set; }
        public string CUNE { get; set; }


    }

    /// <summary>
    /// Esto solo se necesita para Nomina Electronica de Ajuste
    /// </summary>
    public class documento_referencia
    {

        /// <summary>
        /// Prefijo + Consecutivo de la nómina que se quiere Reeplazar o Eliminar
        /// </summary>
        public string Numero { get; set; }

        /// <summary>
        /// CUNE de la nómina que se quiere Reemplazar o Eliminar
        /// </summary>
        public string Cune { get; set; }

        /// <summary>
        /// Fecha de la nómina que se quiere Reemplazar o Eliminar
        /// </summary>
        public DateTime FechaGeneracion { get; set; }
    }




    public class informacion_general
    {
        public string Moneda { get; set; } = "COP";

        /// <summary>
        /// Corresponde a la tasa de cambio de la moneda utilizada en el documento en el campo Moneda a Pesos Colombianos.
        /// </summary>
        public decimal TRM { get; set; } = 1;

        /// <summary>
        /// Código segun tabla [ne_periodos_nomina]
        /// </summary>
        public string CodigoTipoPeriodo { get; set; }

        //public decimal SMLV = 1000000;

    }

    public class informacion_laboral
    {
        public informacion_laboral()
        {
            HorasMensuales = 240;
        }

        private parametros_legales_nomina ParametrosLegales { get; set; }



        /// <summary>
        /// Salario Mensual del trabajador pactado en el contrato
        /// </summary>
        public decimal SalarioBasico { get; set; }

        /// <summary>
        /// Horas mensuales pactadas en el contrato
        /// El valor predeterminado es 240 para el año 2022, pero habrá una dismunución anual hasta llegar al 210 (42 semanales)
        /// </summary>
        public int HorasMensuales { get; set; }
        
        /// <summary>
        /// Indica si un empleado tiene derecho a auxilio de transporte
        /// Se puede omitir, excepto en los casos en que le aplque por su nivel de ingresos, pero no le aplique otras condiciones como teletrabajo o porque vive muy cerca del trabajo
        /// </summary>
        public bool AuxilioTransporte { get; set; }

        public DateTime FechaIngreso;


        /// <summary>
        /// Código de tipo de comprato segun maestro [ne_tipos_contrato]
        /// </summary>
        public string TipoContrato { get; set; }

        /// <summary>
        /// Normalmente es un valor entero, pero es posible que algunas empresas manejen un prefijo por cada sucursal, por cual se ha definido como alanumérico
        /// </summary>
        public int ContratoID { get; set; }


        /// <summary>
        /// Código del tipo de trabajador segun maestro [ne_tipos_trabajador]
        /// </summary>
        public string TipoTrabajadorCod { get; set; }
        public string TipoTrabajadorDesc { get; set; }

        /// <summary>
        /// Código del subtipo de trabajador según maestro [ne_subtipos_trabajador]
        /// </summary>
        public string SubTipoTrabajadorCod { get; set;}
        public string SubTipoTrabajadorDesc { get; set; }
        public bool AltoRiesgoPension { get; set; } = false;
        public bool SalarioIntegral { get; set; } = false; 

    }

    public class parametros_legales
    {
        /// <summary>
        /// Año en curso
        /// </summary>
        public int AAAA { get; set; }

        public DateTime FechaVigencia { get; set; }

        /// <summary>
        /// Salario Mínimo Legal Vigente, decrecretado por el gobierno nacional para el año en curso
        /// </summary>
        public decimal SalarioMinimo { get; set; }

        /// <summary>
        /// Auxilio de transporte, decretado por el gobierno nacional para el año en curso
        /// </summary>
        public decimal AuxilioTransporte { get; set; }

        /// <summary>
        /// Horas semanales de la Jornada Laboral decretadas por el gobierno nacional para el año e curso
        /// </summary>
        public int HorasSemanales { get; set; }

    }


    public class parametros_legales_nomina: parametros_legales
    {
        public parametros_legales_nomina() { }
        public parametros_legales_nomina(parametros_legales ParametrosLegales)
        {
            AAAA = ParametrosLegales.AAAA;  
            SalarioMinimo = ParametrosLegales.SalarioMinimo;
            AuxilioTransporte = ParametrosLegales.AuxilioTransporte;
            HorasSemanales = ParametrosLegales.HorasSemanales; 
        }

        /// <summary>
        /// Horas mensuales de la Jornada Laboras Legal
        /// </summary>
        /// <returns></returns>
        public int HMJL() { return HorasSemanales / 6 * 30; }


        /// <summary>
        /// mAXIMO 
        /// </summary>
        /// <param name="DiasLaboralesSemana"></param>
        /// <returns></returns>
        public int MaxHorasDiariasOrdinarias(int DiasLaboralesSemana) { return HorasSemanales / DiasLaboralesSemana; }


    }




    public class periodo_nomina
    {
        /// <summary>
        /// Fecha de Ingreso del trabajador a la empresa.
        /// </summary>
        public DateTime FechaIngreso { get; set; }

        /// <summary>
        /// Fecha de Retiro del trabajador a la empresa.
        /// </summary>
        public DateTime? FechaRetiro { get; set; }

        /// <summary>
        /// Fecha de Inicio del período de liquidación del documento.
        /// </summary>
        public DateTime FechaLiquidacionInicio { get; set; }

        /// <summary>
        /// Fecha de Fin del período de liquidación del documento.
        /// </summary>
        public DateTime FechaLiquidacionFin { get; set; }

        /// <summary>
        /// Tiempo que lleva el trabajador laboranod en la empresa.
        /// </summary>
        public int DiasLaborados { get; set; }
    }


    /// <summary>
    /// Sirve para cualquier tipo de auxilio sea o no contitutivo de salario
    /// </summary>
    public class Auxilio
    {
        public decimal? AuxilioSalarial { get; set; }
        public decimal? AuxilioNoSalarial { get; set; }
    }

    public class informacion_devevngados
    {
        public Collection<Auxilio> Auxilios { get; set; } = new Collection<Auxilio>();



        /// <summary>
        /// Es el resultado de: SalarioBasico / 30 * DiasTrabajados
        /// </summary>
        public decimal SueldoDevengado { get; set; }

        /// <summary>
        /// Numero de días laborados durante el periodo liquidado
        /// </summary>
        public int DiasLaborados { get; set; }

        /// <summary>
        /// Es el resultado de AuxilioTransporte / 30 * DiasAuxilioTransporte
        /// </summary>
        public decimal ValorAuxilioTransporte { get; set; }

        /// <summary>
        /// Puede o no ser igual los días laborados
        /// </summary>
        public int DiasAuxilioTransporte { get; set; }



        public vacaciones_compensadas VacacionesCompensadas { get; set; }


        //public decimal ComisionesConstitutivasSalario { get; set; }
        //public decimal BonificacionesConstitutivasSalario { get; set; }
        public primas Primas { get; set; }


        public void SetPrimas(int cantidad_dias, decimal valor_ordinario)
        {
            if (Primas == null) Primas = new primas();  
            Primas.CantidadDias = cantidad_dias;
            Primas.Pago = valor_ordinario;
            //SetPrimas(cantidad_dias, valor_ordinario, null);
        }

        public void SetPrimasExtra(decimal? valor_extraordinario)
        {
            if (Primas == null) Primas = new primas();
            Primas.PagoNoSalarial = valor_extraordinario;
            //var x = new primas { CantidadDias = 180, }
        }





        public vacaciones_comunes VacacionesComunes { get; set; }

        /// <summary>
        /// Vacaciones remuneradas y disfrutadas en tiempo.
        /// </summary>
        /// <param name="cantidad_dias">Cantidad de días disfrutados</param>
        /// <param name="fecha_ini">Primer día de descanso remunerado</param>
        /// <param name="fecha_fin">Ultimo día de descanso remuerado</param>
        /// <param name="valor">Valor pagado por vacaciones</param>
        [Obsolete("Este metodo fue reemplazado por [SetVacacionesComunes]")]
        public void SetVacaciones(int cantidad_dias, DateTime fecha_ini, DateTime fecha_fin, decimal valor)
        {
            VacacionesComunes = new vacaciones_comunes() { CantidadDias = cantidad_dias, FechaInicio = fecha_ini, FechaFin = fecha_fin, Pago = valor };
        }

        /// <summary>
        /// Vacaciones remuneradas y disfrutadas en tiempo. (Es igua a [SetVacaciones])
        /// </summary>
        /// <param name="cantidad_dias">Cantidad de días disfrutados</param>
        /// <param name="fecha_ini">Primer día de descanso remunerado</param>
        /// <param name="fecha_fin">Ultimo día de descanso remuerado</param>
        /// <param name="valor">Valor pagado por vacaciones</param>
        public void SetVacacionesComunes(int cantidad_dias, DateTime fecha_ini, DateTime fecha_fin, decimal valor)
        {
            VacacionesComunes = new vacaciones_comunes() { CantidadDias = cantidad_dias, FechaInicio = fecha_ini, FechaFin = fecha_fin, Pago = valor };
        }


        /// <summary>
        /// Vacaciones remuneradas y disfrutadas en tiempo.
        /// </summary>
        /// <param name="cantidad_dias">Cantidad de días disfrutados</param>
        /// <param name="valor">Valor pagado por vacaciones</param>
        public void SetVacacionesCompensadas(int cantidad_dias, decimal valor)
        {
            VacacionesCompensadas = new vacaciones_compensadas() { CantidadDias = cantidad_dias, Pago = valor };
        }


        public cesantias Cesantias { get; set; }
        public void SetCesantias(decimal valor_cesantias, decimal porcentaje_interes, decimal valor_intereses)
        {
            Cesantias = new cesantias() { Pago = valor_cesantias, Porcentaje = porcentaje_interes, PagoIntereses = valor_intereses };
        }

        public incapacidad Incapacidad { get; set; }
        public void SetIncapacidad(enmTipoIncapacidad tipo_incapacidad, DateTime fecha_inicio, DateTime fecha_fin, int cantidad_dias, decimal pago)
        {
            Incapacidad = new incapacidad() { TipoIncapacidad = tipo_incapacidad, FechaInicio = fecha_inicio, FechaFin = fecha_fin, CantidadDias = cantidad_dias, Pago = pago };
        }


    }


    // ############  MAESTROS ####################

    public class vacaciones_comunes
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        /// <summary>
        /// Número de días que el trabajador estuvo inactivo durante el mes por vacaciones.
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Corresponde al valor pagado al trabajador, por el descanso remunerado que tiene derecho por haber trabajado un determinado tiempo. (Vacaciones SI disfrutadas)
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class vacaciones_compensadas
    {
        /// <summary>
        /// Número de días que el trabajador estuvo activo durante el mes sin disfrutar sus vacaciones. (Vacaciones NO disfrutadas)
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Corresponde al valor pagado al trabajador, por el descanso remunerado que no disfrutó y que tiene derecho por haber trabajado un determinado tiempo. (Vacaciones NO disfrutadas)
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class primas
    {
        /// <summary>
        /// Cantidad de días trabajados para cálculo de Pago de Corte de Prima
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Valor Pagado por Prima Legal con respecto a Cantidad de Días
        /// </summary>
        public decimal Pago { get; set; }

        /// <summary>
        /// Valor Pagado por Prima No Salarial
        /// </summary>
        public decimal? PagoNoSalarial { get; set; }
    }

    public class cesantias
    {
        /// <summary>
        /// Valor Pagado por Cesantías
        /// </summary>
        public decimal Pago { get; set; }

        /// <summary>
        /// Porcentaje de Interes de Cesantías
        /// </summary>
        public decimal Porcentaje { get; set; }

        /// <summary>
        /// Valor Pagado por Intereses de Cesantías
        /// </summary>
        public decimal PagoIntereses { get; set; }
    }

    public class incapacidad
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
        public enmTipoIncapacidad TipoIncapacidad { get; set; }
        public decimal Pago { get; set; }
    }


    public class licencia_maternidad_paternidad
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        /// <summary>
        /// Número de días que el trabajador o aprendiz efectivamente estuvo inactivo por licencia de maternidad o paternidad.
        /// </summary>
        public int CantidadDias { get; set; }

        /// <summary>
        /// Valor pagado al trabajador del descanso remunerado que la ley confiere por el nacimiento de un hijo, y que es reconocido y pagado por la EPS a la que está 
        /// afiliado el padre o la madre, o en su defecto por el empleador.
        /// </summary>
        public decimal Pago { get; set; }
    }

    public class licencia_renumerada
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
        public decimal Pago { get; set; }
    }

    public class licencia_no_renumerada
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
    }

    public class bonificacion
    {
        public decimal? BonificacionSalarial { get; set; }
        public decimal? BonificacionNoSalarial { get; set; }
    }

    public class auxilio
    {
        public decimal? AuxilioSalarial { get; set; }
        public decimal? AuxilioNoSalarial { get; set; }
    }

    public class huelga_legal
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int CantidadDias { get; set; }
    }

    public class otro_concepto_salarial
    {
        public string Descripcion { get; set; }
        public decimal? ConceptoSalarial { get; set; }
        public decimal? ConceptoNoSalarial { get; set; }
    }

    public class compensacion
    {
        /// <summary>
        /// Suma de dinero definido en el régimen de compensaciones como retribución mensual recibido por el asociado por la ejecución de su actividad material o inmaterial, la cual 
        /// se fija teniendo en cuenta el tipo de labor desempeñada, el rendimiento o la productividad y la cantidad de trabajo aportado. El monto de la compensación ordinaria podrá ser
        /// una suma básica igual para todos los asociados (Ordinaria).
        /// </summary>
        public decimal CompensacionOrdinaria { get; set; }

        /// <summary>
        /// Los demás pagos adicionales a la Compensación Ordinaria que recibe el asociado como retribución por su trabajo, definidos en el régimen de compensaciones (Extraordinaria).
        /// </summary>
        public decimal CompensacionExtraordinaria { get; set; }
    }

    /// <summary>
    /// Bonos Electronicos o de Papel de Servicio, Cheques, Tarjetas, Vales, etc.
    /// </summary>
    public class bono_epctv
    {
        public decimal? PagoSalarial { get; set; }
        public decimal? PagoNoSalarial { get; set; }
        public decimal? PagoAlimentacionSalarial { get; set; }
        public decimal? PagoAlimentacionNoSalarial { get; set; }
    }

    // ###########################################

    public class informacion_pago
    {
        public string FormaPago { get; set; }
        public string MetodoPago { get; set;}
    }

    public class cuenta_banco
    {
        public string NombreBanco { get; set; }
        public string TipoCuenta { get; set; }
        public string NumeroCuenta { get; set; }

    }







    public class persona
    {
        public persona()
        {
            PrimerNombre = "";
            SegundoNombre = "";
            PrimerApellido = "";
            SegundoApellido = "";
            RazonSocial = "";
        }


        private string AdditionalAccountID;
        private string tipoIdDian;
        private string tipoPersona;

        /// <summary>
        /// Empleador/Empleado  
        /// </summary>
        public enmRolPersona Rol { get; set; }

        public string TipoPersona
        {
            get { return tipoPersona; }
            set
            {
                tipoPersona = value;
                if (value == "JURIDICA")
                {
                    AdditionalAccountID = "1";
                }
                else if (value == "NATURAL")
                {
                    AdditionalAccountID = "2";
                }
            }
        }



        //private string tipoIdDian;
        /// <summary>
        /// ID correspondiente al Tipo de Identificación
        /// </summary>
        public string TipoIdDian { get { return tipoIdDian; } set { tipoIdDian = value; } }


        /// <summary>
        /// Es un codigo interno usado por el ERP para identificar al Cliente, un Nit puede estar asociado a uno o mas códigos
        /// </summary>
        public string Codigo { get; set; }
        public string MatriculaMercantil { get; set; }
        public string NIT { get; set; }
        public string DV { get; set; }
        public string PrimerNombre { get; set; }
        public string SegundoNombre { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }
        public string RazonSocial { get; set; }
        public string Nombre_o_RazonSocial
        {
            get
            {
                string nom;
                if (AdditionalAccountID == "1") nom = RazonSocial;
                else nom = PrimerNombre + " " + SegundoNombre + " " + PrimerApellido + " " + SegundoApellido;
                return Regex.Replace(nom, @"\s+", " ");
            }
            set {; }
        }
        public LocacionFisica LocacionPrincipal { get; set; }
        public LocacionFisica LocacionComercial { get; set; }
        public string NombreComercial { get; set; }
        public string NombreContacto { get; set; }
        public string TelefonoContacto { get; set; }
        public string EmailContacto { get; set; }
        public string CargoContacto { get; set; }


        #region METODOS
        //private int AdditionalAccountID;
        /// <summary>
        /// Persona Jurpidica = "1", Persona Natural = "2"
        /// </summary>
        public string IdTipoPersona() { return AdditionalAccountID; }


        public void Validacion(ref List<Error> errores)
        {
            if (tipoPersona == null)
            {
                errores.Add(new Error { Source = ErrorSource.FrontEnd, Descripcion = "La variable TipoPersona tiene un valor null" });
                return;
            }
            switch (tipoPersona.ToUpper())
            {
                case "NATURAL":
                    if (PrimerNombre == "" || PrimerApellido == "")
                        errores.Add(new Error { Source = ErrorSource.FrontEnd, Descripcion = $"El tercero [{Nombre_o_RazonSocial}] es persona NATURAL pero no especificó Primer Nombre o Primer Apellido" });
                    break;
                case "JURIDICA":
                    if (RazonSocial == "")
                        errores.Add(new Error { Source = ErrorSource.FrontEnd, Descripcion = $"El tercero [{Nombre_o_RazonSocial}] es persona JURIDICA pero no especificó Razón Social" });
                    break;
                default:
                    errores.Add(new Error { Source = ErrorSource.FrontEnd, Descripcion = $"El tipo de persona <{tipoPersona}> no es válido para el tercero  [{Nombre_o_RazonSocial}] " });
                    break;
            }

        }
        #endregion

    }


    public class LocacionFisica
    {
        public string Direccion { get; set; }
        public string CodigoMunicipio { get; set; }
        public string NombreMunicipio { get; set; }
        public string CodigoDepartamento { get; set; }
        public string NombreDepartamento { get; set; }
        public string CodigoPais { get; set; }
        public string NombrePais { get; set; }
    }

    // Tipos.cs
    public class HoraExtraLab
    {
        /// <summary>
        /// Fecha y Hora de inicio de Hora Extra
        /// </summary>
        public DateTime? HoraInicio { get; set; }

        /// <summary>
        /// Fecha y Hora de fin de Hora Extra
        /// </summary>
        public DateTime? HoraFin { get; set; }

        /// <summary>
        /// Cantidad de Horas Extra
        /// </summary>
        public decimal Cantidad { get; set; }

        ///// <summary>
        ///// Porcentaje al cual corresponde el calculo de 1 hora Extra
        ///// </summary>
        //public decimal Porcentaje { get; set; }

        ///// <summary>
        ///// Es el valor pagado por el tiempo que se trabaja adicional a la jornada legal o pactada contractualmente
        ///// </summary>
        //public decimal Pago { get; set; }
    }


    public class TipoHoraExtraRec
    {

        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public decimal Porcentaje { get; set; }

        public decimal PorcentajeCalculo
        {
            get { return 1 + (Porcentaje / 100); }
        }

    }



    public static class maestro_tipos_hora_extra
    {
        private static bool cargado = false; 

        private static Dictionary<string, TipoHoraExtraRec> TiposHER = new Dictionary<string, TipoHoraExtraRec>();

        private static void CargarDatos()
        {
            if (!cargado)
            {
                TiposHER = new Dictionary<string, TipoHoraExtraRec>();
                TiposHER.Add("HORA_EXTRA_DIURNA", new TipoHoraExtraRec { Codigo = "1", Descripcion = "Hora Extra Diurna", Porcentaje = 25.00m });
                TiposHER.Add("HORA_EXTRA_NOCTURNA", new TipoHoraExtraRec { Codigo = "2", Descripcion = "Hora Extra Nocturna", Porcentaje = 75.00m });
                TiposHER.Add("HORA_RECARGO_NOCTURNO", new TipoHoraExtraRec { Codigo = "3", Descripcion = "Hora Recargo Nocturno", Porcentaje = 35.00m });
                TiposHER.Add("HORA_EXTRA_DIURNA_DOMINICAL_FESTIVOS", new TipoHoraExtraRec { Codigo = "4", Descripcion = "Hora Extra Diurna Dominical y Festivos", Porcentaje = 100.00m });
                TiposHER.Add("HORA_RECARGO_DIURNO_DOMINICAL_FESTIVOS", new TipoHoraExtraRec { Codigo = "5", Descripcion = "Hora Recargo Diurno Dominical y Festivos", Porcentaje = 75.00m });
                TiposHER.Add("HORA_EXTRA_NOCTURNA_DOMINICAL_FESTIVOS", new TipoHoraExtraRec { Codigo = "6", Descripcion = "Hora Extra Nocturno Dominical y Festivos", Porcentaje = 150.00m });
                TiposHER.Add("HORA_RECARGO_NOCTURNO_DOMINICAL_FESTIVOS", new TipoHoraExtraRec { Codigo = "7", Descripcion = "Hora Recargo Nocturno Dominical y Festivos", Porcentaje = 110.00m });
                cargado = true;
            }
        }

        public static TipoHoraExtraRec GetTipoHoraExtraRecargo(string Key)
        {
            try
            {
                CargarDatos();
                return TiposHER[Key];
            }
            catch
            {
                return null;
            }
        }


    }


    //public class nomina_info
    //{
    //    public int AAAA { get; set; }
    //    public decimal SalarioMinimo { get; set; }
    //    public decimal SubsidioTransporte { get; set; }
    //    public int N 
    //}



}



//if (InformacionLaboral.SalarioBasico < (InformacionGeneral.ParametrosLegales.SMLV * 2))
//{
//    if (InformacionLaboral.AuxilioTransporte <= 0)
//    {
//        errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "El empleado tiene derecho a Auxilio de transporte" });
//    }
//}
//else
//{
//    if (InformacionLaboral.AuxilioTransporte > 0)
//    {
//        errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "El empleado no tiene derecho a Auxilio de transporte" });
//    }
//}

//switch (AAAA)
//{
//    case 2021:
//        if (SMLV != 908526)
//            errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El salario minimo para el año {AAAA} está errado {SMLV.ToString("$##,###.##")}" });
//        if (InformacionLaboral.AuxilioTransporte != 106454 && InformacionLaboral.Salario < 908526 * 2)
//            errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El auxilio de transporte para el año {AAAA} está errado {InformacionLaboral.AuxilioTransporte.ToString("$##,###.##")}" });
//        break;
//    case 2022:
//        if (SMLV != 1000000)
//            errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El salario minimo para el año {AAAA} está errado {SMLV.ToString("$##,###.##")}" });
//        if (InformacionLaboral.AuxilioTransporte < 117172 && InformacionLaboral.Salario < 2000000)
//            errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El auxilio de transporte para el año {AAAA} está errado {InformacionLaboral.AuxilioTransporte.ToString("$##,###.##")}" });
//        break;
//    default:
//        if(AAAA < 2021)
//            errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "La fecha de inicio del periodo liquidado está errada" });
//        else
//        {
//            if (SMLV <= 1000000)
//                errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El salario minimo para el año {AAAA} está errado {SMLV.ToString("$##,###.##")}" });
//            if (InformacionLaboral.AuxilioTransporte <= 117172 && InformacionLaboral.Salario < (SMLV * 2))
//                errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = $"El auxilio de transporte para el año {AAAA} está errado {InformacionLaboral.AuxilioTransporte.ToString("$##,###.##")}" });
//            break;
//        }
//        break;
//}

//if(InformacionLaboral.AuxilioTransporte < 0) // && (InformacionLaboral.Salario > (SMLV * 2) )
//    errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "El Auxilio de transporte no puede ser menor que cero ($0.00)" });
//else if (InformacionLaboral.AuxilioTransporte == 0)
//{
//    if (InformacionLaboral.Salario <= (SMLV * 2))
//        errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "El empleado tiene derecho a Auxilio de transporte" });
//}
//else
//{
//    if (InformacionLaboral.Salario > (SMLV * 2))
//        errors.Add(new MtxError { Ambito = enmAmbito.FrontEnd, Codigo = "PREV001", Descripcion = "El empleado no tiene derecho a Auxilio de transporte" });
//}



