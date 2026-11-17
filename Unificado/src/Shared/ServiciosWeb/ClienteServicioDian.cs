using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Threading.Tasks;

using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using eFacturacionColombia_NETCore.Util;

namespace eFacturacionColombia_NETCore.ServiciosWeb
{
    public class ClienteServicioDian
    {
        public static string ENDPOINT_PRUEBAS = "https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc";

        public static string ENDPOINT_PRODUCCION = "https://vpfe.dian.gov.co/WcfDianCustomerServices.svc";

        public AmbienteServicio Ambiente { get; set; }

        public string RutaCertificado { get; set; }

        public string ClaveCertificado { get; set; }

        public X509Certificate2 Certificado { get; set; }

        public int LimiteRespuestaMb { get; set; } = 100;

        public ClienteServicioDian()
        {
            Ambiente = AmbienteServicio.PRUEBAS;
        }

        public ClienteServicioDian(AmbienteServicio ambiente, string rutaCertificado, string claveCertificado)
        {
            Ambiente = ambiente;
            RutaCertificado = rutaCertificado;
            ClaveCertificado = claveCertificado;
        }

        public ClienteServicioDian(AmbienteServicio ambiente, X509Certificate2 certificado)
        {
            Ambiente = ambiente;
            Certificado = certificado;
        }


        public IWcfDianCustomerServices ObtenerClienteServicio()
        {
            var binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.Security.Message.AlgorithmSuite = SecurityAlgorithmSuite.Basic256Sha256Rsa15;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;
            binding.Security.Message.EstablishSecurityContext = false;
            binding.MaxReceivedMessageSize = 1024 * 1024 * LimiteRespuestaMb;
            
            var url = (Ambiente == AmbienteServicio.PRODUCCION) ? ENDPOINT_PRODUCCION : ENDPOINT_PRUEBAS;
            var endpoint = new EndpointAddress(url);

            var certificate = Certificado ?? new X509Certificate2(RutaCertificado, ClaveCertificado, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            var factory = new ChannelFactory<IWcfDianCustomerServices>(binding, endpoint);
            factory.Credentials.ClientCertificate.Certificate = certificate;

            factory.Endpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());

            var client = factory.CreateChannel();

            return client;
        }

        /// <summary>
        /// Método para enviar un solo documento en AMBIENTE_PRUEBAS
        /// </summary>
        /// <param name="xmlFirmados"></param>
        /// <returns></returns>
        public UploadDocumentResponse EnviarSetDocumentos(List<byte[]> xmlFirmados)
        {
            var client = ObtenerClienteServicio();
            
            var files = new Dictionary<string, byte[]>();

            for (int x = 0; x < xmlFirmados.Count; x++)
            {
                var name = "document" + (x + 1).ToString("00") + ".xml";

                files.Add(name, xmlFirmados[x]);
            }

            var bytesZip = ZipHelper.Compress(files);

            return client.SendBillAsync("documents.zip", bytesZip);
        }

        /// <summary>
        /// Método para enviar varios documentos en AMBIENTE_PRUEBAS
        /// </summary>
        /// <param name="xmlFirmados"></param>
        /// <param name="idSetPruebas"></param>
        /// <returns></returns>
        public UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas)
        {
            var client = ObtenerClienteServicio();

            var files = new Dictionary<string, byte[]>();

            for (int x = 0; x < xmlFirmados.Count; x++)
            {
                var name = "document" + (x + 1).ToString("00") + ".xml";

                files.Add(name, xmlFirmados[x]);
            }

            var bytesZip = ZipHelper.Compress(files);

            return client.SendTestSetAsync("documents.zip", bytesZip, idSetPruebas);
        }


        /// <summary>
        /// Método para envíar un documento en AMBIENTE_PRODUCCION
        /// </summary>
        /// <param name="xmlFirmado"></param>
        /// <returns></returns>
        public DianResponse EnviarDocumento(byte[] xmlFirmado)
        {
            var client = ObtenerClienteServicio();

            var bytesZip = ZipHelper.Compress(xmlFirmado, "document.xml");

            return client.SendBillSync("document.zip", bytesZip);
        }

        public DianResponse[] ConsultarDocumentos(string zipKey)
        {
            var client = ObtenerClienteServicio();

            return client.GetStatusZip(zipKey);
        }

        public DianResponse ConsultarDocumento(string uuid)
        {
            var client = ObtenerClienteServicio();

            return client.GetStatus(uuid);
        }

        public DianResponse EnviarEvento(byte[] xmlFirmado)
        {
            var client = ObtenerClienteServicio();

            var bytesZip = ZipHelper.Compress(xmlFirmado, "event.xml");

            return client.SendEventUpdateStatus(bytesZip);
        }

        public DianResponse ConsultarEvento(string uuid)
        {
            var client = ObtenerClienteServicio();

            return client.GetStatusEvent(uuid);
        }

        public EventResponse ObtenerDocumentoXml(string uuid)
        {
            var client = ObtenerClienteServicio();

            return client.GetXmlByDocumentKey(uuid);
        }

        public NumberRangeResponseList ObtenerRangosNumeracion(string nit, string identificadorSoftware, string nitProveedor = null)
        {
            var client = ObtenerClienteServicio();

            return client.GetNumberingRange(nit, nitProveedor ?? nit, identificadorSoftware);
        }

        public DianResponse EnviarNomina(byte[] xmlFirmado)
        {
            var client = ObtenerClienteServicio();

            var bytesZip = ZipHelper.Compress(xmlFirmado, "nomina.xml");

            return client.SendNominaSync(bytesZip);
        }

        public DianResponse ConsultarEventoFactura(string cufe)
        {
            var client = ObtenerClienteServicio();

            return client.GetStatusEvent(cufe);
        }

        public DianResponse ConsultarNotaFactura(string cufe)
        {
            var client = ObtenerClienteServicio();

            return client.GetReferenceNotes(cufe);
        }
    }
}