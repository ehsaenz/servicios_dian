using System;
using System.Collections.Generic;
using System.Linq;
using DianModelDoc;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Tipos.Standard;
using FacturacionColombia_Metadatos;
using FE.Enum;
using FE.Models.Doc;

namespace DIAN.Traduccion
{
    /// <summary>Datos de la emisión que no vienen en el documento del ERP: el software DIAN del cliente y el ambiente.</summary>
    public sealed class ParametrosEmision
    {
        public AmbienteDestino Ambiente { get; set; } = AmbienteDestino.PRUEBAS;

        /// <summary>NIT del proveedor del software (sin dígito de verificación).</summary>
        public string SoftwareNit { get; set; } = string.Empty;

        public string SoftwareIdentificador { get; set; } = string.Empty;
        public string SoftwarePin { get; set; } = string.Empty;
    }

    /// <summary>El documento del ERP no se puede traducir a un documento electrónico válido.</summary>
    public sealed class DocumentoNoTraducibleException : Exception
    {
        public DocumentoNoTraducibleException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Traduce un <see cref="DocumentoDian"/> (el JSON que arma QuantyERP) a la factura electrónica de venta UBL de la solución.
    /// Los importes salen de las cuentas del propio modelo (item.BaseGral, impuestos por línea), las mismas con las que el ERP
    /// valida y las que la API vieja ya enviaba a la DIAN: el traductor no recalcula impuestos por su cuenta.
    /// </summary>
    public static class TraductorFactura
    {
        public static InvoiceType Factura(DocumentoDian doc, ParametrosEmision parametros)
        {
            Validar(doc, parametros);

            var generador = new GeneradorFactura(parametros.Ambiente, TipoFactura.FACTURA_VENTA)
                .ConNumero(doc.Numero)
                .ConFecha(doc.FechaHoraDoc)
                .ConVencimiento(doc.FormaPago.FechaVencimiento)
                .ConMoneda(Moneda.PESO_COLOMBIANO)
                .ConExtensionDian(Extension(doc, parametros))
                .ConEmisor(Emisor(doc.Emisor, doc.Resolucion.Prefijo))
                .ConAdquiriente(Adquiriente(doc.Receptor));

            var notas = doc.Notas.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (notas.Count > 0)
            {
                generador.ConNota(notas[0]);
                foreach (var nota in notas.Skip(1))
                {
                    generador.AgregarNota(nota);
                }
            }

            if (doc.OrdenRef != null && !string.IsNullOrWhiteSpace(doc.OrdenRef.Numero))
            {
                generador.ConOrdenCompra(new OrdenCompra { Numero = doc.OrdenRef.Numero, Fecha = doc.OrdenRef.Fecha });
            }

            foreach (var item in doc.Items)
            {
                generador.AgregarLinea(Linea(item));
            }

            var resumenes = Resumenes(doc);
            foreach (var resumen in resumenes)
            {
                generador.AgregarResumenImpuesto(resumen);
            }

            return generador
                .ConTotales(Totales(doc))
                .ConDetallePago(DetallePago(doc))
                .AsignarCUFE(doc.Resolucion.ClaveTecnica, parametros.SoftwarePin)
                .Obtener();
        }

        private static void Validar(DocumentoDian doc, ParametrosEmision parametros)
        {
            if (doc == null) throw new DocumentoNoTraducibleException("Falta el documento.");
            if (doc.TipoDocDian != "01") throw new DocumentoNoTraducibleException($"Por ahora solo se traduce la factura de venta (01); llegó el tipo {doc.TipoDocDian}.");
            if (string.IsNullOrWhiteSpace(doc.Numero)) throw new DocumentoNoTraducibleException("El documento no trae número.");
            if (doc.Emisor == null || doc.Receptor == null) throw new DocumentoNoTraducibleException("El documento necesita emisor y receptor.");
            if (doc.Resolucion == null || string.IsNullOrWhiteSpace(doc.Resolucion.NumeroResolucion)) throw new DocumentoNoTraducibleException("El documento no trae la resolución de numeración.");
            if (string.IsNullOrWhiteSpace(doc.Resolucion.ClaveTecnica)) throw new DocumentoNoTraducibleException("La resolución no trae la clave técnica.");
            if (doc.Items == null || doc.Items.Count == 0) throw new DocumentoNoTraducibleException("El documento no tiene líneas.");
            if (doc.FormaPago == null) throw new DocumentoNoTraducibleException("El documento no trae la forma de pago.");
            if (string.IsNullOrWhiteSpace(parametros.SoftwareIdentificador) || string.IsNullOrWhiteSpace(parametros.SoftwarePin) || string.IsNullOrWhiteSpace(parametros.SoftwareNit))
            {
                throw new DocumentoNoTraducibleException("Faltan los datos del software DIAN del cliente.");
            }
        }

