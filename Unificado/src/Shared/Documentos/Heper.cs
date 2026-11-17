using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace eFacturacionColombia_NETCore.Documentos
{
    public static class Utilidades
    {
        public static decimal RedondearDian(decimal valor)
        {
            CultureInfo culturaActual = CultureInfo.CurrentCulture;

            // Obtiene el separador decimal de la cultura actual
            string separadorDecimal = culturaActual.NumberFormat.NumberDecimalSeparator;

            if(valor == Math.Truncate(valor))
            {
                if (decimal.TryParse($"{valor}{separadorDecimal}00", out decimal nd))
                {
                    return nd;
                }
            }
            return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        }
    }

}
