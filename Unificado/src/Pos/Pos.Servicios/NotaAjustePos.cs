using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DIAN.Pos.Servicios.Documentos;
using DIAN.Tenants;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Documentos.Extensiones;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using Microsoft.Extensions.Logging;

namespace DIAN.Pos.Servicios
{
    /// <summary>Nota de ajuste crédito del POS (tipo 94) de UN cliente. No consulta bases de datos.</summary>
    public sealed class NotaAjustePosServicio
    {
        // Bloqueo acotado: evita envíos simultáneos del mismo número del mismo cliente en esta instancia.
        private static readonly object[] Locks = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

        private readonly DocumentoEquivalenteServicio _documentoEquivalente;
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly RutasCliente _rutas;
        private readonly TimeSpan _esperaSet;
        private readonly ILogger _registro;

        public NotaAjustePosServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, TimeSpan esperaSet, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _rutas = new RutasCliente(carpetaDatos, tenant.Id);
            _esperaSet = esperaSet;
            _registro = registro;
            _documentoEquivalente = new DocumentoEquivalenteServicio(tenant, certificados, fabrica, carpetaDatos, esperaSet, registro);
        }

        public static void Validar(MetaDatosDE de)
        {
            void Requerir([DoesNotReturnIf(false)] bool condicion, string mensaje)
            {
                if (!condicion) throw new ArgumentException(mensaje);
            }

            Requerir(de?.TipoDocumento?.Codigo == "94", "Se requiere una nota de ajuste crédito (94).");
            Requerir(!string.IsNullOrWhiteSpace(de!.Numero) &&
                Regex.IsMatch(de.Numero, @"\A[A-Za-z0-9_-]{1,80}\z"),
                "Numero es obligatorio y solo admite letras, números, guion y guion bajo (máximo 80 caracteres).");
            Requerir(de.FechaEmision != default, "FechaEmision es obligatoria.");
            Requerir(new[] { "1", "2", "3", "4", "5" }.Contains(de.ConceptoAjuste?.Codigo),
                "ConceptoAjuste.Codigo debe estar entre 1 y 5.");
            Requerir(!string.IsNullOrWhiteSpace(de.ConceptoAjuste?.Descripcion),
                "ConceptoAjuste.Descripcion es obligatoria.");
            var referencia = de.DocumentoReferencia;
            Requerir(referencia?.TipoDocumento?.Codigo == "20", "DocumentoReferencia debe identificar el POS original (20).");
            Requerir(!string.IsNullOrWhiteSpace(referencia!.Numero), "Falta el número del POS original.");
            Requerir(referencia.Fecha != default && referencia.Fecha.Date <= de.FechaEmision.Date,
                "La fecha del POS original es obligatoria y no puede ser posterior a la nota.");
            Requerir(referencia.AlgoritmoCufe?.Codigo == AlgoritmoSeguridadUUID.CUDE_SHA384.Codigo &&
                Regex.IsMatch(referencia.UUID ?? "", @"\A[0-9a-fA-F]{96}\z"),
                "La referencia requiere el CUDE del POS original (96 caracteres hexadecimales) y CUDE-SHA384.");
            Requerir(!string.IsNullOrWhiteSpace(de.Emisor?.Identificacion) &&
                !string.IsNullOrWhiteSpace(de.Emisor?.RazonSocial) &&
                de.Emisor?.TipoIdentificacion != null && de.Emisor?.DireccionFiscal?.Pais != null &&
                de.Emisor?.DatosComerciales != null && de.Emisor?.ResponsabilidadTributaria != null,
                "Faltan datos fiscales, dirección o datos comerciales del emisor.");
            Requerir(!string.IsNullOrWhiteSpace(de.Adquiriente?.Identificacion), "Falta identificar al adquiriente del POS original.");
            if (de.Adquiriente!.Identificacion != "222222222222")
                Requerir(de.Adquiriente.TipoIdentificacion != null &&
                    de.Adquiriente.DireccionComercial?.Pais != null &&
                    de.Adquiriente.ResponsabilidadTributaria != null &&
                    !string.IsNullOrWhiteSpace(de.Adquiriente.RazonSocial ?? de.Adquiriente.NombreComercial),
                    "Faltan datos del adquiriente o su dirección comercial.");
            Requerir(de.DetallePago != null &&
                (de.DetallePago.Forma == FormaPago.CONTADO || de.DetallePago.Forma == FormaPago.CREDITO) &&
                !string.IsNullOrWhiteSpace(de.DetallePago.Metodo?.Codigo), "DetallePago es obligatorio.");
            Requerir(de.DetallePago.Forma != FormaPago.CREDITO || de.DetallePago.Fecha.Date >= de.FechaEmision.Date,
                "El pago a crédito requiere una fecha de vencimiento válida.");
            Requerir(de.Items != null && de.Items.Count > 0, "La nota requiere al menos una línea.");
            foreach (var item in de.Items!)
            {
                Requerir(item != null && item.Cantidad > 0 && item.PrecioUnitario >= 0 &&
                    item.BaseGeneral >= 0 && item.UnidadMedida != null &&
                    !string.IsNullOrWhiteSpace(item.Descripcion), "Cada línea requiere descripción, unidad, cantidad positiva e importes no negativos.");
                Requerir(item!.Impuestos != null && item.Cargos != null && item.Descuentos != null,
                    "Las colecciones de impuestos, cargos y descuentos no pueden ser nulas.");
                Requerir(item.CodigoProducto == null || item.CodigoProducto.Tipo != null, "Falta el tipo del código del producto.");
                Requerir(item.Cargos.All(x => x != null && x.Monto >= 0) &&
                    item.Descuentos.All(x => x != null && x.Monto >= 0 && x.Tipo != null),
                    "Cargos y descuentos deben tener importes no negativos y tipo de descuento.");
                Requerir(item.Impuestos.All(x => x != null && x.Tipo != null && !x.Retenido &&
                    x.Importe >= 0 && x.BaseImponible >= 0 && x.Porcentaje >= 0 && x.PorUnidad >= 0),
                    "Impuestos inválidos: esta nota no admite retenciones ni importes negativos.");
                Requerir(!item.Excluido || item.Impuestos.Count == 0, "Una línea excluida no debe contener impuestos.");
                Requerir(Coincide(item.BaseGeneral, item.Cantidad * item.PrecioUnitario +
                    item.Cargos.Sum(x => x.Monto) - item.Descuentos.Sum(x => x.Monto)),
                    "BaseGeneral debe corresponder a cantidad por precio, más cargos y menos descuentos de la línea.");
            }
            Requerir(de.Totales != null, "Totales es obligatorio.");
            var totales = de.Totales!;
            Requerir(totales.Descuentos == 0 && totales.Cargos == 0 && totales.Anticipo == 0,
                "Esta integración admite descuentos y cargos por línea; no admite descuentos/cargos globales ni anticipos.");
            var bruto = de.Items.Sum(x => x.BaseGeneral);
            var impuestos = de.Items.SelectMany(x => x.Impuestos).Sum(x => x.Importe);
            // La base de una línea se cuenta una sola vez aunque tenga varios impuestos.
            var baseImponible = de.Items.Where(x => x.Impuestos.Count > 0).Sum(x => x.BaseGeneral);
            Requerir(totales.TotalPagable > 0 && Coincide(totales.ValorBruto, bruto) &&
                Coincide(totales.BaseImponible, baseImponible) &&
                Coincide(totales.ValorBrutoConImpuestos, bruto + impuestos) &&
                Coincide(totales.TotalPagable, bruto + impuestos),
                "Los totales de la nota no coinciden con sus líneas e impuestos.");
        }

