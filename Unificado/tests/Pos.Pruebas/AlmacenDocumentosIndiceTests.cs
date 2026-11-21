using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Pruebas;
using Xunit;

namespace Pos.Pruebas;

public class AlmacenDocumentosIndiceTests : IClassFixture<CarpetaClientes>
{
    private readonly CarpetaClientes _carpeta;

    public AlmacenDocumentosIndiceTests(CarpetaClientes carpeta) => _carpeta = carpeta;

    private sealed class FabricaLenta : IFabricaEnviadorDian
    {
        private int _envios;
        public int Envios => Volatile.Read(ref _envios);

        public IEnviadorDian Crear(AmbienteServicio ambiente, X509Certificate2 certificado) => new Enviador(this);

        private sealed class Enviador : IEnviadorDian
        {
            private readonly FabricaLenta _padre;
            public Enviador(FabricaLenta padre) => _padre = padre;

            public DianResponse EnviarDocumento(byte[] xmlFirmado)
            {
                Interlocked.Increment(ref _padre._envios);
                Thread.Sleep(80);
                return new DianResponse { StatusCode = "00", StatusMessage = "Procesado Correctamente", XmlDocumentKey = "uuid-lento" };
            }

            public DianResponse EnviarNomina(byte[] xmlFirmado) => throw new NotSupportedException();
            public DianResponse EnviarEvento(byte[] xmlFirmado) => throw new NotSupportedException();
            public EventResponse ObtenerDocumentoXml(string uuid) => throw new NotSupportedException();
            public UploadDocumentResponse EnviarSetPruebas(System.Collections.Generic.List<byte[]> xmlFirmados, string idSetPruebas) => throw new NotSupportedException();
            public DianResponse[] ConsultarDocumentos(string zipKey) => throw new NotSupportedException();
            public DianResponse ConsultarDocumento(string uuid) => throw new NotSupportedException();
        }
    }

    private TenantConfig TenantA() =>
        new JsonTenantConfigProvider(_carpeta.Ruta).BuscarPorClaveAsync(CarpetaClientes.ClaveA).GetAwaiter().GetResult()!;
    [Fact]
    public void Las_lecturas_son_copias_y_el_indice_se_recarga_desde_el_disco()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "almacen-indice-" + Guid.NewGuid().ToString("N"));
        try
        {
            var almacen = new AlmacenDocumentos(carpeta);
            almacen.Guardar(new DocumentoRegistro { Numero = "SETP1", TipoDocDian = "01", Estado = EstadoDocumento.Recibido });

            // Un cambio sobre la copia leída no altera el índice mientras no se guarde.
            var copia = almacen.Buscar("SETP1")!;
            copia.Estado = EstadoDocumento.Aprobado;
            Assert.Equal(EstadoDocumento.Recibido, almacen.Buscar("SETP1")!.Estado);

            // Un almacén nuevo sobre la misma carpeta carga lo guardado desde los archivos.
            var recargado = new AlmacenDocumentos(carpeta);
            Assert.Equal("SETP1", recargado.Buscar("SETP1")!.Numero);

            // El cursor de cambios se resuelve desde el índice.
            Assert.Single(almacen.CambiosDesde(0, 10));
            Assert.Empty(almacen.CambiosDesde(almacen.Buscar("SETP1")!.SecuenciaCambio, 10));
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Para_devuelve_el_mismo_almacen_para_la_misma_carpeta_y_ve_los_cambios()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "almacen-compartido-" + Guid.NewGuid().ToString("N"));
        try
        {
            var primero = AlmacenDocumentos.Para(carpeta);
            var segundo = AlmacenDocumentos.Para(carpeta);
            Assert.Same(primero, segundo);

            primero.Guardar(new DocumentoRegistro { Numero = "SETP2", TipoDocDian = "01", Estado = EstadoDocumento.Recibido });
            Assert.NotNull(segundo.Buscar("SETP2"));
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public async Task Procesar_en_paralelo_desde_distintas_instancias_de_DocumentoServicio_solo_envia_una_vez_a_la_DIAN()
    {
        var datos = Path.Combine(Path.GetTempPath(), "almacen-conc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tenant = TenantA();
            var cache = new CertificadoCache();
            var fabrica = new FabricaLenta();
            var doc = ModeloDianTests.Factura();
            doc.Numero = "SETP-CONC-1";

            var inicial = new DocumentoServicio(tenant, cache, fabrica, datos, NullLogger.Instance);
            Assert.Equal(EstadoDocumento.Recibido, inicial.Agregar(doc).Estado);

            using var barrera = new Barrier(4);
            var tareas = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                var servicio = new DocumentoServicio(tenant, cache, fabrica, datos, NullLogger.Instance);
                barrera.SignalAndWait(TimeSpan.FromSeconds(5));
                return servicio.Procesar("SETP-CONC-1");
            })).ToArray();

            var resultados = await Task.WhenAll(tareas);

            Assert.Equal(1, fabrica.Envios);
            Assert.All(resultados, r => Assert.Equal(EstadoDocumento.Aprobado, r.Estado));
            Assert.Equal(1, inicial.Obtener("SETP-CONC-1")!.Intentos);
        }
        finally
        {
            if (Directory.Exists(datos)) Directory.Delete(datos, recursive: true);
        }
    }

    [Fact]
    public void Dos_instancias_distintas_de_DocumentoServicio_comparten_el_indice_en_memoria()
    {
        var datos = Path.Combine(Path.GetTempPath(), "almacen-idx-inst-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tenant = TenantA();
            var cache = new CertificadoCache();
            var fabrica = new FabricaFalsa();
            var doc = ModeloDianTests.Factura();
            doc.Numero = "SETP-IDX-1";

            var servicio1 = new DocumentoServicio(tenant, cache, fabrica, datos, NullLogger.Instance);
            var servicio2 = new DocumentoServicio(tenant, cache, fabrica, datos, NullLogger.Instance);

            var agregado = servicio1.Agregar(doc);
            Assert.Equal(EstadoDocumento.Recibido, agregado.Estado);

            var vistoPorServicio2 = servicio2.Obtener("SETP-IDX-1");
            Assert.NotNull(vistoPorServicio2);
            Assert.Equal(EstadoDocumento.Recibido, vistoPorServicio2!.Estado);
            var cambiosIniciales = servicio2.Cambios(0, 10);
            Assert.Single(cambiosIniciales);

            var cursor = cambiosIniciales[0].SecuenciaCambio;
            var procesado = servicio2.Procesar("SETP-IDX-1");
            Assert.Equal(EstadoDocumento.Aprobado, procesado.Estado);

            Assert.Equal(EstadoDocumento.Aprobado, servicio1.Obtener("SETP-IDX-1")!.Estado);
            var cambiosSiguientes = servicio1.Cambios(cursor, 10);
            var cambio = Assert.Single(cambiosSiguientes);
            Assert.Equal("SETP-IDX-1", cambio.Numero);
            Assert.Equal(EstadoDocumento.Aprobado, cambio.Estado);
        }
        finally
        {
            if (Directory.Exists(datos)) Directory.Delete(datos, recursive: true);
        }
    }
}
