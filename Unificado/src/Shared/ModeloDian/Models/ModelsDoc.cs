using DianModelDoc.Models;
using DianModelDoc.Servicios;
using FE.Enum;
using FE.Models.Gral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static DianModelDoc.UtilsMath;


namespace FE.Models.Doc
{
    //Convert.ToDecimal(Math.Round(VAR, 2).ToString("##0.#0"))



    public class ImpuestoRes
    {
        private List<impuesto> sbtls;
        public List<impuesto> Subtotales { get { return sbtls; } set { sbtls = value; } }
        public string ID { get { return sbtls.Select(x => x.ID).FirstOrDefault(); } set {; } }
        public string Nombre { get { return sbtls.Select(x => x.Nombre).FirstOrDefault(); } set {; } }
        public TipoCalculo Calculo { get { return sbtls.Select(x => x.Calculo).FirstOrDefault(); } set {; } }
        public FE.Enum.TipoOperacion Operacion { get { return sbtls.Select(x => x.Operacion).FirstOrDefault(); } set {; } }
        public decimal TotalBase { get { return RedondearBancario(sbtls.Sum(x => RedondearBancario((x.ValorBase),2)),2); } set {; }}
        public decimal TotalImpuesto { get { return RedondearBancario(sbtls.Sum(x => RedondearBancario(x.ValorImp,2)),2); } set {; } }
        


        //decimal valorImpuestos = Impuestos.Sum(i => DianModelDoc.UtilsMath.RedondearBancario(i.TotalImpuesto, 2));
        //decimal valorRetenciones = Retenciones.Sum(i => DianModelDoc.UtilsMath.RedondearBancario(i.TotalImpuesto, 2));


    }


    public static class ParametrosFis
    {
        public static bool Cargado { get; set; }
        public static decimal BaseReteIva { get; set; }
        public static decimal BaseReteFte { get; set; }
        public static decimal BaseReteIca { get; set; }
        public static decimal PorcentajeReteIva { get; set; }
        public static decimal PorcentajeReteFteDec { get; set; }
        public static decimal PorcentajeReteFteNoDec { get; set; }
        public static decimal PorcentajeReteIca { get; set; }
    }


    public class DocumentoDian
    {
        private string clave_dev_time => SecretosLocales.Obtener("ModeloDian.ClaveDesarrollo");
        private string ClaveDev;
        private List<string> notas;

        public bool ReadyDevTime { 
            get { 
                if (ClaveDev == null) return false; 
                else return clave_dev_time == MtxCrypt.Decrypt(ClaveDev); 
            } 
        } 

        private void inicializar()
        {
            Errores = new List<error>();
            Resolucion = new resolucion();
            Items = new List<item>();
            Impuestos = new List<ImpuestoRes>();
            Retenciones = new List<ImpuestoRes>();
            FormaPago = new forma_pago(this);
            notas = new List<string>();
            DocumentoRef = new documento_ref();
        }


        /// <summary>
        /// Es el constructor de la Clase, solo se utiliza para inicializar variables
        /// </summary>
        public DocumentoDian()
        {
            inicializar();
        }

        public DocumentoDian(string clave_dev)
        {
            inicializar();
            ClaveDev = MtxCrypt.Encrypt(clave_dev);
        }


        #region PROPIEDADES PUBLICAS 

        public enmModo ModoEnvio { get; set; } = enmModo.CONTINGENCIA;


        /// <summary>
        /// Es el tipo de documento que viene del ERP Ej: FV, DC, ND, NC
        /// </summary>
        public string TipoDocErp { get; set; }

        public List<impuesto> ImpuestosList { get; set; } 


        /// <summary>
        /// Es el tipo de documento de acuerdo a la Tabla de la DIAN
        /// Invoice.InvoiceTypeCode.Value
        /// 01 = Factura de Venta
        /// 02 = Factura de Exportacion
        /// 03 = Factura de Contingencia
        /// 91 = Nota Crédito
        /// 92 = Nota Débito
        /// </summary>
        public string TipoDocDian { get; set; }


        public TipoBase TipoDocObj { get; set; }

        /// <summary>
        /// Se usa en las NotasCrédito y NotasDébito, para referenciar el documento origen. 
        /// </summary>
        public documento_ref DocumentoRef { get; set; }


        public orden_ref OrdenRef { get; set; }

        /// <summary>
        /// Es el numero del documento incluido el Prefijo
        /// Corresponde a: Invoice.ID.Value
        /// </summary>
        public string Numero { get; set; } = "";

        public string Folio 
        { 
            get {
                if(Resolucion == null)
                {
                    return Numero;
                }
                if (Numero == "") return "";
                if ((Resolucion.Prefijo??"") == "") return "";
                return   Numero?.Substring(Resolucion.Prefijo.Length ); 
            } 
            set {; } 
        }

        public List<string> Notas { get { return notas; }  }

        public void SetNota(string StrNota)
        {
            if(notas.Count > 0)
            {
                for(int cn = 0; cn < notas.Count; cn++)
                {
                    ClaveValor cv = new ClaveValor(notas[cn]);
                    if (cv != null)
                    {
                        if(!cv.EsClaveValor)
                        {
                            notas[cn] = StrNota;
                            return;
                        }
                    }
                }
            }
            notas.Add(StrNota);
        }


        public string GetNota()
        {
            if (notas.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int cn = 0; cn < notas.Count; cn++)
                {
                    ClaveValor cv = new ClaveValor(notas[cn]);
                    if (cv != null)
                    {
                        if (!cv.EsClaveValor)
                        {
                            sb.AppendLine(notas[cn]);
                        }
                    }
                }
                return sb.ToString();
            }
            return "";
        }


        public void AddNota(string dato)
        {
            notas.Add(dato);
        }


        public string GetVariable(string NombreVariable)
        {
            if (notas.Count > 0)
            {
                foreach (string n in notas)
                {
                    var x = new ClaveValor(n);
                    if (x.EsClaveValor)
                    {
                        if(x.Clave.ToUpper().Equals(NombreVariable.ToUpper()))
                        {
                            return x.Valor;
                        }
                    }
                }
                return "";
            }
            else return "";
        }

