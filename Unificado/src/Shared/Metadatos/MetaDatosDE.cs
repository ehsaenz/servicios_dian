using FacturacionColombia_Metadatos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FacturacionColombia_Metadatos
{
    public partial class MetaDatosDE
    {

        public MetaDatosDE() { }
        private TipoDocumentoElectronico _tipoDocumento; 
        public MetaDatosDE(TipoDocumentoElectronico tipo_documento) 
        {
            _tipoDocumento = tipo_documento;
        }


        string IdDocumento { get; set; } = string.Empty;

        public TipoDocumentoElectronico TipoDocumento { get { return _tipoDocumento; } set { _tipoDocumento = value; } }
        
        public string Numero { get; set; }
        public string Nota { get; set; } = string.Empty;

        private DateTime _fechaemision;
        public DateTime FechaEmision 
        { 
            get { return _fechaemision; }
            set { _fechaemision = value; } // DefPeriod(); } 
        }

        public DateTime FechaVencimiento { get; set;}

        private PeriodoFacturacion _periodo = null;
        public PeriodoFacturacion Periodo { get { return _periodo; } set { _periodo = value; } }

        public ExtensionDian ExtensionDian { get; set; } = new ExtensionDian();

        public Adquiriente Adquiriente { get; set; } = new Adquiriente();

        public InfoCaja InformacionCaja { get; set; } = new InfoCaja();


        public Proveedor Proveedor { get; set; } = new Proveedor();

        public Emisor Emisor { get; set; } = new Emisor();

        public DetallePago DetallePago { get; set; }

        public Totales Totales { get; set; }


        List<Linea> _items =  new List<Linea>();
        public List<Linea> Items { get { return _items; } set { _items = value; } }

        public ConceptoNotaAjusteDocumentoEquivalente ConceptoAjuste { get; set; }

        public ReferenciaDocumento DocumentoReferencia { get; set; }    


        #region METODOS

        public void AddDatosBasicos(TipoDocumentoElectronico tipo_documento, string numero, DateTime fecha_emision)
        {
            TipoDocumento = tipo_documento;
            Numero = numero;
            _fechaemision = fecha_emision;
            FechaVencimiento = fecha_emision.Date;
            ExtensionDian = new ExtensionDian();
        }

        public void AddDocumentoReferencia(string uuid, string numero, DateTime fecha)
        {
            DocumentoReferencia = new ReferenciaDocumento
            {
                TipoDocumento = TipoDocumento.Codigo == "94" ? TipoDocumentoElectronico.DOCUMENTO_EQUIVALENTE_POS : TipoDocumento,
                UUID = uuid,
                Numero = numero,
                Fecha = fecha,
                
            };
            switch (DocumentoReferencia.TipoDocumento.Codigo)
            {
                case "20":
                    DocumentoReferencia.AlgoritmoCufe = AlgoritmoSeguridadUUID.CUDE_SHA384;
                    break;
                case "01":
                case "02":
                case "03":
                case "04":
                    DocumentoReferencia.AlgoritmoCufe = AlgoritmoSeguridadUUID.CUFE_SHA384;


                    break;
            }
        }


        public void AddFormaPago(FormaPago forma_pago, MetodoPago metodo_pago, DateTime? fecha_vencimiento = null)
        {
            FechaVencimiento = (fecha_vencimiento.HasValue) ? fecha_vencimiento.Value : _fechaemision;
            DetallePago = new DetallePago
            {
                Forma = forma_pago,
                Metodo = metodo_pago,
                Fecha = FechaVencimiento, 
            };
        }

        public void AddResolucion(long numero_res, DateTime fecha_res, DateTime fecha_ini, DateTime fecha_fin, string prefijp, long desde, long hasta)
        {
            ExtensionDian.RangoNumeracion = new RangoNumeracion
            {
                NumeroResolucion = numero_res,
                FechaResolucion = fecha_res,
                VigenciaDesde = fecha_ini,
                VigenciaHasta = fecha_fin,
                Prefijo = prefijp,
                Desde = desde,
                Hasta = hasta
            };

        }

        public void AddPeriodoFacturacion(DateTime FechaEmision)
        {
            AddPeriodoFacturacion(FechaEmision, FechaEmision, null);
        }

        public void AddPeriodoFacturacion(DateTime FechaINI, DateTime FechaFIN, FormaGeneracionTransmision forma_gt = null)
        {
            //var xx =  FormaGeneracioTransmision.GetFromCodigo("POR_OPERACION");
            _periodo = new PeriodoFacturacion()
            {
                FechaInicio = FechaINI,
                FechaFin = FechaFIN,
                FormaGT = forma_gt ?? FormaGeneracionTransmision.GetFromCodigo("POR_OPERACION")
            };
        }

        public void AddDatosSoftware(string SoftwareProveedorNit, string SoftwareIdentificador, string SoftwarePin)
        {
            ExtensionDian.PaisOrigen = new Pais { Codigo = "CO", Nombre = "Colombia" };
            ExtensionDian.SoftwareProveedorNit = SoftwareProveedorNit;
            ExtensionDian.SoftwareIdentificador = SoftwareIdentificador;
            ExtensionDian.SoftwarePin = SoftwarePin;
        }

        // PASAR AL MIDDLE 
        public void AddEmisor(DatosFiscales datos_fiscales, DatosComerciales datos_comerciales)
        {
            if (_tipoDocumento.Codigo == TipoDocumentoElectronico.DOCUMENTO_SOPORTE.Codigo)
            {
                Proveedor = new Proveedor
                {
                    TipoProcedencia = TipoProcedencia.RESIDENTE,
                    TipoPersona = datos_fiscales.TipoPersona,
                    TipoIdentificacion = datos_fiscales.TipoIdentificacion,
                    Identificacion = datos_fiscales.Identificacion,
                    RazonSocial = datos_fiscales.NombreRazonsocial(),
                    DireccionFisica = datos_fiscales.Direccion,
                    NumeroTelefonico = datos_fiscales.Telefono,
                    CorreoElectronico = datos_fiscales.Email,
                    RegimenFiscal = datos_fiscales.RegimenFiscal,
                    ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ"),
                    Nombre = datos_comerciales.NombreComercial,
                };


                if (datos_fiscales.TipoPersona == TipoPersona.NATURAL)
                {
                    Proveedor.Persona = new Persona
                    {
                        PrimerNombre = datos_fiscales.PrimerNombre,
                        SegundoNombre = datos_fiscales.SegundoNombre,
                        PrimerApellido = datos_fiscales.PrimerApellido,
                        SegundoApellido = datos_fiscales.SegundoApellido,
                        DireccionResidencia = datos_comerciales.Direccion,
                        DocumentoIdentificacion = DocumentoIdentificacion.GetFromTipoFiscal(datos_fiscales.TipoIdentificacion.Codigo),
                        NumeroIdentificacion = datos_comerciales.CodigoCliente ?? datos_fiscales.Identificacion
                    };
                }
            }
            //else
            //{
                Emisor = new Emisor
                {
                    TipoPersona = datos_fiscales.TipoPersona,
                    TipoIdentificacion = datos_fiscales.TipoIdentificacion,
                    Identificacion = datos_fiscales.Identificacion,
                    DV = datos_fiscales.DV,
                    RazonSocial = datos_fiscales.RazonSocial,
                    DireccionFiscal = datos_fiscales.Direccion,
                    DireccionComercial = datos_comerciales.Direccion,
                    NumeroTelefonico = datos_fiscales.Telefono,
                    CorreoElectronico = datos_fiscales.Email,
                    RegimenFiscal = datos_fiscales.RegimenFiscal,
                    ResponsabilidadTributaria = datos_fiscales.ResponsabilidadTributaria,
                    DatosComerciales = datos_comerciales
                    //MatriculaMercantil = datos_comerciales.MatriculaMercantil
                };

                if (Emisor.TipoPersona == TipoPersona.NATURAL)
                {
                    Emisor.Persona = new Persona
                    {
                        PrimerNombre = datos_fiscales.PrimerNombre,
                        SegundoNombre = datos_fiscales.SegundoNombre,
                        PrimerApellido = datos_fiscales.PrimerApellido,
                        SegundoApellido = datos_fiscales.SegundoApellido,
                        DireccionResidencia = datos_comerciales.Direccion
                        //DocumentoIdentificacion = datos_fiscales.TipoIdentificacion,
                        // NumeroIdentificacion
                    };
                    Emisor.RazonSocial = $"{datos_fiscales.PrimerNombre.Trim()} {datos_fiscales.SegundoNombre.Trim()} {datos_fiscales.PrimerApellido.Trim()} {datos_fiscales.SegundoApellido.Trim()}".Replace("  ", " ").Trim();
                }

                // ESTOS PARAMETROS DEBEN PASARSE COMO PARAMETRO
                // CUANDO <<TipoDocumento>> sea diferene de <<DOCUMENTO_SOPORTE>>
                 
                Emisor.RegimenFiscal = datos_fiscales.RegimenFiscal ?? RegimenFiscal.GetFromCodigo("R-99-PN");
                //emisor.ResponsabilidadFiscal = ResponsabilidadFiscal.GetFromCodigo("NO_RESPONSABLE");

                // PENDIENTE REVISAR 
                if (Emisor.ResponsabilidadTributaria == null ) Emisor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");

            //}

        }


        [Obsolete]
        public void AddEmisorPersonaNatural(string identificacion, string nombre, DireccionFisica direccion, string telefono, string email, string matricula_mercantil = "", string regimen_fiscal = "R-99-PN")
        {
            if (_tipoDocumento?.Codigo == TipoDocumentoElectronico.DOCUMENTO_SOPORTE.Codigo) // se compara por código: DOCUMENTO_SOPORTE crea un objeto nuevo en cada acceso
            {
                Proveedor.TipoPersona = TipoPersona.NATURAL;
                Proveedor.TipoProcedencia = TipoProcedencia.RESIDENTE;
                Proveedor.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");
                Proveedor.Identificacion = identificacion;
                Proveedor.Nombre = nombre;
                Proveedor.DireccionFisica = direccion;
                Proveedor.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);  //.GetFromCodigo("R-99-PN");
                //Proveedor.ResponsabilidadFiscal = ResponsabilidadFiscal.GetFromCodigo("NO_RESPONSABLE");
                Proveedor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");
                Proveedor.NumeroTelefonico = telefono;
                Proveedor.CorreoElectronico = email;
            }
            else
            {
                Emisor.TipoPersona = TipoPersona.NATURAL;
                // PENDIENTE TipoIdentificación
                Emisor.Identificacion = identificacion;
                Emisor.RazonSocial = nombre;
                Emisor.DireccionFiscal = direccion;
                Emisor.NumeroTelefonico = telefono;
                Emisor.CorreoElectronico = email;

                // ESTOS PARAMETROS DEBEN PASARSE COMO PARAMETRO
                // CUANDO <<TipoDocumento>> sea diferene de <<DOCUMENTO_SOPORTE>>
                Emisor.RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN");
                //emisor.ResponsabilidadFiscal = ResponsabilidadFiscal.GetFromCodigo("NO_RESPONSABLE");
                Emisor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");

            }

        }

        [Obsolete]
        public void AddEmisorPersonaJuridica(string identificacion, string nombre, DireccionFisica direccion, string telefono, string email, string matricula_mercantil = "", string regimen_fiscal = "R-99-PN")
        {
            if (_tipoDocumento?.Codigo == TipoDocumentoElectronico.DOCUMENTO_SOPORTE.Codigo) // se compara por código: DOCUMENTO_SOPORTE crea un objeto nuevo en cada acceso
            {
                Proveedor.TipoPersona = TipoPersona.JURIDICA;
                Proveedor.TipoProcedencia = TipoProcedencia.RESIDENTE;
                Proveedor.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");
                Proveedor.RazonSocial = nombre;
                Proveedor.Identificacion = identificacion;
                Proveedor.DireccionFisica = direccion;
                Proveedor.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
                //Proveedor.ResponsabilidadFiscal = ResponsabilidadFiscal.GetFromCodigo("NO_RESPONSABLE");
                Proveedor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");
                Proveedor.NumeroTelefonico = telefono;
                Proveedor.CorreoElectronico = email;
            }
            else
            {
                Emisor.TipoPersona = TipoPersona.JURIDICA;
                // PENDIENTE TipoIdentificación
                Emisor.Identificacion = identificacion;
                Emisor.RazonSocial = nombre;
                Emisor.DireccionFiscal = direccion;
                Emisor.NumeroTelefonico = telefono;
                Emisor.CorreoElectronico = email;

                // ESTOS PARAMETROS DEBEN PASARSE COMO PARAMETRO
                // CUANDO <<TipoDocumento>> sea diferene de <<DOCUMENTO_SOPORTE>>
                Emisor.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
                //emisor.ResponsabilidadFiscal = ResponsabilidadFiscal.GetFromCodigo("NO_RESPONSABLE");
                Emisor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");
            }
        }


        public void AddAdquiriente(DatosFiscales datos_fiscales, DatosComerciales datos_comerciales)
        {
            Adquiriente.TipoPersona = datos_fiscales.TipoPersona;
            Adquiriente.TipoIdentificacion = datos_fiscales.TipoIdentificacion;
            Adquiriente.Identificacion = datos_fiscales.Identificacion;
            Adquiriente.RazonSocial = datos_fiscales.NombreRazonsocial();
            Adquiriente.DireccionFiscal = datos_fiscales.Direccion;
            Adquiriente.NumeroTelefonico = datos_fiscales.Telefono;
            Adquiriente.CorreoElectronico = datos_fiscales.Email;
            Adquiriente.RegimenFiscal = datos_fiscales.RegimenFiscal;

            if (datos_fiscales.TipoPersona == TipoPersona.NATURAL)
            {
                Adquiriente.Persona = new Persona
                {
                    PrimerNombre = $"{datos_fiscales.PrimerNombre.Trim()}".Replace("  ", " ").Replace("  ", " "),
                    SegundoNombre = $"{datos_fiscales.SegundoNombre}".Trim().Replace("  ", " "),
                    PrimerApellido = $"{datos_fiscales.PrimerApellido.Trim()}".Replace("  ", " ").Replace("  ", " "),
                    SegundoApellido = $"{datos_fiscales.SegundoApellido}".Trim().Replace("  ", " "),
                    DireccionResidencia = datos_fiscales.Direccion
                };
            }
            Adquiriente.CodigoCliente = datos_comerciales.CodigoCliente;
            Adquiriente.NombreComercial = datos_comerciales.NombreComercial;
            Adquiriente.MatriculaMercantil = datos_comerciales.MatriculaMercantil;
            Adquiriente.DireccionComercial = datos_comerciales.Direccion;
        }





        [Obsolete]
        public void AddAdquirientePersonaNatural(string identificacion, string nombre_razonsocial, DireccionFisica direccion_comercial, string telefono, string email, string matricula_mercantil = "", string regimen_fiscal = "R-99-PN")
        {
            Adquiriente.Identificacion = identificacion;
            Adquiriente.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("13");
            Adquiriente.RazonSocial = nombre_razonsocial;
            //Adquiriente.NombreComercial = nombre_cliente;
            Adquiriente.DireccionComercial = direccion_comercial;
            Adquiriente.TipoPersona = TipoPersona.NATURAL;
            Adquiriente.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
            //adquiriente.ResponsabilidadFiscal = responsabilidad_fiscal;
            Adquiriente.MatriculaMercantil = matricula_mercantil;
            Adquiriente.NumeroTelefonico = telefono;
            Adquiriente.CorreoElectronico = email;
        }


        [Obsolete]
        public void AddAdquirientePersonaJuridica(string identificacion, string razon_social, DireccionFisica direccion, string telefono, string email, string matricula_mercantil = "", string regimen_fiscal = "R-99-PN")
        {
            Adquiriente.TipoPersona = TipoPersona.JURIDICA;
            Adquiriente.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");
            Adquiriente.Identificacion = identificacion;
            Adquiriente.RazonSocial = razon_social;
            Adquiriente.DireccionComercial = direccion;
            Adquiriente.RegimenFiscal = RegimenFiscal.GetFromCodigo(regimen_fiscal);
            //adquiriente.ResponsabilidadFiscal = responsabilidad_fiscal;
            Adquiriente.MatriculaMercantil = matricula_mercantil;
            Adquiriente.NumeroTelefonico = telefono;
            Adquiriente.CorreoElectronico = email;
            // PENDIENTE REVISAR
            Proveedor.ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ");
        }

        public void AddTotales(decimal total_bruto, decimal base_gravada, decimal total_neto, decimal recargos, decimal descuentos, decimal valor_pagar)
        {
            Totales = new Totales
            {
                ValorBruto = total_bruto,
                BaseImponible = base_gravada,
                ValorBrutoConImpuestos = total_neto,
                Descuentos = descuentos,
                Cargos = recargos,
                TotalPagable = valor_pagar
            };
        }

        // PASARA AL MIDDLE
        [Obsolete]
        public void AddItem(string codigo, string descripcion, decimal cantidad, string unit_code, decimal valor_unitario, bool excluido = true, decimal tarifa_iva = 0M)
        {
            AddItem(codigo, descripcion, "", "", cantidad, "EA"     , valor_unitario, excluido, tarifa_iva, 0.00M, "Promocion", 0.00M, "", 0 );
        }

        public void AddItem(string codigo, string descripcion, string marca, string referencia, decimal cantidad, string unit_code, decimal valor_unitario, bool excluido = true, decimal tarifa_iva = 0.00M, decimal porcent_dto = 0.00M, string concepto_dto = "Promocion", decimal porcent_recargo = 0.00M, string concepto_recargo = "", decimal Tarifa_icbp = 0.00M)
        {
            var undmed = (unit_code == "EA") ? UnidadMedida.CADA : new UnidadMedida { Codigo = unit_code };

            if (excluido)
            {
                if (tarifa_iva != 0)  { throw new Exception("Un producto excluido no lleva tarifa de IVA"); }
            }

            // Crea un InvoiceLine
            var lineaX = new Linea
            {
                CodigoProducto = new CodigoProducto { Tipo = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO"), Valor = codigo },
                Descripcion = descripcion,
                Marca = marca,
                Referencia = referencia,
                Cantidad = cantidad.RedondearDian(),
                UnidadMedida = undmed,
                PrecioUnitario = valor_unitario.RedondearDian(),
                PeriodoFacturacionLine = _periodo,
                Excluido = excluido,
                Impuestos = new List<Impuesto>(),
                Descuentos = new List<Descuento>(),
                Cargos = new List<Cargo>()
            };

            // Si tiene descuento 
            if (porcent_dto > 0)
            {
                var dto = new Descuento
                {
                    Base = (cantidad * valor_unitario).RedondearDian(),
                    Porcentaje = (porcent_dto * 100).RedondearDian(),
                    Monto = (cantidad * valor_unitario * porcent_dto).RedondearDian(),
                    Razon = concepto_dto,
                    Tipo = TipoDescuento.NO_CONDICIONADO
                };
                lineaX.Descuentos.Add(dto);
            }
            var total_dtos = lineaX.Descuentos.Sum(dto => dto.Monto);


            // Si tiene recargo 
            if (porcent_recargo > 0)
            {
                var carg = new Cargo
                {
                    Base = lineaX.BaseGeneral,
                    Porcentaje = (porcent_recargo * 100).RedondearDian(),
                    Monto = (lineaX.BaseGeneral * porcent_recargo).RedondearDian(),
                    Razon = concepto_recargo
                };
            }
            var total_cargos = lineaX.Cargos.Sum(crg => crg.Monto);

            lineaX.BaseGeneral = (cantidad * valor_unitario) + total_cargos - total_dtos;


            // Si es un producto gravado
            if (!excluido) 
            {
                lineaX.Impuestos.Add
                    (
                        new Impuesto
                        {
                            Tipo = TipoImpuesto.GetFromCodigo("01"),
                            BaseImponible = (lineaX.BaseGeneral - total_dtos + total_cargos).RedondearDian(),
                            Importe = ((lineaX.BaseGeneral - total_dtos + total_cargos) * tarifa_iva).RedondearDian(),
                            Porcentaje = (tarifa_iva * 100).RedondearDian()
                        }
                    );
            }

            // Si el item corresponde a ICBP
            if(Tarifa_icbp > 0)
            {
                lineaX.Impuestos.Add
                    (
                        new Impuesto
                        {
                            Tipo = TipoImpuesto.GetFromCodigo("22"),
                            PorUnidad = 1.00M,
                            ValorUnidadMedida = Tarifa_icbp.RedondearDian(),
                            UnidadMedida = new UnidadMedida { Codigo = "94" },
                            Importe = (cantidad * Tarifa_icbp).RedondearDian() 
                            //BaseImponible = (lineaX.CostoTotal - total_dtos + total_cargos).RedondearDian(),
                            //Porcentaje = (tarifa_iva * 100).RedondearDian()
                        }
                    );

            }
            _items.Add(lineaX);
        }


        //// PASARA AL MIDDLE
        ///// <summary>
        ///// Adicionar Item especificando tarifa de IVA
        ///// </summary>
        ///// <param name="codigo"></param>
        ///// <param name="descripcion"></param>
        ///// <param name="cantidad"></param>
        ///// <param name="unit_code"></param>
        ///// <param name="valor_unitario"></param>
        ///// <param name="excluido"></param>
        ///// <param name="tarifa">Asegurese de enviar el Porcentaje de IVA en formato decimal por ejemplo para 19% envpie 0.19</param>
        ///// <param name="dto_dir">Asegurese de enviar el Porcentaje de descuento en formato decimal por ejemplo para 5% envpie 0.05</param>
        ///// <exception cref="Exception"></exception>
        //[Obsolete]
        //public void AddItem(string codigo, string descripcion, decimal cantidad, string unit_code, decimal valor_unitario, bool excluido = true, decimal tarifa_iva = 0M)
        //{
        // // AddItem(codigo, descripcion, cantidad, "EA"     , valor_unitario, excluido, tarifa    , dto_dir);
        //    AddItem(codigo, descripcion, cantidad, unit_code, valor_unitario, excluido, tarifa_iva, 0.00M, 0.00M, 0.00M);
        //}


        #endregion

    }



    public class ImpuestoRes
    {
        private List<Impuesto> sbtls;
        public List<Impuesto> Subtotales { get { return sbtls; } set { sbtls = value; } }
        //public string ID { get { return sbtls.Select(x => x.ID).FirstOrDefault(); } set {; } }
        //public string Nombre { get { return sbtls.Select(x => x.Nombre).FirstOrDefault(); } set {; } }
        //public TipoCalculo Calculo { get { return sbtls.Select(x => x.Calculo).FirstOrDefault(); } set {; } }
        //public FE.Enum.TipoOperacion Operacion { get { return sbtls.Select(x => x.Operacion).FirstOrDefault(); } set {; } }
        public decimal TotalBase { get { return (sbtls.Sum(x => Math.Round((x.BaseImponible), 2))).RedondearDian(); } set {; } }
        public decimal TotalImpuesto { get { return (sbtls.Sum(x => Math.Round(x.Importe, 2))).RedondearDian(); } set {; } }
    }


    public static class DecimalExtensions
    {
        public static decimal RedondearDian(this decimal valor)
        {
            CultureInfo culturaActual = CultureInfo.CurrentCulture;

            // Obtiene el separador decimal de la cultura actual
            string separadorDecimal = culturaActual.NumberFormat.NumberDecimalSeparator;

            if (valor == Math.Truncate(valor))
            {
                string numero = $"{Math.Truncate(valor)}{separadorDecimal}00";
                if (decimal.TryParse(numero, out decimal nd))
                {
                    return nd;
                }
            }
            return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        }
    }

    public class InfoCaja
    {
        public InfoCaja() { }
        public InfoCaja(string placa, string ubicacion, string cajero, string tipo_caja  ) 
        {
            this.PlacaCaja = placa;
            this.UbicacionCaja = ubicacion;
            this.NombreCajero = cajero;
            this.TipoCaja = tipo_caja;
        }
        public string PlacaCaja { get; set; }
        public string UbicacionCaja { get; set; }
        public string NombreCajero { get; set; }
        public string TipoCaja { get; set; }
    }



}
