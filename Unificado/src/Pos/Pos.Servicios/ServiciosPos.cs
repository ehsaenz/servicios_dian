using System;
using DIAN.Tenants;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Servicios
{
    /// <summary>Punto único (singleton) que crea los servicios de cada cliente en cada solicitud.</summary>
    public sealed class ServiciosPos
    {
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly string _carpetaDatos;
        private readonly ILoggerFactory _registros;

        /// <summary>Espera entre enviar al set de pruebas de habilitación y consultar su resultado.</summary>
        public TimeSpan EsperaSetPruebas { get; set; } = TimeSpan.FromSeconds(5);

        public ServiciosPos(CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILoggerFactory registros)
        {
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaDatos = carpetaDatos;
            _registros = registros;
        }

        /// <summary>
        /// Si es true (lo normal), el envío a la DIAN de un documento recibido corre en segundo plano y la API responde 202 al
        /// instante; si es false, se envía antes de responder (pruebas).
        /// </summary>
        public bool EnvioEnSegundoPlano { get; set; } = true;

        /// <summary>PDF, perfil y correo de los documentos aprobados (opcionales).</summary>
        public Documentos.SalidasDocumento Salidas { get; set; } = new Documentos.SalidasDocumento();

        public Documentos.DocumentoServicio Documentos(TenantConfig tenant)
        {
            return new Documentos.DocumentoServicio(tenant, _certificados, _fabrica, _carpetaDatos, _registros.CreateLogger<Documentos.DocumentoServicio>(), Salidas)
            {
                EsperaSet = EsperaSetPruebas
            };
        }

        public DocumentoSoporteServicio DocumentoSoporte(TenantConfig tenant)
        {
            return new DocumentoSoporteServicio(tenant, _certificados, _fabrica, _carpetaDatos, _registros.CreateLogger<DocumentoSoporteServicio>())
            {
                EsperaSet = EsperaSetPruebas
            };
        }

        public DocumentoEquivalenteServicio DocumentoEquivalente(TenantConfig tenant)
        {
            return new DocumentoEquivalenteServicio(tenant, _certificados, _fabrica, _carpetaDatos, EsperaSetPruebas, _registros.CreateLogger<DocumentoEquivalenteServicio>());
        }

        public NotaAjustePosServicio NotaAjustePos(TenantConfig tenant)
        {
            return new NotaAjustePosServicio(tenant, _certificados, _fabrica, _carpetaDatos, EsperaSetPruebas, _registros.CreateLogger<NotaAjustePosServicio>());
        }
    }
}
