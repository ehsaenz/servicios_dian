using BasicLibraryExt;
using System;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace FacturacionColombia_Metadatos
{
    public class ResponseDian
    {

        public string statusCode { get; set; } = string.Empty;
        public string statusMessage { get; set; } = string.Empty;

        /// <summary>
        /// En ambiente de pruebas es un identificador de cada documento enviado a la DIAN, en ambiente Productivo 
        /// se puede usar el UUID (CUFE, CUDE, CUNE, CUDS, etc.) 
        /// </summary>
        public string TrackID { get; set; } = string.Empty;

        /// <summary>
        /// Es el código: CUFE, CUDE, CUNE, CUDS ETC.
        /// </summary>
        public string UUID { get; set; } = string.Empty;
        
        /// <summary>
        /// Es el metadatos apra en Código QR
        /// </summary>
        public string StrQR {  get; set; }
        public DateTime FechaValidacion { get; set; }
        public List<Error> Errors { get; set; } = new List<Error>();
        public List<Error> Warnings { get; set; } = new List<Error>();


        /// <summary>
        /// Interpreta los mensajes de error de la dian y los clasifica como RECHAZOS o NOTIFICACIONES
        /// </summary>
        /// <param name="msg"></param>
        public void AddMessage(string msg)
        {
            bool msgok = true;
            var partes = msg.Split(new char[] { ',' }, 2); // msg.Split(',', 2);
            if (partes.Length > 1)
            {
                var RegNum = partes[0].Split(':');
                var TipDes = partes[1].Split(':');

                string Regla;
                if (RegNum.Length > 0 && TipDes.Length > 0)
                {
                    Regla = RegNum[1];
                    if (TipDes[0].Trim().ToUpper() == "RECHAZO")
                        Errors.Add(new Error { Source = ErrorSource.ExternalService, Codigo = Regla, Descripcion = TipDes[1].Trim() });
                    else
                        Warnings.Add(new Error { Source = ErrorSource.ExternalService, Codigo = Regla, Descripcion = TipDes[1].Trim() });
                }
                else msgok = false;
            }
            else msgok = false;
            if (!msgok) Warnings.Add(new Error { Source = ErrorSource.ExternalService, Codigo = "0001", Descripcion = msg });
        }


    }

}
