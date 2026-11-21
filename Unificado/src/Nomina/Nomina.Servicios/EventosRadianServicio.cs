using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BasicLibraryExt;
using DIAN.Pos.Servicios;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Documentos.Extensiones;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using Microsoft.Extensions.Logging;

namespace DIAN.Nomina.Servicios
{
    /// <summary>Cuerpo que envía el ERP para un evento RADIAN sobre una factura recibida (el mismo de la API anterior).</summary>
    public class EventoRadian
    {
        /// <summary>030 acuse, 031 reclamo (rechazo), 032 recibo de bienes o servicios, 033 aceptación expresa.</summary>
        public string Codigo { get; set; }

        /// <summary>CUFE de la factura recibida.</summary>
        public string UUID { get; set; }

        /// <summary>Solo para el reclamo (031): causa y su significado.</summary>
        public CausaReclamo Causa { get; set; } = new CausaReclamo();

        /// <summary>Opcional. Número del evento (por ejemplo EVT15). Si no viene, la API lo toma del consecutivo del cliente.</summary>
        public string Numero { get; set; }
    }

    public class CausaReclamo
    {
        public string Codigo { get; set; }
        public string Significado { get; set; }
    }

    /// <summary>
    /// Eventos RADIAN (acuse, reclamo, recibo y aceptación de una factura recibida) de UN cliente. Migrado de la API anterior
    /// sin base de datos ni usuarios con sesión: la factura recibida y el consecutivo viven en archivos del cliente.
    /// </summary>
    public sealed class EventosRadianServicio
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Candados = new ConcurrentDictionary<string, SemaphoreSlim>();

        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly string _carpetaCliente;
        private readonly string _carpeta;
        private readonly ILogger _registro;

        public TimeSpan EsperaSet { get; set; } = TimeSpan.FromSeconds(5);