        internal static ExtensionDian Extension(DocumentoDian doc, ParametrosEmision parametros)
        {
            var r = doc.Resolucion;
            return new ExtensionDian
            {
                RangoNumeracion = new RangoNumeracion
                {
                    NumeroResolucion = long.Parse(r.NumeroResolucion.Trim()),
                    FechaResolucion = r.FechaExpedicion,
                    Prefijo = r.Prefijo,
                    Desde = r.Desde,
                    Hasta = r.Hasta,
                    VigenciaDesde = r.FechaExpedicion,
                    VigenciaHasta = r.FechaVencimiento
                },
                PaisOrigen = Pais.COLOMBIA,
                SoftwareProveedorNit = parametros.SoftwareNit,
                SoftwareIdentificador = parametros.SoftwareIdentificador,
                SoftwarePin = parametros.SoftwarePin
            };
        }

        // ---------- terceros ----------

        private static bool EsJuridica(persona p) => p.IdTipoPersona == "1";

        private static string Responsabilidades(persona p)
        {
            // Solo van al TaxLevelCode los códigos de responsabilidad fiscal (O-13, O-15, R-99-PN...). Los marcadores "48"
            // (responsable de IVA) y "49" del modelo del ERP no son códigos de la DIAN: solo definen el tributo (IVA o ZZ).
            var codigos = p.responsabilidades_fiscales
                .Select(r => string.IsNullOrWhiteSpace(r.CodigoFE) ? r.Codigo : r.CodigoFE)
                .Where(c => !string.IsNullOrWhiteSpace(c) && (c.StartsWith("O-") || c.StartsWith("R-")))
                .Distinct()
                .ToList();
            return codigos.Count == 0 ? "R-99-PN" : string.Join(";", codigos);
        }

        private static ResponsabilidadTributaria Tributo(persona p) => ResponsabilidadTributaria.GetFromCodigo(p.Resp_IVA ? "01" : "ZZ");

        private static DireccionFisica Direccion(LocacionFisica? l, LocacionFisica? respaldo = null)
        {
            l ??= respaldo;
            if (l == null || string.IsNullOrWhiteSpace(l.CodigoMunicipio))
            {
                throw new DocumentoNoTraducibleException("Falta la locación (dirección y código de municipio) de un tercero.");
            }

            var municipio = l.CodigoMunicipio.Trim();
            var codigoDepartamento = string.IsNullOrWhiteSpace(l.CodigoDepartamento) ? municipio.Substring(0, 2) : l.CodigoDepartamento.Trim();
            var nombreMunicipio = string.IsNullOrWhiteSpace(l.NombreMunicipio)
                ? DianModelDoc.Servicios.Catalogos.Municipios.FirstOrDefault(m => m.Codigo == municipio)?.Nombre ?? municipio
                : l.NombreMunicipio;
            var nombreDepartamento = string.IsNullOrWhiteSpace(l.NombreDepartamento)
                ? DianModelDoc.Servicios.Catalogos.Departamentos.FirstOrDefault(d => d.Codigo == codigoDepartamento)?.Nombre ?? codigoDepartamento
                : l.NombreDepartamento;

            return new DireccionFisica
            {
                Departamento = new Departamento { Codigo = codigoDepartamento, Nombre = nombreDepartamento },
                Municipio = new Municipio { Codigo = municipio, Nombre = nombreMunicipio },
                Linea = l.Direccion ?? string.Empty,
                Pais = Pais.COLOMBIA
            };
        }

