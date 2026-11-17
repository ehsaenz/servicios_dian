using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DianModelDoc
{
    public static class UtilsMath
    {

        public static decimal RedondearBancario(decimal valor, int cant_decimales = 2)
        {
            return Math.Round(valor,  cant_decimales);
            //return Math.Round(valor, cant_decimales);

            /*
                int factor =  (int) Math.Pow(Convert.ToDouble(valor), cant_decimales);
                decimal valor_enjuego = (valor * factor) - (int) (valor * factor);
                if (valor_enjuego == 0.5M)
                    return Math.Round(valor, cant_decimales, MidpointRounding.ToEven);
                else
                    return Math.Round(valor, 2);
            */

        }

    }
}
