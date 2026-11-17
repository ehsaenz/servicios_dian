using FE.Models.Gral;
using BasicLibraryExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace FE.Models.Service
{
    public class Login
    {
        public string id { get; set; }
        public string nombre { get; set; }
        public string email { get; set; }
        public string token { get; set; }
        public string rol { get; set; }
        public Configuracion Configuracion { get; set; }
    }

    public class Configuracion
    {
        public string _id { get; set; }
        public string logo { get; set; }
        public string nombreEmpresa { get; set; }
        public string nombreAdmin { get; set; }
        public string correoAdmin { get; set; }
        public string asuntoEmailCliente { get; set; }
        public string asuntoEmailAdmin { get; set; }
        public string textoEmailCliente { get; set; }
        public string textoEmailAdmin { get; set; }
    }


    //public class Documento
    //{
    //    public String _id { get; set; }
    //    public Catalogo tipo { get; set; }
    //    public String idUsuario { get; set; }
    //    public Usuario usuario { get; set; }
    //    public String xml { get; set; }
    //    public String numero { get; set; }
    //}


    public class Catalogo
    {
        public String _id { get; set; }
        public String nombre { get; set; }
        public String tipo { get; set; }
        public String color { get; set; }
    }


    public class Usuario
    {
        public String _id { get; set; }
        public String usuario { get; set; } 
        public String password { get; set; }
        public String nombre { get; set; }
        public String email { get; set; }
        public String[] emails { get; set; }
        public String celular { get; set; }
        public String rol { get; set; }
    }


    public class RespStatusDoc
    {
        public virtual List<string> Errores { get; set; }
        public virtual List<string> Warnings { get; set; }
        public virtual List<string> Trasabilidad { get; set; }

        //private bool EsValido = false;
        //public bool Enviado { get { return EsValido; } set { EsValido = value; } }
        public bool IsValid { get ;  set ;  }

        public string ZipKey { get; set; }
        public string UUID { get; set; }
        public string StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string StatusDescription { get; set; }
        public string NombreXML { get; set; }
        public string Url { get; set; }
        public FE.Enum.Ambito OrigenError { get; set; }

        public DateTime? FechaEmision { get; set; }
        public DateTime FechaValidacion { get; set; }

        public RespStatusDoc()
        {
            OrigenError = Enum.Ambito.NONE;
            Errores = new List<string>();
            Warnings = new List<string>();
            Trasabilidad = new List<string>();
        }

        public RespStatusDoc(Exception e)
        {
            IsValid = false;
            ZipKey = "";
            StatusCode = "";
            OrigenError = FE.Enum.Ambito.WEBSERVICE;
            StatusMessage = "Error interno del WebService";
            StatusDescription = e.StackTrace; // Esto puede traer un string muy largo
            Errores = new List<string>();
            Errores.Add($"Error {e.HResult.ToString()}:  {e.Message}");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("EsValido: " + IsValid);
            sb.AppendLine("TrackID: " + ZipKey);
            sb.AppendLine("XmlName: " + NombreXML);
            sb.AppendLine("URL: " + Url);
            sb.AppendLine("StatusCode: " + StatusCode);
            sb.AppendLine("StatusMessage: " + StatusMessage);
            sb.AppendLine("StatusDescription: " + StatusDescription);
            if (Errores.Count > 0)
            {
                if (!IsValid) { sb.AppendLine("Origen del error: " + OrigenError.ToString()); }
                sb.AppendLine("Errores: ");
                foreach (string s in Errores)
                {
                    sb.AppendLine("> " + s);
                }
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("Advertencias: ");
                foreach (string s in Warnings)
                {
                    sb.AppendLine($">>: {s}");
                }
            }
            if (Trasabilidad.Count > 0)
            {
                sb.AppendLine("#######################");
                sb.AppendLine("# *** Trasabilidad ***");
                sb.AppendLine("#######################");
                foreach (string s in Trasabilidad)
                {
                    sb.AppendLine($">>: {s}");
                }
            }

            return sb.ToString();
        }

    }

    public class RespWebService
    {
        private List<error> errs; 

        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public string ObjetoJson { get; set; }
        public Type ObjetoTipo { get; set; }
        public List<error> Errores { get { return errs; } }
        public RespWebService() { errs = new List<error>(); }

   }



    public class acusesdocumento
    {
        public string TipoDocumento { get; set; }
        public string NumeroDoc { get; set; }
        public DateTime FechaDoc { get; set; }
        public List<acuse> Acuses { get; set; } = new List<acuse>();
    }

    public class acuse
    {
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
    }



    //public class RespEnvioDoc: RespStatusDoc
    //{
    //    //public RespEnvioDoc()
    //    //{
    //    //    Errores = new List<string>();
    //    //    Warnings = new List<string>();
    //    //}

    //    //public RespEnvioDoc(Exception e)
    //    //{
    //    //    Enviado = false;
    //    //    ZipKey = "";
    //    //    StatusCode = "";
    //    //    OrigenError = FE.Enum.Ambito.WEBSERVICE;
    //    //    StatusMessage = "Error interno del WebService";
    //    //    StatusDescription = e.StackTrace; // Esto puede traer un string muy largo
    //    //    Errores = new List<string>();
    //    //    Errores.Add($"Error {e.HResult.ToString()}:  {e.Message}");
    //    //}

    //    //public bool Enviado { get; set;}
    //    //public bool IsValid { get { return Enviado; } }
    //    //public string ZipKey { get; set;}
    //    //public List<string> Errores { get; }
    //    //public List<string> Warnings { get; } 

    //    //public FE.Enum.Ambito OrigenError { get; set; }
    //    //public string StatusCode { get; set; }
    //    //public string StatusMessage { get; set; }
    //    //public string StatusDescription { get; set; }
    //    //public string NombreXML { get; set; }
    //    //public string Url { get; set; }

    //    //public override string ToString ()
    //    //{
    //    //    StringBuilder sb = new StringBuilder();
    //    //    sb.AppendLine("Enviado: " + Enviado);
    //    //    sb.AppendLine("ZipKey: " + ZipKey);
    //    //    sb.AppendLine("NombreXml: " + NombreXML);
    //    //    sb.AppendLine("URL: " + Url);
    //    //    sb.AppendLine("StatusCode: " + StatusCode);
    //    //    sb.AppendLine("StatusMessage: " + StatusMessage);
    //    //    sb.AppendLine("StatusDescription: " + StatusDescription);
    //    //    if (Errores.Count > 0)
    //    //    {
    //    //        if (!Enviado) { sb.AppendLine("Origen del error: " + OrigenError.ToString()); }
    //    //        sb.AppendLine("Errores: ");
    //    //        foreach (string s in Errores)
    //    //        {
    //    //            sb.AppendLine("> " + s);
    //    //        }
    //    //    }
    //    //    if (Warnings.Count > 0)
    //    //    {
    //    //        sb.AppendLine("Advertencias: ");
    //    //        foreach (string s in Warnings)
    //    //        {
    //    //            sb.AppendLine($">>: {s}");
    //    //        }
    //    //    }
    //    //    return sb.ToString();
    //    //}
    //}


    //public class RespConsultaDoc
    //{
    //    public RespConsultaDoc()
    //    {
    //        ErroresMsg = new List<string>();
    //    }

    //    public bool IsValid { get; set; }
    //    public string NombreXML { get; set; }
    //    public string StatusCode { get; set; }
    //    public string StatusMessage { get; set; }
    //    public string StatusDescription { get; set; }
    //    public string Url { get; set; }
    //    public Enum.Ambito OrigenError { get; set; }
    //    public virtual List<string> ErroresMsg { get;  }

    //    public override string ToString()
    //    {
    //        StringBuilder StrRta = new StringBuilder();
    //        StrRta.AppendLine("Validaciones por la autoridad tributaria:");
    //        StrRta.AppendLine("Es Válido: " + IsValid);
    //        StrRta.AppendLine("NombreXml: " + NombreXML);
    //        StrRta.AppendLine("Codigo: " + StatusCode);
    //        StrRta.AppendLine("Status: " + StatusMessage);
    //        StrRta.AppendLine("Descripcion: " + StatusDescription);
    //        if(!IsValid) { StrRta.AppendLine("Origen del Error: " + OrigenError.ToString()); }

    //        if (ErroresMsg.Count > 0)
    //        {
    //            StrRta.AppendLine("\r\nDETALLES:");
    //            foreach (string s in ErroresMsg)
    //            {
    //                StrRta.AppendLine($">>: {s}");
    //            }
    //        }
    //        return StrRta.ToString();
    //    }
    //}


}
