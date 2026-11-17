using System;
using System.Threading;
using System.Threading.Tasks;
using DIAN.Pos.Servicios;
using DIAN.Tenants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Api
{
    /// <summary>
    /// Trabajo de fondo: cada <c>Documentos:ReintentoSegundos</c> (60 por defecto; 0 lo apaga) recorre los clientes activos y
    /// reenvía los documentos que quedaron sin enviar o con error y los avisos de webhook pendientes. Solo funciona con el
    /// almacén de clientes de la app Admin (el índice de archivos antiguo no se puede enumerar).
    /// </summary>
    public sealed class ReintentosWorker : BackgroundService
    {
        private readonly IServiceProvider _servicios;
        private readonly ILogger<ReintentosWorker> _registro;

        public ReintentosWorker(IServiceProvider servicios, ILogger<ReintentosWorker> registro)
        {
            _servicios = servicios;
            _registro = registro;
        }

        protected override async Task ExecuteAsync(CancellationToken detener)
        {
            var segundos = _servicios.GetRequiredService<IConfiguration>().GetValue("Documentos:ReintentoSegundos", 60);
            if (segundos <= 0 || _servicios.GetRequiredService<ITenantConfigProvider>() is not ITenantEnumerador enumerador)
            {
                return;
            }

            var pos = _servicios.GetRequiredService<ServiciosPos>();
            using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(segundos));
            while (await reloj.WaitForNextTickAsync(detener))
            {
                try
                {
                    foreach (var tenant in await enumerador.ListarActivosAsync(detener))
                    {
                        if (tenant.ObtenerSoftware(TipoSoftware.FacturacionElectronica) == null) continue;
                        var acciones = pos.Documentos(tenant).Mantenimiento(DateTime.UtcNow);
                        if (acciones > 0) _registro.LogInformation("Cliente {Tenant}: {Acciones} reintento(s) de documentos", tenant.Id, acciones);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _registro.LogError(ex, "Error en el trabajo de reintentos");
                }
            }
        }
    }
}
