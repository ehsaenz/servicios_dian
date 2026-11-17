using DIAN.Pos.Servicios;
using DIAN.Tenants;
using Microsoft.Extensions.Logging;

namespace DIAN.Nomina.Servicios
{
    /// <summary>Punto único (singleton) que crea el servicio de nómina de cada cliente en cada solicitud.</summary>
    public sealed class ServiciosNomina
    {
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly string _carpetaDatos;
        private readonly ILoggerFactory _registros;

        public ServiciosNomina(CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILoggerFactory registros)
        {
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaDatos = carpetaDatos;
            _registros = registros;
        }

        public NominaServicio Nomina(TenantConfig tenant)
        {
            return new NominaServicio(tenant, _certificados, _fabrica, _carpetaDatos, _registros.CreateLogger<NominaServicio>());
        }

        public EventosRadianServicio Eventos(TenantConfig tenant)
        {
            return new EventosRadianServicio(tenant, _certificados, _fabrica, _carpetaDatos, _registros.CreateLogger<EventosRadianServicio>());
        }
    }
}
