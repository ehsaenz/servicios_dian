using BasicLibraryExt;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DianModelDoc.Models;

namespace DianModelDoc.Servicios
{
    public class CatalogosNew<T>
    {
        static string NombreCatalogo;
        public CatalogosNew() { }
        public CatalogosNew(string nombre_catalogo)
        {
            NombreCatalogo = nombre_catalogo;
        }

        public static List<T> Coleccion
        {
            get
            {
                List<T> col = null;
                try
                {
                    string StrCatalogo = CatalogResource.Read(NombreCatalogo);
                    return StrCatalogo.ToObject<List<T>>();
                }
                catch (System.Exception)
                {
                    return col;
                }
            }
        }

    }

    public class Catalogos
    {
        // MUNICIPIOS
        private static List<TipoBase> mncps;
        public static List<TipoBase> Municipios
        {
            get
            {
                if (mncps == null)
                {
                    string StrCatalogo = CatalogResource.Read("Municipios");
                    mncps = StrCatalogo.ToObject<List<TipoBase>>();
                }
                return mncps;
            }
        }

        // DEPARTAMENTOS
        private static List<TipoBase> dpts;
        public static List<TipoBase> Departamentos
        {
            get
            {
                if (dpts == null)
                {
                    string StrCatalogo = CatalogResource.Read("Departamentos");
                    dpts = StrCatalogo.ToObject<List<TipoBase>>();
                }
                return dpts;
            }
        }

        // PAISES 
        private static List<TipoBase> pss;
        public static List<TipoBase> Paises
        {
            get
            {
                if (pss == null)
                {
                    string StrCatalogo = CatalogResource.Read("Paises");
                    pss = StrCatalogo.ToObject<List<TipoBase>>();
                }
                return pss;
            }
        }

        // TIPOS_DOCUMENTO 
        private static List<TipoBase> tpsdoc;
        public static List<TipoBase> TiposDoc
        {
            get
             {
                if (tpsdoc == null)
                {
                    string StrCatalogo = CatalogResource.Read("TiposDocumento");
                    tpsdoc = StrCatalogo.ToObject<List<TipoBase>>();
                }
                return tpsdoc;
            }
        }

        // TIPOS_ID 
        private static List<TipoBase> tpsId;
        public static List<TipoBase> TiposId
        {
            get
            {
                if (tpsId == null)
                {
                    string StrCatalogo = CatalogResource.Read("TiposIdFiscal");
                    tpsId = StrCatalogo.ToObject<List<TipoBase>>();
                }
                return tpsId;
            }
        }

        // TIPOS_IMPUESTO 
        private static List<TipoImpuesto> tpsImp;
        public static List<TipoImpuesto> TiposImp
        {
            get
            {
                if (tpsImp == null)
                {
                    string StrCatalogo = CatalogResource.Read("TiposImpuesto");
                    tpsImp = StrCatalogo.ToObject<List<TipoImpuesto>>();
                }
                return tpsImp;
            }
        }


        private static List<tercero> trcs_prov;
        public static List<tercero> Terceros_Prov
        {
            get
            {
                if (trcs_prov == null)
                {
                    string StrCatalogo = CatalogResource.Read("terceros_prov");
                    trcs_prov = StrCatalogo.ToObject<List<tercero>>();
                }
                return trcs_prov;
            }
        }

        private static List<tipo_codigo> tps_cod_prod;
        public static List<tipo_codigo> TiposCodigoProd
        {
            get
            {
                if (tps_cod_prod == null)
                {
                    string StrCatalogo = CatalogResource.Read("TiposCodigoProd");
                    tps_cod_prod = StrCatalogo.ToObject<List<tipo_codigo>>();
                }
                return tps_cod_prod;
            }
        }
    }

    internal static class CatalogResource
    {
        public static string Read(string name)
        {
            var assembly = typeof(CatalogResource).GetTypeInfo().Assembly;
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith(".Resources." + name + ".txt"));

            if (resourceName == null)
            {
                return string.Empty;
            }

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}