        public void SetVariable(string NombreVariable, string ValorVariable)
        {
            if (notas.Count > 0)
            {
                for (int cn = 0; cn <notas.Count; cn++)
                {
                    var x = new ClaveValor(notas[cn]);
                    if (x.EsClaveValor)
                    {
                        if (x.Clave.Trim().ToUpper().Equals(NombreVariable.Trim().ToUpper()))
                        {
                            notas[cn] = $"{NombreVariable} = {ValorVariable}";
                            return;
                        }
                    }
                }
            }
            notas.Add($"{NombreVariable} = {ValorVariable}");
        }

        
        /// <summary>
        /// Fecha y hora de creación del documento, si ese valor no se registró en la base de datos, puede usar la Fecha del documentos + Hora del Sistema 
        /// </summary>
        public DateTime FechaHoraDoc { get; set; }

        /// <summary>
        /// Función cuyo resultado proviene de la propiedad FechaHoraDoc
        /// </summary>
        /// <param name="Formato"></param>
        /// <returns>La FECHA del documento sin la hora</returns>
        public string FechaDoc(string Formato = "yyyy-MM-dd")
        {
            if (Formato == "") Formato = "yyyy-MM-dd";
            return FechaHoraDoc.ToString(Formato);
        }

        /// <summary>
        /// Función cuyo resultado proviene de la propiedad FechaHoraDoc
        /// </summary>
        /// <param name="Formato"></param>
        /// <returns>La HORA del documento sin la Fecha</returns>
        public string HoraDoc(string Formato)
        {
            if (Formato == "") Formato = "HH:mm:ss";
            return FechaHoraDoc.ToString(Formato);
        }


        /// <summary>
        /// Para especificar el medio de pago
        /// </summary>
        private forma_pago _formaPago;
        public forma_pago FormaPago
        {
            get { return _formaPago; }
            set
            {
                _formaPago = value;
                // Revincula el FormaPago con este DocumentoDian (necesario para FechaVencimiento),
                // ya que al deserializar desde JSON (BasicLibraryExt/System.Text.Json) se crea la instancia
                // con el constructor sin parámetros y este enlace no queda establecido.
                _formaPago?.VincularDocumento(this);
            }
        }


        /// <summary>
        /// Es el Valor a pagar del Documento incluido impuestos y descuentos
        /// </summary>
        public decimal ValorDoc { get; set; }
        public decimal SubTotal { get; set; }
        public decimal BaseGravada { get { return Items.Sum(x => x.Excluido ? 0: x.BaseGravada); } set {; } }
        public decimal BaseNoGravada { get { return Items.Sum(x => x.Excluido ? x.BaseExcluida: 0); } set {; } }
        public decimal BaseTotal { get { return Items.Sum(x => x.BaseGral); } set {; } }
        public decimal BaseRetefuente { get; set; }
        public decimal BaseReteIVA { get; set; }
        public decimal BaseReteICA { get; set; }
        public decimal ValorImp { get; set; }

        public decimal IVA { get; set; }
        public decimal ReteIVA { get; set; }
        public decimal ReteFuente { get; set; }
        public decimal ReteICA { get; set; }


        //decimal ValorImpuestos  Impuestos.Sum(i => DianModelDoc.UtilsMath.RedondearBancario(i.TotalImpuesto, 2));
        public decimal TotaRetenciones { get { return Retenciones.Sum(i => RedondearBancario(i.TotalImpuesto, 2)); } set {; } }

        //private List<impuesto> IvaRes;
        //public List<impuesto> IvaPorTarifa { get { return IvaRes; } set {; } }

        public resolucion Resolucion { get; set; }
        public persona Emisor { get; set; }
        public persona Receptor { get; set; }
        public List<item> Items { get; set; }

        //List<impuesto> ImpuestosDet;

        public List<ImpuestoRes> Impuestos { get; set; }
        public List<ImpuestoRes> Retenciones { get; set; }

        /// <summary>
        /// Es para uso interno del ERP 
        /// </summary>
        public List<error> Errores { get; set; }

        #endregion

        #region METODOS PUBLICOS

        public bool ParametrosFiscales(decimal base_rete_iva, decimal base_rete_fte, decimal base_rete_ica, decimal porcentaje_rete_iva, decimal porcentaje_rete_fte_dec, decimal porcentaje_rete_fte_nodec, decimal porcentaje_rete_ica)
        {
            if (ReadyDevTime)
            {
                ParametrosFis.BaseReteIva = base_rete_iva;
                ParametrosFis.BaseReteFte = base_rete_fte;
                ParametrosFis.BaseReteIca = base_rete_ica;

                ParametrosFis.PorcentajeReteIva = porcentaje_rete_iva;
                ParametrosFis.PorcentajeReteFteDec = porcentaje_rete_fte_dec;
                ParametrosFis.PorcentajeReteFteNoDec = porcentaje_rete_fte_nodec;
                ParametrosFis.PorcentajeReteIca = porcentaje_rete_ica;

                ParametrosFis.Cargado = true;

                return true;
            }
            else return false;
        }

