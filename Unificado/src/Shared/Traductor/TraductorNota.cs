using System;
using System.Linq;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Tipos.Standard;
using FacturacionColombia_Metadatos;
using FE.Models.Doc;

namespace DIAN.Traduccion
{
    /// <summary>
    /// Traduce las notas crédito (91) y débito (92) del ERP. Comparten con la factura los terceros, líneas, impuestos y totales;
    /// la diferencia es el documento que referencian (DocumentoRef) y el concepto de la nota.
    /// </summary>
    public static class TraductorNota
    {
        public static CreditNoteType NotaCredito(DocumentoDian doc, ParametrosEmision parametros)
        {
            Validar(doc, parametros, "91");
            var concepto = ConceptoCredito(doc.DocumentoRef.Concepto);

            var generador = new GeneradorNotaCredito(parametros.Ambiente, concepto, Referencia(doc))
                .ConNumero(doc.Numero)
                .ConFecha(doc.FechaHoraDoc)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(TraductorFactura.Extension(doc, parametros))
                .ConEmisor(TraductorFactura.Emisor(doc.Emisor, doc.Resolucion.Prefijo))
                .ConAdquiriente(TraductorFactura.Adquiriente(doc.Receptor));

            var notas = Notas(doc);
            if (notas.Count > 0)
            {
                generador.ConNota(notas[0]);
                foreach (var nota in notas.Skip(1)) generador.AgregarNota(nota);
            }

            foreach (var item in doc.Items) generador.AgregarLinea(TraductorFactura.Linea(item));
            foreach (var resumen in TraductorFactura.Resumenes(doc)) generador.AgregarResumenImpuesto(resumen);

            return generador
                .ConTotales(TraductorFactura.Totales(doc))
                .ConDetallePago(TraductorFactura.DetallePago(doc))
                .AsignarCUDE(parametros.SoftwarePin)
                .Obtener();
        }

        public static DebitNoteType NotaDebito(DocumentoDian doc, ParametrosEmision parametros)
        {
            Validar(doc, parametros, "92");
            var concepto = ConceptoDebito(doc.DocumentoRef.Concepto);

            var generador = new GeneradorNotaDebito(parametros.Ambiente, concepto, Referencia(doc))
                .ConNumero(doc.Numero)
                .ConFecha(doc.FechaHoraDoc)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(TraductorFactura.Extension(doc, parametros))
                .ConEmisor(TraductorFactura.Emisor(doc.Emisor, doc.Resolucion.Prefijo))
                .ConAdquiriente(TraductorFactura.Adquiriente(doc.Receptor));

            var notas = Notas(doc);
            if (notas.Count > 0)
            {
                generador.ConNota(notas[0]);
                foreach (var nota in notas.Skip(1)) generador.AgregarNota(nota);
            }

            foreach (var item in doc.Items) generador.AgregarLinea(TraductorFactura.Linea(item));
            foreach (var resumen in TraductorFactura.Resumenes(doc)) generador.AgregarResumenImpuesto(resumen);

            return generador
                .ConTotales(TraductorFactura.Totales(doc))
                .ConDetallePago(TraductorFactura.DetallePago(doc))
                .AsignarCUDE(parametros.SoftwarePin)
                .Obtener();
        }

        private static System.Collections.Generic.List<string> Notas(DocumentoDian doc) =>
            doc.Notas.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        private static void Validar(DocumentoDian doc, ParametrosEmision parametros, string tipoEsperado)
        {
            if (doc == null) throw new DocumentoNoTraducibleException("Falta el documento.");
            if (doc.TipoDocDian != tipoEsperado) throw new DocumentoNoTraducibleException($"Se esperaba el tipo {tipoEsperado}; llegó el tipo {doc.TipoDocDian}.");
            if (string.IsNullOrWhiteSpace(doc.Numero)) throw new DocumentoNoTraducibleException("El documento no trae número.");
            if (doc.Emisor == null || doc.Receptor == null) throw new DocumentoNoTraducibleException("El documento necesita emisor y receptor.");
            if (doc.Resolucion == null || string.IsNullOrWhiteSpace(doc.Resolucion.NumeroResolucion)) throw new DocumentoNoTraducibleException("El documento no trae la resolución de numeración.");
            if (doc.Items == null || doc.Items.Count == 0) throw new DocumentoNoTraducibleException("El documento no tiene líneas.");
            if (doc.FormaPago == null) throw new DocumentoNoTraducibleException("El documento no trae la forma de pago.");
            if (doc.DocumentoRef == null || string.IsNullOrWhiteSpace(doc.DocumentoRef.Numero) || string.IsNullOrWhiteSpace(doc.DocumentoRef.CUFE))
            {
                throw new DocumentoNoTraducibleException("La nota debe referenciar el documento origen (número y CUFE).");
            }

            if (string.IsNullOrWhiteSpace(parametros.SoftwareIdentificador) || string.IsNullOrWhiteSpace(parametros.SoftwarePin) || string.IsNullOrWhiteSpace(parametros.SoftwareNit))
            {
                throw new DocumentoNoTraducibleException("Faltan los datos del software DIAN del cliente.");
            }
        }

        private static ReferenciaDocumento Referencia(DocumentoDian doc) => new ReferenciaDocumento
        {
            Numero = doc.DocumentoRef.Numero,
            UUID = doc.DocumentoRef.CUFE,
            // El documento origen de una nota es una factura (CUFE). Referenciar otra nota no está soportado.
            AlgoritmoCufe = AlgoritmoSeguridadUUID.CUFE_SHA384,
            TipoDocumento = TipoDocumentoElectronico.FACTURA_VENTA_NACIONAL,
            Fecha = doc.DocumentoRef.Fecha
        };

        private static ConceptoNotaCredito ConceptoCredito(string codigo)
        {
            switch ((codigo ?? string.Empty).Trim())
            {
                case "":
                case "1": return ConceptoNotaCredito.DEVOLUCION_PARCIAL;
                case "2": return ConceptoNotaCredito.ANULACION_TOTAL;
                case "3": return ConceptoNotaCredito.REBAJA_O_DESCUENTO;
                case "4": return ConceptoNotaCredito.AJUSTE_PRECIO;
                case "5": return ConceptoNotaCredito.DESCUENTO_COMERCIAL_PRONTO_PAGO;
                case "6": return ConceptoNotaCredito.DESCUENTO_COMERCIAL_VOLUMEN_VENTAS;
                default: throw new DocumentoNoTraducibleException($"Concepto de nota crédito desconocido: {codigo}.");
            }
        }

        private static ConceptoNotaDebito ConceptoDebito(string codigo)
        {
            switch ((codigo ?? string.Empty).Trim())
            {
                case "1": return ConceptoNotaDebito.INTERESES;
                case "2": return ConceptoNotaDebito.GASTOS_COBRAR;
                case "3": return ConceptoNotaDebito.CAMBIO_VALOR;
                case "":
                case "4": return ConceptoNotaDebito.OTROS;
                default: throw new DocumentoNoTraducibleException($"Concepto de nota débito desconocido: {codigo}.");
            }
        }
    }
}
