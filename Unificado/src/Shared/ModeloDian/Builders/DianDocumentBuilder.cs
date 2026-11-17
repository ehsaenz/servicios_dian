using FE.Enum;
using FE.Models.Doc;
using BasicLibraryExt;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WS_V2_HttpClientLib.Builders
{
    /// <summary>
    /// Fachada de alto nivel para construir documentos electronicos sin exponer
    /// los detalles internos de DocumentoDian.
    /// </summary>
    public sealed class DianDocumentBuilder
    {
        private static string DeveloperKey => DianModelDoc.Servicios.SecretosLocales.Obtener("ModeloDian.ClaveDesarrollo");

        private readonly DocumentoDian documento;

        private DianDocumentBuilder(string tipoDocDian, string tipoDocErp, string numero, DateTime fecha)
        {
            documento = new DocumentoDian(DeveloperKey)
            {
                TipoDocDian = tipoDocDian,
                TipoDocErp = tipoDocErp,
                Numero = numero,
                FechaHoraDoc = fecha
            };

            if (tipoDocDian == "91" || tipoDocDian == "92")
            {
                documento.DocumentoRef = new documento_ref();
            }
        }

        public DocumentoDian Documento
        {
            get { return documento; }
        }

        public static DianDocumentBuilder Crear(string tipoDocDian, string tipoDocErp, string numero, DateTime fecha)
        {
            return new DianDocumentBuilder(tipoDocDian, tipoDocErp, numero, fecha);
        }

        public static DianDocumentBuilder FacturaVenta(string tipoDocErp, string numero, DateTime fecha)
        {
            return Crear("01", tipoDocErp, numero, fecha);
        }

        public static DianDocumentBuilder FacturaExportacion(string tipoDocErp, string numero, DateTime fecha)
        {
            return Crear("02", tipoDocErp, numero, fecha);
        }

        public static DianDocumentBuilder FacturaContingencia(string tipoDocErp, string numero, DateTime fecha)
        {
            return Crear("03", tipoDocErp, numero, fecha);
        }

        public static DianDocumentBuilder NotaCredito(string tipoDocErp, string numero, DateTime fecha)
        {
            return Crear("91", tipoDocErp, numero, fecha);
        }

        public static DianDocumentBuilder NotaDebito(string tipoDocErp, string numero, DateTime fecha)
        {
            return Crear("92", tipoDocErp, numero, fecha);
        }

        public DianDocumentBuilder ConModoEnvio(enmModo modo)
        {
            documento.ModoEnvio = modo;
            return this;
        }

        public DianDocumentBuilder ConResolucion(resolucion resolucion)
        {
            documento.Resolucion = resolucion;
            return this;
        }

        public DianDocumentBuilder ConResolucion(
            string numero,
            string prefijo,
            DateTime fechaExpedicion,
            DateTime fechaVencimiento,
            long desde,
            long hasta,
            string claveTecnica = "")
        {
            documento.Resolucion = new resolucion
            {
                NumeroResolucion = numero,
                Prefijo = prefijo,
                FechaExpedicion = fechaExpedicion,
                FechaVencimiento = fechaVencimiento,
                Desde = desde,
                Hasta = hasta,
                ClaveTecnica = claveTecnica
            };

            return this;
        }

        public DianDocumentBuilder ConParametrosFiscales(
            decimal baseReteIva,
            decimal baseReteFuente,
            decimal baseReteIca,
            decimal porcentajeReteIva,
            decimal porcentajeReteFuenteDeclarante,
            decimal porcentajeReteFuenteNoDeclarante,
            decimal porcentajeReteIca)
        {
            documento.ParametrosFiscales(
                baseReteIva,
                baseReteFuente,
                baseReteIca,
                porcentajeReteIva,
                porcentajeReteFuenteDeclarante,
                porcentajeReteFuenteNoDeclarante,
                porcentajeReteIca);

            return this;
        }

        public DianDocumentBuilder ConRetencionesReportadas(decimal reteIva, decimal reteFuente, decimal reteIca)
        {
            documento.ReteIVA = reteIva;
            documento.ReteFuente = reteFuente;
            documento.ReteICA = reteIca;
            return this;
        }

        public DianDocumentBuilder ConEmisor(persona emisor)
        {
            documento.Emisor = emisor;
            return this;
        }

        public DianDocumentBuilder ConReceptor(persona receptor)
        {
            documento.Receptor = receptor;
            return this;
        }

        public DianDocumentBuilder ConEmisorPersonaNatural(
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string primerNombre,
            string segundoNombre,
            string primerApellido,
            string segundoApellido,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return ConEmisor(CrearPersonaNatural(
                "EMISOR",
                codigo,
                matriculaMercantil,
                nit,
                dv,
                primerNombre,
                segundoNombre,
                primerApellido,
                segundoApellido,
                nombreComercial,
                nombreContacto,
                emailContacto,
                telefonoContacto,
                cargoContacto,
                responsabilidades,
                regimenIva));
        }

        public DianDocumentBuilder ConEmisorPersonaJuridica(
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string razonSocial,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return ConEmisor(CrearPersonaJuridica(
                "EMISOR",
                codigo,
                matriculaMercantil,
                nit,
                dv,
                razonSocial,
                nombreComercial,
                nombreContacto,
                emailContacto,
                telefonoContacto,
                cargoContacto,
                responsabilidades,
                regimenIva));
        }

        public DianDocumentBuilder ConReceptorPersonaNatural(
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string primerNombre,
            string segundoNombre,
            string primerApellido,
            string segundoApellido,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return ConReceptor(CrearPersonaNatural(
                "RECEPTOR",
                codigo,
                matriculaMercantil,
                nit,
                dv,
                primerNombre,
                segundoNombre,
                primerApellido,
                segundoApellido,
                nombreComercial,
                nombreContacto,
                emailContacto,
                telefonoContacto,
                cargoContacto,
                responsabilidades,
                regimenIva));
        }

        public DianDocumentBuilder ConReceptorPersonaJuridica(
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string razonSocial,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return ConReceptor(CrearPersonaJuridica(
                "RECEPTOR",
                codigo,
                matriculaMercantil,
                nit,
                dv,
                razonSocial,
                nombreComercial,
                nombreContacto,
                emailContacto,
                telefonoContacto,
                cargoContacto,
                responsabilidades,
                regimenIva));
        }

        public DianDocumentBuilder ConLocacionPrincipalEmisor(
            string direccion,
            string codigoMunicipio,
            string codigoPais = "CO",
            string nombreMunicipio = "",
            string codigoDepartamento = "",
            string nombreDepartamento = "",
            string nombrePais = "COLOMBIA")
        {
            if (documento.Emisor == null)
            {
                throw new InvalidOperationException("Debe agregar el emisor antes de asignar su locacion.");
            }

            documento.Emisor.LocacionPrincipal = CrearLocacion(
                direccion,
                codigoMunicipio,
                codigoPais,
                nombreMunicipio,
                codigoDepartamento,
                nombreDepartamento,
                nombrePais);

            return this;
        }

        public DianDocumentBuilder ConLocacionComercialEmisor(
            string direccion,
            string codigoMunicipio,
            string codigoPais = "CO",
            string nombreMunicipio = "",
            string codigoDepartamento = "",
            string nombreDepartamento = "",
            string nombrePais = "COLOMBIA")
        {
            if (documento.Emisor == null)
            {
                throw new InvalidOperationException("Debe agregar el emisor antes de asignar su locacion.");
            }

            documento.Emisor.LocacionComercial = CrearLocacion(
                direccion,
                codigoMunicipio,
                codigoPais,
                nombreMunicipio,
                codigoDepartamento,
                nombreDepartamento,
                nombrePais);

            return this;
        }

        public DianDocumentBuilder ConLocacionPrincipalReceptor(
            string direccion,
            string codigoMunicipio,
            string codigoPais = "CO",
            string nombreMunicipio = "",
            string codigoDepartamento = "",
            string nombreDepartamento = "",
            string nombrePais = "COLOMBIA")
        {
            if (documento.Receptor == null)
            {
                throw new InvalidOperationException("Debe agregar el receptor antes de asignar su locacion.");
            }

            documento.Receptor.LocacionPrincipal = CrearLocacion(
                direccion,
                codigoMunicipio,
                codigoPais,
                nombreMunicipio,
                codigoDepartamento,
                nombreDepartamento,
                nombrePais);

            return this;
        }

        public DianDocumentBuilder ConLocacionComercialReceptor(
            string direccion,
            string codigoMunicipio,
            string codigoPais = "CO",
            string nombreMunicipio = "",
            string codigoDepartamento = "",
            string nombreDepartamento = "",
            string nombrePais = "COLOMBIA")
        {
            if (documento.Receptor == null)
            {
                throw new InvalidOperationException("Debe agregar el receptor antes de asignar su locacion.");
            }

            documento.Receptor.LocacionComercial = CrearLocacion(
                direccion,
                codigoMunicipio,
                codigoPais,
                nombreMunicipio,
                codigoDepartamento,
                nombreDepartamento,
                nombrePais);

            return this;
        }

        public DianDocumentBuilder ConFormaPago(
            MetodoPagoEnum metodoPago,
            string codigoMedioPago,
            int plazo = 0,
            string identificadorPago = "")
        {
            documento.FormaPago.ID = metodoPago;
            documento.FormaPago.CodigoMedioPago = codigoMedioPago;
            documento.FormaPago.Plazo = plazo;
            documento.FormaPago.IdenficadorPago = identificadorPago;
            return this;
        }

        public DianDocumentBuilder ConDocumentoReferencia(
            string numero,
            string cufe,
            DateTime fecha,
            string descripcion = "")
        {
            documento.DocumentoRef = new documento_ref
            {
                Numero = numero,
                CUFE = cufe,
                Fecha = fecha,
                Descripcion = descripcion
            };

            return this;
        }

        public DianDocumentBuilder ConOrdenReferencia(string numero, DateTime fecha, string descripcion = "")
        {
            documento.OrdenRef = new orden_ref
            {
                Numero = numero,
                Fecha = fecha,
                Descripcion = descripcion
            };

            return this;
        }

        public DianDocumentBuilder AgregarNota(string nota)
        {
            documento.AddNota(nota);
            return this;
        }

        public DianDocumentBuilder ConNotaPrincipal(string nota)
        {
            documento.SetNota(nota);
            return this;
        }

        public DianDocumentBuilder ConVariable(string nombre, string valor)
        {
            documento.SetVariable(nombre, valor);
            return this;
        }

        public DianDocumentBuilder AgregarItem(
            string codigo,
            string referencia,
            string detalle,
            decimal cantidad,
            decimal valorUnitario,
            decimal iva = 0.19M,
            decimal descuento = 0M,
            bool excluido = false)
        {
            var item = new item(excluido)
            {
                ID = documento.Items.Count + 1,
                Codigo = codigo,
                Referencia = referencia,
                Detalle = detalle,
                Cant = cantidad,
                ValorUnit = valorUnitario,
                TarifaIVA = iva,
                DtoDir = descuento
            };

            if (!excluido)
            {
                item.AddImpuesto("01", iva);
            }

            documento.Items.Add(item);
            return this;
        }

        public DianDocumentBuilder AgregarItem(item item)
        {
            if (item.ID == 0)
            {
                item.ID = documento.Items.Count + 1;
            }

            documento.Items.Add(item);
            return this;
        }

        public DianDocumentBuilder AgregarImpuestoAlUltimoItem(string codigoImpuesto, decimal tarifa)
        {
            var ultimoItem = documento.Items.LastOrDefault();
            if (ultimoItem == null)
            {
                throw new InvalidOperationException("No hay items para agregar impuestos.");
            }

            ultimoItem.AddImpuesto(codigoImpuesto, tarifa);
            return this;
        }

        public DocumentoDian Construir(bool validar = true)
        {
            Preparar(validar);
            return documento;
        }

        public bool EsValido()
        {
            Preparar(true);
            return documento.Errores.Count == 0;
        }

        public string GenerarJson(bool validar = true)
        {
            Preparar(validar);
            if (validar && documento.Errores.Count > 0)
            {
                return string.Empty;
            }

            return documento.ToJson();
        }

        private void Preparar(bool validar)
        {
            documento.Errores.Clear();
            documento.Totalizar();

            if (validar)
            {
                documento.Validate();
            }
        }

        private static persona CrearPersonaNatural(
            string rol,
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string primerNombre,
            string segundoNombre,
            string primerApellido,
            string segundoApellido,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return new persona
            {
                Rol = rol,
                TipoPersona = "NATURAL",
                TipoIdDian = "13",
                Codigo = codigo,
                MatriculaMercantil = matriculaMercantil,
                NIT = nit,
                DV = dv,
                PrimerNombre = primerNombre,
                SegundoNombre = segundoNombre,
                PrimerApellido = primerApellido,
                SegundoApellido = segundoApellido,
                NombreComercial = nombreComercial,
                NombreContacto = nombreContacto,
                EmailContacto = emailContacto,
                TelefonoContacto = telefonoContacto,
                CargoContacto = cargoContacto,
                responsabilidades_fiscales = ToResponsabilidades(responsabilidades),
                RegimenIVA = regimenIva
            };
        }

        private static persona CrearPersonaJuridica(
            string rol,
            string codigo,
            string matriculaMercantil,
            string nit,
            string dv,
            string razonSocial,
            string nombreComercial,
            string nombreContacto,
            string emailContacto,
            string telefonoContacto,
            string cargoContacto,
            IEnumerable<responsabilidad> responsabilidades,
            string regimenIva)
        {
            return new persona
            {
                Rol = rol,
                TipoPersona = "JURIDICA",
                TipoIdDian = "31",
                Codigo = codigo,
                MatriculaMercantil = matriculaMercantil,
                NIT = nit,
                DV = dv,
                RazonSocial = razonSocial,
                NombreComercial = nombreComercial,
                NombreContacto = nombreContacto,
                EmailContacto = emailContacto,
                TelefonoContacto = telefonoContacto,
                CargoContacto = cargoContacto,
                responsabilidades_fiscales = ToResponsabilidades(responsabilidades),
                RegimenIVA = regimenIva
            };
        }

        private static List<responsabilidad> ToResponsabilidades(IEnumerable<responsabilidad> responsabilidades)
        {
            return responsabilidades == null
                ? new List<responsabilidad>()
                : new List<responsabilidad>(responsabilidades);
        }

        private static LocacionFisica CrearLocacion(
            string direccion,
            string codigoMunicipio,
            string codigoPais,
            string nombreMunicipio,
            string codigoDepartamento,
            string nombreDepartamento,
            string nombrePais)
        {
            return new LocacionFisica
            {
                Direccion = direccion,
                CodigoMunicipio = codigoMunicipio,
                CodigoPais = codigoPais,
                NombreMunicipio = nombreMunicipio,
                CodigoDepartamento = codigoDepartamento,
                NombreDepartamento = nombreDepartamento,
                NombrePais = nombrePais
            };
        }
    }
}