        public void Totalizar()
        {
            if (!ReadyDevTime) return;


            this.Retenciones?.Clear() ;
            foreach (item i in Items)
            {
                i.retenciones?.Clear();
            }

            List<impuesto> ImpIva = new List<impuesto>();
            Impuestos = new List<ImpuestoRes>();
            Retenciones = new List<ImpuestoRes>();




            //SetNota ("Nota principal del documento");

            // impts = Todos los impuestos especificados a nivel de Item que no son Excluidos
            List<impuesto> impts = Items.Where(x => !x.Excluido).SelectMany(x => x.impuestos).ToList();
            
            if (impts.Count > 0)
            {
                Impuestos = impts.GroupBy(imp => imp.ID).Select(impr => new ImpuestoRes
                {
                    Subtotales = impr.GroupBy(x => x.Porcentaje).Select(x => new impuesto
                    {
                        ID = x.Select(k => k.ID).FirstOrDefault(),
                        Nombre = x.Select(k => k.Nombre).FirstOrDefault(),
                        Calculo = x.Select(k => k.Calculo).FirstOrDefault(),
                        Operacion = x.Select(k => k.Operacion).FirstOrDefault(),
                        Porcentaje = x.Select(k => k.Porcentaje).FirstOrDefault(),
                        ValorBase = x.Sum(k => k.ValorBase)
                    }).ToList()
                }).ToList();
            }


            decimal subTotal = Items.Sum(x => x.SubTotal);
            decimal baseGravada = Items.Where(x => !x.Excluido).Sum(x => x.BaseGravada);
            decimal baseNoGravada = Items.Where(x => x.Excluido).Sum(x => x.BaseExcluida);
            decimal baseTotal = Items.Sum(x => x.BaseGral);

            //decimal valorImp = impts.Where(x => x.Operacion.Equals(TipoOperacion.SUMA)).Sum(x => x.ValorImp);

            this.SubTotal      = RedondearBancario(subTotal, 2);        //Convert.ToDecimal(Math.Round(subTotal, 2).ToString("##0.#0"));
            this.BaseGravada   = RedondearBancario(baseGravada, 2);     //Convert.ToDecimal(Math.Round(baseGravada, 2).ToString("##0.#0"));
            this.BaseNoGravada = RedondearBancario(baseNoGravada, 2);   //Convert.ToDecimal(Math.Round(baseNoGravada, 2).ToString("##0.#0"));
            this.BaseTotal     = RedondearBancario(baseTotal, 2);       //Convert.ToDecimal(Math.Round(baseTotal, 2).ToString("##0.#0"));



            //decimal valorIVA = Impuestos.Where(x => x.ID.Equals("01")).Sum(i => RedondearBancario(i.TotalImpuesto, 2));

            decimal valorIVA = 0;
            foreach(var imp in Impuestos)
            {
                if(imp.ID == "01")
                {
                    valorIVA += imp.TotalImpuesto;
                }
            }


            if (Emisor != null)
            {
                if (BaseTotal >= ParametrosFis.BaseReteIva) 
                {
                    if (Receptor == null) return;

                    if (Receptor.Resp_ReteIVA)
                    {
                        //if (ParametrosFis.Cargado)
                        //{
                        //    Console.WriteLine(ParametrosFis.BaseReteFte);
                        //}
                        // RETE IVA
                        if (!Emisor.AutoRetenedor && !Emisor.GranContribuyente)
                        {
                            decimal PorcetReteIVA;
                            if (Emisor.Resp_Renta) PorcetReteIVA = ParametrosFis.PorcentajeReteIva;
                            else PorcetReteIVA = ParametrosFis.PorcentajeReteIva;
                            // #########################################################
                            decimal ReteIvaCal = valorIVA * PorcetReteIVA;
                            if (Math.Abs(ReteIvaCal - ReteIVA) > 1)
                            {
                                Errores.Add(new error() { Ambito = "VALIDACIONES", MensajeErr = $"El ReteIVA Calculada <<{ReteIvaCal.ToString("#,###.##")}>> no concuerda con el reportado en el documento <<{ReteIVA.ToString("#,###.##")}>>" });
                            }
                            // #########################################################
                            var RIV = new ImpuestoRes { Subtotales = new List<impuesto>() { new impuesto() { ID = "05", Nombre = "ReteIVA", ValorBase = valorIVA, Porcentaje = PorcetReteIVA, Calculo = TipoCalculo.PORCENTUAL, Operacion = TipoOperacion.RESTA } } };
                            this.Retenciones.Add(RIV);
                            // Agregar ReteIVA a cada item Gravado 
                            foreach (item i in Items)
                            {
                                if(i.BaseGravada > 0) i.retenciones.Add(new impuesto { ID = RIV.ID, Nombre = RIV.Nombre, Operacion = TipoOperacion.RESTA, Calculo = TipoCalculo.PORCENTUAL, Porcentaje = RIV.Subtotales[0].Porcentaje, ValorBase = i.BaseGravada });
                            }
                        }
                    }

                    // RETEFEUNTE 
                    if (BaseTotal >= ParametrosFis.BaseReteFte) 
                    {
                        if (Receptor.Resp_ReteFuente)
                        {
                            if (!Emisor.RegimenSimple && !Emisor.GranContribuyente && !Emisor.AutoRetenedor)
                            {
                                decimal PorcetReteFuente;
                                if (Emisor.Resp_Renta) PorcetReteFuente = ParametrosFis.PorcentajeReteFteDec;
                                else PorcetReteFuente = ParametrosFis.PorcentajeReteFteNoDec;
                                // #########################################################
                                decimal ReteFuenteCal = baseTotal * PorcetReteFuente;
                                if (Math.Abs(ReteFuenteCal - ReteFuente) > 1)
                                {
                                    Errores.Add(new error() { Ambito = "VALIDACIONES", MensajeErr = $"La ReteFuente Calculada <<{ReteFuenteCal.ToString("##0.#0")}>> no concuerda con la reportada en el documento <<{ReteFuente.ToString("##0.#0")}>>" });
                                }
                                // #########################################################
                                var RFT = new ImpuestoRes { Subtotales = new List<impuesto>() { new impuesto { ID = "06", Nombre = "ReteFuente", ValorBase = BaseTotal, Porcentaje = PorcetReteFuente, Calculo = TipoCalculo.PORCENTUAL, Operacion = TipoOperacion.RESTA } } };
                                this.Retenciones.Add(RFT);

                                
                                // Agregar ReteFuente a cada item Gravado 
                                foreach (item i in Items)
                                {
                                    if (i.BaseGravada > 0) i.retenciones.Add(new impuesto { ID = RFT.ID, Nombre = RFT.Nombre, Operacion = TipoOperacion.RESTA, Calculo = TipoCalculo.PORCENTUAL, Porcentaje = RFT.Subtotales[0].Porcentaje, ValorBase = i.BaseGravada });
                                }
                            }
                        }
                    }

                    // RETEICA
                    if (baseTotal >= ParametrosFis.BaseReteIca) 
                    {
                        // #########################################################
                        if (Receptor.Resp_ReteICA)
                        {
                            if (!Emisor.RegimenSimple && !Emisor.GranContribuyenteICA && !Emisor.AutoretenedorICA)
                            {
                                //Emisor.LocacionPrincipal
                                if (Emisor.LocacionComercial == null)
                                {
                                    Errores.Add(new error() { Ambito = "VALIDACIONES", MensajeErr = $"No se ha definido una Locacion Comercial válida pra el Emisor" });
                                }
                                else
                                {
                                    if (Emisor.LocacionComercial.CodigoMunicipio == Receptor.LocacionComercial.CodigoMunicipio)
                                    {
                                        decimal ReteIcaCal = baseTotal * ParametrosFis.PorcentajeReteIca;
                                        if (Math.Abs(ReteIcaCal - ReteICA) > 1)
                                        {
                                            Errores.Add(new error() { Ambito = "VALIDACIONES", MensajeErr = $"El ReteICA Calculado <<{ReteIcaCal.ToString("#,###.##")}>> no concuerda con el reportado en el documento <<{ReteICA.ToString("#,###.##")}>>" });
                                        }

                                        var RFT = new ImpuestoRes { Subtotales = new List<impuesto>() { new impuesto() { ID = "07", Nombre = "ReteICA", ValorBase = this.BaseTotal, Porcentaje = ParametrosFis.PorcentajeReteIca, Calculo = TipoCalculo.PORCENTUAL, Operacion = TipoOperacion.RESTA } } };
                                        this.Retenciones.Add(RFT);

                                        foreach (item i in Items)
                                        {
                                            if (i.BaseGravada > 0) i.retenciones.Add(new impuesto { ID = RFT.ID, Nombre = RFT.Nombre, Operacion = TipoOperacion.RESTA, Calculo = TipoCalculo.PORCENTUAL, Porcentaje = RFT.Subtotales[0].Porcentaje, ValorBase = i.BaseGravada });
                                        }


                                    }
                                }
                            }
                        }
                        // #########################################################

                    }
                }
            }

            //Notas = new List<string>();
            if (Retenciones.Count > 0)
            {
                switch (TipoDocDian)
                {
                    case "91":
                    case "92":
                        foreach (ImpuestoRes ret in Retenciones)
                        {
                            //notas.Add($"{ret.Nombre} = {ret.TotalImpuesto.ToString("##,###.#0")}");
                            //AddNota($"{ret.Nombre} = {ret.TotalImpuesto.ToString("##,###.#0")}");
                            SetVariable(ret.Nombre, ret.TotalImpuesto.ToString("##,###.#0"));
                        }
                        break;
                }
            }
            //Nota = "Nota usando el metodo nota despues de varias notas";
            //AddNota("última nota");

            //decimal valorImpuestos = Impuestos.Where(x => x.Operacion.Equals(TipoOperacion.SUMA)).Sum(i => i.TotalImpuesto);
            //decimal valorRetenciones = Retenciones.Where(x => x.Operacion.Equals(TipoOperacion.RESTA)).Sum(i => i.TotalImpuesto);
            decimal valorImpuestos = Impuestos.Sum(i => DianModelDoc.UtilsMath.RedondearBancario(i.TotalImpuesto,2));
            decimal valorRetenciones = Retenciones.Sum(i => DianModelDoc.UtilsMath.RedondearBancario(i.TotalImpuesto,2));

            //valorDoc = baseTotal + valorImpuestos;

            baseTotal = DianModelDoc.UtilsMath.RedondearBancario(baseTotal, 2);
            valorImpuestos = DianModelDoc.UtilsMath.RedondearBancario(valorImpuestos, 2);

            this.ValorDoc = DianModelDoc.UtilsMath.RedondearBancario((baseTotal + valorImpuestos), 2);  // Convert.ToDecimal(Math.Round((baseTotal + valorImpuestos), 2).ToString("##0.#0"));
            this.ValorImp = DianModelDoc.UtilsMath.RedondearBancario(valorImpuestos, 2);                // Convert.ToDecimal(Math.Round(valorImpuestos, 2).ToString("##0.#0"));

            if (Retenciones.Count > 0)
            {
                switch (TipoDocDian)
                {
                    case "91":
                    case "92":
                        SetVariable("NetoPagar", "$ " + (baseTotal + valorImpuestos - valorRetenciones).ToString("##,###.#0"));
                        break;
                }
            }

        }


        private void ValidarNumeroDoc()
        {
            if (!ReadyDevTime) return;
            //if(TipoDocDian in ("01", "02")
            string PrefijoRes = "";
            switch (TipoDocDian)
            {
                case "01":
                case "02":
                case "03":
                    string StrNum;
                    if (Resolucion == null)
                    {
                        Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado la Resolución de Facturación" });
                        StrNum = this.Numero;
                    }
                    else
                    {
                        PrefijoRes = Resolucion.Prefijo;
                        if (PrefijoRes == null) PrefijoRes = "";
                        StrNum = PrefijoRes == "" ? this.Numero : this.Numero.Replace(PrefijoRes, "");
                    }
                    long Num = 0;
                    //string StrNumero = this.Numero.Replace(PrefijoRes, "");
                    //StrNumero = StrNumero.Replace("-", "");

                    StrNum = StrNum.Replace("-", "");
                    bool result = long.TryParse(StrNum, out Num);

                    if (!result) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El numero del documento << " + Numero + " >> no es válido" });
                    else if (Num == 0) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El numero del documento << " + Numero + " >> no puede ser cero (0)" });
                    else if (Num < Resolucion.Desde || Num > Resolucion.Hasta) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El numero del documento << " + Numero + " >> está fuera del rango autorizado" });

                    break;
            }
            if (PrefijoRes != "")
            {
                if (this.Numero.Contains(PrefijoRes))
                {
                    if (this.Numero.Substring(0, PrefijoRes.Length) != PrefijoRes)
                        Errores.Add(new error { Ambito = "Validaciones", MensajeErr = $"Hay una inconsistencia entre el Numero del Documento <{Numero}> y el Prefijo de la resolución <{PrefijoRes}>" });
                }
            }
        }

        public bool Validate()
        {
            if (!ReadyDevTime) return false;
            string codigo;
            TipoBase tb;

            //VARIABLE PARA DEPARTAMENTO
            string CodDep;

            // VALIDAR NUMERO DE DOCUMENTO
            ValidarNumeroDoc();

            // VALIDAR EL TIPO DE DOCUMENTO
            tb = Catalogos.TiposDoc.Find(x => x.Codigo.Equals(TipoDocDian));
            if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo del Tipo de Documento << " + TipoDocDian + ">> no existe" });
            else TipoDocObj = tb;


            // ################################################
            // VALIDACIONES DATOS DEL EMISOR
            // VALIDAR EL TIPO_ID - Emisor
            // ################################################
            if (Emisor == null)
            {
                Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado un Emisor válido" });
            }
            else
            {
                if (Emisor == null)
                {
                    Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado un Emisor válido" });
                }
                else if (Emisor.NIT == null)
                {
                    Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado un Emisor válido" });
                }
                else
                {
                    Emisor.Validacion(Errores);
                    //TipoId
                    tb = Catalogos.TiposId.Find(x => x.Codigo.Equals(Emisor.TipoIdDian));
                    if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El campo TipoIdDian << " + Emisor.TipoIdDian + ">> del emisor, no existe" });
                    //TipoPersona
                    switch (Emisor.TipoPersona.ToUpper())
                    {
                        case "NATURAL":
                        case "JURIDICA":
                            break;
                        default:
                            Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El tipo de persona " + Emisor.TipoPersona + " para el Emisor es incorrecto" });
                            break;
                    }

                    // VALIDAR LOCACIONES - Emisor
                    if(Emisor.LocacionPrincipal == null)
                    {
                        Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha definido Locación principal del Emisor" });
                    }
                    else
                    {
                        Emisor.LocacionPrincipal.CodigoPais = "CO";
                        Emisor.LocacionPrincipal.NombrePais = "Colombia";

                        // VALIDAR DATOS LOCACION PRINCIPAL EMISOR
                        // Municipio 
                        codigo = Emisor.LocacionPrincipal.CodigoMunicipio;
                        tb = Catalogos.Municipios.Find(x => x.Codigo.Equals(codigo));
                        if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de municipio del Emisor << " + codigo + " >> en Locación Principal no existe " });
                        else Emisor.LocacionPrincipal.NombreMunicipio = tb.Nombre;
                        // Departamento 
                        CodDep = codigo.Substring(0, 2);
                        Emisor.LocacionPrincipal.CodigoDepartamento = CodDep;
                        tb = Catalogos.Departamentos.Find(x => x.Codigo.Equals(CodDep));
                        if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de departamento del Emisor << " + CodDep + " >> en Locación Principal no existe" });
                        else Emisor.LocacionPrincipal.NombreDepartamento = tb.Nombre;
                    }


                    if (Emisor.LocacionComercial == null)
                    {
                        if(Emisor.LocacionPrincipal != null)
                        {
                            Emisor.LocacionComercial = Emisor.LocacionPrincipal;
                        }
                    }
                    else
                    {
                        Emisor.LocacionComercial.CodigoPais = "CO";
                        Emisor.LocacionComercial.NombrePais = "Colombia";
                    }

                    // Municipio 
                    if (Emisor.LocacionComercial != null)
                    {
                        codigo = Emisor.LocacionComercial.CodigoMunicipio;
                        tb = Catalogos.Municipios.Find(x => x.Codigo.Equals(codigo));
                        if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de municipio del Emisor << " + codigo + " >> en Locación Comercial no existe" });
                        else Emisor.LocacionComercial.NombreMunicipio = tb.Nombre;
                        // Departamento 
                        CodDep = codigo.Substring(0, 2);
                        Emisor.LocacionComercial.CodigoDepartamento = CodDep;
                        tb = Catalogos.Departamentos.Find(x => x.Codigo.Equals(CodDep));
                        if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de departamento del Emisor << " + CodDep + " >> en Locación Comercial no existe" });
                        else Emisor.LocacionComercial.NombreDepartamento = tb.Nombre;
                    }
                }
            }

            // ################################################
            // VALIDACIONES DATOS DEL RECEPTOR 
            // ################################################
            Receptor.Validacion(Errores);
            // Tipo_Id - Receptor
            tb = Catalogos.TiposId.Find(x => x.Codigo.Equals(Receptor.TipoIdDian));
            if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El campo TipoIdDian << " + Receptor.TipoIdDian + ">> del Receptor, no existe" });

            //TipoPersona
            switch (Receptor.TipoPersona.ToUpper())
            {
                case "NATURAL":
                case "JURIDICA":
                    break;
                default:
                    Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El tipo de persona " + Receptor.TipoPersona + " para el Receptor es incorrecto" });
                    break;
            }

            // CodigoPais - Receptor LP 
            codigo = Receptor.LocacionPrincipal.CodigoPais.ToUpper().Trim();
            if (codigo == "CO" || codigo == "")
            {
                Receptor.LocacionPrincipal.CodigoPais = "CO";
                Receptor.LocacionPrincipal.NombrePais = "Colombia";

                // VALIDAR CODIGO DE MUNICIPIO - Receptor LP 
                codigo = Receptor.LocacionPrincipal.CodigoMunicipio;
                tb = Catalogos.Municipios.Find(x => x.Codigo.Equals(codigo));
                if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de municipio del Receptor << " + codigo + " >> en Locación Principal no existe" });
                else Receptor.LocacionPrincipal.NombreMunicipio = tb.Nombre;

                // VALIDAR CODIGO DE DEPARTAMENTO - Receptor LP
                CodDep = codigo.Substring(0, 2);
                Receptor.LocacionPrincipal.CodigoDepartamento = CodDep;
                tb = Catalogos.Departamentos.Find(x => x.Codigo.Equals(CodDep));
                if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de departamento del Receptor << " + CodDep + " >> en Locación Principal no existe" });
                else Receptor.LocacionPrincipal.NombreDepartamento = tb.Nombre;
            }
            else
            {
                tb = Catalogos.Paises.Find(x => x.Codigo.Equals(codigo));
                if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de País del Receptor << " + codigo + " >> en Locación Principal no existe" });
                else Receptor.LocacionPrincipal.NombrePais = tb.Nombre;

                Receptor.LocacionPrincipal.CodigoMunicipio = "";
                //Receptor.LocacionPrincipal.NombreMunicipio = "";
                Receptor.LocacionPrincipal.CodigoDepartamento = "";
                //Receptor.LocacionPrincipal.NombreDepartamento = "";
            }

            if (Receptor.LocacionComercial == null) Receptor.LocacionComercial = Receptor.LocacionPrincipal;
            else
            {
                // VALIDAR CODIGO DE PAIS - Receptor LC 
                codigo = Receptor.LocacionComercial.CodigoPais.ToUpper().Trim();
                if (codigo == "CO" || codigo == "")
                {
                    Receptor.LocacionComercial.CodigoPais = "CO";
                    Receptor.LocacionComercial.NombrePais = "Colombia";

                    // VALIDAR CODIGO DE MUNICIPIO - Receptor LC 
                    codigo = Receptor.LocacionComercial.CodigoMunicipio;
                    tb = Catalogos.Municipios.Find(x => x.Codigo.Equals(codigo));
                    if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de municipio del Receptor << " + codigo + " >> en Locación Comercial no existe" });
                    else Receptor.LocacionComercial.NombreMunicipio = tb.Nombre;

                    // VALIDAR CODIGO DE DEPARTAMENTO - Receptor LC
                    CodDep = codigo.Substring(0, 2);
                    Receptor.LocacionComercial.CodigoDepartamento = CodDep;
                    tb = Catalogos.Departamentos.Find(x => x.Codigo.Equals(CodDep));
                    if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de departamento del Receptor << " + CodDep + " >> en Locación Comercial no existe" });
                    else Receptor.LocacionComercial.NombreDepartamento = tb.Nombre;
                }
                else
                {
                    tb = Catalogos.Paises.Find(x => x.Codigo.Equals(codigo));
                    if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de País del Receptor << " + codigo + " >> en Locación Principal no existe" });
                    else Receptor.LocacionComercial.NombrePais = tb.Nombre;

                    Receptor.LocacionComercial.CodigoMunicipio = "";
                    //Receptor.LocacionComercial.NombreMunicipio = "";
                    Receptor.LocacionComercial.CodigoDepartamento = "";
                    //Receptor.LocacionComercial.NombreDepartamento = "";
                }
            }


            // VALIDAR IMPUESTOS 
            foreach (ImpuestoRes imp in Impuestos)
            {
                codigo = imp.ID;
                tb = Catalogos.TiposImp.Find(x => x.Codigo.Equals(codigo));
                if (tb == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "El codigo de impuesto << " + codigo + " >> no existe " });
            }

            if (Emisor == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado el Emisor" });
            if (Receptor == null) Errores.Add(new error { Ambito = "Validaciones", MensajeErr = "No se ha especificado el Receptor" });

            //#################################################
            if (Errores.Count > 0) return false;
            else return true;
            //#################################################

        }
        #endregion

        public class forma_pago
        {
            private DocumentoDian doc;

            /// <summary>
            /// Constructor sin parámetros requerido para deserialización (BasicLibraryExt / System.Text.Json).
            /// El vínculo con el <see cref="DocumentoDian"/> padre se restablece mediante <see cref="VincularDocumento"/>,
            /// invocado automáticamente por el setter de <see cref="DocumentoDian.FormaPago"/>.
            /// </summary>
            public forma_pago() { }

            public forma_pago(DocumentoDian _doc)
            {
                ID = MetodoPagoEnum.Contado;
                CodigoMedioPago = "10";
                IdenficadorPago = "";
                doc = _doc;
            }

            /// <summary>
            /// Restablece la referencia al <see cref="DocumentoDian"/> padre, necesaria para calcular <see cref="FechaVencimiento"/>.
            /// Se invoca automáticamente al asignar esta instancia a <see cref="DocumentoDian.FormaPago"/>.
            /// </summary>
            internal void VincularDocumento(DocumentoDian _doc)
            {
                doc = _doc;
            }

            public MetodoPagoEnum ID { get; set; }
            public string CodigoMedioPago { get; set; }
            public string IdenficadorPago { get; set; }
            public int Plazo { get; set; }

            private DateTime FechaVenc;
            public DateTime FechaVencimiento
            {
                get
                {
                    //this.Parent    
                    return doc.FechaHoraDoc.AddDays(Plazo);
                }
                set { FechaVenc = value; }
            }
        }

    }

    public class orden_ref
    {
        public string Numero { get; set; }
        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; }
    }

    public class documento_ref
    {
        public string Numero { get; set; }
        public string CUFE { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Código DIAN del concepto de la nota. Crédito: 1 devolución parcial, 2 anulación, 3 rebaja o descuento,
        /// 4 ajuste de precio, 5 descuento por pronto pago, 6 descuento por volumen. Débito: 1 intereses, 2 gastos por cobrar,
        /// 3 cambio de valor, 4 otros. Opcional: si falta, la nota crédito se emite como devolución parcial y la débito como otros.
        /// </summary>
        public string Concepto { get; set; }
    }


    public class resolucion
    {
        /// <summary>
        /// Invoice.UBLExtensions[0].ExtensionContent.DianExtensions.InvoiceControl.InvoiceAuthorization.Value 
        /// </summary>

        public resolucion() {}
        public resolucion(string numero, string prefijo)
        {
            NumeroResolucion = numero;
            Prefijo = prefijo;
        }

        public bool ValidarDian { get { if (ClaveTecnica == "") return true; else return false; } set {;} }
        public string NumeroResolucion { get; set; }
        public DateTime FechaExpedicion { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Prefijo { get; set; }
        public long Desde { get; set; }
        public long Hasta { get; set; }
        public string ClaveTecnica { get; set; }
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
            //Responsabilidades = "O-23";
            responsabilidades_fiscales = new List<responsabilidad>();
        }

        private string AdditionalAccountID;
        private string tipoIdDian;
        private string tipoPersona;
        //private List<string> respons;

        /// <summary>
        /// Emisor/Receptor Emisor es el Facturador Electronico y Recerptor el Cliente 
        /// </summary>
        public string Rol { get; set; }
        public string TipoPersona
        {
            get { return tipoPersona; }
            set
            {
                tipoPersona = value;
                if (value == "NATURAL") AdditionalAccountID = "2";
                else if (value == "JURIDICA") AdditionalAccountID = "1";
            }
        }


        public void Validacion(List<error> errores)
        {
            if(tipoPersona == null)
            {
                errores.Add(new error {  Ambito = "WEB SERVICE", MensajeErr = "La variable TipoPersona tiene un valor null" });
                return;
            }
            switch (tipoPersona.ToUpper())
            {
                case "NATURAL":
                    if (PrimerNombre == "" || PrimerApellido == "")
                        errores.Add(new error { Ambito = "Validacion - tercero", MensajeErr = "El tercero es persona NATURAL pero no especificó Nombre o Apellido" });
                    break;
                case "JURIDICA":
                    if (RazonSocial == "")
                        errores.Add(new error { Ambito = "Validacion - tercero", MensajeErr = "El tercero es persona JURIDICA pero no especificó Razón Social" });
                    break;
                default:
                    errores.Add(new error { Ambito = "Validacion - Tercero", MensajeErr = $"El tipo de persona <{tipoPersona}> no es válido" });
                    break;
            }

        }
        
        //private int AdditionalAccountID;
        /// <summary>
        /// Persona Jurpidica = "1", Persona Natural = "2"
        /// </summary>
        public string IdTipoPersona { get { return AdditionalAccountID;} set { AdditionalAccountID = value; } }

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
        public string NombreComercial { get; set; }
        public LocacionFisica LocacionComercial { get; set; }
        public string NombreContacto { get; set; }
        public string TelefonoContacto { get; set; }
        public string EmailContacto { get; set; }
        public string CargoContacto { get; set; }




        public List<responsabilidad> responsabilidades_fiscales { get; set; }

        //public List<string> ResponsabilidadesList { get { return respons; }}

        public string Responsabilidades
        {
            get
            {
                if(responsabilidades_fiscales == null)
                {
                    return "R-99-PN";
                }
                else
                {
                    string StrRespons = "";
                    string sep = "";
                    foreach (responsabilidad r in responsabilidades_fiscales)
                    {
                        if(r.CodigoFE != "")
                        {
                            StrRespons += sep + r.CodigoFE;
                            sep = ";";
                        }
                    }
                    if (StrRespons == "") return "R-99-PN";
                    else return StrRespons;
                }
            }
            set {; }
            //set
            //{
            //    respons = new List<string>();
            //    if (value != "")
            //    {
            //        char[] sep = { ';', ',' };
            //        string[] rspns = value.Split(sep); 
            //        foreach(string s in rspns)
            //        {
            //            switch (s)
            //            {
            //                case "O-13":
            //                    respons.Add(s);
            //                    //GranCotribuyente = true;
            //                    break;
            //                case "O-15":
            //                    respons.Add(s);
            //                    //AutoRetenedor =  true;
            //                    break;
            //                case "O-23":
            //                    respons.Add(s);
            //                    //Resp_ReteIVA = true;
            //                    break;
            //                case "O-47":
            //                    respons.Add(s);
            //                    //RegimenSimple = true;
            //                    break;
            //            }
            //            //respons.Add(s);
            //        }

            //        //Resp_Renta = respons.Exists(x => x.Equals("O-05"));
            //        //Resp_ReteFuente = respons.Exists(x => x.Equals("O-07"));

            //        //GranCotribuyente = respons.Exists(x => x.Equals("O-13"));
            //        //AutoRetenedor = respons.Exists(x => x.Equals("O-15"));
            //        //Resp_ReteIVA = respons.Exists(x => x.Equals("O-23"));
            //        //RegimenSimple = respons.Exists(x => x.Equals("O-47"));

            //    }
            //    if (respons.Count == 0)
            //    {
            //        respons.Add("R-99-PN");
            //    }
            //}
        }

        public bool GranContribuyente
        {
            get {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("13"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        }

        public bool AutoRetenedor
        { 
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("15"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        } 

        public bool Resp_Renta
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("05"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        }

        public bool Resp_ReteFuente
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("07"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        }

        public bool Resp_ReteIVA
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("09"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        }


        public bool Resp_ReteICA
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("D07"));
                if (r == null) return false;
                else return true;
            }
            set {; }
        }


        public bool GranContribuyenteICA
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("D13"));
                if (r == null) return false;
                else return true;
            }
            set {; }
        }

        public bool AutoretenedorICA
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("D15"));
                if (r == null) return false;
                else return true;
            }
            set {; }
        }