        private static Persona? PersonaNatural(persona p) => EsJuridica(p) ? null : new Persona
        {
            PrimerNombre = p.PrimerNombre,
            SegundoNombre = p.SegundoNombre,
            PrimerApellido = p.PrimerApellido,
            SegundoApellido = p.SegundoApellido,
            NumeroIdentificacion = p.NIT
        };

        internal static Emisor Emisor(persona p, string prefijo)
        {
            var principal = Direccion(p.LocacionPrincipal);
            var emisor = new Emisor
            {
                Identificacion = p.NIT,
                DV = p.DV,
                RazonSocial = p.Nombre_o_RazonSocial,
                DireccionFiscal = principal,
                TipoPersona = EsJuridica(p) ? FacturacionColombia_Metadatos.TipoPersona.JURIDICA : FacturacionColombia_Metadatos.TipoPersona.NATURAL,
                RegimenFiscal = new RegimenFiscal { Codigo = Responsabilidades(p), Descripcion = "Responsabilidades fiscales del emisor" },
                ResponsabilidadTributaria = Tributo(p),
                PrefijoRangoNumeracion = prefijo,
                CorreoElectronico = p.EmailContacto,
                NumeroTelefonico = p.TelefonoContacto,
                Persona = PersonaNatural(p),
                DatosComerciales = new DatosComerciales
                {
                    CodigoCliente = p.Codigo ?? string.Empty,
                    MatriculaMercantil = p.MatriculaMercantil ?? string.Empty,
                    Direccion = Direccion(p.LocacionComercial, p.LocacionPrincipal),
                    // GeneradorFactura toma el nombre del emisor de este campo (PartyName y RegistrationName):
                    // debe ser la razón social tal como está en el RUT.
                    NombreComercial = p.Nombre_o_RazonSocial
                }
            };

            return emisor;
        }

        internal static Adquiriente Adquiriente(persona p)
        {
            var principal = Direccion(p.LocacionPrincipal);
            return new Adquiriente
            {
                TipoIdentificacion = TipoIdentificacion.GetFromCodigo(string.IsNullOrWhiteSpace(p.TipoIdDian) ? "31" : p.TipoIdDian),
                Identificacion = p.NIT,
                RazonSocial = p.Nombre_o_RazonSocial,
                CodigoCliente = p.Codigo ?? string.Empty,
                NombreComercial = p.Nombre_o_RazonSocial,
                DireccionComercial = Direccion(p.LocacionComercial, p.LocacionPrincipal),
                DireccionFiscal = principal,
                TipoPersona = EsJuridica(p) ? FacturacionColombia_Metadatos.TipoPersona.JURIDICA : FacturacionColombia_Metadatos.TipoPersona.NATURAL,
                RegimenFiscal = new RegimenFiscal { Codigo = Responsabilidades(p), Descripcion = "Responsabilidades fiscales del adquiriente" },
                ResponsabilidadTributaria = Tributo(p),
                MatriculaMercantil = string.IsNullOrWhiteSpace(p.MatriculaMercantil) ? "00000" : p.MatriculaMercantil,
                CorreoElectronico = p.EmailContacto,
                NumeroTelefonico = p.TelefonoContacto,
                Persona = PersonaNatural(p)
            };
        }

        // ---------- líneas e impuestos ----------

        // El modelo guarda las tarifas como fracción (0,19); la DIAN las pide en porcentaje (19,00).
        private static decimal Porcentaje(decimal fraccion) => Math.Round(fraccion * 100m, 2);

