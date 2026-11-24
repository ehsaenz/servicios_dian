using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DIAN.Impresion;
using DIAN.Tenants;
using DIAN.Traduccion;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using FE.Models.Doc;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Servicios.Documentos
{
    /// <summary>
    /// Documentos electrónicos (factura, nota crédito y débito) de UN cliente: recibe el <see cref="DocumentoDian"/> del ERP,
    /// lo traduce a UBL, lo firma, lo envía a la DIAN y guarda el resultado. Solo un documento aprobado por la DIAN
    /// (validación previa) llega al estado <see cref="EstadoDocumento.Aprobado"/>.
    /// </summary>

    /// <summary>
    /// Resultado de un envío al set de pruebas. Estado: "aceptado" (la DIAN confirma el set: código 2 y «se encuentra Aceptado»),
    /// "rechazado" (código 99) o "en_proceso" (cualquier otra cosa: la DIAN aún no decide).
    /// </summary>
    /// <summary>CodigoDian y Uuid son lo que el ERP necesita: el código de respuesta de la DIAN y el CUFE/CUDE/CUDS del documento.</summary>
    public sealed record ResultadoSet(string Estado, string Mensaje, string ZipKey, string Numero, DateTime FechaUtc, string CodigoDian = "", string Uuid = "")
    {
        public static ResultadoSet Desde(string? codigo, string? mensaje, string? zipKey, string numero, string uuid = "")
        {
            var texto = mensaje ?? string.Empty;
            var estado = codigo?.Trim() == "2" && texto.Contains("se encuentra Aceptado", StringComparison.OrdinalIgnoreCase) ? "aceptado"
                : codigo?.Trim() == "99" ? "rechazado"
                : "en_proceso";
            return new ResultadoSet(estado, texto, zipKey ?? string.Empty, numero, DateTime.UtcNow, codigo?.Trim() ?? string.Empty, uuid ?? string.Empty);
        }
    }

    public sealed class DocumentoServicio
    {
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly AlmacenDocumentos _almacen;
        private readonly AlmacenDocumentos _almacenSet;
        // Documentos traídos de la DIAN por CUFE (no estaban completos en local). No aparecen en /cambios ni entran en reintentos.
        private readonly AlmacenDocumentos _almacenRecuperados;
        private readonly ILogger _registro;
        private readonly SalidasDocumento _salidas;
        private readonly string _carpetaCliente;

        /// <summary>Espera antes de consultar el resultado de un envío al set de pruebas (la DIAN tarda unos segundos).</summary>
        public TimeSpan EsperaSet { get; set; } = TimeSpan.FromSeconds(5);

        public DocumentoServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILogger registro, SalidasDocumento? salidas = null)
        {
            _salidas = salidas ?? new SalidasDocumento();
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaCliente = Path.Combine(carpetaDatos, AlmacenDocumentos.Nombre(tenant.Id));
            _almacen = AlmacenDocumentos.Para(_carpetaCliente);
            _almacenSet = AlmacenDocumentos.Para(_carpetaCliente, "documentos-set");
            _almacenRecuperados = AlmacenDocumentos.Para(_carpetaCliente, "documentos-recuperados");
            _registro = registro;
        }

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.FacturacionElectronica);

        // Un candado por documento (cliente + número). Impide que Agregar, Procesar, el reintento manual y el
        // mantenimiento envíen o re-firmen el mismo documento a la vez. Es por proceso: con varias instancias haría falta
        // un candado distribuido (ver docs/desarrollo/problemas-conocidos.md).
        private static readonly ConcurrentDictionary<string, object> CandadosDocumento = new ConcurrentDictionary<string, object>();

        private static object CandadoDe(string tenantId, string numero) =>
            CandadosDocumento.GetOrAdd(tenantId + "|" + (numero ?? string.Empty), _ => new object());

        /// <summary>Recibe un documento (ver <see cref="AgregarSinCandado"/>), exclusivo por número.</summary>
        public DocumentoRegistro Agregar(DocumentoDian doc)
        {
            lock (CandadoDe(_tenant.Id, doc?.Numero ?? string.Empty))
            {
                return AgregarSinCandado(doc);
            }
        }

        /// <summary>
        /// Recibe un documento. Lo valida y lo traduce al instante (si no es válido lanza <see cref="DocumentoNoTraducibleException"/>
        /// y no se guarda nada). Es idempotente por número: si ya existe y no fue rechazado ni falló, devuelve el existente.
        /// </summary>
        private DocumentoRegistro AgregarSinCandado(DocumentoDian doc)
        {
            if (_tenant.ObtenerSoftware(TipoSoftware.FacturacionElectronica) == null)
            {
                throw new DocumentoNoTraducibleException("El cliente no tiene configurado el software de facturación electrónica.");
            }

            if (doc == null || string.IsNullOrWhiteSpace(doc.Numero))
            {
                throw new DocumentoNoTraducibleException("El documento no trae número.");
            }

            var existente = _almacen.Buscar(doc.Numero);
            if (existente != null && existente.Estado != EstadoDocumento.Rechazado && existente.Estado != EstadoDocumento.Error)
            {
                return existente;
            }

            var firmado = FirmarDocumento(doc, out var cufe);

            var registro = existente ?? new DocumentoRegistro { FechaRecibido = DateTime.UtcNow };
            registro.Numero = doc.Numero;
            registro.TipoDocDian = doc.TipoDocDian;
            registro.TipoDocErp = doc.TipoDocErp ?? string.Empty;
            registro.Matricula = doc.Emisor?.MatriculaMercantil ?? string.Empty;
            registro.CUFE = cufe;
            registro.Estado = EstadoDocumento.Recibido;
            registro.Mensaje = string.Empty;
            registro.Errores = new List<string>();
            registro.Habilitacion = Habilitacion;
            _almacen.GuardarXml(doc.Numero, ".xml", firmado);
            return _almacen.Guardar(registro);
        }

        /// <summary>
        /// Envío al set de pruebas de facturación (solo habilitación, con TestSetId): firma el documento, lo envía con
        /// EnviarSetPruebas y consulta el resultado. El documento NO entra en el almacén principal: no aparece en
        /// <c>/cambios</c>, no entra en reintentos ni en el webhook. Sí se guarda en el almacén separado <c>documentos-set</c>
        /// (XML firmado, respuesta de la DIAN y PDF si quedó aceptado) para que las descargas funcionen durante habilitación.
        /// El estado resumido queda en set-facturacion.json, que el panel de administración solo lee.
        /// </summary>
        public ResultadoSet ValidarEnSet(DocumentoDian doc)
        {
            if (!Habilitacion) throw new DocumentoNoTraducibleException("El envío al set de pruebas solo aplica en habilitación.");
            var software = _tenant.ObtenerSoftware(TipoSoftware.FacturacionElectronica)
                           ?? throw new DocumentoNoTraducibleException("El cliente no tiene configurado el software de facturación electrónica.");
            if (string.IsNullOrEmpty(software.TestSetId)) throw new DocumentoNoTraducibleException("El software de facturación no tiene TestSetId (identificador del set de pruebas).");
            if (doc == null || string.IsNullOrWhiteSpace(doc.Numero)) throw new DocumentoNoTraducibleException("El documento no trae número.");

            var firmado = FirmarDocumento(doc, out var cufe);
            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            byte[]? respuestaDian = null;
            // Si el envío anterior de este número sigue en proceso, el lote ya existe en la DIAN: se consulta ese ZipKey
            // y NO se reenvía (reenviar crea un lote nuevo que vuelve a quedar en proceso).
            var previo = _almacenSet.Buscar(doc.Numero);
            var resultado = previo != null && previo.Estado == EstadoDocumento.Error && !string.IsNullOrEmpty(previo.ZipKeySet)
                ? EnviadorSetPruebas.Consultar(enviador, previo.ZipKeySet, doc.Numero, _carpetaCliente,
                    "set-facturacion.json", cufe, r => respuestaDian = r)
                : EnviadorSetPruebas.Enviar(enviador, firmado, software.TestSetId, doc.Numero, _carpetaCliente,
                    "set-facturacion.json", EsperaSet, cufe, r => respuestaDian = r);
            GuardarEnSet(doc, cufe, firmado, respuestaDian, resultado);
            return resultado;
        }

        // Guarda un envío al set en el almacén separado. Un envío nuevo del mismo número reemplaza al anterior.
        private void GuardarEnSet(DocumentoDian doc, string cufe, byte[] firmado, byte[]? respuestaDian, ResultadoSet resultado)
        {
            var registro = _almacenSet.Buscar(doc.Numero) ?? new DocumentoRegistro { FechaRecibido = DateTime.UtcNow };
            registro.Numero = doc.Numero;
            registro.TipoDocDian = doc.TipoDocDian;
            registro.TipoDocErp = doc.TipoDocErp ?? string.Empty;
            registro.Matricula = doc.Emisor?.MatriculaMercantil ?? string.Empty;
            registro.CUFE = cufe;
            registro.Estado = resultado.Estado == "aceptado" ? EstadoDocumento.Aprobado
                : resultado.Estado == "rechazado" ? EstadoDocumento.Rechazado
                : EstadoDocumento.Error;
            registro.Mensaje = resultado.Mensaje;
            registro.ZipKeySet = resultado.ZipKey;
            registro.Errores = new List<string>();
            registro.Habilitacion = true;
            registro.Pdf = false;
            registro.PdfMensaje = string.Empty;

            _almacenSet.GuardarXml(doc.Numero, ".xml", firmado);
            if (respuestaDian != null) _almacenSet.GuardarXml(doc.Numero, "-respuesta.xml", respuestaDian);
            registro = _almacenSet.Guardar(registro, esCambioParaElERP: false);

            if (registro.Estado == EstadoDocumento.Aprobado) GenerarPdfAprobado(_almacenSet, registro);
        }

        // PDF de un documento aprobado guardado en un almacén de descarga (set o recuperados). Sin correo: no es un envío real
        // al cliente. Un fallo queda anotado en el registro.
        private void GenerarPdfAprobado(AlmacenDocumentos almacen, DocumentoRegistro registro)
        {
            try
            {
                if (_salidas.Pdf == null)
                {
                    registro.PdfMensaje = "El servicio no tiene generador de PDF configurado.";
                }
                else
                {
                    var xml = System.Text.Encoding.UTF8.GetString(almacen.LeerXml(registro.Numero, ".xml")!);
                    var respuesta = almacen.LeerXml(registro.Numero, "-respuesta.xml");
                    // Sin ApplicationResponse (la DIAN no lo devolvió en el set) el set ya lo marcó aceptado: se usa el XML firmado
                    // y la fecha de envío al set (el envío es síncrono, así que coincide con la aceptación).
                    var modelo = respuesta != null
                        ? DIAN.Impresion.LectorDocumentoAprobado.DesdeDocumentoYRespuesta(xml, System.Text.Encoding.UTF8.GetString(respuesta))
                        : DIAN.Impresion.LectorDocumentoAprobado.DesdeDocumentoAceptadoEnSet(xml, registro.FechaRecibido);
                    var perfil = _salidas.Perfil?.Invoke(_tenant, modelo.Emisor.MatriculaMercantil)
                                 ?? new PerfilImpresion { RazonSocial = _tenant.Emisor.RazonSocial, Establecimiento = _tenant.Emisor.RazonSocial };
                    almacen.GuardarXml(registro.Numero, ".pdf", _salidas.Pdf.Generar(modelo, perfil.Logo));
                    registro.Pdf = true;
                    registro.PdfMensaje = string.Empty;
                }
            }
            catch (Exception ex)
            {
                registro.Pdf = false;
                registro.PdfMensaje = ex is SinPlantillaException ? ex.Message : "No se pudo generar el PDF: " + ex.Message;
                if (!(ex is SinPlantillaException)) _registro.LogError(ex, "Error generando el PDF de {Documento}", registro.Numero);
            }

            almacen.Guardar(registro, esCambioParaElERP: false);
        }

        /// <summary>
        /// Trae de la DIAN, por CUFE, el XML y el acuse (ApplicationResponse) de un documento que no está completo en el
        /// almacén local (facturas anteriores a esta versión, o enviadas desde otra instancia). Solo guarda si el acuse
        /// aprueba (02) el mismo CUFE del documento; si no, lanza una excepción y no guarda nada.
        /// </summary>
        private DocumentoRegistro RecuperarDesdeDian(string numero, string cufe)
        {
            // Mismo candado que el resto de operaciones del documento: no compite con un envío o un PDF en curso.
            lock (CandadoDe(_tenant.Id, numero))
            {
                var enviador = _fabrica.Crear(Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION, _certificados.Obtener(_tenant));

                var documento = enviador.ObtenerDocumentoXml(cufe);
                if (string.IsNullOrEmpty(documento?.XmlBytesBase64))
                {
                    throw new InvalidOperationException("La DIAN no devolvió el XML del documento con ese CUFE.");
                }

                var acuse = enviador.ConsultarDocumento(cufe);
                if (acuse?.XmlBase64Bytes == null || acuse.XmlBase64Bytes.Length == 0)
                {
                    throw new InvalidOperationException("La DIAN no devolvió el acuse (ApplicationResponse) del documento.");
                }

                var xml = Convert.FromBase64String(documento.XmlBytesBase64);
                // Lanza si la respuesta no aprueba (02) o si no corresponde al mismo CUFE del documento.
                var modelo = DIAN.Impresion.LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
                    System.Text.Encoding.UTF8.GetString(xml), System.Text.Encoding.UTF8.GetString(acuse.XmlBase64Bytes));
                if (!string.Equals(modelo.CUFE, cufe, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("El documento recibido de la DIAN no corresponde al CUFE pedido.");
                }

                var registro = new DocumentoRegistro
                {
                    FechaRecibido = DateTime.UtcNow,
                    Numero = numero,
                    TipoDocDian = modelo.TipoDocDian,
                    TipoDocErp = modelo.TipoDocErp ?? string.Empty,
                    Matricula = modelo.Emisor?.MatriculaMercantil ?? string.Empty,
                    CUFE = cufe,
                    Estado = EstadoDocumento.Aprobado,
                    Mensaje = string.Empty,
                    Errores = new List<string>(),
                    Habilitacion = Habilitacion,
                    Pdf = false,
                    PdfMensaje = string.Empty
                };

                _almacenRecuperados.GuardarXml(numero, ".xml", xml);
                _almacenRecuperados.GuardarXml(numero, "-respuesta.xml", acuse.XmlBase64Bytes);
                registro = _almacenRecuperados.Guardar(registro, esCambioParaElERP: false);
                GenerarPdfAprobado(_almacenRecuperados, registro);
                return _almacenRecuperados.Buscar(numero)!;
            }
        }


        /// <summary>Firma un documento (UBL, CUFE y firma XAdES) sin guardarlo. Lo usan la recepción normal y el envío al set.</summary>
        private byte[] FirmarDocumento(DocumentoDian doc, out string cufe)
        {
            var software = _tenant.ObtenerSoftware(TipoSoftware.FacturacionElectronica)
                           ?? throw new DocumentoNoTraducibleException("El cliente no tiene configurado el software de facturación electrónica.");
            var parametros = new ParametrosEmision
            {
                Ambiente = Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION,
                SoftwareNit = _tenant.Emisor.Nit,
                SoftwareIdentificador = software.Identificador,
                SoftwarePin = software.PIN
            };

            var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) };
            byte[] firmado;
            switch (doc.TipoDocDian)
            {
                case "01":
                    var factura = TraductorFactura.Factura(doc, parametros);
                    cufe = factura.UUID.Value;
                    firmado = firma.FirmarFactura(factura.SerializeXmlDocument().ToXmlString(), DateTimeHelper.GetColombianDate());
                    break;
                case "91":
                    var credito = TraductorNota.NotaCredito(doc, parametros);
                    cufe = credito.UUID.Value;
                    firmado = firma.FirmarNotaCredito(credito.SerializeXmlDocument().ToXmlString(), DateTimeHelper.GetColombianDate());
                    break;
                case "92":
                    var debito = TraductorNota.NotaDebito(doc, parametros);
                    cufe = debito.UUID.Value;
                    firmado = firma.FirmarNotaDebito(debito.SerializeXmlDocument().ToXmlString(), DateTimeHelper.GetColombianDate());
                    break;
                default:
                    throw new DocumentoNoTraducibleException($"El tipo de documento {doc.TipoDocDian} no está soportado (solo 01, 91 y 92).");
            }
            return firmado;
        }

        /// <summary>Envía a la DIAN un documento recibido (o reintenta uno rechazado o con error), exclusivo por número. Guarda la respuesta.</summary>
        public DocumentoRegistro Procesar(string numero)
        {
            lock (CandadoDe(_tenant.Id, numero))
            {
                return ProcesarSinCandado(numero);
            }
        }

        private DocumentoRegistro ProcesarSinCandado(string numero)
        {
            var registro = _almacen.Buscar(numero) ?? throw new InvalidOperationException($"No existe el documento {numero}.");
            if (registro.Estado == EstadoDocumento.Aprobado) return registro;

            var firmado = _almacen.LeerXml(numero, ".xml") ?? throw new InvalidOperationException($"Falta el XML firmado de {numero}.");
            registro.Intentos++;

            try
            {
                var ambiente = registro.Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION;
                _registro.LogInformation("Enviando {Documento} a {Ambiente}", numero, registro.Habilitacion ? "habilitación" : "producción");
                var respuesta = _fabrica.Crear(ambiente, _certificados.Obtener(_tenant)).EnviarDocumento(firmado);

                registro.Mensaje = respuesta.StatusMessage ?? respuesta.StatusDescription ?? string.Empty;
                registro.Errores = respuesta.ErrorMessage?.ToList() ?? new List<string>();
                if (respuesta.XmlBase64Bytes != null)
                {
                    _almacen.GuardarXml(numero, "-respuesta.xml", respuesta.XmlBase64Bytes);
                }

                if (respuesta.StatusCode == eFacturacionColombia_NETCore.ServiciosWeb.RespuestaDian.PROCESADO_CORRECTAMENTE)
                {
                    registro.Estado = EstadoDocumento.Aprobado;
                }
                else if (respuesta.StatusCode == "99")
                {
                    registro.Estado = EstadoDocumento.Rechazado;
                }
                else
                {
                    // Cualquier otra respuesta (p. ej. 98, en proceso): la DIAN aún no decidió; se puede reintentar.
                    registro.Estado = EstadoDocumento.Error;
                }
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error enviando {Documento}", numero);
                registro.Estado = EstadoDocumento.Error;
                registro.Mensaje = "No se pudo enviar a la DIAN: " + ex.Message;
            }

            registro = _almacen.Guardar(registro);
            if (registro.Estado == EstadoDocumento.Aprobado) registro = GenerarSalidas(registro);
            return Notificar(registro);
        }

        // ---------- webhook y mantenimiento ----------

        /// <summary>Cuando el documento llega a un estado final (aprobado o rechazado) y el cliente tiene webhook, deja el aviso pendiente y lo intenta.</summary>
        private DocumentoRegistro Notificar(DocumentoRegistro registro)
        {
            if (string.IsNullOrEmpty(_tenant.WebhookUrl)) return registro;
            if (registro.Estado != EstadoDocumento.Aprobado && registro.Estado != EstadoDocumento.Rechazado) return registro;

            registro.WebhookPendiente = true;
            registro.WebhookIntentos = 0;
            registro.WebhookError = string.Empty;
            return IntentarWebhook(_almacen.Guardar(registro));
        }

        private DocumentoRegistro IntentarWebhook(DocumentoRegistro registro)
        {
            registro.WebhookIntentos++;
            try
            {
                // El aviso es solo una pista: el ERP confirma el estado con api/documento/estado/{numero}.
                var cuerpo = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Evento = "documento.estado",
                    registro.Numero,
                    Estado = registro.Estado.ToString(),
                    registro.CUFE,
                    registro.Mensaje,
                    registro.Pdf,
                    registro.SecuenciaCambio
                });
                _salidas.Webhook.Entregar(_tenant.WebhookUrl, _tenant.WebhookSecreto, cuerpo);
                registro.WebhookPendiente = false;
                registro.WebhookError = string.Empty;
                registro.WebhookEntregado = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                registro.WebhookError = ex.Message;
                _registro.LogWarning("No se entregó el webhook de {Documento} (intento {Intento}): {Motivo}", registro.Numero, registro.WebhookIntentos, ex.Message);
            }

            // El intento de aviso es una anotación interna: no mueve el cursor de cambios del ERP.
            return _almacen.Guardar(registro, esCambioParaElERP: false);
        }

        public const int MaximoIntentos = 10;

        /// <summary>Espera antes del intento N (1, 2, 4, 8... minutos, hasta 30).</summary>
        private static TimeSpan Espera(int intentos) => TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, Math.Max(0, intentos - 1))));

        /// <summary>
        /// Trabajo de fondo: reenvía a la DIAN los documentos recibidos que quedaron sin enviar o con error, y reintenta los
        /// avisos de webhook pendientes. Devuelve cuántas acciones hizo.
        /// </summary>
        public int Mantenimiento(DateTime ahoraUtc)
        {
            var acciones = 0;
            foreach (var d in _almacen.Todos())
            {
                var inactivo = ahoraUtc - d.FechaActualizado;

                // Recibido sin enviar (el proceso se reinició a mitad de camino): se le da un margen por si sigue en curso.
                var reenviar = d.Estado == EstadoDocumento.Recibido && inactivo > TimeSpan.FromMinutes(2)
                               || d.Estado == EstadoDocumento.Error && d.Intentos < MaximoIntentos && inactivo > Espera(d.Intentos);
                if (reenviar)
                {
                    Procesar(d.Numero);
                    acciones++;
                    continue;
                }

                if (d.WebhookPendiente && !string.IsNullOrEmpty(_tenant.WebhookUrl) && d.WebhookIntentos < MaximoIntentos && inactivo > Espera(d.WebhookIntentos))
                {
                    // Se vuelve a leer dentro del candado: otro envío pudo cambiar el documento desde que se listó.
                    lock (CandadoDe(_tenant.Id, d.Numero))
                    {
                        var actual = _almacen.Buscar(d.Numero);
                        if (actual != null && actual.WebhookPendiente)
                        {
                            IntentarWebhook(actual);
                            acciones++;
                        }
                    }
                }
            }

            return acciones;
        }

        /// <summary>
        /// PDF oficial y correo de un documento aprobado. Un fallo aquí nunca cambia el estado de la DIAN: queda anotado en
        /// el registro y se puede reintentar con <see cref="Pdf"/>.
        /// </summary>
        private DocumentoRegistro GenerarSalidas(DocumentoRegistro registro)
        {
            byte[]? pdf = null;
            try
            {
                pdf = CrearPdf(registro);
            }
            catch (Exception ex)
            {
                registro.Pdf = false;
                registro.PdfMensaje = ex is SinPlantillaException ? ex.Message : "No se pudo generar el PDF: " + ex.Message;
                if (!(ex is SinPlantillaException)) _registro.LogError(ex, "Error generando el PDF de {Documento}", registro.Numero);
            }

            if (pdf != null)
            {
                registro.Pdf = true;
                registro.PdfMensaje = string.Empty;
                EnviarCorreo(registro, pdf);
            }

            return _almacen.Guardar(registro);
        }

        private byte[]? CrearPdf(DocumentoRegistro registro)
        {
            if (_salidas.Pdf == null)
            {
                registro.PdfMensaje = "El servicio no tiene generador de PDF configurado.";
                return null;
            }

            var xml = _almacen.LeerXml(registro.Numero, ".xml");
            var respuesta = _almacen.LeerXml(registro.Numero, "-respuesta.xml");
            if (xml == null || respuesta == null) throw new InvalidOperationException("Faltan el XML o la respuesta de la DIAN.");

            // El lector solo marca como aprobado un documento con respuesta 02 de la DIAN y CUFE coincidente.
            var modelo = DIAN.Impresion.LectorDocumentoAprobado.DesdeDocumentoYRespuesta(
                System.Text.Encoding.UTF8.GetString(xml), System.Text.Encoding.UTF8.GetString(respuesta));
            var perfil = _salidas.Perfil?.Invoke(_tenant, modelo.Emisor.MatriculaMercantil)
                         ?? new PerfilImpresion { RazonSocial = _tenant.Emisor.RazonSocial, Establecimiento = _tenant.Emisor.RazonSocial };

            var pdf = _salidas.Pdf.Generar(modelo, perfil.Logo);
            _almacen.GuardarXml(registro.Numero, ".pdf", pdf);
            registro.CorreoDestino = modelo.Receptor.EmailContacto;
            return pdf;
        }

        private void EnviarCorreo(DocumentoRegistro registro, byte[] pdf)
        {
            if (_salidas.Correo == null || registro.CorreoEnviado || string.IsNullOrWhiteSpace(registro.CorreoDestino)) return;

            try
            {
                var adjuntos = new List<AdjuntoCorreo>
                {
                    new AdjuntoCorreo { Nombre = registro.Numero + ".pdf", TipoMime = "application/pdf", Contenido = pdf },
                    new AdjuntoCorreo { Nombre = registro.Numero + ".xml", TipoMime = "application/xml", Contenido = _almacen.LeerXml(registro.Numero, ".xml")! }
                };
                var respuesta = _almacen.LeerXml(registro.Numero, "-respuesta.xml");
                if (respuesta != null) adjuntos.Add(new AdjuntoCorreo { Nombre = registro.Numero + "-respuesta-dian.xml", TipoMime = "application/xml", Contenido = respuesta });

                _salidas.Correo.Enviar(registro.CorreoDestino, $"{_tenant.Emisor.RazonSocial}: documento electrónico {registro.Numero}",
                    $"Adjuntamos el documento electrónico {registro.Numero} emitido por {_tenant.Emisor.RazonSocial}, validado por la DIAN.", adjuntos);
                registro.CorreoEnviado = true;
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "Error enviando el correo de {Documento}", registro.Numero);
            }
        }

        /// <summary>
        /// PDF oficial de un documento aprobado (lo genera si falta). Devuelve null si el documento no existe o no está
        /// aprobado: sin aprobación de la DIAN no hay representación gráfica oficial.
        /// </summary>
        public byte[]? Pdf(string numero)
        {
            // Exclusivo por número: no debe pisar el registro si otro envío o aviso del mismo documento está en curso.
            lock (CandadoDe(_tenant.Id, numero))
            {
                var registro = _almacen.Buscar(numero);
                if (registro == null || registro.Estado != EstadoDocumento.Aprobado) return null;

                var guardado = _almacen.LeerXml(numero, ".pdf");
                if (guardado != null) return guardado;

                GenerarSalidas(registro);
                return _almacen.LeerXml(numero, ".pdf");
            }
        }

        /// <summary>Estado de un documento recibido (almacén principal). Los envíos al set no aparecen aquí.</summary>
        public DocumentoRegistro? Obtener(string numero) => _almacen.Buscar(numero);

        public List<DocumentoRegistro> Cambios(long desde, int limite) => _almacen.CambiosDesde(desde, limite);

        // Almacén donde están los archivos de un número, en orden: recuperados (traídos de la DIAN), principal, set. Null si no existe.
        private AlmacenDocumentos? AlmacenDe(string numero)
        {
            if (_almacenRecuperados.Buscar(numero) != null) return _almacenRecuperados;
            if (_almacen.Buscar(numero) != null) return _almacen;
            if (_almacenSet.Buscar(numero) != null) return _almacenSet;
            return null;
        }

        /// <summary>
        /// Registro para descargar archivos (PDF, XML y respuesta DIAN). Si se envía el <paramref name="cufe"/> y el almacén
        /// local no tiene el documento aprobado con XML y acuse, los trae de la DIAN por CUFE (y lanza si la DIAN no lo
        /// aprueba o no responde). Sin CUFE, solo mira lo local. El estado y los cambios siguen leyendo el almacén principal.
        /// </summary>
        public DocumentoRegistro? ObtenerParaDescarga(string numero, string? cufe = null)
        {
            var almacen = AlmacenDe(numero);
            var registro = almacen?.Buscar(numero);
            if (string.IsNullOrWhiteSpace(cufe)) return registro;

            var completo = registro != null && registro.Estado == EstadoDocumento.Aprobado
                           && almacen!.LeerXml(numero, ".xml") != null && almacen.LeerXml(numero, "-respuesta.xml") != null;
            return completo ? registro : RecuperarDesdeDian(numero, cufe!.Trim());
        }

        /// <summary>PDF de un documento aprobado: del almacén donde esté el documento (ver <see cref="AlmacenDe"/>).</summary>
        public byte[]? PdfParaDescarga(string numero)
        {
            var almacen = AlmacenDe(numero);
            if (almacen == null) return null;
            if (ReferenceEquals(almacen, _almacen)) return Pdf(numero);

            var registro = almacen.Buscar(numero);
            if (registro == null || registro.Estado != EstadoDocumento.Aprobado) return null;
            // Envíos al set aceptados antes de este cambio (o cuyo PDF falló): se genera bajo demanda desde el XML firmado guardado.
            if (!registro.Pdf) GenerarPdfAprobado(almacen, registro);
            return almacen.Buscar(numero)?.Pdf == true ? almacen.LeerXml(numero, ".pdf") : null;
        }

        public byte[]? XmlFirmado(string numero) =>
            _almacenRecuperados.LeerXml(numero, ".xml") ?? _almacen.LeerXml(numero, ".xml") ?? _almacenSet.LeerXml(numero, ".xml");

        public byte[]? XmlRespuestaDian(string numero) =>
            _almacenRecuperados.LeerXml(numero, "-respuesta.xml") ?? _almacen.LeerXml(numero, "-respuesta.xml") ?? _almacenSet.LeerXml(numero, "-respuesta.xml");
    }
}