        private static bool Coincide(decimal a, decimal b) => Math.Abs(a - b) <= 0.02M;

        /// <summary>Construcción pura: permite validar XML sin certificado, disco ni conexión DIAN.</summary>
        public static GeneradorNotaAjusteDocumentoEquivalente CrearGenerador(
            MetaDatosDE de, AmbienteDestino ambiente, string proveedorNit, string softwareId, string pin)
        {
            Validar(de);
            var extension = new ExtensionDian
            {
                PaisOrigen = new Pais { Codigo = "CO", Nombre = "Colombia" },
                SoftwareProveedorNit = proveedorNit,
                SoftwareIdentificador = softwareId,
                SoftwarePin = pin
            };
            var generador = new GeneradorNotaAjusteDocumentoEquivalente(ambiente, de.ConceptoAjuste, de.DocumentoReferencia)
                .ConNumero(de.Numero).ConFecha(de.FechaEmision).ConNota(de.Nota)
                .ConMoneda(Moneda.PESO_COLOMBIANO).ConExtensionDian(extension).ConEmisor(de.Emisor);
            if (de.Adquiriente.Identificacion == "222222222222")
                generador.ParaConsumidorFinal();
            else
                generador.ConAdquiriente(de.Adquiriente);
            foreach (var item in de.Items) generador.AgregarLinea(item);
            foreach (var grupo in de.Items.SelectMany(x => x.Impuestos).GroupBy(x => x.Tipo.Codigo))
            {
                generador.AgregarResumenImpuesto(new ResumenImpuesto
                {
                    Tipo = grupo.First().Tipo,
                    Importe = grupo.Sum(x => x.Importe),
                    Detalles = grupo.GroupBy(x => new { x.Porcentaje, x.PorUnidad, Unidad = x.UnidadMedida?.Codigo })
                        .Select(g => new Impuesto
                        {
                            Tipo = g.First().Tipo, Porcentaje = g.Key.Porcentaje, PorUnidad = g.Key.PorUnidad,
                            UnidadMedida = g.First().UnidadMedida, ValorUnidadMedida = g.Sum(x => x.ValorUnidadMedida),
                            BaseImponible = g.Sum(x => x.BaseImponible), Importe = g.Sum(x => x.Importe)
                        }).ToList()
                });
            }
            var pago = new DetallePago
            {
                Forma = de.DetallePago.Forma, Metodo = de.DetallePago.Metodo,
                Fecha = de.DetallePago.Fecha == default ? de.FechaEmision.Date : de.DetallePago.Fecha,
                Identificador = de.DetallePago.Identificador
            };
            return generador.ConTotales(de.Totales).ConDetallePago(pago).AsignarCUDE(pin);
        }