        internal static Linea Linea(item i)
        {
            var linea = new Linea
            {
                Cantidad = i.Cant,
                CodigoProducto = new CodigoProducto
                {
                    Tipo = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO"),
                    Valor = string.IsNullOrWhiteSpace(i.Codigo) ? i.Referencia : i.Codigo
                },
                Descripcion = i.Detalle,
                PrecioUnitario = i.ValorUnit,
                // LineExtensionAmount: cantidad x precio menos el descuento de la línea.
                BaseGeneral = i.BaseGral
            };

            var descuento = i.SubTotal - i.BaseGral;
            if (descuento > 0m)
            {
                linea.Descuentos.Add(new Descuento
                {
                    Razon = "Descuento",
                    Porcentaje = Porcentaje(i.DtoDir),
                    Monto = descuento,
                    Base = i.SubTotal
                });
            }

            if (!i.Excluido)
            {
                foreach (var impuesto in i.impuestos)
                {
                    linea.Impuestos.Add(ImpuestoLinea(impuesto));
                }
            }

            foreach (var retencion in i.retenciones)
            {
                linea.Impuestos.Add(ImpuestoLinea(retencion));
            }

            return linea;
        }

        private static Impuesto ImpuestoLinea(impuesto i) => new Impuesto
        {
            Tipo = FacturacionColombia_Metadatos.TipoImpuesto.GetFromCodigo(i.ID),
            Porcentaje = Porcentaje(i.Porcentaje),
            BaseImponible = i.ValorBase,
            Importe = i.ValorImp
        };

        internal static IEnumerable<ResumenImpuesto> Resumenes(DocumentoDian doc)
        {
            // Impuestos del documento: se agrupan las líneas por tipo de impuesto y tarifa.
            var normales = doc.Items.Where(i => !i.Excluido).SelectMany(i => i.impuestos)
                .GroupBy(i => i.ID)
                .Select(grupo => Resumen(grupo.Key, grupo));
            var retenidos = doc.Items.SelectMany(i => i.retenciones)
                .GroupBy(i => i.ID)
                .Select(grupo => Resumen(grupo.Key, grupo));
            return normales.Concat(retenidos).ToList();
        }

        private static ResumenImpuesto Resumen(string id, IEnumerable<impuesto> impuestos)
        {
            var detalles = impuestos
                .GroupBy(i => i.Porcentaje)
                .Select(g => new Impuesto
                {
                    Tipo = FacturacionColombia_Metadatos.TipoImpuesto.GetFromCodigo(id),
                    Porcentaje = Porcentaje(g.Key),
                    BaseImponible = g.Sum(x => x.ValorBase),
                    Importe = g.Sum(x => x.ValorImp)
                })
                .ToList();

            return new ResumenImpuesto
            {
                Tipo = FacturacionColombia_Metadatos.TipoImpuesto.GetFromCodigo(id),
                Importe = detalles.Sum(d => d.Importe),
                Detalles = detalles
            };
        }

        // ---------- totales y pago ----------

        internal static Totales Totales(DocumentoDian doc)
        {
            var bruto = doc.Items.Sum(i => i.BaseGral);
            var impuestos = doc.Items.Where(i => !i.Excluido).SelectMany(i => i.impuestos).Sum(i => i.ValorImp);

            // Las retenciones no reducen el total de la factura electrónica: se informan aparte (WithholdingTaxTotal).
            return new Totales
            {
                ValorBruto = bruto,
                BaseImponible = bruto,
                ValorBrutoConImpuestos = bruto + impuestos,
                TotalPagable = bruto + impuestos
            };
        }

        internal static DetallePago DetallePago(DocumentoDian doc)
        {
            var pago = new DetallePago
            {
                Forma = doc.FormaPago.ID == MetodoPagoEnum.Credito ? FormaPago.CREDITO : FormaPago.CONTADO,
                Metodo = MetodoPago.GetFromCodigo(string.IsNullOrWhiteSpace(doc.FormaPago.CodigoMedioPago) ? "10" : doc.FormaPago.CodigoMedioPago),
                Fecha = doc.FormaPago.FechaVencimiento
            };

            if (!string.IsNullOrWhiteSpace(doc.FormaPago.IdenficadorPago))
            {
                pago.Identificador = doc.FormaPago.IdenficadorPago;
            }

            return pago;
        }
    }
}
