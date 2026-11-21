using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Servicios
{
    /// <summary>
    /// Nota de ajuste del documento soporte (tipo 95) de UN cliente. Referencia un documento soporte (05) por su CUDS.
    /// Su estado de set va a set-ds.json, junto con el documento soporte (bloque documento soporte).
    /// </summary>
    public sealed class NotaAjusteDocumentoSoporteServicio
    {
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly string _carpetaCliente;
        private readonly TimeSpan _esperaSet;
        private readonly ILogger _registro;

        public NotaAjusteDocumentoSoporteServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, TimeSpan esperaSet, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _carpetaCliente = Path.Combine(carpetaDatos, Nombre(tenant.Id));
            _esperaSet = esperaSet;
            _registro = registro;
        }

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.DocumentoSoporte);

        /// <summary>Valida lo que la nota necesita para construirse. No toca disco ni la DIAN.</summary>
        public static void Validar(MetaDatosDE de)
        {
            void Requerir(bool condicion, string mensaje)
            {
                if (!condicion) throw new ArgumentException(mensaje);
            }

            Requerir(de?.TipoDocumento?.Codigo == "95", "Se requiere una nota de ajuste del documento soporte (95).");
            Requerir(!string.IsNullOrWhiteSpace(de!.Numero) && Regex.IsMatch(de.Numero, @"\A[A-Za-z0-9_-]{1,80}\z"),
                "Numero es obligatorio y solo admite letras, números, guion y guion bajo (máximo 80 caracteres).");
            Requerir(de.FechaEmision != default, "FechaEmision es obligatoria.");
            Requerir(de.ConceptoAjuste != null && new[] { "1", "2", "3", "4", "5" }.Contains(de.ConceptoAjuste.Codigo),
                "ConceptoAjuste.Codigo debe estar entre 1 y 5 (concepto de la nota de ajuste del documento soporte).");

            var referencia = de.DocumentoReferencia;
            Requerir(referencia?.TipoDocumento?.Codigo == "05", "DocumentoReferencia debe identificar el documento soporte original (05).");
            Requerir(!string.IsNullOrWhiteSpace(referencia!.Numero), "Falta el número del documento soporte original.");
            Requerir(referencia.Fecha != default && referencia.Fecha.Date <= de.FechaEmision.Date,
                "La fecha del documento soporte original es obligatoria y no puede ser posterior a la nota.");
            Requerir(referencia.AlgoritmoCufe?.Codigo == AlgoritmoSeguridadUUID.CUDS_SHA384.Codigo &&
                Regex.IsMatch(referencia.UUID ?? "", @"\A[0-9a-fA-F]{96}\z"),
                "La referencia requiere el CUDS del documento soporte original (96 caracteres hexadecimales) y CUDS-SHA384.");

            Requerir(de.Items != null && de.Items.Count > 0, "La nota requiere al menos una línea.");
            Requerir(de.Totales != null, "Totales es obligatorio.");
            Requerir(de.DetallePago != null, "DetallePago es obligatorio.");
            Requerir(de.Proveedor != null && !string.IsNullOrWhiteSpace(de.Proveedor.Identificacion), "Falta identificar al proveedor (sujeto no obligado).");
            Requerir(de.Adquiriente != null && !string.IsNullOrWhiteSpace(de.Adquiriente.Identificacion), "Falta identificar al adquiriente.");
        }

        /// <summary>Construcción pura de la nota (sin certificado ni DIAN).</summary>
        public static GeneradorNotaAjusteDocumentoSoporte CrearGenerador(MetaDatosDE de, AmbienteDestino ambiente, string pin)
        {
            Validar(de);
            var concepto = ConceptoNotaAjusteDocumentoSoporte._tiposMap.Values.First(c => c.Codigo == de.ConceptoAjuste.Codigo);

            var generador = new GeneradorNotaAjusteDocumentoSoporte(ambiente, concepto, de.DocumentoReferencia)
                .ConNumero(de.Numero)
                .ConFecha(de.FechaEmision)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(de.ExtensionDian)
                .ConProveedor(de.Proveedor)
                .ConAdquiriente(de.Adquiriente);
            if (!string.IsNullOrWhiteSpace(de.Nota)) generador.ConNota(de.Nota);
            if (de.Periodo != null) generador.ConPeriodoFacturacion(de.Periodo);

            foreach (var linea in de.Items) generador.AgregarLinea(linea);
            generador.ConTotales(de.Totales).ConDetallePago(de.DetallePago);
            return generador.AsignarCUDS(pin);
        }

        /// <summary>Envío a la DIAN (habilitación o producción), igual que el documento soporte.</summary>
        public ResponseDian Enviar(MetaDatosDE de)
        {
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_SOPORTE no están bien configurados");
            var (nota, firmado, xml) = Construir(de, software);
            var idDoc = "NSDS-" + Nombre(de.Numero);

            Directory.CreateDirectory(Path.Combine(_carpetaCliente, "XML"));
            File.WriteAllText(Path.Combine(_carpetaCliente, "XML", idDoc + "-unsigned.xml"), xml);
            File.WriteAllBytes(Path.Combine(_carpetaCliente, "XML", idDoc + ".xml"), firmado);

            _registro.LogInformation("Enviando {Documento} a {Ambiente}", idDoc, Habilitacion ? "habilitación" : "producción");
            var respuesta = _fabrica.Crear(Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION, _certificados.Obtener(_tenant)).EnviarDocumento(firmado);

            // Igual que el documento soporte: el ApplicationResponse queda en disco para contar las notas aprobadas.
            if (respuesta.XmlBase64Bytes != null)
            {
                File.WriteAllBytes(Path.Combine(_carpetaCliente, "XML", idDoc + "-appresponse.xml"), respuesta.XmlBase64Bytes);
            }

            var resultado = new ResponseDian
            {
                statusCode = respuesta.StatusCode,
                statusMessage = respuesta.StatusMessage,
                UUID = nota.UUID.Value,
                TrackID = respuesta.XmlDocumentKey ?? nota.UUID.Value
            };
            if (respuesta.ErrorMessage != null)
            {
                foreach (var mensaje in respuesta.ErrorMessage) resultado.AddMessage(mensaje);
            }
            return resultado;
        }

        /// <summary>Envío al set de pruebas de documento soporte (solo habilitación). Persiste set-ds.json.</summary>
        public ResultadoSet ValidarEnSet(MetaDatosDE de)
        {
            if (!Habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoSoporte)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_SOPORTE no están bien configurados");
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de documento soporte no tiene TestSetId (identificador del set de pruebas).");

            var (nota, firmado, _) = Construir(de, software);
            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            return EnviadorSetPruebas.Enviar(enviador, firmado, software.TestSetId, de.Numero, _carpetaCliente, "set-ds.json", _esperaSet, nota.UUID.Value);
        }

        private (CreditNoteType Nota, byte[] Firmado, string Xml) Construir(MetaDatosDE de, SoftwareConfig software)
        {
            if (de.Adquiriente == null || de.Adquiriente.Identificacion != _tenant.Emisor.Nit)
                throw new ArgumentException("El adquiriente de la nota debe ser el NIT configurado para este cliente.");

            de.AddDatosSoftware(_tenant.Emisor.Nit, software.Identificador, software.PIN);
            // Igual que el documento soporte original: si no viene el proveedor, se toma del emisor.
            if ((de.Proveedor == null || string.IsNullOrEmpty(de.Proveedor.Identificacion)) && de.Emisor != null)
            {
                de.Proveedor = new Proveedor
                {
                    Identificacion = de.Emisor.Identificacion,
                    Persona = de.Emisor.Persona,
                    RazonSocial = de.Emisor.RazonSocial,
                    TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                    CorreoElectronico = de.Emisor.CorreoElectronico,
                    TipoPersona = de.Emisor.TipoPersona,
                    DireccionFisica = de.Emisor.DireccionFiscal,
                    NumeroTelefonico = de.Emisor.NumeroTelefonico,
                    RegimenFiscal = de.Emisor.RegimenFiscal,
                    ResponsabilidadTributaria = de.Emisor.ResponsabilidadTributaria,
                    Nombre = de.Emisor.DatosComerciales?.NombreComercial ?? de.Emisor.RazonSocial
                };
            }
            de.Proveedor.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");

            var ambiente = Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;
            var nota = CrearGenerador(de, ambiente, software.PIN).Obtener();
            var xml = nota.SerializeXmlDocument().ToXmlString();
            var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) };
            var firmado = firma.FirmarNotaAjusteDocumentoSoporte(xml, DateTimeHelper.GetColombianDate());
            return (nota, firmado, xml);
        }

        private static string Nombre(string? texto)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = new string((texto ?? string.Empty).Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
            return limpio.Length == 0 ? "_" : limpio;
        }
    }
}
