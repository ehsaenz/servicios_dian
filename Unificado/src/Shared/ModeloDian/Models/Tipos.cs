using System.Collections.Generic;

namespace DianModelDoc.Models
{
    public class TipoBase
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }

    public class TipoBaseDesc : TipoBase
    {
        public string Descripcion { get; set; }
    }
	
    public class TipoImpuesto: TipoBase
    {
        public FE.Enum.TipoCalculo tipoCalculo { get; set; }
        public FE.Enum.TipoOperacion tipoOperacion { get; set; }
    }


    public class tercero
    {
        public string NumeroDoc { get; set; }
        public string CodigoContrib { get; set; }
        public string _id { get; set; }
        public string Password { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
    }

    public class catalogo<T>
    {
        public string Table { get; set; }
        public List<T> rows { get; set; }
    }

    public class tipo_codigo
    {
        public string SchemeID { get; set; }
        public string SchemeName { get; set; }
        public string SchemeAgencyID { get; set; }
        public string Uso { get; set; }
    }

    //public class terceros
    //{
    //    public string Table { get; set; }
    //    public List<tercero> rows { get; set; }
    //}

    //public class tipos_codigo
    //{
    //    public string Table { get; set; }
    //    public List<tipo_codigo> rows { get; set; }

    //}


}