        public ResponseDian Enviar(MetaDatosDE de)
        {
            Validar(de);
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_EQUIVALENTE no están bien configurados");
            var habilitacion = _tenant.EsHabilitacion(TipoSoftware.DocumentoEquivalente);
            var (generador, nota, xml) = Construir(de, software, habilitacion);
            var id = "NATP-" + de.Numero;
            var ruta = _rutas.Xml(id);
            var rutaSinFirmar = _rutas.XmlSinFirmar(id);

            lock (Locks[(StringComparer.OrdinalIgnoreCase.GetHashCode(_tenant.Id + ruta) & int.MaxValue) % Locks.Length])
            {
                _rutas.Crear();
                // Archivo abierto en exclusión: también cubre varios procesos sobre el mismo directorio.
                using var reserva = new FileStream(ruta + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var transporte = _documentoEquivalente.Transporte();

                if (File.Exists(ruta))
                {
                    var anterior = CreditNoteType.DeserializeXmlString(File.ReadAllText(ruta));
                    var estado = transporte.Consultar(anterior.UUID.Value, id, porCude: true);
                    estado.UUID = anterior.UUID.Value;
                    estado.TrackID = string.IsNullOrEmpty(estado.TrackID) ? anterior.UUID.Value : estado.TrackID;
                    estado.StrQR = new GeneradorNotaAjusteDocumentoEquivalente(anterior).GenerarTextoQR();
                    // Solo un rechazo explícito permite corregir y volver a transmitir.
                    if (estado.statusCode != RespuestaDian.VALIDACION_CON_ERRORES)
                    {
                        if (anterior.UUID.Value != nota.UUID.Value ||
                            !File.Exists(rutaSinFirmar) ||
                            File.ReadAllText(rutaSinFirmar) != xml)
                            throw new InvalidOperationException("Ya existe una nota con este número y otros datos. Debe resolver su estado antes de modificarla.");
                        return estado;
                    }
                }

                File.WriteAllText(rutaSinFirmar, xml);
                var firmado = FirmarXml(xml, de.FechaEmision);
                // Guardar antes de transmitir permite recuperar el CUDE tras una interrupción.
                var temporal = ruta + ".tmp";
                File.WriteAllBytes(temporal, firmado);
                File.Move(temporal, ruta, true);

                _registro.LogInformation("Enviando {Documento}", id);
                var respuesta = transporte.Enviar(firmado, id);
                respuesta.UUID = nota.UUID.Value;
                respuesta.StrQR = generador.GenerarTextoQR();
                if (string.IsNullOrEmpty(respuesta.TrackID)) respuesta.TrackID = nota.UUID.Value;
                return respuesta;
            }
        }

        /// <summary>
        /// Envío al set de pruebas de la nota de ajuste POS (solo habilitación, con TestSetId): firma la nota y la envía
        /// por <c>EnviarSetPruebas</c> sin guardar XML ni avanzar archivos de idempotencia. Persiste <c>set-pos.json</c>.
        /// </summary>
        public ResultadoSet ValidarEnSet(MetaDatosDE de)
        {
            Validar(de);
            var habilitacion = _tenant.EsHabilitacion(TipoSoftware.DocumentoEquivalente);
            if (!habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = _tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente)
                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_EQUIVALENTE no están bien configurados");
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de documento equivalente (POS) no tiene TestSetId (identificador del set de pruebas).");

            var (_, _, xml) = Construir(de, software, habilitacion);
            var firmado = FirmarXml(xml, de.FechaEmision);
            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            return EnviadorSetPruebas.Enviar(enviador, firmado, software.TestSetId, de.Numero, _rutas.CarpetaCliente, "set-pos.json", _esperaSet);
        }

        private (GeneradorNotaAjusteDocumentoEquivalente Generador, CreditNoteType Nota, string Xml) Construir(
            MetaDatosDE de,
            SoftwareConfig software,
            bool habilitacion)
        {
            if (de.Emisor.Identificacion != _tenant.Emisor.Nit)
                throw new ArgumentException("El NIT del emisor no corresponde al configurado para este cliente.");

            // Prefijo de la numeración (NAAJ49) e identificador de pago: en producción los debe enviar el ERP.
            // Solo en habilitación se completan: el prefijo con las letras iniciales del número, y el identificador con
            // "NP" + el número (DetallePago.Identificador toma por defecto "NP" + la hora actual, y eso hace distinto
            // el XML de cada intento y bloquearía el reenvío de la misma nota).
            var sinPrefijo = string.IsNullOrWhiteSpace(de.Emisor.PrefijoRangoNumeracion);
            var identificadorPorDefecto = Regex.IsMatch(de.DetallePago.Identificador ?? string.Empty, @"\ANP\d{15,19}\z");
            if (!habilitacion && (sinPrefijo || identificadorPorDefecto))
            {
                throw new ArgumentException("Emisor.PrefijoRangoNumeracion y DetallePago.Identificador son obligatorios en producción.");
            }

            if (sinPrefijo)
            {
                var prefijo = Regex.Match(de.Numero, @"\A[A-Za-z]+").Value;
                if (prefijo.Length > 0)
                {
                    de.Emisor.PrefijoRangoNumeracion = prefijo;
                }
            }

            if (identificadorPorDefecto)
            {
                de.DetallePago.Identificador = "NP" + de.Numero;
            }

            var ambiente = habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;
            var generador = CrearGenerador(de, ambiente, _tenant.Emisor.Nit, software.Identificador, software.PIN);
            var nota = generador.Obtener();
            var xml = nota.SerializeXmlDocument().ToXmlString();
            return (generador, nota, xml);
        }

        private byte[] FirmarXml(string xml, DateTime fechaEmision)
        {
            var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) };
            return firma.FirmarNotaAjusteDocumentoEquivalente(xml, fechaEmision);
        }
    }
}
