using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DianModelDoc.Models
{
    //Lista = new List<TipoIdentificacion>
    //{
    //    new TipoIdentificacion { CodigoDian = "11", CodigoERP = "RC", Descripcion = "Registro civil" },
    //    new TipoIdentificacion { CodigoDian = "12", CodigoERP = "TI", Descripcion = "Tarjeta de identidad" },
    //    new TipoIdentificacion { CodigoDian = "13", CodigoERP = "CC", Descripcion = "Cédula de ciudadanía" },
    //    new TipoIdentificacion { CodigoDian = "21", CodigoERP = "TE", Descripcion = "Tarjeta de extranjería" },
    //    new TipoIdentificacion { CodigoDian = "22", CodigoERP = "CE", Descripcion = "Cédula de extranjería" },
    //    new TipoIdentificacion { CodigoDian = "31", CodigoERP = "NIT", Descripcion = "Número de Identificación Tributaria" },
    //    new TipoIdentificacion { CodigoDian = "41", CodigoERP = "PA", Descripcion = "Pasaporte" },
    //    new TipoIdentificacion { CodigoDian = "42", CodigoERP = "DNIE", Descripcion = "Documento de Identificación Extranjero" },
    //    new TipoIdentificacion { CodigoDian = "47", CodigoERP = "PEP", Descripcion = "Permiso Especial de Permanencia" },
    //    new TipoIdentificacion { CodigoDian = "48", CodigoERP = "PPT", Descripcion = "Permiso de Protección Temporal" },
    //    new TipoIdentificacion { CodigoDian = "50", CodigoERP = "NOP", Descripcion = "Nit de Otro País" },
    //    new TipoIdentificacion { CodigoDian = "91", CodigoERP = "NUIP", Descripcion = "NUIP" }
    //};


    public class TipoIdent
    {
        public string CodigoDian { get; set; }
        public string CodigoERP { get; set; }
        public string Descripcion { get; set; }

        // Lista estática de tipos de documento (readonly para que no se pueda sobrescribir)
        public static readonly List<TipoIdent> Lista;

        // Inicialización estática
        static TipoIdent()
        {
            Lista = new List<TipoIdent>
            {
                new TipoIdent { CodigoDian = "11", CodigoERP = "RC", Descripcion = "Registro civil" },
                new TipoIdent { CodigoDian = "12", CodigoERP = "TI", Descripcion = "Tarjeta de identidad" },
                new TipoIdent { CodigoDian = "13", CodigoERP = "CC", Descripcion = "Cédula de ciudadanía" },
                new TipoIdent { CodigoDian = "21", CodigoERP = "TE", Descripcion = "Tarjeta de extranjería" },
                new TipoIdent { CodigoDian = "22", CodigoERP = "CE", Descripcion = "Cédula de extranjería" },
                new TipoIdent { CodigoDian = "31", CodigoERP = "NIT", Descripcion = "Número de Identificación Tributaria" },
                new TipoIdent { CodigoDian = "41", CodigoERP = "PA", Descripcion = "Pasaporte" },
                new TipoIdent { CodigoDian = "42", CodigoERP = "DNIE", Descripcion = "Documento de Identificación Extranjero" },
                new TipoIdent { CodigoDian = "47", CodigoERP = "PEP", Descripcion = "Permiso Especial de Permanencia" },
                new TipoIdent { CodigoDian = "48", CodigoERP = "PPT", Descripcion = "Permiso de Protección Temporal" },
                new TipoIdent { CodigoDian = "50", CodigoERP = "NOP", Descripcion = "Nit de Otro País" },
                new TipoIdent { CodigoDian = "91", CodigoERP = "NUIP", Descripcion = "NUIP" }
            };
        }
    }



    public class AdquirienteDian
    {
        public string TipoIdentificacion { get; set; } = "";
    }
}
