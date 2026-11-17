using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Pruebas;

namespace Pos.Pruebas;

// Avisos al webhook del cliente y trabajo de reintentos, directamente sobre el servicio (sin HTTP).
public class DocumentoWebhookTests : IClassFixture<CarpetaClientes>, IDisposable
{
    private sealed class WebhookFalso : IEntregaWebhook
    {
        public List<(string Url, string Secreto, string Cuerpo)> Entregas { get; } = new();
        public bool Falla { get; set; }

        public void Entregar(string url, string secreto, string cuerpoJson)
        {
            if (Falla) throw new InvalidOperationException("el webhook respondió 500");
            Entregas.Add((url, secreto, cuerpoJson));
        }
    }

    private readonly FabricaFalsa _fabrica = new() { ConApplicationResponse = true };
    private readonly WebhookFalso _webhook = new();
    private readonly string _datos = Path.Combine(Path.GetTempPath(), "dian-doc-wh-" + Guid.NewGuid().ToString("N"));
    private readonly CarpetaClientes _carpeta;

    public DocumentoWebhookTests(CarpetaClientes carpeta) => _carpeta = carpeta;

    public void Dispose()
    {
        try { Directory.Delete(_datos, true); } catch (IOException) { }
    }

    private DocumentoServicio Servicio(bool conWebhook)
    {
        var tenant = new JsonTenantConfigProvider(_carpeta.Ruta).BuscarPorClaveAsync(CarpetaClientes.ClaveA).GetAwaiter().GetResult()!;
        if (conWebhook)
        {
            tenant.WebhookUrl = "https://erp.ejemplo.com/dian/aviso";
            tenant.WebhookSecreto = "secreto-de-prueba";
        }

        return new DocumentoServicio(tenant, new CertificadoCache(), _fabrica, _datos, NullLogger.Instance, new SalidasDocumento { Webhook = _webhook });
    }

    private static FE.Models.Doc.DocumentoDian Factura(string numero)
    {
        var doc = ModeloDianTests.Factura();
        doc.Numero = numero;
        return doc;
    }

    [Fact]
    public void Al_aprobarse_se_avisa_al_webhook_con_la_firma_HMAC_y_sin_mover_el_cursor_del_ERP()
    {
        var servicio = Servicio(conWebhook: true);
        servicio.Agregar(Factura("SETP990000031"));
        var aprobado = servicio.Procesar("SETP990000031");

        var entrega = Assert.Single(_webhook.Entregas);
        Assert.Equal("https://erp.ejemplo.com/dian/aviso", entrega.Url);
        Assert.Contains("\"Estado\":\"Aprobado\"", entrega.Cuerpo);
        Assert.Contains(aprobado.CUFE, entrega.Cuerpo);
        Assert.Equal("sha256=" + Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("secreto-de-prueba"), System.Text.Encoding.UTF8.GetBytes(entrega.Cuerpo))).ToLowerInvariant(),
            EntregaWebhookHttp.Firma("secreto-de-prueba", entrega.Cuerpo));
        Assert.False(aprobado.WebhookPendiente);
        Assert.NotNull(aprobado.WebhookEntregado);

        // La anotación del aviso no cuenta como cambio nuevo para el ERP.
        Assert.Equal(aprobado.SecuenciaCambio, servicio.Obtener("SETP990000031")!.SecuenciaCambio);
        Assert.Equal(aprobado.SecuenciaCambio, entrega.Cuerpo.Contains($"\"SecuenciaCambio\":{aprobado.SecuenciaCambio}") ? aprobado.SecuenciaCambio : -1);
    }

    [Fact]
    public void Un_webhook_caido_se_reintenta_con_espera_creciente()
    {
        _webhook.Falla = true;
        var servicio = Servicio(conWebhook: true);
        servicio.Agregar(Factura("SETP990000032"));
        var registro = servicio.Procesar("SETP990000032");

        Assert.Equal(EstadoDocumento.Aprobado, registro.Estado);
        Assert.True(registro.WebhookPendiente);
        Assert.Equal(1, registro.WebhookIntentos);
        Assert.Contains("500", registro.WebhookError);

        // Antes de la espera (1 minuto) no se reintenta; después, con el webhook ya sano, sí.
        Assert.Equal(0, servicio.Mantenimiento(DateTime.UtcNow));
        _webhook.Falla = false;
        Assert.Equal(1, servicio.Mantenimiento(DateTime.UtcNow.AddMinutes(5)));

        var despues = servicio.Obtener("SETP990000032")!;
        Assert.False(despues.WebhookPendiente);
        Assert.Single(_webhook.Entregas);
    }

    [Fact]
    public void Un_documento_con_error_de_la_DIAN_se_reenvia_en_el_mantenimiento()
    {
        _fabrica.CodigoRespuesta = "98";
        var servicio = Servicio(conWebhook: false);
        servicio.Agregar(Factura("SETP990000033"));
        Assert.Equal(EstadoDocumento.Error, servicio.Procesar("SETP990000033").Estado);

        _fabrica.CodigoRespuesta = "00";
        Assert.Equal(0, servicio.Mantenimiento(DateTime.UtcNow));
        Assert.Equal(1, servicio.Mantenimiento(DateTime.UtcNow.AddMinutes(5)));
        Assert.Equal(EstadoDocumento.Aprobado, servicio.Obtener("SETP990000033")!.Estado);
        Assert.Empty(_webhook.Entregas);
    }

    [Fact]
    public void Un_documento_recibido_que_quedo_sin_enviar_se_envia_al_reiniciar()
    {
        var servicio = Servicio(conWebhook: false);
        servicio.Agregar(Factura("SETP990000034"));   // el proceso se "cae" antes de Procesar

        Assert.Equal(0, servicio.Mantenimiento(DateTime.UtcNow));            // aún puede estar en curso
        Assert.Equal(1, servicio.Mantenimiento(DateTime.UtcNow.AddMinutes(3)));
        Assert.Equal(EstadoDocumento.Aprobado, servicio.Obtener("SETP990000034")!.Estado);
    }

    [Fact]
    public void Sin_webhook_configurado_no_se_avisa_a_nadie()
    {
        var servicio = Servicio(conWebhook: false);
        servicio.Agregar(Factura("SETP990000035"));
        var registro = servicio.Procesar("SETP990000035");

        Assert.False(registro.WebhookPendiente);
        Assert.Empty(_webhook.Entregas);
    }
}
