using BasicLibraryExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DianModelDoc.Servicios
{
    public static class MtxJson
    {
        public static string SerializarJson(Object Obj)
        {
            try
            { 
                return Obj.ToJson(); }
            catch (Exception e)
            {
                // ADICIONAR A LISTA DE ERRORES
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static T DesSerializarJson<T>(string StrJson)
        {
            try
            {
                return StrJson.ToObject<T>();
            }
            catch (Exception e)
            {
                // ADICIONAR A LISTA DE ERRORES
                Console.WriteLine(e.Message);
                return default(T);
            }
        }

    }

    public static class ToolsString
    {
        public static List<ClaveValor> NotasLst(string[] nts)
        {
            List<ClaveValor> new_nts = new List<ClaveValor>();
            if (nts == null) return new_nts;
            else
            {
                foreach (string s in nts)
                {
                    new_nts.Add(new ClaveValor(s));
                }
                return new_nts;
            }
        }

        public static string GetVariable(string NombreVariable, List<ClaveValor> lcv)
        {
            if (lcv.Count == 0) return "";
            else
            {
                //List<impuesto> impts = Items.Where(x => !x.Excluido).SelectMany(x => x.impuestos).ToList();
                //                var vars = lcv.Where(x => x.EsClaveValor).ToList<ClaveValor>();

                var vars = lcv.Where(x => x.EsClaveValor).ToList<ClaveValor>();
                //ClaveValor cv = vars.Find(x => x.Clave == NombreVariable);
                //cv = vars.Find(x => x.Clave.Equals(NombreVariable));
                //if (cv == null) return "";
                //else return cv.Valor;

                foreach(ClaveValor cv in lcv)
                {
                    if (cv.EsClaveValor)
                    {
                        if (cv.Clave.Trim().Equals(NombreVariable)) return cv.Valor;
                    }
                }
                return "";
            }
        }

    }



    public class ClaveValor
    {
        public ClaveValor(string dato)
        {
            EsClaveValor = false;
            if(dato.Contains("="))
            {
                string[] v = dato.Split('=');
                if (!v[0].Trim().Contains(" "))
                {
                    StringBuilder sb = new StringBuilder();
                    string sep = "";
                    Clave = v[0].Trim();
                    for (int i = 1; i < v.Length; i++)
                    {
                        sb.Append(v[i] + sep) ;
                        sep = "=";
                    }
                    Valor = sb.ToString().Trim();
                    EsClaveValor = true;
                }
                else
                {
                    EsClaveValor = false;
                    Clave = "";
                    Valor = dato;
                }

            }
            else
            {
                EsClaveValor = false;
                Clave = "";
                Valor = dato;
            }
        }
        public bool EsClaveValor { get; internal set; }
        public string Clave { get; internal set; }
        public string Valor { get; internal set; }
        public string Tipo { get; internal set; }

        public override string ToString()
        {
            if (EsClaveValor)
                return $"{Clave.Trim()} = {Valor.Trim()}";
            else
                return Valor;
        }
    }
}
