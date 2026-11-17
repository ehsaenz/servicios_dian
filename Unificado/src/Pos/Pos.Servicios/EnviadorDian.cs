using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;

namespace DIAN.Pos.Servicios
{
    /// <summary>Lo que los servicios necesitan del web service de la DIAN. Existe para poder probar sin llamar a la DIAN.</summary>
    public interface IEnviadorDian
    {
        DianResponse EnviarDocumento(byte[] xmlFirmado);
        DianResponse EnviarNomina(byte[] xmlFirmado);
        DianResponse EnviarEvento(byte[] xmlFirmado);
        EventResponse ObtenerDocumentoXml(string uuid);
        UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas);
        DianResponse[] ConsultarDocumentos(string zipKey);
        DianResponse ConsultarDocumento(string uuid);
    }

    public interface IFabricaEnviadorDian
    {
        IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado);
    }

    public sealed class FabricaEnviadorDian : IFabricaEnviadorDian
    {
        public IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado)
        {
            return new EnviadorDian(new ClienteServicioDian(ambiente, certificado));
        }
    }

    internal sealed class EnviadorDian : IEnviadorDian
    {
        private readonly ClienteServicioDian _cliente;

        public EnviadorDian(ClienteServicioDian cliente)
        {
            _cliente = cliente;
        }

        public DianResponse EnviarDocumento(byte[] xmlFirmado) => _cliente.EnviarDocumento(xmlFirmado);

        public DianResponse EnviarNomina(byte[] xmlFirmado) => _cliente.EnviarNomina(xmlFirmado);

        public DianResponse EnviarEvento(byte[] xmlFirmado) => _cliente.EnviarEvento(xmlFirmado);

        public EventResponse ObtenerDocumentoXml(string uuid) => _cliente.ObtenerDocumentoXml(uuid);

        public UploadDocumentResponse EnviarSetPruebas(List<byte[]> xmlFirmados, string idSetPruebas) => _cliente.EnviarSetPruebas(xmlFirmados, idSetPruebas);

        public DianResponse[] ConsultarDocumentos(string zipKey) => _cliente.ConsultarDocumentos(zipKey);

        public DianResponse ConsultarDocumento(string uuid) => _cliente.ConsultarDocumento(uuid);
    }
}
