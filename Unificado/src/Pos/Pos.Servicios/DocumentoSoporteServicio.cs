using System;
using System.IO;
using System.Linq;
using System.Text;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Documentos.Extensiones;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Servicios
{
    /// <summary>
    /// Documento soporte para no obligados a facturar (DSNO) de UN cliente. Se crea por solicitud con la configuración
    /// de ese cliente; no lee estado global.
    /// </summary>
    public sealed class DocumentoSoporteServicio
    {
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly string _carpetaDatos;
        private readonly ILogger _registro;

        public TimeSpan EsperaSet { get; set; } = TimeSpan.FromSeconds(5);

        public DocumentoSoporteServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaDatos = Path.Combine(carpetaDatos, Nombre(tenant.Id));
            _registro = registro;
        }

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.DocumentoSoporte);

        private AmbienteServicio AmbienteServicio => Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION;

        public ResponseDian Enviar(MetaDatosDE ds)
        {
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_SOPORTE no están bien configurados");

            var firmado = Firmar(ds, software, out var factura, out var xml);
            var idDoc = "DSNO-" + Nombre(ds.Numero);

            Directory.CreateDirectory(Path.Combine(_carpetaDatos, "XML"));
            File.WriteAllText(Path.Combine(_carpetaDatos, "XML", idDoc + "-unsigned.xml"), xml);
            File.WriteAllBytes(Path.Combine(_carpetaDatos, "XML", idDoc + ".xml"), firmado);

            _registro.LogInformation("Enviando {Documento} a {Ambiente}", idDoc, Habilitacion ? "habilitación" : "producción");

            var respuesta = _fabrica.Crear(AmbienteServicio, _certificados.Obtener(_tenant)).EnviarDocumento(firmado);
            var resultado = Convertir(respuesta, idDoc);
            resultado.UUID = factura.UUID?.Value ?? string.Empty;
            return resultado;
        }

        /// <summary>
        /// Envío al set de pruebas de documento soporte (solo habilitación, con TestSetId): firma el documento y lo envía
        /// por <c>EnviarSetPruebas</c> sin guardar el XML ni generar idempotencia local. Persiste <c>set-ds.json</c>.
        /// </summary>
        public ResultadoSet ValidarEnSet(MetaDatosDE ds)
        {
            if (!Habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_SOPORTE no están bien configurados");
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de documento soporte no tiene TestSetId (identificador del set de pruebas).");
            if (ds == null || string.IsNullOrWhiteSpace(ds.Numero)) throw new ArgumentException("El documento soporte no trae número.");

            var firmado = Firmar(ds, software, out _, out _);
            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            return EnviadorSetPruebas.Enviar(enviador, firmado, software.TestSetId, ds.Numero, _carpetaDatos, "set-ds.json", EsperaSet);
        }

        private byte[] Firmar(MetaDatosDE ds, SoftwareConfig software, out InvoiceType factura, out string xml)
        {
            if (ds.Adquiriente == null || ds.Adquiriente.Identificacion != _tenant.Emisor.Nit)
            {
                throw new ArgumentException($"Este servicio no puede enviar Documentos Soporte para el adquiriente con NIT {ds.Adquiriente?.Identificacion}");
            }

            if (!string.Equals(ds.Adquiriente.RazonSocial?.Trim(), _tenant.Emisor.RazonSocial.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Este servicio no puede enviar Documentos Soporte para el adquiriente {ds.Adquiriente.RazonSocial}");
            }

            ds.AddDatosSoftware(_tenant.Emisor.Nit, software.Identificador, software.PIN);

            if (string.IsNullOrEmpty(ds.Proveedor.Identificacion))
            {
                ds.Proveedor = new Proveedor
                {
                    Identificacion = ds.Emisor.Identificacion,
                    Persona = ds.Emisor.Persona,
                    RazonSocial = ds.Emisor.RazonSocial,
                    TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                    CorreoElectronico = ds.Emisor.CorreoElectronico,
                    TipoPersona = ds.Emisor.TipoPersona,
                    DireccionFisica = ds.Emisor.DireccionFiscal,
                    NumeroTelefonico = ds.Emisor.NumeroTelefonico,
                    RegimenFiscal = ds.Emisor.RegimenFiscal,
                    ResponsabilidadTributaria = ds.Emisor.ResponsabilidadTributaria,
                    Nombre = ds.Emisor.DatosComerciales.NombreComercial ?? ds.Emisor.RazonSocial
                };
            }

            ds.Proveedor.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");

            var ambienteDestino = Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;
            var generador = new GeneradorDocumentoSoporte(ambienteDestino)
                .ConNumero(ds.Numero)
                .ConFecha(ds.FechaEmision)
                .ConVencimiento(ds.FechaVencimiento)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(ds.ExtensionDian)
                .ConProveedor(ds.Proveedor)
                .ConAdquiriente(ds.Adquiriente)
                .AgregarLineas(ds.Items)
                .ConTotales(ds.Totales)
                .ConDetallePago(ds.DetallePago)
                .AsignarCUDS(software.PIN)
                .ConPeriodoFacturacion(ds.Periodo);

            factura = generador.Obtener();
            xml = factura.SerializeXmlDocument().ToXmlString();

            var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) };
            return firma.FirmarDocumentoSoporte(xml, DateTimeHelper.GetColombianDate());
        }

        public ResponseDian Consultar(string trackId)
        {
            var enviador = _fabrica.Crear(AmbienteServicio, _certificados.Obtener(_tenant));

            // En habilitación el identificador es el ZipKey del envío; en producción, el UUID del documento.
            var respuesta = Habilitacion
                ? enviador.ConsultarDocumentos(trackId).FirstOrDefault()
                : enviador.ConsultarDocumento(trackId);

            if (respuesta == null)
            {
                throw new InvalidOperationException("La DIAN no devolvió información para ese identificador.");
            }

            return Convertir(respuesta, "consulta-" + Nombre(trackId));
        }

        private ResponseDian Convertir(DianResponse respuesta, string idDoc)
        {
            var resultado = new ResponseDian
            {
                statusCode = respuesta.StatusCode,
                statusMessage = respuesta.StatusMessage
            };

            if (respuesta.ErrorMessage != null)
            {
                foreach (var mensaje in respuesta.ErrorMessage)
                {
                    resultado.AddMessage(mensaje);
                }
            }

            if (respuesta.XmlBase64Bytes != null)
            {
                Directory.CreateDirectory(Path.Combine(_carpetaDatos, "XML"));
                File.WriteAllText(Path.Combine(_carpetaDatos, "XML", idDoc + "-appresponse.xml"), Encoding.UTF8.GetString(respuesta.XmlBase64Bytes));
            }

            return resultado;
        }

        private static string Nombre(string? texto)
        {
            var limpio = new string((texto ?? string.Empty).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }
    }
}
