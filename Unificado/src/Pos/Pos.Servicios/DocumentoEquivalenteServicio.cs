using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Documento equivalente electrónico del POS (tiquete, tipo 20) de UN cliente. Migrado de la API anterior sin PDF,
    /// imagen de QR ni correo (quedan fuera de la fase 2). Es idempotente: reenviar un número ya autorizado devuelve
    /// el resultado anterior en vez de duplicarlo.
    /// </summary>
    public sealed class DocumentoEquivalenteServicio
    {
        private readonly TenantConfig _tenant;
        private readonly CertificadoCache _certificados;
        private readonly IFabricaEnviadorDian _fabrica;
        private readonly RutasCliente _rutas;
        private readonly TimeSpan _esperaSet;
        private readonly ILogger _registro;

        public DocumentoEquivalenteServicio(TenantConfig tenant, CertificadoCache certificados, IFabricaEnviadorDian fabrica, string carpetaDatos, TimeSpan esperaSet, ILogger registro)
        {
            _tenant = tenant;
            _certificados = certificados;
            _fabrica = fabrica;
            _rutas = new RutasCliente(carpetaDatos, tenant.Id);
            _esperaSet = esperaSet;
            _registro = registro;
        }

        private SoftwareConfig Software => _tenant.ObtenerSoftware(TipoSoftware.DocumentoEquivalente)
                                           ?? throw new InvalidOperationException("Los parámetros del módulo de DOCUMENTO_EQUIVALENTE no están bien configurados");

        private bool Habilitacion => _tenant.EsHabilitacion(TipoSoftware.DocumentoEquivalente);

        internal TransporteDocumentoEquivalente Transporte()
        {
            var ambiente = Habilitacion ? AmbienteServicio.PRUEBAS : AmbienteServicio.PRODUCCION;
            var enviador = _fabrica.Crear(ambiente, _certificados.Obtener(_tenant));
            return new TransporteDocumentoEquivalente(enviador, Habilitacion, Software, _rutas, _esperaSet);
        }

        public ResponseDian Consultar(string trackId)
        {
            return Transporte().Consultar(trackId, string.Empty);
        }

        public ResponseDian Enviar(MetaDatosDE tpe)
        {
            var software = Software;
            ValidarDatos(tpe, out var codigoComprador, out var nombreComprador);

            var idDoc = "DETP-" + tpe.Numero;

            var transporte = Transporte();

            // Envíos anteriores del mismo número.
            var previo = CargarFactura(idDoc);
            var respuestaPrevia = CargarAppResponse(idDoc);
            if (respuestaPrevia != null &&
                respuestaPrevia.DocumentResponse[0].Response.ResponseCode.Value == "02")
            {
                var uuid = respuestaPrevia.DocumentResponse[0].DocumentReference[0].UUID.Value;
                return new ResponseDian
                {
                    UUID = uuid,
                    TrackID = uuid,
                    statusCode = "00",
                    statusMessage = "El documento ha sido enviado previamente y ha sido autorizado por la autoridad tributaria"
                };
            }

            if (previo != null)
            {
                var estado = transporte.Consultar(previo.UUID.Value, idDoc, porCude: true);
                if (estado.statusCode == "00")
                {
                    return estado;
                }
            }

            _registro.LogDebug("El documento {Documento} no se ha enviado antes o no fue aceptado; se procesa de cero", idDoc);
            _rutas.Crear();
            foreach (var ruta in new[] { _rutas.Xml(idDoc), _rutas.AppResponse(idDoc), _rutas.XmlSinFirmar(idDoc) })
            {
                File.Delete(ruta);
            }

            var firmado = Firmar(tpe, software, codigoComprador, nombreComprador, out var documento, out var textoQr, out var xml);
            File.WriteAllText(_rutas.XmlSinFirmar(idDoc), xml);
            File.WriteAllBytes(_rutas.Xml(idDoc), firmado);

            _registro.LogInformation("Enviando {Documento} a {Ambiente}", idDoc, Habilitacion ? "habilitación" : "producción");
            var respuesta = transporte.Enviar(firmado, idDoc);

            if (respuesta.statusCode == "00")
            {
                respuesta.StrQR = textoQr;
            }

            if (string.IsNullOrEmpty(respuesta.UUID))
            {
                respuesta.UUID = documento.UUID?.Value ?? string.Empty;
            }

            return respuesta;
        }

        /// <summary>
        /// Envío al set de pruebas de documento equivalente POS (solo habilitación, con TestSetId): firma el tiquete y lo envía
        /// por <c>EnviarSetPruebas</c> sin guardar el XML ni generar idempotencia local. Persiste <c>set-pos.json</c>.
        /// </summary>
        public ResultadoSet ValidarEnSet(MetaDatosDE tpe)
        {
            if (!Habilitacion) throw new InvalidOperationException("El envío al set de pruebas solo aplica en habilitación.");
            var software = Software;
            if (string.IsNullOrEmpty(software.TestSetId)) throw new InvalidOperationException("El software de documento equivalente (POS) no tiene TestSetId (identificador del set de pruebas).");
            if (tpe == null || string.IsNullOrWhiteSpace(tpe.Numero)) throw new ArgumentException("El documento no trae número.");

            ValidarDatos(tpe, out var codigoComprador, out var nombreComprador);
            var firmado = Firmar(tpe, software, codigoComprador, nombreComprador, out var documento, out _, out _);
            var enviador = _fabrica.Crear(AmbienteServicio.PRUEBAS, _certificados.Obtener(_tenant));
            return EnviadorSetPruebas.Enviar(enviador, firmado, software.TestSetId, tpe.Numero, _rutas.CarpetaCliente, "set-pos.json", _esperaSet, documento.UUID?.Value ?? string.Empty);
        }

        private void ValidarDatos(MetaDatosDE tpe, out string codigoComprador, out string nombreComprador)
        {
            if (tpe.Emisor.Identificacion != _tenant.Emisor.Nit)
            {
                throw new ArgumentException($"Este servicio no puede enviar Documentos Equivalentes para el emisor con NIT {tpe.Emisor.Identificacion}");
            }

            if (tpe.ExtensionDian.RangoNumeracion == null)
            {
                throw new ArgumentException("El documento no trae la resolución de numeración (RangoNumeracion).");
            }

            // La DIAN exige código y nombre del comprador (DEPD16, DEPD18). En producción los debe enviar el ERP;
            // solo en habilitación se completan con la identificación y la razón social del adquiriente.
            codigoComprador = tpe.Adquiriente.CodigoCliente;
            nombreComprador = tpe.Adquiriente.NombreComercial;
            if (string.IsNullOrWhiteSpace(codigoComprador) || string.IsNullOrWhiteSpace(nombreComprador))
            {
                if (!Habilitacion)
                {
                    throw new ArgumentException("Adquiriente.CodigoCliente y Adquiriente.NombreComercial son obligatorios en producción.");
                }

                codigoComprador = string.IsNullOrWhiteSpace(codigoComprador) ? tpe.Adquiriente.Identificacion : codigoComprador;
                nombreComprador = string.IsNullOrWhiteSpace(nombreComprador) ? tpe.Adquiriente.RazonSocial : nombreComprador;
            }
        }

        private byte[] Firmar(
            MetaDatosDE tpe,
            SoftwareConfig software,
            string codigoComprador,
            string nombreComprador,
            out InvoiceType documento,
            out string textoQr,
            out string xml)
        {
            foreach (var itm in tpe.Items)
            {
                itm.Cantidad = itm.Cantidad.RedondearDian();
                itm.BaseGeneral = itm.BaseGeneral.RedondearDian();
                foreach (var imp in itm.Impuestos)
                {
                    imp.Porcentaje = imp.Porcentaje.RedondearDian();
                    imp.Importe = imp.Importe.RedondearDian();
                    imp.BaseImponible = imp.BaseImponible.RedondearDian();
                    imp.ValorUnidadMedida = imp.ValorUnidadMedida.RedondearDian();
                }
            }

            var impuestos = tpe.Items.Where(x => !x.Excluido).SelectMany(x => x.Impuestos).ToList();
            var resumenImpuestos = impuestos.GroupBy(imp => imp.Tipo.Codigo).Select(grupo => new ResumenImpuesto
            {
                Tipo = grupo.Select(k => k.Tipo).FirstOrDefault(),
                Importe = grupo.Sum(k => k.Importe),
                Detalles = grupo.GroupBy(x => x.Porcentaje).Select(x => new Impuesto
                {
                    Tipo = x.Select(k => k.Tipo).FirstOrDefault(),
                    Importe = x.Sum(k => k.Importe),
                    Porcentaje = x.Select(k => k.Porcentaje).FirstOrDefault(),
                    BaseImponible = x.Sum(k => k.BaseImponible),
                    PorUnidad = x.Select(k => k.PorUnidad).FirstOrDefault(),
                    UnidadMedida = x.Select(k => k.UnidadMedida).FirstOrDefault(),
                    ValorUnidadMedida = x.Sum(k => k.ValorUnidadMedida)
                }).ToList()
            }).ToList();

            var totalValorUnit = tpe.Items.Sum(item => item.BaseGeneral).RedondearDian();
            var totalBaseImponible = tpe.Items.SelectMany(item => item.Impuestos).Sum(impuesto => impuesto.BaseImponible).RedondearDian();
            var totalImpuestos = tpe.Items.SelectMany(item => item.Impuestos).Sum(impuesto => impuesto.Importe).RedondearDian();

            // Como en la API anterior: los descuentos van por línea; el total no lleva descuentos globales.
            var totales = new Totales
            {
                ValorBruto = totalValorUnit,
                Descuentos = 0M.RedondearDian(),
                BaseImponible = totalBaseImponible,
                ValorBrutoConImpuestos = (totalValorUnit + totalImpuestos).RedondearDian(),
                TotalPagable = totalValorUnit.RedondearDian() + totalImpuestos
            };

            var extFabricante = new ExtensionFabricanteSoftware();
            extFabricante.ConInformacion(new ExtensionFabricanteSoftware.Informacion
            {
                NombreApellido = tpe.Emisor.RazonSocial,
                RazonSocial = tpe.Emisor.RazonSocial,
                NombreSoftware = "MatrixSQL"
            });

            var extBeneficios = new ExtensionBeneficiosComprador();
            extBeneficios.ConInformacion(new ExtensionBeneficiosComprador.Informacion
            {
                CodigoComprador = codigoComprador,
                NombresApellidos = nombreComprador,
                PuntosAcumulados = Math.Round(tpe.Totales.TotalPagable / 1000, 0).ToString("N0")
            });

            var tipoCaja = (tpe.InformacionCaja.TipoCaja ?? "Fija").Trim();
            var extCaja = new ExtensionCajaVenta();
            extCaja.ConInformacion(new ExtensionCajaVenta.Informacion
            {
                PlacaCaja = tpe.InformacionCaja.PlacaCaja,
                CodigoVenta = tpe.Numero,
                NombreCajero = tpe.InformacionCaja.NombreCajero,
                UbicacionCaja = tpe.InformacionCaja.UbicacionCaja,
                TipoCaja = tipoCaja == "" ? "Fija" : tipoCaja,
                SubTotal = tpe.Totales.TotalPagable
            });

            tpe.AddDatosSoftware(_tenant.Emisor.Nit, software.Identificador, software.PIN);
            tpe.Emisor.PrefijoRangoNumeracion = tpe.ExtensionDian.RangoNumeracion.Prefijo;
            if (tpe.Emisor.DireccionComercial == null)
            {
                tpe.Emisor.DireccionComercial = tpe.Emisor.DireccionFiscal;
            }

            var ambienteDestino = Habilitacion ? AmbienteDestino.PRUEBAS : AmbienteDestino.PRODUCCION;
            var generador = new GeneradorDocumentoEquivalente(ambienteDestino, TipoDocumentoEquivalente.POS)
                .ConOperacion(OperacionDocumentoEquivalente.GENERAL)
                .ConNumero(tpe.Numero)
                .ConFecha(tpe.FechaEmision)
                .ConVencimiento(tpe.FechaVencimiento)
                .ConNota(tpe.Nota)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(tpe.ExtensionDian)
                .AgregarExtension(extFabricante)
                .AgregarExtension(extBeneficios)
                .AgregarExtension(extCaja)
                .ConEmisor(tpe.Emisor)
                .ConAdquiriente(tpe.Adquiriente);

            generador.AgregarLineas(tpe.Items);
            generador.ConTotales(totales);
            generador.ConDetallePago(tpe.DetallePago);
            foreach (var resumen in resumenImpuestos)
            {
                generador.AgregarResumenImpuesto(resumen);
            }

            generador.AsignarCUDE(software.PIN);
            documento = generador.Obtener();
            textoQr = generador.StringQR;
            xml = documento.SerializeXmlDocument().ToXmlString();

            var firma = new FirmaElectronica { RolFirmante = RolFirmante.EMISOR, Certificado = _certificados.Obtener(_tenant) };
            return firma.FirmarDocumentoEquivalente(xml, DateTimeHelper.GetColombianDate(tpe.FechaEmision));
        }

        private InvoiceType? CargarFactura(string idDoc)
        {
            var ruta = _rutas.Xml(idDoc);
            return File.Exists(ruta) ? InvoiceType.DeserializeXmlString(File.ReadAllText(ruta)) : null;
        }

        private ApplicationResponseType? CargarAppResponse(string idDoc)
        {
            var ruta = _rutas.AppResponse(idDoc);
            return File.Exists(ruta) ? ApplicationResponseType.DeserializeXmlString(File.ReadAllText(ruta)) : null;
        }
    }
}