        public bool RegimenSimple
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("47"));
                if (r == null) return false;
                else return true;
            }
            private set {; }
        }

        public bool Resp_IVA
        {
            get
            {
                var r = responsabilidades_fiscales.Find(x => x.Codigo.Equals("48"));
                if (r == null) return false;
                else return true;
            }
            set {; }
        }






        public string RegimenIVA
        {
            get
            {
                if (this.Resp_IVA) return "48";
                else return "49";
            }
            set
            {
                if (value == "48") { this.Resp_IVA = true; }
                else { this.Resp_IVA = false; }
            }
        }



    }

    public class responsabilidad
    {
        public string Codigo { get; set; }
        public string CodigoFE { get; set; }
        public string Descripcion { get; set; }
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

    public class item : ICloneable
    {
        private tipo_codigo tipcod = null;
        private bool excluido;
        private decimal tarifa_iva = 0;
        //private decimal valor_unit = 0;
        private decimal dto_dir = 0;

        private List<impuesto> impts;
        private List<impuesto> rtcns;

        private void init()
        {
            impts = new List<impuesto>();
            rtcns = new List<impuesto>();
        }

        public item()
        {
            init();
            excluido = false;
        }

        public item(bool _excluido)
        {
            init();
            excluido = _excluido;
        }

        public Int32 ID { get; set; }
        
        /// <summary>
        /// Se refiere al código del producto, en la base de datos del Facturador Electrónico
        /// </summary>
        public string Codigo { get; set; }
        
        /// <summary>
        /// "001" = UNSPSC (Colombia compra eficiente)
        /// "010" = GTIN (Números Globales de Identificación de Productos – GTIN)
        /// "020" = SubPartidas Arancelarias 
        /// "999" = Estándar de adopción del contribuyente
        /// </summary>
        public string TipoCodigo
        {
            get
            {
                if(tipcod == null) { return ""; }
                else { return tipcod.SchemeID; }
            }
            set
            {
                if(value != "")
                {
                    tipcod = Catalogos.TiposCodigoProd.Find(x => x.SchemeID.Equals(value));
                    TipoCodigo = tipcod.SchemeID;
                }
            }
        }
        
        /// <summary>
        /// No se debe asignar, depende de TipoCodigo y se refiere al tipo de Codificación del Facturador Electrónico 
        /// </summary>
        public string SchemeName { get { if (tipcod == null) { return ""; } else { return tipcod.SchemeName; } } set {; } }
        
        /// <summary>
        /// No se debe asignar, depende de TipoCodigo
        /// </summary>
        public string SchemeAgencyID { get { if (tipcod == null) { return ""; } else { return tipcod.SchemeAgencyID; } } set {; } }
        
        /// <summary>
        /// Es de solo lectura y depende del campo TipoCodigo
        /// </summary>
        public string UsoTipCod { get { if (tipcod == null) { return ""; } else { return tipcod.Uso; } } set {; } }
        
        /// <summary>
        /// Identificación del producto dada por el fabricante
        /// </summary>
        public string Referencia { get; set; }
        
        /// <summary>
        /// Descrioción del producto o servicio
        /// </summary>
        public string Detalle { get; set; }
        
        /// <summary>
        /// Cantidad de unidades vendidas
        /// </summary>
        public decimal Cant { get; set; }
        
        /// <summary>
        /// Es el valor unitario antes de IVA y antes de descuentos
        /// </summary>
        public decimal ValorUnit { get; set; }
        
        /// <summary>
        /// Clasificación del producto o servicio referente al impuesto a las ventas (IVA)
        /// true = Excluido
        /// false = Gravado
        /// </summary>
        public bool Excluido
        {
            get { return excluido; }
            set { excluido = value; }
        }

        public impuesto AddImpuesto(string Codigo, Decimal Tarifa)
        {
            impuesto impt;
            TipoImpuesto imp = Catalogos.TiposImp.Find(x => x.Codigo.Equals(Codigo));
            if (imp == null)
            {
                Console.WriteLine("Impuesto no definido");
                impt = null;
            }
            else
            {
                impt = new impuesto() { ID = Codigo, Nombre = imp.Nombre, ValorBase = BaseGravada, Porcentaje = Tarifa, Calculo = imp.tipoCalculo, Operacion = imp.tipoOperacion};
                impts.Add(impt);
            }

            return impt;
        }

        /// <summary>
        /// ADVERTENCIA: al asignar esta lista directamente (p.ej. desde deserialización JSON) se
        /// omite la validación contra el catálogo DIAN que aplica <see cref="AddImpuesto"/>. Úsese
        /// solo cuando los valores ya vengan validados/calculados por el emisor de los datos.
        /// </summary>
        public List<impuesto> impuestos { get { return impts; } set { impts = value ?? new List<impuesto>(); } }
        /// <summary>
        /// ADVERTENCIA: al asignar esta lista directamente (p.ej. desde deserialización JSON) se
        /// omite la validación contra el catálogo DIAN que aplica <see cref="AddImpuesto"/>. Úsese
        /// solo cuando los valores ya vengan validados/calculados por el emisor de los datos.
        /// </summary>
        public List<impuesto> retenciones { get { return rtcns; } set { rtcns = value ?? new List<impuesto>(); } }

        /// <summary>
        /// Porcentaje de IVA aplicado al producto
        /// </summary>
        public decimal TarifaIVA { get { if (excluido) { return 0; } else { return tarifa_iva; } } set { tarifa_iva = value; } }
        
        /// <summary>
        /// Porcentaje de decuento comercial (Afecta impuestos)
        /// </summary>
        public decimal DtoDir { get { return dto_dir; } set { dto_dir = value; } }

        public decimal SubTotal
        { 
            get { return DianModelDoc.UtilsMath.RedondearBancario(Cant * ValorUnit, 2); } set {; } }

        public decimal BaseGral
        {
            get {
                //Console.WriteLine(DianModelDoc.UtilsMath.RedondearBancario((Cant * ValorUnit * (1 - DtoDir)), 2) );
                return DianModelDoc.UtilsMath.RedondearBancario((Cant * ValorUnit * (1 - DtoDir)), 2);  
            }
            set {; }
        }

        public decimal BaseGravada
        {
            get 
            {
                if (excluido) { return 0; }
                else { return BaseGral; }
            }
            set {; }
        }

        public decimal BaseExcluida
        {
            get
            {
                if (excluido) { return BaseGral; }
                else { return 0; }
            }
            set {; }
        }

        //{ get { return DianModelDoc.UtilsMath.RedondearBancario((BaseGral * TarifaIVA), 2);  } set {; } }
        public decimal ValorIVA // DEBE SER LA BASEGRAVADA EN LUGAR DE LA BASE GENERAL
        { 
            get 
            { 
                return DianModelDoc.UtilsMath.RedondearBancario((BaseGral * TarifaIVA), 2); 
            } 
            set {; } 
        }

        public decimal ValorTotal
        { get { return DianModelDoc.UtilsMath.RedondearBancario((BaseGral * (1 + TarifaIVA)), 2); } set {; } }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    public class impuesto
    {
        public impuesto()
        {
            Calculo = TipoCalculo.PORCENTUAL;
        }


        private int Factor = 1; 
        public string ID { get; set; }
        public string Nombre { get; set; }
        public decimal Porcentaje { get; set; }
        private decimal valorBase = 0;
        public decimal ValorBase { get { return valorBase;  } set { valorBase = RedondearBancario(value, 2); } }
        public TipoOperacion Operacion { get ; set; }
        public TipoCalculo Calculo { get; set; }
        private decimal valorImp = 0;
        public decimal ValorImp
        {
            get
            {
                if (Operacion == TipoOperacion.SUMA) Factor = 1;
                else Factor = 1;
                if ((Calculo == TipoCalculo.PORCENTUAL) || (Calculo == TipoCalculo.MILESIMAL))
                {
                    valorImp = RedondearBancario(RedondearBancario(valorBase * Factor, 2) * Porcentaje, 2);
                    return valorImp;
                }
                else
                    return valorImp;  //Convert.ToDecimal(Math.Round(, 2).ToString("##0.#0"));

            }
            set 
            {
                if (Operacion == TipoOperacion.SUMA) Factor = 1;
                else Factor = 1;

                valorImp = value;
                
                if ((Calculo == TipoCalculo.PORCENTUAL) || (Calculo == TipoCalculo.MILESIMAL))
                    valorImp = RedondearBancario(RedondearBancario(valorBase * Factor, 2) * Porcentaje, 2);
                else
                    valorImp = RedondearBancario(RedondearBancario(valorBase * Factor, 2), 2);  //Convert.ToDecimal(Math.Round(, 2).ToString("##0.#0"));


            }
        }
    }

    public class DocElecBas
    {
        public string FileID { get; set; }
        public string FullPathXml { get; set; }
        public string FullPathPdf { get; set; }
        public string FullPathZip { get; set; } // Obsoleto
        public string FullPathAR { get; set; }
        public string FullPathAtt { get; set; }
        public string FullPathAttZip { get; set; }
        public string TipoDocID { get; set; }
        public string TipoDocAPI { get; set; }
        public string NumeroDoc { get; set; }
        public string UUID { get; set; }
        public string ZipKey { get; set; }
        public string XmlString { get; set; }
        public byte[] XmlBytes { get; set; }
        public string PdfStringB64 { get; set; }
        public DocumentoDian documento { get; set; }
    }


}

