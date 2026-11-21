using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using NominaElectronica.Enums;
using NominaElectronica.MetaDatos;

namespace DIAN.Nomina.Servicios
{
    /// <summary>
    /// Nómina electrónica (individual y de ajuste) de UN cliente. Migrada de la API anterior (<c>NominaIndividual</c>) con las
    /// mismas reglas y los mismos códigos de estado (00, SP00, 100, 108...), pero sin MySQL: el estado de cada documento vive
    /// en archivos de la carpeta del cliente, y sin estado global.
    /// </summary>
    public sealed class NominaServicio
    {
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly AlmacenRegistrosNomina _almacen;
        private readonly string _carpetaCliente;
        private readonly ILogger _registro;

        public TimeSpan EsperaSet { get; set; } = TimeSpan.FromSeconds(5);

        public NominaServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaCliente = Path.Combine(carpetaDatos, AlmacenRegistrosNomina.Seguro(tenant.Id));
            _almacen = new AlmacenRegistrosNomina(carpetaDatos, tenant.Id);
            _registro = registro;
        }

        private SoftwareConfig Software => _tenant.ObtenerSoftware(TipoSoftware.NominaElectronica)
                                           ?? throw new InvalidOperationException("Los parámetros del módulo de NÓMINA ELECTRÓNICA no están configurados para este cliente.");

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.NominaElectronica);

        private AmbienteDestino AmbienteDestino => Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;

        private AmbienteServicio AmbienteServicio => Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION;

        /// <summary>En habilitación con TestSetId el envío va al set de pruebas (y se consulta por ZipKey); si no, es síncrono.</summary>
        private bool UsaSet => Habilitacion && !string.IsNullOrEmpty(Software.TestSetId);

        private IEnviadorDian Enviador() => _fabrica.Crear(AmbienteServicio, _certificados.Obtener(_tenant));

        private static ResponseDian Rechazo(string codigo, string mensaje) => new ResponseDian { statusCode = codigo, statusMessage = mensaje };

        // ------------------------------------------------------------------ envío

        public Task<ResponseDian> GenerarAsync(MetaDatosNEI nom, bool soloCrearXml = false) => Task.FromResult(Generar(nom, soloCrearXml));

        public Task<ResultadoSet> ValidarEnSetAsync(MetaDatosNEI nom) => Task.FromResult(ValidarEnSet(nom));

        /// <summary>
        /// Envío al set de pruebas de nómina electrónica (solo habilitación, con TestSetId): construye y firma la nómina
        /// (102 o 103) sin guardarla en disco y la envía por <c>EnviarSetPruebas</c>. Persiste <c>set-nomina.json</c>.
        /// </summary>
        public ResultadoSet ValidarEnSet(MetaDatosNEI nom)
        {
            if (!Habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = Software;
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de nómina electrónica no tiene TestSetId (identificador del set de pruebas).");
            if (nom?.InformacionEnvio == null) throw new ArgumentException("Falta la información de envío de la nómina.");
            if (nom.Empleador?.NIT != _tenant.Emisor.Nit)
                throw new ArgumentException($"Este servicio no está registrado al NIT del emisor de éste documento ({nom.Empleador?.NIT})");

            var info = nom.InformacionEnvio;
            if (info.Numero <= 0) throw new ArgumentException("El documento de nómina no trae un número válido.");
            var numeroAjuste = nom.DocumentoReferencia?.Numero ?? "";
            var cuneAjuste = nom.DocumentoReferencia?.Cune ?? "";

            string numeroDocumento;
            byte[] firmado;
            if (info.TipoEnvio == enmTipoEnvio.Eliminar || info.TipoEnvio == enmTipoEnvio.Reemplazar)
            {
                if (info.TipoDocumentoElectronico != enmTipoDocElec.NominaElectronicaIndividualAjuste)
                    throw new ArgumentException($"El campo TipoDocumentoElectrónico {(int)info.TipoDocumentoElectronico} no coincide con el tipo de envío {info.TipoEnvio}");
                if (numeroAjuste == "" || cuneAjuste == "")
                    throw new ArgumentException("Cuando se trata de Nómina de Ajuste los campos [Referencia.Numero] y [Referencia.CUNE] son obligatorios.");

                var letra = info.TipoEnvio == enmTipoEnvio.Eliminar ? "E" : "R";
                numeroDocumento = info.Prefijo + letra + info.Numero;
                var referencia = new ReferenciaNominaIndividual { Numero = numeroAjuste, Cune = cuneAjuste, FechaGeneracion = nom.DocumentoReferencia.FechaGeneracion };
                (firmado, _, _) = FirmarAjuste(nom, info.TipoEnvio, referencia);
            }
            else
            {
                if (info.TipoDocumentoElectronico != enmTipoDocElec.NominaElectronicaIndividual)
                    throw new ArgumentException("El campo [InformacionEnvio.TipoDocumentoElectronico] tiene un valor errado.");
                if (numeroAjuste != "" || cuneAjuste != "")
                    throw new ArgumentException("Solo los documentos de Nómina de Ajuste necesitan los campos [Referencia.Numero] y [Referencia.CUNE].");

                numeroDocumento = info.Prefijo + info.Numero;
                (firmado, _, _) = FirmarIndividual(nom);
            }

            return EnviadorSetPruebas.Enviar(Enviador(), firmado, software.TestSetId, numeroDocumento, _carpetaCliente, "set-nomina.json", EsperaSet);
        }

        public ResponseDian Generar(MetaDatosNEI nom, bool soloCrearXml)
        {
            _ = Software; // falla pronto si el cliente no tiene software de nómina
            var numeroAjuste = nom.DocumentoReferencia?.Numero ?? "";
            var cuneAjuste = nom.DocumentoReferencia?.Cune ?? "";
            var info = nom.InformacionEnvio;

            switch (info.TipoEnvio)
            {
                case enmTipoEnvio.Eliminar:
                case enmTipoEnvio.Reemplazar:
                    if (info.TipoDocumentoElectronico == enmTipoDocElec.NominaElectronicaIndividualAjuste)
                    {
                        if (numeroAjuste == "" || cuneAjuste == "")
                        {
                            return Rechazo("108", "El documento no se ha enviado. Cuando se trata Nómina de Ajuste los campos[Referencia.Numero] y [Referencia.CUNE] son obligatorios para nómina de ajuste.");
                        }

                        var referencia = new ReferenciaNominaIndividual { Numero = numeroAjuste, Cune = cuneAjuste, FechaGeneracion = nom.DocumentoReferencia.FechaGeneracion };
                        return Ajuste(nom, info.TipoEnvio, referencia);
                    }

                    return Rechazo("108", $"El campo TipoDocumentoElectrónico {(int)info.TipoDocumentoElectronico} no coincide con el tipo de envío {info.TipoEnvio}");
                default:
                    if (info.TipoDocumentoElectronico == enmTipoDocElec.NominaElectronicaIndividual)
                    {
                        if (numeroAjuste == "" && cuneAjuste == "")
                        {
                            return Individual(nom, soloCrearXml);
                        }

                        return Rechazo("108", "El documento no se ha enviado. Solo los documentos de Nómina de Ajuste necesitan los campos[Referencia.Numero] y[Referencia.CUNE]");
                    }

                    return Rechazo("88", "El documento no se ha enviado. El campo [InformacionEnvio.TipoDocumentoElectronico] tiene un valor errado");
            }
        }

        /// <summary>Revisa si el documento ya se envió. Devuelve null si se puede (volver a) enviar, o la respuesta que lo impide.</summary>
        private ResponseDian VerificarEnvioPrevio(RegistroNomina anterior, string numeroDocumento)
        {
            if (anterior == null)
            {
                return null;
            }

            switch (anterior.Estado)
            {
                case enmEstadoEnvio.RECHAZADO: // un rechazo es como no haberlo enviado
                case enmEstadoEnvio.PENDIENTE: // no llegó a la DIAN
                    return null;
                case enmEstadoEnvio.ENVIADO:
                {
                    var r = Rechazo("108", "Este documento ya fue enviado, pero aún no se ha consultado su estado");
                    r.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = "1001", Descripcion = $"El documento {numeroDocumento} no se puede volver a enviar, porque, ya fue enviado y no se ha consultado el Estado" });
                    return r;
                }
                default:
                {
                    var r = Rechazo("108", $"Este documento ya fue enviado a la DIAN y se encuentra en estado [{anterior.Estado}]");
                    r.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = "1001", Descripcion = $"El documento {numeroDocumento} no se puede volver a enviar porque se encuentra en estado [{anterior.Estado}]" });
                    return r;
                }
            }
        }

        private ResponseDian Individual(MetaDatosNEI nom, bool soloCrearXml)
        {
            var tipo = (int)nom.InformacionEnvio.TipoDocumentoElectronico;
            var numeroDocumento = nom.InformacionEnvio.Prefijo + nom.InformacionEnvio.Numero;
            var clave = AlmacenRegistrosNomina.Clave(tipo, numeroDocumento);

            if (!soloCrearXml)
            {
                var impide = VerificarEnvioPrevio(_almacen.Obtener(clave), numeroDocumento);
                if (impide != null)
                {
                    return impide;
                }
            }

            var (firmado, xml, cune) = FirmarIndividual(nom);
            var nombre = soloCrearXml ? numeroDocumento + "_copia" : numeroDocumento;
            Directory.CreateDirectory(_almacen.CarpetaXml);
            File.WriteAllText(_almacen.RutaXml(nombre + "_sinfirma.xml"), xml);
            File.WriteAllBytes(_almacen.RutaXml(nombre + ".xml"), firmado);

            if (soloCrearXml)
            {
                return new ResponseDian { statusCode = "100", statusMessage = "", UUID = cune };
            }

            var registro = new RegistroNomina
            {
                TipoDocumento = tipo,
                NumeroCompleto = numeroDocumento,
                Cune = cune,
                TipoEnvio = nom.InformacionEnvio.TipoEnvio,
                Estado = enmEstadoEnvio.PENDIENTE
            };
            _almacen.Guardar(registro);

            return Transmitir(registro, firmado, numeroDocumento);
        }

        private ResponseDian Ajuste(MetaDatosNEI nom, enmTipoEnvio tipoEnvio, ReferenciaNominaIndividual referencia)
        {
            var letra = tipoEnvio == enmTipoEnvio.Eliminar ? "E" : "R";
            var tipo = (int)nom.InformacionEnvio.TipoDocumentoElectronico;
            var numeroDocumento = nom.InformacionEnvio.Prefijo + letra + nom.InformacionEnvio.Numero;
            var clave = AlmacenRegistrosNomina.Clave(tipo, numeroDocumento);

            var impide = VerificarEnvioPrevio(_almacen.Obtener(clave), numeroDocumento);
            if (impide != null)
            {
                return impide;
            }

            byte[] firmado;
            string xml;
            string cune;
            try
            {
                (firmado, xml, cune) = FirmarAjuste(nom, tipoEnvio, referencia);
            }
            catch (ArgumentException ex)
            {
                return Rechazo("101", ex.Message);
            }

            Directory.CreateDirectory(_almacen.CarpetaXml);
            File.WriteAllText(_almacen.RutaXml(numeroDocumento + "_sinfirma.xml"), xml);
            File.WriteAllBytes(_almacen.RutaXml(numeroDocumento + ".xml"), firmado);

            var registro = new RegistroNomina
            {
                TipoDocumento = tipo,
                NumeroCompleto = numeroDocumento,
                Cune = cune,
                TipoEnvio = tipoEnvio,
                Estado = enmEstadoEnvio.PENDIENTE,
                CuneReferencia = referencia.Cune
            };
            _almacen.Guardar(registro);

            return Transmitir(registro, firmado, numeroDocumento);
        }

        private (byte[] Firmado, string Xml, string Cune) FirmarIndividual(MetaDatosNEI nom)
        {
            var ahora = DateTimeHelper.GetColombianDate();
            var nomina = ConstructorNomina.ConstruirIndividual(nom, Software, AmbienteDestino, Habilitacion, ahora).NominaIndividual;
            var xml = nomina.SerializeXmlDocument().ToXmlString();
            var firmado = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) }
                .FirmarNominaIndividual(xml, ahora);
            return (firmado, xml, nomina.InformacionGeneral.CUNE);
        }

        private (byte[] Firmado, string Xml, string Cune) FirmarAjuste(MetaDatosNEI nom, enmTipoEnvio tipoEnvio, ReferenciaNominaIndividual referencia)
        {
            var ahora = DateTimeHelper.GetColombianDate();
            var ajuste = ConstructorNomina.ConstruirAjuste(nom, referencia, Software, AmbienteDestino, Habilitacion, ahora).AjusteNominaIndividual;
            var cune = tipoEnvio == enmTipoEnvio.Reemplazar ? ajuste.Reemplazar.InformacionGeneral.CUNE : ajuste.Eliminar.InformacionGeneral.CUNE;
            var xml = ajuste.SerializeXmlDocument().ToXmlString();
            var firmado = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) }
                .FirmarAjusteNominaIndividual(xml, ahora);
            return (firmado, xml, cune);
        }

        /// <summary>Envía el documento firmado a la DIAN y deja registrado su estado.</summary>
        private ResponseDian Transmitir(RegistroNomina registro, byte[] firmado, string numeroDocumento)
        {
            var respuesta = new ResponseDian();
            var enviador = Enviador();
            registro.FechaEnvioUtc = DateTime.UtcNow;

            if (UsaSet)
            {
                UploadDocumentResponse subida;
                try
                {
                    subida = enviador.EnviarSetPruebas(new List<byte[]> { firmado }, Software.TestSetId);
                }
                catch (Exception ex)
                {
                    _registro.LogError(ex, "No se pudo enviar el documento {Documento} a la DIAN", numeroDocumento);
                    var r = Rechazo("108", $"El documento {numeroDocumento} no se ha enviado, es probable que no tenga conexión con la DIAN");
                    r.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = ex.HResult.ToString(), Descripcion = ex.Message });
                    return r;
                }

                if (subida.ErrorMessageList != null)
                {
                    var texto = new StringBuilder();
                    foreach (var item in subida.ErrorMessageList)
                    {
                        texto.AppendLine($"[{item.DocumentKey ?? ""}] {item.ProcessedMessage}");
                    }

                    respuesta.Errors.Add(new Error { Source = ErrorSource.ExternalService, Codigo = "1000", Descripcion = texto.ToString() });
                    return respuesta;
                }

                var zipKey = subida.ZipKey ?? "";
                registro.Estado = enmEstadoEnvio.ENVIADO;
                registro.PorSet = true;
                registro.TrackId = zipKey;
                _almacen.Guardar(registro);

                respuesta.statusCode = zipKey == "" ? "SP99" : "SP00";
                respuesta.statusMessage = $"El documento [{numeroDocumento}] ha sido recibido, para revisión por parte de la autoridad tributaria";
                respuesta.TrackID = zipKey;
                respuesta.UUID = registro.Cune;
                return respuesta;
            }

            DianResponse dian;
            try
            {
                dian = enviador.EnviarNomina(firmado);
            }
            catch (Exception ex)
            {
                _registro.LogError(ex, "No se pudo enviar el documento {Documento} a la DIAN", numeroDocumento);
                var r = Rechazo("108", $"El documento {numeroDocumento} no se ha enviado, es probable que no tenga conexión con la DIAN");
                r.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = ex.HResult.ToString(), Descripcion = ex.Message });
                return r;
            }

            registro.PorSet = false;
            registro.TrackId = registro.Cune;
            Aplicar(registro, dian, numeroDocumento);
            _almacen.Guardar(registro);
            MarcarReferenciado(registro);

            respuesta.statusCode = dian.StatusCode;
            respuesta.statusMessage = dian.StatusMessage;
            respuesta.TrackID = dian.XmlDocumentKey;
            respuesta.UUID = registro.Cune;
            AgregarMensajes(respuesta, dian);
            return respuesta;
        }

        /// <summary>Al aprobarse un ajuste, el documento que reemplaza o elimina cambia de estado.</summary>
        private void MarcarReferenciado(RegistroNomina ajuste)
        {
            if (ajuste.Estado != enmEstadoEnvio.APROBADO || string.IsNullOrEmpty(ajuste.CuneReferencia))
            {
                return;
            }

            var referido = _almacen.PorCune(ajuste.CuneReferencia);
            if (referido != null)
            {
                referido.Estado = ajuste.TipoEnvio == enmTipoEnvio.Eliminar ? enmEstadoEnvio.ELIMINADO : enmEstadoEnvio.REEMPLAZADO;
                _almacen.Guardar(referido);
            }
        }

        // ------------------------------------------------------------------ consulta

        public Task<ResponseDian> ConsultarAsync(string trackId) => Task.FromResult(Consultar(trackId));

        public ResponseDian Consultar(string trackId)
        {
            var registro = _almacen.PorTrackId(trackId);
            if (registro == null)
            {
                var r = Rechazo("108", "No fue posible consultar este documento en la base de datos");
                r.Errors.Add(new Error { Source = ErrorSource.BackEnd, Codigo = "DB_NOT_FOUND", Descripcion = "No existe un documento con ese TrackID" });
                r.TrackID = trackId;
                return r;
            }

            var numeroDocumento = registro.NumeroCompleto;
            var respuesta = new ResponseDian { TrackID = trackId };

            switch (registro.Estado)
            {
                case enmEstadoEnvio.PENDIENTE:
                case enmEstadoEnvio.ENVIADO:
                case enmEstadoEnvio.APROBADO:
                case enmEstadoEnvio.RECHAZADO:
                    break;
                default:
                    respuesta.statusCode = "108";
                    respuesta.statusMessage = $"El documento tiene un estado no programado {registro.Estado}";
                    return respuesta;
            }

            // Una respuesta definitiva (aprobado, o rechazo con errores) se guarda y no se vuelve a pedir; las intermedias sí.
            var definitiva = registro.StatusCode == "00" || registro.StatusCode == "99";
            if (!definitiva)
            {
                DianResponse dian;
                var enviador = Enviador();
                if (registro.PorSet)
                {
                    dian = enviador.ConsultarDocumentos(registro.TrackId).FirstOrDefault()
                           ?? throw new InvalidOperationException("La DIAN no devolvió información para ese TrackID.");

                    // Nota: cuando el set de pruebas de habilitación ya está aceptado, la DIAN responde solo con el estado del set
                    // (código 2, "se encuentra Aceptado") y deja de informar el resultado de cada documento nuevo; tampoco lo
                    // devuelve por CUNE (66). Esa respuesta se entrega tal cual: el envío síncrono es la vía una vez habilitado el emisor.
                }
                else
                {
                    dian = enviador.ConsultarDocumento(registro.TrackId);
                }

                registro.FechaAcuseUtc = DateTime.UtcNow;
                Aplicar(registro, dian, numeroDocumento);
                if (string.IsNullOrEmpty(registro.XmlDocumentKey) && !registro.PorSet)
                {
                    registro.XmlDocumentKey = trackId;
                }

                _almacen.Guardar(registro);
                MarcarReferenciado(registro);
            }

            respuesta.statusCode = registro.StatusCode;
            respuesta.statusMessage = registro.StatusMessage;
            respuesta.UUID = string.IsNullOrEmpty(registro.XmlDocumentKey) ? (registro.PorSet ? "" : trackId) : registro.XmlDocumentKey;
            if (registro.StatusCode != RespuestaDian.PROCESADO_CORRECTAMENTE)
            {
                foreach (var mensaje in registro.Mensajes)
                {
                    respuesta.AddMessage(mensaje);
                }
            }

            return respuesta;
        }

        // ------------------------------------------------------------------ comunes

        /// <summary>Traslada la respuesta de la DIAN al registro del documento y guarda el acuse (application response).</summary>
        private void Aplicar(RegistroNomina registro, DianResponse dian, string numeroDocumento)
        {
            registro.StatusCode = dian.StatusCode ?? (dian.IsValid ? "00" : "99");
            registro.StatusMessage = dian.StatusMessage ?? dian.StatusDescription;
            registro.XmlDocumentKey = dian.XmlDocumentKey ?? "";
            registro.Mensajes = dian.ErrorMessage ?? Array.Empty<string>();
            registro.Estado = registro.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE ? enmEstadoEnvio.APROBADO : enmEstadoEnvio.RECHAZADO;

            if (dian.XmlBase64Bytes != null)
            {
                Directory.CreateDirectory(_almacen.CarpetaXml);
                File.WriteAllText(_almacen.RutaXml(numeroDocumento + "-appresponse.xml"), Encoding.UTF8.GetString(dian.XmlBase64Bytes));
            }
        }

        private static void AgregarMensajes(ResponseDian respuesta, DianResponse dian)
        {
            if (dian.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE || dian.ErrorMessage == null)
            {
                return;
            }

            foreach (var mensaje in dian.ErrorMessage)
            {
                respuesta.AddMessage(mensaje);
            }
        }
    }
}