        public EventosRadianServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaCliente = Path.Combine(carpetaDatos, AlmacenRegistrosNomina.Seguro(tenant.Id));
            _carpeta = Path.Combine(_carpetaCliente, "radian");
            _registro = registro;
        }

        private SoftwareConfig Software => _tenant.ObtenerSoftware(TipoSoftware.EventosRadian)
                                           ?? throw new InvalidOperationException("Los parámetros del módulo de EVENTOS RADIAN no están configurados para este cliente.");

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.EventosRadian);

        public Task<ResponseDian> GenerarEventoAsync(EventoRadian evento) => Task.FromResult(GenerarEvento(evento));

        public ResponseDian GenerarEvento(EventoRadian evento)
        {
            var software = Software;
            var enviador = _fabrica.Crear(Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION, _certificados.Obtener(_tenant));
            var prep = PrepararYFirmar(evento, software, enviador);
            if (!string.IsNullOrEmpty(prep.ErrorCodigo))
            {
                return new ResponseDian { statusCode = prep.ErrorCodigo, statusMessage = prep.ErrorMensaje };
            }

            Directory.CreateDirectory(Path.Combine(_carpeta, "xml"));
            // El consecutivo es independiente por tipo de evento (un acuse y un recibo pueden ser ambos EVT1): el código va en el nombre.
            File.WriteAllBytes(Path.Combine(_carpeta, "xml", AlmacenRegistrosNomina.Seguro(evento.Codigo + "_" + prep.Numero) + ".xml"), prep.Firmado);

            var respuesta = new ResponseDian();
            DianResponse dian;
            try
            {
                dian = enviador.EnviarEvento(prep.Firmado);
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "No se pudo enviar el evento {Numero} a la DIAN", prep.Numero);
                respuesta.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = ex.HResult.ToString(), Descripcion = ex.Message });
                return respuesta;
            }

            respuesta.statusCode = dian.StatusCode;
            respuesta.statusMessage = dian.StatusMessage;
            respuesta.UUID = prep.UuidReferencia;

            if (dian.StatusCode != RespuestaDian.PROCESADO_CORRECTAMENTE)
            {
                respuesta.statusCode = "99";
                respuesta.statusMessage = dian.StatusDescription;
                foreach (var texto in dian.ErrorMessage ?? Array.Empty<string>())
                {
                    var partes = texto.Split(new[] { ',' }, 2);
                    respuesta.Errors.Add(new Error
                    {
                        Source = ErrorSource.ExternalService,
                        Codigo = partes[0].Replace("Regla:", "").Trim(),
                        Descripcion = partes.Length > 1 ? partes[1].Trim() : string.Empty
                    });
                }
            }

            GuardarEvento(prep.Numero, evento, prep.UuidReferencia, dian.StatusCode);
            return respuesta;
        }

        /// <summary>
        /// Envío al set de pruebas de eventos RADIAN (solo habilitación, con TestSetId): firma el evento y lo envía
        /// por <c>EnviarSetPruebas</c> sin guardar en <c>radian/eventos/</c>. Persiste <c>set-radian.json</c>.
        /// </summary>
        public ResultadoSet ValidarEnSet(EventoRadian evento)
        {
            if (!Habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = Software;
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de eventos RADIAN no tiene TestSetId (identificador del set de pruebas).");
            if (evento == null) throw new ArgumentException("Falta el cuerpo del evento RADIAN.");

            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            var prep = PrepararYFirmar(evento, software, enviador);
            if (!string.IsNullOrEmpty(prep.ErrorCodigo))
            {
                throw new ArgumentException(prep.ErrorMensaje);
            }

            return EnviadorSetPruebas.Enviar(enviador, prep.Firmado, software.TestSetId, prep.Numero, _carpetaCliente, "set-radian.json", EsperaSet);
        }

        private (byte[] Firmado, string Numero, string UuidReferencia, string ErrorCodigo, string ErrorMensaje) PrepararYFirmar(
            EventoRadian evento,
            SoftwareConfig software,
            IEnviadorDian enviador)
        {
            if (evento.Codigo == "034")
            {
                return (null, "", "", "109", "Opcion en desarrollo");
            }

            var xmlFactura = ObtenerFactura(evento.UUID, enviador);
            if (xmlFactura == null)
            {
                return (null, "", "", "199", "No fue posible obtener el XML de la factura soliciada");
            }

            var (referencia, emisor, adquiriente) = LeerFactura(xmlFactura);

            // Estos eventos los registra quien RECIBE la factura (el adquiriente).
            if (adquiriente.Identificacion != _tenant.Emisor.Nit)
            {
                return (null, "", "", "101", $"El evento [{evento.Codigo}] solo se puede generar para facturas en las cuales usted es el [receptor]");
            }

            var numero = string.IsNullOrWhiteSpace(evento.Numero) ? NuevoConsecutivo(evento.Codigo) : evento.Numero.Trim();
            var fecha = DateTimeHelper.GetColombianDate();
            var ambiente = Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;

            var generador = new GeneradorEvento(ambiente, referencia)
                .ConFecha(fecha)
                .ConNumero(numero)
                .ConExtensionDian(new ExtensionDian
                {
                    PaisOrigen = Pais.COLOMBIA,
                    SoftwareProveedorNit = _tenant.Emisor.Nit,
                    SoftwareIdentificador = software.Identificador,
                    SoftwarePin = software.PIN
                })
                .ConRegistrante(adquiriente)
                .ConReceptor(emisor);

            switch (evento.Codigo)
            {
                case "030": generador = generador.ParaAcuseRecibo(); break;
                case "031": generador = generador.ParaRechazoDocumento(new ConceptoEventoRechazo { Codigo = evento.Causa?.Codigo }); break;
                case "032": generador = generador.ParaRecepcionBienes(); break;
                case "033": generador = generador.ParaAceptacionExpresa(); break;
                default:
                    return (null, "", "", "101", $"El código de evento {evento.Codigo} no es válido");
            }

            var applicationResponse = generador.AsignarCUDE(software.PIN).Obtener();
            var xml = applicationResponse.SerializeXmlDocument().ToXmlString();
            var firmado = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) }
                .FirmarEvento(xml, DateTimeHelper.GetColombianDate());

            return (firmado, numero, referencia.UUID, "", "");
        }

        /// <summary>La factura recibida se guarda en la carpeta del cliente la primera vez; si no está, se pide a la DIAN.</summary>
        private string ObtenerFactura(string uuid, IEnviadorDian enviador)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return null;
            }

            var ruta = Path.Combine(_carpeta, "facturas", AlmacenRegistrosNomina.Seguro(uuid) + ".xml");
            if (File.Exists(ruta))
            {
                return File.ReadAllText(ruta);
            }

            var respuesta = enviador.ObtenerDocumentoXml(uuid);
            if (respuesta?.XmlBytesBase64 == null)
            {
                return null;
            }

            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(respuesta.XmlBytesBase64));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta, xml);
            return xml;
        }

        private static (ReferenciaDocumento, ParteEvento, ParteEvento) LeerFactura(string xml)
        {
            var doc = XDocument.Parse(xml);
            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            var raiz = doc.Root;
            var uuid = raiz.Element(cbc + "UUID");

            var referencia = new ReferenciaDocumento
            {
                Numero = raiz.Element(cbc + "ID").Value,
                UUID = uuid.Value,
                AlgoritmoCufe = new AlgoritmoSeguridadUUID { Codigo = uuid.Attribute("schemeName").Value },
                TipoDocumento = new TipoDocumentoElectronico { Codigo = raiz.Element(cbc + "InvoiceTypeCode").Value },
                Fecha = DateTime.Parse(raiz.Element(cbc + "IssueDate").Value)
            };

            ParteEvento Parte(string rol)
            {
                var impuestos = raiz.Element(cac + rol).Element(cac + "Party").Element(cac + "PartyTaxScheme");
                var empresa = impuestos.Element(cbc + "CompanyID");
                return new ParteEvento
                {
                    TipoIdentificacion = new TipoIdentificacion { Codigo = (string)empresa.Attribute("schemeName") },
                    Identificacion = empresa.Value,
                    Nombre = impuestos.Element(cbc + "RegistrationName").Value
                };
            }

            return (referencia, Parte("AccountingSupplierParty"), Parte("AccountingCustomerParty"));
        }

        /// <summary>Consecutivo propio del cliente por tipo de evento: EVT1, EVT2... Se guarda en un archivo y no se repite.</summary>
        private string NuevoConsecutivo(string codigo)
        {
            var ruta = Path.Combine(_carpeta, "consecutivos", "EVENTO_" + AlmacenRegistrosNomina.Seguro(codigo) + ".txt");
            var candado = Candados.GetOrAdd(ruta, _ => new SemaphoreSlim(1, 1));
            candado.Wait();
            try
            {
                var actual = File.Exists(ruta) && long.TryParse(File.ReadAllText(ruta).Trim(), out var n) ? n : 0;
                var siguiente = actual + 1;
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                File.WriteAllText(ruta, siguiente.ToString());
                return "EVT" + siguiente;
            }
            finally
            {
                candado.Release();
            }
        }

        private void GuardarEvento(string numero, EventoRadian evento, string uuidFactura, string estado)
        {
            var ruta = Path.Combine(_carpeta, "eventos", AlmacenRegistrosNomina.Seguro(evento.Codigo + "_" + numero) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta, JsonSerializer.Serialize(new
            {
                Numero = numero,
                CodigoEvento = evento.Codigo,
                UuidFactura = uuidFactura,
                FechaUtc = DateTime.UtcNow,
                EstadoDian = estado
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
