using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Documentos.Extensiones;
using eFacturacionColombia_NETCore.Firma;
using eFacturacionColombia_NETCore.ServiciosWeb;
using eFacturacionColombia_NETCore.ServiciosWeb.DianServices;
using eFacturacionColombia_NETCore.Tipos.Standard;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;

// Prueba de punta a punta contra el AMBIENTE DE HABILITACIÓN de la DIAN, con las bibliotecas nuevas de
// Unificado\src\Shared: genera una factura SETP, la firma con el certificado de MARLIT y la envía al set de pruebas.
//
//   generar  --consecutivo N [--provisional]     genera y firma en local; no envía nada
//   enviar   --consecutivo N --confirmar         además la envía al set de pruebas de habilitación
//
// - Siempre apunta a habilitación. Si el endpoint no es el de habilitación, aborta.
// - Los datos salen de Unificado\config-local (fuera del repositorio). La clave del certificado se toma de la
//   variable de entorno indicada en emisor-marlit.json (MARLIT_P12) o, si no existe, de FIRMA_PFX_CLAVE. Nunca se imprime.
// - --provisional rellena con valores de relleno los datos del RUT que falten. Solo vale para generar: enviar los exige completos.
namespace Habilitacion.Pruebas
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static int Main(string[] args)
        {
            try
            {
                var modo = args.Length > 0 ? args[0] : "";
                var provisional = args.Contains("--provisional");
                var confirmar = args.Contains("--confirmar");
                var consecutivo = ObtenerValor(args, "--consecutivo");

                if (modo == "consultar")
                {
                    var zipKey = ObtenerValor(args, "--zipkey");
                    if (string.IsNullOrEmpty(zipKey))
                    {
                        Console.Error.WriteLine("uso: consultar --zipkey <ZipKey devuelto por el envío>");
                        return 2;
                    }

                    return Consultar(zipKey);
                }

                if (modo == "rangos")
                {
                    return ConsultarRangos();
                }

                var esEnvio = modo == "enviar" || modo == "enviar-sync";

                if ((modo != "generar" && !esEnvio) || string.IsNullOrEmpty(consecutivo))
                {
                    Console.Error.WriteLine("uso: generar --consecutivo N [--provisional] | enviar --consecutivo N --confirmar | enviar-sync --consecutivo N --confirmar | consultar --zipkey ID");
                    return 2;
                }

                if (esEnvio && !confirmar)
                {
                    Console.Error.WriteLine("Para enviar a la DIAN hay que pasar --confirmar de forma explícita.");
                    return 2;
                }

                if (esEnvio && provisional)
                {
                    Console.Error.WriteLine("No se envía con datos provisionales. Completa config-local\\emisor-rut.json.");
                    return 2;
                }

                var tipo = ObtenerValor(args, "--tipo") ?? "factura";
                var referencia = ObtenerValor(args, "--referencia");
                if (tipo != "factura" && tipo != "factura-erp" && tipo != "nota-credito-erp" && tipo != "nota-debito-erp" && tipo != "nota-credito" && tipo != "nota-debito" && tipo != "documento-soporte" && tipo != "nota-ajuste-ds" && tipo != "nomina" && tipo != "evento" && tipo != "documento-equivalente" && tipo != "nomina-ajuste")
                {
                    Console.Error.WriteLine("--tipo debe ser factura, nota-credito, nota-debito, documento-soporte, nota-ajuste-ds, nomina, nomina-ajuste, evento o documento-equivalente.");
                    return 2;
                }

                if ((tipo == "nota-credito" || tipo == "nota-debito" || tipo == "nota-credito-erp" || tipo == "nota-debito-erp" || tipo == "nota-ajuste-ds" || tipo == "evento" || tipo == "nomina-ajuste") && string.IsNullOrEmpty(referencia))
                {
                    Console.Error.WriteLine("Las notas necesitan --referencia <número de la factura ya generada, p. ej. SETP990000504>.");
                    return 2;
                }

                return Ejecutar(modo, long.Parse(consecutivo), provisional, tipo, referencia);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                foreach (var linea in (ex.StackTrace ?? "").Split('\n').Take(4))
                {
                    Console.Error.WriteLine(linea.TrimEnd());
                }
                return 1;
            }
        }

        // Consulta en HABILITACIÓN los rangos de numeración autorizados para el NIT y el software (solo lectura).
        // No imprime la clave técnica.
        private static int ConsultarRangos()
        {
            var carpeta = BuscarConfigLocal();
            var emisorCfg = Leer<EmisorCfg>(carpeta, "emisor-marlit.json");
            var hab = Leer<HabilitacionCfg>(carpeta, "habilitacion-sw-propio.json");
            var clave = Environment.GetEnvironmentVariable(emisorCfg.Certificado.VariableEntornoClave)
                        ?? Environment.GetEnvironmentVariable("FIRMA_PFX_CLAVE");
            if (string.IsNullOrEmpty(clave))
            {
                throw new InvalidOperationException("No hay clave del certificado en la variable " + emisorCfg.Certificado.VariableEntornoClave + " ni en FIRMA_PFX_CLAVE.");
            }

            if (!ClienteServicioDian.ENDPOINT_PRUEBAS.Contains("-hab."))
            {
                throw new InvalidOperationException("El endpoint de pruebas no es el de habilitación; se aborta.");
            }

            var cliente = new ClienteServicioDian
            {
                Ambiente = AmbienteServicio.PRUEBAS,
                RutaCertificado = ResolverRutaCertificado(carpeta, emisorCfg.Certificado.Ruta),
                ClaveCertificado = clave
            };

            // HAB_SOFTWARE permite consultar con el software de otro documento (documento soporte, POS...).
            var softwareConsulta = Environment.GetEnvironmentVariable("HAB_SOFTWARE") ?? hab.InformacionSoftware.Identificacion;
            Console.WriteLine("Software consultado: " + softwareConsulta);
            var respuesta = cliente.ObtenerRangosNumeracion(emisorCfg.Nit, softwareConsulta);
            Console.WriteLine("(" + respuesta.OperationCode + ") " + respuesta.OperationDescription);
            if (respuesta.ResponseList != null)
            {
                foreach (var r in respuesta.ResponseList)
                {
                    Console.WriteLine("resolución {0}  fecha {1}  prefijo {2}  {3}-{4}  vigencia {5} a {6}",
                        r.ResolutionNumber, r.ResolutionDate, r.Prefix, r.FromNumber, r.ToNumber, r.ValidDateFrom, r.ValidDateTo);
                }
            }

            return 0;
        }

        // Consulta el resultado de un lote ya enviado a HABILITACIÓN (solo lectura; no envía documentos).
        private static int Consultar(string zipKey)
        {
            var carpeta = BuscarConfigLocal();
            var emisorCfg = Leer<EmisorCfg>(carpeta, "emisor-marlit.json");
            var clave = Environment.GetEnvironmentVariable(emisorCfg.Certificado.VariableEntornoClave)
                        ?? Environment.GetEnvironmentVariable("FIRMA_PFX_CLAVE");
            if (string.IsNullOrEmpty(clave))
            {
                throw new InvalidOperationException("No hay clave del certificado en la variable " + emisorCfg.Certificado.VariableEntornoClave + " ni en FIRMA_PFX_CLAVE.");
            }

            if (!ClienteServicioDian.ENDPOINT_PRUEBAS.Contains("-hab."))
            {
                throw new InvalidOperationException("El endpoint de pruebas no es el de habilitación; se aborta.");
            }

            var cliente = new ClienteServicioDian
            {
                Ambiente = AmbienteServicio.PRUEBAS,
                RutaCertificado = ResolverRutaCertificado(carpeta, emisorCfg.Certificado.Ruta),
                ClaveCertificado = clave
            };

            DianResponse respuesta = null;
            for (var intento = 1; intento <= 12; intento++)
            {
                respuesta = cliente.ConsultarDocumentos(zipKey).FirstOrDefault();
                Console.WriteLine("Intento " + intento + ": " + (respuesta == null ? "sin respuesta" : respuesta.StatusCode + " - " + respuesta.StatusDescription));
                if (respuesta != null && (respuesta.XmlBase64Bytes != null || respuesta.ErrorMessage != null)) break;
                Thread.Sleep(10000);
            }

            if (respuesta == null) return 6;

            Console.WriteLine(respuesta.StatusCode + " - " + respuesta.StatusDescription + ": " + respuesta.StatusMessage);
            if (respuesta.ErrorMessage != null)
            {
                Console.WriteLine(string.Join(Environment.NewLine, respuesta.ErrorMessage));
            }
            if (respuesta.XmlBase64Bytes != null)
            {
                var salida = Path.Combine(carpeta, "salida");
                Directory.CreateDirectory(salida);
                File.WriteAllText(Path.Combine(salida, "respuesta-" + zipKey + ".xml"), Encoding.UTF8.GetString(respuesta.XmlBase64Bytes));
                Console.WriteLine("Respuesta de la DIAN guardada en config-local\\salida.");
            }

            return respuesta.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE ? 0 : 7;
        }

        private static string ObtenerValor(string[] args, string nombre)
        {
            var i = Array.IndexOf(args, nombre);
            return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
        }

        private static string BuscarConfigLocal()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidato = Path.Combine(dir.FullName, "config-local");
                if (Directory.Exists(candidato)) return candidato;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("No se encontró la carpeta config-local.");
        }

        // La ruta del certificado en emisor-marlit.json suele ser de Windows. Si no existe en esta máquina, se busca el
        // mismo nombre de archivo dentro de config-local (útil al probar en Linux).
        private static string ResolverRutaCertificado(string carpeta, string ruta)
        {
            if (File.Exists(ruta)) return ruta;

            var nombre = Path.GetFileName(ruta.Replace('\\', '/'));
            var local = Path.Combine(carpeta, nombre);
            if (File.Exists(local)) return local;

            throw new FileNotFoundException("No se encontró el certificado ni en " + ruta + " ni en " + local);
        }

        private static T Leer<T>(string carpeta, string archivo)
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(carpeta, archivo)), Json);
        }

        // Lee de una nómina ya generada los datos que un ajuste debe referenciar (número, CUNE y fecha).
        private static ReferenciaNominaIndividual LeerReferenciaNomina(string archivoNomina)
        {
            var doc = System.Xml.Linq.XDocument.Load(archivoNomina);
            var numero = doc.Root.Elements().First(e => e.Name.LocalName == "NumeroSecuenciaXML");
            var informacion = doc.Root.Elements().First(e => e.Name.LocalName == "InformacionGeneral");
            return new ReferenciaNominaIndividual
            {
                Numero = (string)numero.Attribute("Numero"),
                Cune = (string)informacion.Attribute("CUNE"),
                FechaGeneracion = DateTime.Parse((string)informacion.Attribute("FechaGen"))
            };
        }

        // Lee de una factura ya generada: la referencia y las dos partes (emisor y adquiriente) para un evento.
        private static (ReferenciaDocumento, ParteEvento, ParteEvento) LeerFacturaParaEvento(string archivoFactura)
        {
            var referencia = LeerReferencia(archivoFactura);
            var doc = System.Xml.Linq.XDocument.Load(archivoFactura);
            System.Xml.Linq.XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

            ParteEvento Parte(string rol)
            {
                var impuestos = doc.Root.Element(cac + rol).Element(cac + "Party").Element(cac + "PartyTaxScheme");
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

        // Entrega el documento del ERP a la API unificada y espera su resultado; baja el PDF si quedó aprobado.
        private static int EnviarPorApi(string urlApi, string clave, string json, string numero, string salida)
        {
            using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(urlApi), Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.Add("x-api-key", clave);

            var respuesta = http.PostAsync("api/documento/agregar", new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            Console.WriteLine("agregar -> " + (int)respuesta.StatusCode);
            if ((int)respuesta.StatusCode != 202)
            {
                Console.WriteLine(respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                return 8;
            }

            Newtonsoft.Json.Linq.JObject estado = null;
            for (var i = 0; i < 30; i++)
            {
                estado = Newtonsoft.Json.Linq.JObject.Parse(http.GetStringAsync("api/documento/estado/" + numero).GetAwaiter().GetResult());
                if (estado["Estado"].ToString() != "Recibido") break;
                System.Threading.Thread.Sleep(2000);
            }

            Console.WriteLine("Estado: " + estado["Estado"] + " | CUFE: " + estado["CUFE"] + " | " + estado["Mensaje"]);
            Console.WriteLine("Pdf: " + estado["Pdf"] + " " + estado["PdfMensaje"] + " | Correo: " + estado["CorreoEnviado"]);
            if (estado["Errores"] != null) foreach (var error in estado["Errores"]) Console.WriteLine("  error: " + error);

            if (estado["Estado"].ToString() != "Aprobado") return 7;

            var pdf = http.GetAsync("api/documento/pdf/" + numero).GetAwaiter().GetResult();
            Console.WriteLine("pdf -> " + (int)pdf.StatusCode);
            if (pdf.IsSuccessStatusCode)
            {
                var ruta = Path.Combine(salida, numero + ".pdf");
                File.WriteAllBytes(ruta, pdf.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
                Console.WriteLine("PDF guardado: " + ruta + " (" + new FileInfo(ruta).Length + " bytes)");
                return 0;
            }

            Console.WriteLine(pdf.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return 9;
        }

        // Lee de una factura ya generada y firmada los datos que una nota debe referenciar.
        private static ReferenciaDocumento LeerReferencia(string archivoFactura)
        {
            var doc = System.Xml.Linq.XDocument.Load(archivoFactura);
            System.Xml.Linq.XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            var raiz = doc.Root;
            var uuid = raiz.Element(cbc + "UUID");
            return new ReferenciaDocumento
            {
                Numero = raiz.Element(cbc + "ID").Value,
                UUID = uuid.Value,
                AlgoritmoCufe = new AlgoritmoSeguridadUUID { Codigo = uuid.Attribute("schemeName").Value },
                TipoDocumento = new TipoDocumentoElectronico { Codigo = raiz.Element(cbc + "InvoiceTypeCode").Value },
                Fecha = DateTime.Parse(raiz.Element(cbc + "IssueDate").Value)
            };
        }

        private static int Ejecutar(string modo, long consecutivo, bool provisional, string tipo, string referenciaNumero)
        {
            var carpeta = BuscarConfigLocal();
            var emisorCfg = Leer<EmisorCfg>(carpeta, "emisor-marlit.json");
            var hab = Leer<HabilitacionCfg>(carpeta, "habilitacion-sw-propio.json");
            var rut = Leer<RutCfg>(carpeta, "emisor-rut.json");

            // ---- Datos del RUT que faltan ----
            var faltan = new List<string>();
            if (string.IsNullOrWhiteSpace(rut.RegimenFiscal)) faltan.Add("regimenFiscal");
            if (string.IsNullOrWhiteSpace(rut.ResponsabilidadTributaria)) faltan.Add("responsabilidadTributaria");
            if (string.IsNullOrWhiteSpace(rut.MatriculaMercantil)) faltan.Add("matriculaMercantil");
            if (string.IsNullOrWhiteSpace(rut.Telefono)) faltan.Add("telefono");

            if (faltan.Count > 0)
            {
                if (!provisional)
                {
                    Console.Error.WriteLine("Faltan datos en config-local\\emisor-rut.json: " + string.Join(", ", faltan));
                    return 3;
                }

                Console.WriteLine("AVISO: datos provisionales (no válidos para enviar): " + string.Join(", ", faltan));
                if (string.IsNullOrWhiteSpace(rut.RegimenFiscal)) rut.RegimenFiscal = "R-99-PN";
                if (string.IsNullOrWhiteSpace(rut.ResponsabilidadTributaria)) rut.ResponsabilidadTributaria = "01";
                if (string.IsNullOrWhiteSpace(rut.MatriculaMercantil)) rut.MatriculaMercantil = "00000";
                if (string.IsNullOrWhiteSpace(rut.Telefono)) rut.Telefono = "0000000";
            }

            // ---- Certificado ----
            var rutaCert = ResolverRutaCertificado(carpeta, emisorCfg.Certificado.Ruta);
            var clave = Environment.GetEnvironmentVariable(emisorCfg.Certificado.VariableEntornoClave)
                        ?? Environment.GetEnvironmentVariable("FIRMA_PFX_CLAVE");
            if (string.IsNullOrEmpty(clave))
            {
                throw new InvalidOperationException("No hay clave del certificado en la variable " + emisorCfg.Certificado.VariableEntornoClave + " ni en FIRMA_PFX_CLAVE.");
            }

            // ---- Rango, software y emisor ----
            var rango = hab.RangoNumeracionAsignado;
            var software = hab.InformacionSoftware;
            var prefijo = rango.Prefijo;
            if (tipo != "documento-soporte" && tipo != "nota-ajuste-ds" && tipo != "nomina" && tipo != "evento" && tipo != "documento-equivalente" && tipo != "nomina-ajuste" && (consecutivo < rango.RangoDesde || consecutivo > rango.RangoHasta))
            {
                throw new ArgumentOutOfRangeException(nameof(consecutivo), "Fuera del rango " + rango.RangoDesde + "-" + rango.RangoHasta);
            }

            var numero = prefijo + consecutivo;
            var fechaResolucion = DateTime.Parse(rango.FechaDesde);
            var fecha = DateTimeHelper.GetColombianDate();
            var vencimiento = fecha.AddDays(30);

            var pais = new Pais { Codigo = "CO", Nombre = "Colombia" };
            var direccionFiscal = new DireccionFisica
            {
                Departamento = new Departamento { Codigo = rut.Departamento.Codigo, Nombre = rut.Departamento.Nombre },
                Municipio = new Municipio { Codigo = rut.Municipio.Codigo, Nombre = rut.Municipio.Nombre },
                Linea = rut.Direccion,
                Pais = pais
            };

            var extensionDian = new ExtensionDian
            {
                RangoNumeracion = new RangoNumeracion
                {
                    NumeroResolucion = long.Parse(rango.NumeroResolucion),
                    FechaResolucion = fechaResolucion,
                    Prefijo = prefijo,
                    Desde = rango.RangoDesde,
                    Hasta = rango.RangoHasta,
                    VigenciaDesde = fechaResolucion,
                    VigenciaHasta = DateTime.Parse(rango.FechaHasta)
                },
                PaisOrigen = Pais.COLOMBIA,
                SoftwareProveedorNit = emisorCfg.Nit,
                SoftwareIdentificador = software.Identificacion,
                SoftwarePin = software.Pin
            };

            var emisor = new Emisor
            {
                Identificacion = emisorCfg.Nit,
                RazonSocial = emisorCfg.RazonSocial,
                DireccionFiscal = direccionFiscal,
                TipoPersona = rut.TipoPersona == "NATURAL" ? TipoPersona.NATURAL : TipoPersona.JURIDICA,
                RegimenFiscal = RegimenFiscal.GetFromCodigo(rut.RegimenFiscal),
                ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo(rut.ResponsabilidadTributaria),
                PrefijoRangoNumeracion = prefijo,
                CorreoElectronico = rut.Correo,
                NumeroTelefonico = rut.Telefono,
                DatosComerciales = new DatosComerciales
                {
                    MatriculaMercantil = rut.MatriculaMercantil,
                    Direccion = direccionFiscal,
                    // GeneradorFactura no usa Emisor.RazonSocial: el nombre del emisor sale de este campo
                    // (PartyName y RegistrationName). Debe ser la razón social tal como está en el RUT.
                    NombreComercial = emisorCfg.RazonSocial
                }
            };

            // Adquiriente de ejemplo tomado de las pruebas originales del POS.
            var adquiriente = new Adquiriente
            {
                TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                Identificacion = "900108281",
                RazonSocial = "ADQUIRIENTE DE EJEMPLO",
                NombreComercial = "ADQUIRIENTE DE EJEMPLO",
                DireccionComercial = new DireccionFisica
                {
                    Departamento = Departamento.BOGOTA,
                    Municipio = Municipio.BOGOTA_BOGOTA_DC,
                    Linea = "CR 9 A N0 99 - 07 OF 802",
                    Pais = Pais.COLOMBIA
                },
                DireccionFiscal = new DireccionFisica
                {
                    Departamento = Departamento.BOGOTA,
                    Municipio = Municipio.BOGOTA_BOGOTA_DC,
                    Linea = "CR 9 A N0 99 - 07 OF 802",
                    Pais = Pais.COLOMBIA
                },
                TipoPersona = TipoPersona.JURIDICA,
                RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN"),
                MatriculaMercantil = "00000",
                CorreoElectronico = "pruebas@ejemplo.com",
                NumeroTelefonico = "+000 00000000"
            };

            // HAB_ADQUIRIENTE=marlit: factura donde MARLIT es también el adquiriente, para poder generar eventos RADIAN
            // sobre ella (solo el adquiriente puede registrar acuse, recibo y aceptación).
            if (Environment.GetEnvironmentVariable("HAB_ADQUIRIENTE") == "marlit")
            {
                adquiriente = new Adquiriente
                {
                    TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                    Identificacion = emisorCfg.Nit,
                    RazonSocial = emisorCfg.RazonSocial,
                    NombreComercial = emisorCfg.RazonSocial,
                    DireccionComercial = direccionFiscal,
                    DireccionFiscal = direccionFiscal,
                    TipoPersona = TipoPersona.JURIDICA,
                    RegimenFiscal = RegimenFiscal.GetFromCodigo("R-99-PN"),
                    MatriculaMercantil = rut.MatriculaMercantil,
                    CorreoElectronico = rut.Correo,
                    NumeroTelefonico = rut.Telefono
                };
            }

            // Una línea con IVA 19 %.
            var linea = new Linea
            {
                Cantidad = 1M,
                CodigoProducto = new CodigoProducto
                {
                    Tipo = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO"),
                    Valor = "PRUEBA001"
                },
                Descripcion = "Producto de prueba de habilitacion",
                PrecioUnitario = 100000,
                BaseGeneral = 100000,
                Impuestos =
                {
                    new Impuesto
                    {
                        Tipo = TipoImpuesto.GetFromNombre("IVA"),
                        Porcentaje = 19.00m,
                        Importe = 19000,
                        BaseImponible = 100000
                    }
                }
            };

            var resumenIva = new ResumenImpuesto
            {
                Tipo = TipoImpuesto.GetFromNombre("IVA"),
                Importe = 19000,
                Detalles =
                {
                    new Impuesto { Porcentaje = 19.00m, Importe = 19000, BaseImponible = 100000 }
                }
            };

            var totales = new Totales
            {
                ValorBruto = 100000,
                BaseImponible = 100000,
                ValorBrutoConImpuestos = 119000,
                Descuentos = 0,
                Cargos = 0,
                TotalPagable = 119000
            };

            // HAB_PAGO=credito: factura a crédito (PaymentMeansID 2). Los eventos RADIAN solo se aceptan sobre facturas a crédito (LGC62).
            var aCredito = Environment.GetEnvironmentVariable("HAB_PAGO") == "credito";
            var detallePago = new DetallePago
            {
                Forma = aCredito ? FormaPago.CREDITO : FormaPago.CONTADO,
                Metodo = MetodoPago.GetFromCodigo(aCredito ? "42" : "10"), // 10 = efectivo, 42 = consignación bancaria
                Fecha = vencimiento
            };

            var salida = Path.Combine(carpeta, "salida");
            Directory.CreateDirectory(salida);

            var firma = new FirmaElectronica
            {
                RolFirmante = RolFirmante.EMISOR,
                RutaCertificado = rutaCert,
                ClaveCertificado = clave
            };

            string xml;
            string identificador;
            byte[] firmado;
            var testSetId = emisorCfg.TestSetId;

            if (tipo == "documento-equivalente")
            {
                // Documento equivalente electrónico del POS: tiene su propio software, set de pruebas y rango de numeración.
                var posRuta = Path.Combine(carpeta, "pos.json");
                var pos = File.Exists(posRuta) ? Leer<DsCfg>(carpeta, "pos.json") : new DsCfg();

                var posFaltan = new List<string>();
                if (string.IsNullOrWhiteSpace(pos.SoftwareIdentificacion)) posFaltan.Add("softwareIdentificacion");
                if (string.IsNullOrWhiteSpace(pos.SoftwarePin)) posFaltan.Add("softwarePin");
                if (string.IsNullOrWhiteSpace(pos.Prefijo)) posFaltan.Add("prefijo");
                if (string.IsNullOrWhiteSpace(pos.NumeroResolucion)) posFaltan.Add("numeroResolucion");
                if (pos.RangoDesde == null || pos.RangoHasta == null) posFaltan.Add("rangoDesde/rangoHasta");
                if (string.IsNullOrWhiteSpace(pos.FechaResolucion)) posFaltan.Add("fechaResolucion");
                if (string.IsNullOrWhiteSpace(pos.VigenciaDesde)) posFaltan.Add("vigenciaDesde");
                if (string.IsNullOrWhiteSpace(pos.VigenciaHasta)) posFaltan.Add("vigenciaHasta");

                if (posFaltan.Count > 0)
                {
                    if (!provisional)
                    {
                        Console.Error.WriteLine("Faltan datos del POS en config-local\\pos.json: " + string.Join(", ", posFaltan));
                        return 3;
                    }

                    Console.WriteLine("AVISO: datos del POS provisionales (no válidos para enviar): " + string.Join(", ", posFaltan));
                    pos.SoftwareIdentificacion = pos.SoftwareIdentificacion ?? software.Identificacion;
                    pos.SoftwarePin = pos.SoftwarePin ?? software.Pin;
                    pos.Prefijo = pos.Prefijo ?? "EPOS";
                    pos.NumeroResolucion = pos.NumeroResolucion ?? "18760000001";
                    pos.RangoDesde = pos.RangoDesde ?? 1;
                    pos.RangoHasta = pos.RangoHasta ?? 1000;
                    pos.FechaResolucion = pos.FechaResolucion ?? "2019-01-19";
                    pos.VigenciaDesde = pos.VigenciaDesde ?? "2019-01-19";
                    pos.VigenciaHasta = pos.VigenciaHasta ?? "2030-01-19";
                }

                numero = pos.Prefijo + consecutivo;
                if (!string.IsNullOrWhiteSpace(pos.TestSetId)) testSetId = pos.TestSetId;

                var metaPos = new MetaDatosDE(TipoDocumentoElectronico.DOCUMENTO_EQUIVALENTE_POS)
                {
                    FechaEmision = fecha,
                    FechaVencimiento = fecha.AddDays(30),
                    Numero = numero
                };

                metaPos.AddResolucion(long.Parse(pos.NumeroResolucion), DateTime.Parse(pos.FechaResolucion), DateTime.Parse(pos.VigenciaDesde),
                    DateTime.Parse(pos.VigenciaHasta), pos.Prefijo, pos.RangoDesde.Value, pos.RangoHasta.Value);

                // Ciudad y departamento con la escritura de la lista de la DIAN y código postal real (el valor por defecto
                // "N/A" no sirve), como se aprendió con el documento soporte.
                var direccionPos = new DireccionFisica
                {
                    Departamento = new Departamento { Codigo = rut.Departamento.Codigo, Nombre = "Valle del Cauca" },
                    Municipio = new Municipio { Codigo = rut.Municipio.Codigo, Nombre = "Cali" },
                    Linea = rut.Direccion,
                    CodigoPostal = "760003",
                    Pais = new Pais { Codigo = "CO", Nombre = "Colombia" }
                };

                metaPos.AddEmisor(
                    new DatosFiscales(emisorCfg.RazonSocial)
                    {
                        Identificacion = emisorCfg.Nit,
                        DV = emisorCfg.Dv,
                        Direccion = direccionPos,
                        Telefono = rut.Telefono,
                        Email = rut.Correo
                    },
                    new DatosComerciales
                    {
                        CodigoCliente = emisorCfg.Nit,
                        NombreComercial = emisorCfg.RazonSocial,
                        Telefono = rut.Telefono,
                        Email = rut.Correo,
                        MatriculaMercantil = rut.MatriculaMercantil,
                        Direccion = direccionPos
                    });

                metaPos.AddPeriodoFacturacion(fecha);
                metaPos.AddDatosSoftware(emisorCfg.Nit, pos.SoftwareIdentificacion, pos.SoftwarePin);
                metaPos.AddFormaPago(FormaPago.CONTADO, MetodoPago.GetFromCodigo("10"), fecha);
                metaPos.AddItem("PRODUCTO001", "PRODUCTO DE PRUEBA POS", "GENERICA", "REF001", 1M, "EA", 100000M, false, 0.19M);

                var posValorBruto = metaPos.Items.Sum(item => item.BaseGeneral);
                var posBase = metaPos.Items.SelectMany(item => item.Impuestos).Sum(impuesto => impuesto.BaseImponible);
                var posImpuestos = metaPos.Items.SelectMany(item => item.Impuestos).Sum(impuesto => impuesto.Importe);

                var posTotales = new Totales
                {
                    ValorBruto = posValorBruto,
                    BaseImponible = posBase,
                    ValorBrutoConImpuestos = posValorBruto + posImpuestos,
                    TotalPagable = posValorBruto + posImpuestos
                };

                var extFabricante = new ExtensionFabricanteSoftware();
                extFabricante.ConInformacion(new ExtensionFabricanteSoftware.Informacion
                {
                    NombreApellido = emisorCfg.RazonSocial,
                    RazonSocial = emisorCfg.RazonSocial,
                    NombreSoftware = "QuantyERP"
                });

                var extBeneficios = new ExtensionBeneficiosComprador();
                extBeneficios.ConInformacion(new ExtensionBeneficiosComprador.Informacion
                {
                    CodigoComprador = "222222222222",
                    NombresApellidos = "Consumidor Final",
                    PuntosAcumulados = "100"
                });

                var extCaja = new ExtensionCajaVenta();
                extCaja.ConInformacion(new ExtensionCajaVenta.Informacion
                {
                    PlacaCaja = "234566",
                    CodigoVenta = "743992",
                    NombreCajero = "Cajero de prueba",
                    UbicacionCaja = "Caja principal",
                    TipoCaja = "Caja de apoyo",
                    SubTotal = posValorBruto
                });

                metaPos.Emisor.PrefijoRangoNumeracion = metaPos.ExtensionDian.RangoNumeracion.Prefijo;

                var generadorPos = new GeneradorDocumentoEquivalente(AmbienteDestino.PRUEBAS, TipoDocumentoEquivalente.POS)
                    .ConOperacion(OperacionDocumentoEquivalente.GENERAL)
                    .ConNumero(metaPos.Numero)
                    .ConFecha(metaPos.FechaEmision)
                    .ConVencimiento(metaPos.FechaVencimiento)
                    .ConNota("")
                    .ConMoneda(Moneda.PESO_COLOMBIANO)
                    .ConExtensionDian(metaPos.ExtensionDian)
                    .AgregarExtension(extFabricante)
                    .AgregarExtension(extBeneficios)
                    .AgregarExtension(extCaja)
                    .ConEmisor(metaPos.Emisor)
                    .ParaConsumidorFinal()
                    .AgregarLineas(metaPos.Items)
                    .ConTotales(posTotales)
                    .ConDetallePago(metaPos.DetallePago);

                foreach (var grupo in metaPos.Items.SelectMany(item => item.Impuestos).GroupBy(imp => imp.Tipo))
                {
                    generadorPos.AgregarResumenImpuesto(new ResumenImpuesto
                    {
                        Tipo = grupo.Key,
                        Importe = grupo.Sum(imp => imp.Importe),
                        Detalles =
                        {
                            new Impuesto
                            {
                                Porcentaje = grupo.First().Porcentaje,
                                BaseImponible = grupo.Sum(imp => imp.BaseImponible),
                                Importe = grupo.Sum(imp => imp.Importe),
                                PorUnidad = grupo.Sum(imp => imp.PorUnidad),
                                UnidadMedida = UnidadMedida.CADA
                            }
                        }
                    });
                }

                generadorPos.AsignarCUDE(pos.SoftwarePin);

                var documentoPos = generadorPos.Obtener();
                xml = documentoPos.SerializeXmlDocument().ToXmlString();
                identificador = documentoPos.UUID.Value;
                firmado = firma.FirmarDocumentoEquivalente(xml, fecha);
            }
            else if (tipo == "evento")
            {
                // Evento RADIAN sobre una factura recibida. HAB_EVENTO elige cuál: acuse (por defecto), recibo,
                // aceptacion o rechazo. Los registra el adquiriente, así que MARLIT debe serlo de la factura referenciada.
                var evento = Environment.GetEnvironmentVariable("HAB_EVENTO") ?? "acuse";
                var (referenciaEv, emisorEv, adquirienteEv) = LeerFacturaParaEvento(Path.Combine(salida, referenciaNumero + "-firmado.xml"));
                if (adquirienteEv.Identificacion != emisorCfg.Nit)
                {
                    throw new InvalidOperationException("Solo se pueden generar eventos para documentos recibidos: el adquiriente de " + referenciaNumero + " no es MARLIT.");
                }

                numero = "EVT" + consecutivo;

                var generadorEvento = new GeneradorEvento(AmbienteDestino.PRUEBAS, referenciaEv)
                    .ConFecha(fecha)
                    .ConNumero(numero)
                    .ConExtensionDian(new ExtensionDian
                    {
                        PaisOrigen = Pais.COLOMBIA,
                        SoftwareProveedorNit = emisorCfg.Nit,
                        SoftwareIdentificador = software.Identificacion,
                        SoftwarePin = software.Pin
                    })
                    .ConRegistrante(adquirienteEv)
                    .ConReceptor(emisorEv);

                switch (evento)
                {
                    case "acuse": generadorEvento = generadorEvento.ParaAcuseRecibo(); break;
                    case "recibo": generadorEvento = generadorEvento.ParaRecepcionBienes(); break;
                    case "aceptacion": generadorEvento = generadorEvento.ParaAceptacionExpresa(); break;
                    case "rechazo": generadorEvento = generadorEvento.ParaRechazoDocumento(); break;
                    default: throw new InvalidOperationException("HAB_EVENTO debe ser acuse, recibo, aceptacion o rechazo.");
                }

                var respuestaEvento = generadorEvento.AsignarCUDE(software.Pin).Obtener();
                xml = respuestaEvento.SerializeXmlDocument().ToXmlString();
                identificador = respuestaEvento.UUID.Value;
                firmado = firma.FirmarEvento(xml, DateTimeHelper.GetColombianDate());
            }
            else if (tipo == "nomina" || tipo == "nomina-ajuste")
            {
                // La nómina electrónica tiene su propio software y su propio set de pruebas, y no usa resolución de numeración.
                var nomRuta = Path.Combine(carpeta, "nomina.json");
                var nom = File.Exists(nomRuta) ? Leer<NominaCfg>(carpeta, "nomina.json") : new NominaCfg();

                var nomFaltan = new List<string>();
                if (string.IsNullOrWhiteSpace(nom.SoftwareIdentificacion)) nomFaltan.Add("softwareIdentificacion");
                if (string.IsNullOrWhiteSpace(nom.SoftwarePin)) nomFaltan.Add("softwarePin");
                if (string.IsNullOrWhiteSpace(nom.Prefijo)) nomFaltan.Add("prefijo");

                if (nomFaltan.Count > 0)
                {
                    if (!provisional)
                    {
                        Console.Error.WriteLine("Faltan datos de nómina en config-local\\nomina.json: " + string.Join(", ", nomFaltan));
                        return 3;
                    }

                    Console.WriteLine("AVISO: datos de nómina provisionales (no válidos para enviar): " + string.Join(", ", nomFaltan));
                    nom.SoftwareIdentificacion = nom.SoftwareIdentificacion ?? software.Identificacion;
                    nom.SoftwarePin = nom.SoftwarePin ?? software.Pin;
                    nom.Prefijo = nom.Prefijo ?? "NOM";
                }

                numero = nom.Prefijo + consecutivo;
                if (!string.IsNullOrWhiteSpace(nom.TestSetId)) testSetId = nom.TestSetId;

                // Los nombres de municipio y departamento van con la escritura de la lista de la DIAN.
                var ubicacion = new DireccionFisica
                {
                    Departamento = new Departamento { Codigo = rut.Departamento.Codigo, Nombre = "Valle del Cauca" },
                    Municipio = new Municipio { Codigo = rut.Municipio.Codigo, Nombre = "Cali" },
                    Linea = rut.Direccion,
                    Pais = new Pais { Codigo = "CO", Nombre = "Colombia" }
                };

                var informacion = new InformacionGeneralNominaIndividual
                {
                    FechaGeneracion = fecha,
                    LugarGeneracion = ubicacion,
                    Moneda = Moneda.PESO_COLOMBIANO,
                    TRM = 1,
                    PeriodoNomina = TipoPeriodoNomina.MENSUAL
                };

                var periodoNomina = new PeriodoNomina
                {
                    FechaIngreso = fecha.AddDays(-30),
                    FechaRetiro = null,
                    FechaLiquidacionInicio = fecha.AddDays(-30),
                    FechaLiquidacionFin = fecha,
                    TiempoLaborado = new TiempoLaborado { Dias = 30 }
                };

                var proveedor = new ProveedorSoftware
                {
                    RazonSocial = emisorCfg.RazonSocial,
                    Nit = emisorCfg.Nit,
                    SoftwareIdentificador = nom.SoftwareIdentificacion,
                    SoftwarePin = nom.SoftwarePin
                };

                var empleador = new Empleador
                {
                    RazonSocial = emisorCfg.RazonSocial,
                    Nit = emisorCfg.Nit,
                    Ubicacion = ubicacion
                };

                const decimal salarioMensual = 8000000M;

                // Trabajador ficticio de prueba.
                var trabajador = new Trabajador
                {
                    Codigo = "EMP000001",
                    Tipo = TipoTrabajador.DEPENDIENTE,
                    SubTipo = SubTipoTrabajador.NO_APLICA,
                    AltoRiesgoPension = false,
                    PrimerNombre = "Trabajador",
                    OtrosNombres = "De",
                    PrimerApellido = "Prueba",
                    SegundoApellido = "Habilitacion",
                    TipoDocumento = TipoIdentificacion.GetFromCodigo("13"),
                    NumeroDocumento = "1000000002",
                    TipoContrato = TipoContrato.TERMINO_INDEFINIDO,
                    SalarioIntegral = false,
                    Sueldo = salarioMensual,
                    LugarTrabajo = ubicacion
                };

                var pagoNomina = new DetallePago
                {
                    Fecha = fecha,
                    Forma = FormaPago.CONTADO, // siempre CONTADO
                    Metodo = MetodoPago.GetFromCodigo("42") // 42 = consignación bancaria
                };

                var cuentaBancaria = new CuentaBancaria
                {
                    NombreBanco = "BANCO DE PRUEBA",
                    TipoCuenta = "Cuenta de Ahorros",
                    NumeroCuenta = "000000000000"
                };

                var devengados = new Devengados
                {
                    Resumen = new DevengadoResumen
                    {
                        DiasTrabajados = periodoNomina.TiempoLaborado.CalcularDias(),
                        SueldoTrabajado = salarioMensual
                    }
                };

                var deducciones = new Deducciones
                {
                    Salud = new DeduccionSalud { Porcentaje = 4, Deduccion = decimal.Round(salarioMensual / 100 * 4, 2) },
                    FondoPension = new FondoPension { Porcentaje = 4, Deduccion = decimal.Round(salarioMensual / 100 * 4, 2) }
                };

                var totalDeducciones = deducciones.Salud.Deduccion + deducciones.FondoPension.Deduccion;
                var totalesNomina = new TotalesNominaIndividual
                {
                    TotalDevengados = salarioMensual,
                    TotalDeducciones = totalDeducciones,
                    ValorTotal = salarioMensual - totalDeducciones
                };

                if (tipo == "nomina-ajuste")
                {
                    // Ajuste de una nómina ya autorizada. HAB_AJUSTE elige reemplazar (por defecto) o eliminar.
                    // Reemplazar lleva los devengados, deducciones y totales nuevos; eliminar solo referencia la nómina.
                    var tipoAjuste = (Environment.GetEnvironmentVariable("HAB_AJUSTE") ?? "reemplazar") == "eliminar"
                        ? TipoAjusteNominaIndividual.ELIMINAR
                        : TipoAjusteNominaIndividual.REEMPLAZAR;
                    var referenciaNomina = LeerReferenciaNomina(Path.Combine(salida, referenciaNumero + "-firmado.xml"));
                    Console.WriteLine("Referencia: " + referenciaNomina.Numero + " (" + referenciaNomina.Cune.Substring(0, 12) + "...), ajuste " +
                        (tipoAjuste.Codigo == "1" ? "REEMPLAZAR" : "ELIMINAR"));

                    numero = "ANE" + consecutivo;

                    var generadorAjuste = new GeneradorAjusteNominaIndividual(AmbienteDestino.PRUEBAS, tipoAjuste, referenciaNomina)
                        .ConInformacionGeneral(informacion)
                        .ConPeriodo(periodoNomina)
                        .ConNumero("ANE", consecutivo)
                        .ConProveedorSoftware(proveedor)
                        .AgregarNota("Set de pruebas")
                        .ConEmpleador(empleador)
                        .ConTrabajador(trabajador)
                        .ConPago(pagoNomina, cuentaBancaria)
                        .AgregarFechaPago(pagoNomina.Fecha);

                    if (tipoAjuste.Codigo == "1")
                    {
                        generadorAjuste = generadorAjuste
                            .ConDevengados(devengados)
                            .ConDeducciones(deducciones)
                            .ConTotales(totalesNomina);
                    }

                    generadorAjuste = generadorAjuste.AsignarCUNE(nom.SoftwarePin);

                    var ajusteNomina = generadorAjuste.AjusteNominaIndividual;
                    xml = ajusteNomina.SerializeXmlDocument().ToXmlString();
                    identificador = ajusteNomina.Reemplazar?.InformacionGeneral?.CUNE ?? ajusteNomina.Eliminar.InformacionGeneral.CUNE;
                    firmado = firma.FirmarAjusteNominaIndividual(xml, DateTimeHelper.GetColombianDate());
                }
                else
                {
                    var generadorNomina = new GeneradorNominaIndividual(AmbienteDestino.PRUEBAS)
                        .ConInformacionGeneral(informacion)
                        .ConPeriodo(periodoNomina)
                        .ConNumero(nom.Prefijo, consecutivo)
                        .ConProveedorSoftware(proveedor)
                        .AgregarNota("Set de pruebas")
                        .ConEmpleador(empleador)
                        .ConTrabajador(trabajador)
                        .ConPago(pagoNomina, cuentaBancaria)
                        .AgregarFechaPago(pagoNomina.Fecha)
                        .ConDevengados(devengados)
                        .ConDeducciones(deducciones)
                        .ConTotales(totalesNomina)
                        .AsignarCUNE(nom.SoftwarePin);

                    var nomina = generadorNomina.NominaIndividual;
                    xml = nomina.SerializeXmlDocument().ToXmlString();
                    identificador = nomina.InformacionGeneral.CUNE;
                    firmado = firma.FirmarNominaIndividual(xml, DateTimeHelper.GetColombianDate());
                }
            }
            else if (tipo == "documento-soporte" || tipo == "nota-ajuste-ds")
            {
                // El documento soporte tiene su propio software, rango de numeración y set de pruebas en la DIAN.
                var dsRuta = Path.Combine(carpeta, "documento-soporte.json");
                var ds = File.Exists(dsRuta) ? Leer<DsCfg>(carpeta, "documento-soporte.json") : new DsCfg();

                var dsFaltan = new List<string>();
                if (string.IsNullOrWhiteSpace(ds.SoftwareIdentificacion)) dsFaltan.Add("softwareIdentificacion");
                if (string.IsNullOrWhiteSpace(ds.SoftwarePin)) dsFaltan.Add("softwarePin");
                if (string.IsNullOrWhiteSpace(ds.Prefijo)) dsFaltan.Add("prefijo");
                if (string.IsNullOrWhiteSpace(ds.NumeroResolucion)) dsFaltan.Add("numeroResolucion");
                if (ds.RangoDesde == null || ds.RangoHasta == null) dsFaltan.Add("rangoDesde/rangoHasta");
                if (string.IsNullOrWhiteSpace(ds.FechaResolucion)) dsFaltan.Add("fechaResolucion");
                if (string.IsNullOrWhiteSpace(ds.VigenciaDesde)) dsFaltan.Add("vigenciaDesde");
                if (string.IsNullOrWhiteSpace(ds.VigenciaHasta)) dsFaltan.Add("vigenciaHasta");

                if (dsFaltan.Count > 0)
                {
                    if (!provisional)
                    {
                        Console.Error.WriteLine("Faltan datos del documento soporte en config-local\\documento-soporte.json: " + string.Join(", ", dsFaltan));
                        return 3;
                    }

                    Console.WriteLine("AVISO: datos del documento soporte provisionales (no válidos para enviar): " + string.Join(", ", dsFaltan));
                    ds.SoftwareIdentificacion = ds.SoftwareIdentificacion ?? software.Identificacion;
                    ds.SoftwarePin = ds.SoftwarePin ?? software.Pin;
                    ds.Prefijo = ds.Prefijo ?? "SEDS";
                    ds.NumeroResolucion = ds.NumeroResolucion ?? rango.NumeroResolucion;
                    ds.RangoDesde = ds.RangoDesde ?? 984000000;
                    ds.RangoHasta = ds.RangoHasta ?? 985000000;
                    ds.FechaResolucion = ds.FechaResolucion ?? rango.FechaDesde;
                    ds.VigenciaDesde = ds.VigenciaDesde ?? rango.FechaDesde;
                    ds.VigenciaHasta = ds.VigenciaHasta ?? rango.FechaHasta;
                }

                numero = ds.Prefijo + consecutivo;
                if (!string.IsNullOrWhiteSpace(ds.TestSetId)) testSetId = ds.TestSetId;

                var meta = new MetaDatosDE
                {
                    FechaEmision = fecha,
                    FechaVencimiento = fecha,
                    TipoDocumento = TipoDocumentoElectronico.DOCUMENTO_SOPORTE,
                    Numero = numero
                };

                meta.AddResolucion(long.Parse(ds.NumeroResolucion), DateTime.Parse(ds.FechaResolucion), DateTime.Parse(ds.VigenciaDesde),
                    DateTime.Parse(ds.VigenciaHasta), ds.Prefijo, ds.RangoDesde.Value, ds.RangoHasta.Value);

                // En el documento soporte el "emisor" del metadato es el proveedor no obligado a facturar.
                var direccionProveedor = new DireccionFisica
                {
                    Departamento = new Departamento { Codigo = "76", Nombre = "Valle del Cauca" },
                    Municipio = new Municipio { Codigo = "76001", Nombre = "Cali" },
                    Linea = "CALLE 1 NO. 1-1",
                    // DireccionFisica.CodigoPostal vale "N/A" por defecto y la regla DSAJ73 exige un valor de la tabla
                    // 16.4.4 del anexo para el proveedor residente. 760003 es el código postal del RUT de MARLIT en Cali.
                    CodigoPostal = "760003",
                    Pais = new Pais { Codigo = "CO", Nombre = "Colombia" }
                };
                // No se usa AddEmisorPersonaNatural: compara el tipo de documento por referencia
                // (TipoDocumentoElectronico.DOCUMENTO_SOPORTE crea un objeto nuevo cada vez), nunca coincide y deja al proveedor vacío.
                var proveedorFiscal = new DatosFiscales
                {
                    TipoPersona = TipoPersona.NATURAL,
                    TipoIdentificacion = TipoIdentificacion.GetFromCodigo("13"),
                    Identificacion = "1000000001",
                    DV = NitHelper.CalcularDigitoVerificador("1000000001").ToString(),
                    PrimerNombre = "PROVEEDOR",
                    SegundoNombre = "DE",
                    PrimerApellido = "PRUEBA",
                    SegundoApellido = "HABILITACION",
                    Direccion = direccionProveedor,
                    Email = "proveedor@ejemplo.com",
                    Telefono = "3000000000",
                    RegimenFiscal = RegimenFiscal.Default,
                    ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ")
                };
                // Experimento: HAB_PROVEEDOR=juridico prueba un proveedor persona jurídica con NIT.
                if (Environment.GetEnvironmentVariable("HAB_PROVEEDOR") == "juridico")
                {
                    proveedorFiscal = new DatosFiscales
                    {
                        TipoPersona = TipoPersona.JURIDICA,
                        TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                        Identificacion = "900108281",
                        DV = NitHelper.CalcularDigitoVerificador("900108281").ToString(),
                        RazonSocial = "PROVEEDOR JURIDICO DE PRUEBA",
                        Direccion = direccionProveedor,
                        Email = "proveedor@ejemplo.com",
                        Telefono = "3000000000",
                        RegimenFiscal = RegimenFiscal.Default,
                        ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo("ZZ")
                    };
                }

                var proveedorComercial = new DatosComerciales
                {
                    CodigoCliente = "1000000001",
                    Direccion = proveedorFiscal.Direccion,
                    Email = proveedorFiscal.Email,
                    NombreComercial = proveedorFiscal.NombreRazonsocial(),
                    Telefono = proveedorFiscal.Telefono
                };
                meta.AddEmisor(proveedorFiscal, proveedorComercial);

                meta.AddPeriodoFacturacion(fecha);
                meta.AddDatosSoftware(emisorCfg.Nit, ds.SoftwareIdentificacion, ds.SoftwarePin);
                meta.AddFormaPago(FormaPago.CONTADO, MetodoPago.GetFromCodigo("10"), fecha);

                // El adquiriente es MARLIT, que es quien expide el documento soporte.
                var datosFiscales = new DatosFiscales
                {
                    TipoPersona = TipoPersona.JURIDICA,
                    TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31"),
                    Identificacion = emisorCfg.Nit,
                    DV = emisorCfg.Dv,
                    RazonSocial = emisorCfg.RazonSocial,
                    Direccion = direccionFiscal,
                    Email = rut.Correo,
                    Telefono = rut.Telefono,
                    RegimenFiscal = RegimenFiscal.GetFromCodigo(rut.RegimenFiscal),
                    ResponsabilidadTributaria = ResponsabilidadTributaria.GetFromCodigo(rut.ResponsabilidadTributaria)
                };
                var datosComerciales = new DatosComerciales
                {
                    CodigoCliente = emisorCfg.Nit,
                    Direccion = datosFiscales.Direccion,
                    Email = datosFiscales.Email,
                    NombreComercial = emisorCfg.RazonSocial,
                    Telefono = datosFiscales.Telefono
                };
                meta.AddAdquiriente(datosFiscales, datosComerciales);

                if (tipo == "nota-ajuste-ds")
                {
                    // La nota de ajuste pide marca y modelo del artículo (reglas NSAZ04 y NSAZ05).
                    meta.AddItem("SER001", "SERVICIOS DE PRUEBA DE HABILITACION", "GENERICA", "REF001", 1M, "EA", 100000);

                    // Sin impuestos en la línea: GeneradorNotaAjusteDocumentoSoporte la reporta como IVA exento (0.00),
                    // que es lo que pide la regla NSAX04.
                }
                else
                {
                    #pragma warning disable CS0612 // el método obsoleto es lo que se prueba
                    meta.AddItem("SER001", "SERVICIOS DE PRUEBA DE HABILITACION", 1M, "EA", 100000);
                    #pragma warning restore CS0612
                }
                meta.AddTotales(100000M, 0M, 100000M, 0, 0, 100000M);                if (tipo == "nota-ajuste-ds")
                {
                    // Anulación total del documento soporte referenciado. Como en el ejemplo original, la extensión
                    // DIAN de la nota solo lleva los datos del software, sin rango de numeración.
                    var referenciaDs = LeerReferencia(Path.Combine(salida, referenciaNumero + "-firmado.xml"));
                    Console.WriteLine("Referencia: " + referenciaDs.Numero + " (" + referenciaDs.UUID.Substring(0, 12) + "...)");

                    var generadorNota = new GeneradorNotaAjusteDocumentoSoporte(AmbienteDestino.PRUEBAS, ConceptoNotaAjusteDocumentoSoporte.ANULACION_TOTAL, referenciaDs)
                        .ConNumero(meta.Numero)
                        .ConFecha(meta.FechaEmision)
                        .ConNota("Set de pruebas")
                        .ConMoneda(Moneda.PESO_COLOMBIANO)
                        .ConExtensionDian(new ExtensionDian
                        {
                            PaisOrigen = Pais.COLOMBIA,
                            SoftwareProveedorNit = emisorCfg.Nit,
                            SoftwareIdentificador = ds.SoftwareIdentificacion,
                            SoftwarePin = ds.SoftwarePin
                        })
                        .ConProveedor(meta.Proveedor)
                        .ConAdquiriente(meta.Adquiriente);

                    foreach (var item in meta.Items)
                    {
                        generadorNota.AgregarLinea(item);
                    }

                    generadorNota.ConTotales(meta.Totales)
                        .ConDetallePago(meta.DetallePago)
                        .AsignarCUDS(ds.SoftwarePin);

                    var notaAjuste = generadorNota.Obtener();
                    xml = notaAjuste.SerializeXmlDocument().ToXmlString();
                    identificador = notaAjuste.UUID.Value;
                    firmado = firma.FirmarNotaAjusteDocumentoSoporte(xml, DateTimeHelper.GetColombianDate());
                }
                else
                {
                    var generadorDs = new GeneradorDocumentoSoporte(AmbienteDestino.PRUEBAS)
                        .ConNumero(meta.Numero)
                        .ConFecha(meta.FechaEmision)
                        .ConVencimiento(meta.FechaVencimiento)
                        .ConMoneda(Moneda.PESO_COLOMBIANO)
                        .ConExtensionDian(meta.ExtensionDian)
                        .ConProveedor(meta.Proveedor)
                        .ConAdquiriente(meta.Adquiriente)
                        .AgregarLineas(meta.Items)
                        .ConTotales(meta.Totales)
                        .ConDetallePago(meta.DetallePago)
                        .AsignarCUDS(ds.SoftwarePin)
                        .ConPeriodoFacturacion(meta.Periodo);

                    var documentoSoporte = generadorDs.Obtener();
                    xml = documentoSoporte.SerializeXmlDocument().ToXmlString();
                    identificador = documentoSoporte.UUID.Value;
                    firmado = firma.FirmarDocumentoSoporte(xml, DateTimeHelper.GetColombianDate());
                }
            }
            else if (tipo == "factura-erp")
            {
                // Factura armada como la entrega QuantyERP (DocumentoDian) y traducida con TraductorFactura: valida en
                // habilitación el XML que producirá la API unificada.
                var responsabilidades = new List<FE.Models.Doc.responsabilidad>
                {
                    new FE.Models.Doc.responsabilidad { Codigo = rut.RegimenFiscal, CodigoFE = rut.RegimenFiscal, Descripcion = "Responsabilidad fiscal del emisor" }
                };
                if (rut.ResponsabilidadTributaria == "01")
                {
                    // Marcador del ERP: el emisor es responsable de IVA (define el tributo 01 en el XML).
                    responsabilidades.Add(new FE.Models.Doc.responsabilidad { Codigo = "48", CodigoFE = "48", Descripcion = "Responsable de IVA" });
                }

                var responsabilidadesAdquiriente = new List<FE.Models.Doc.responsabilidad>
                {
                    new FE.Models.Doc.responsabilidad { Codigo = "R-99-PN", CodigoFE = "R-99-PN", Descripcion = "No aplica - Otros" }
                };

                var docErp = WS_V2_HttpClientLib.Builders.DianDocumentBuilder.FacturaVenta("FV", numero, fecha)
                    .ConFormaPago(FE.Enum.MetodoPagoEnum.Contado, "10", 0, "")
                    .ConParametrosFiscales(0, 0, 0, 0.15m, 0.025m, 0.035m, 0.00966m)
                    .ConResolucion(rango.NumeroResolucion, prefijo, fechaResolucion, DateTime.Parse(rango.FechaHasta), rango.RangoDesde, rango.RangoHasta, software.ClaveTecnica)
                    .ConEmisorPersonaJuridica("EMP1", rut.MatriculaMercantil, emisorCfg.Nit, NitHelper.CalcularDigitoVerificador(emisorCfg.Nit).ToString(), emisorCfg.RazonSocial, emisorCfg.RazonSocial, "Contacto", rut.Correo, rut.Telefono, "Gerente", responsabilidades, "48")
                    .ConLocacionPrincipalEmisor(rut.Direccion, rut.Municipio.Codigo)
                    .ConLocacionComercialEmisor(rut.Direccion, rut.Municipio.Codigo)
                    .ConReceptorPersonaJuridica("CLI1", "00000", "900108281", "3", "ADQUIRIENTE DE EJEMPLO", "ADQUIRIENTE DE EJEMPLO", "", "pruebas@ejemplo.com", "3100000000", "", responsabilidadesAdquiriente, "48")
                    .ConLocacionPrincipalReceptor("CR 9 A N0 99 - 07 OF 802", "11001")
                    .ConLocacionComercialReceptor("CR 9 A N0 99 - 07 OF 802", "11001")
                    .AgregarItem("PRUEBA001", "REF001", "Producto de prueba traductor", 2m, 50000m, 0.19m)
                    .AgregarItem("PRUEBA002", "REF002", "Producto con descuento de prueba", 1m, 120000m, 0.19m, 0.10m)
                    .AgregarNota("Factura de prueba del traductor (ERP a UBL)")
                    .Construir(validar: true);

                // El documento pasa por el JSON como en producción: el ERP lo serializa y la API lo lee.
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(docErp);
                docErp = Newtonsoft.Json.Linq.JObject.Parse(json).ToObject<FE.Models.Doc.DocumentoDian>();

                // Con DIAN_API_URL y DIAN_API_KEY el documento no se firma aquí: se entrega a la API unificada
                // (api/documento/agregar), que traduce, firma, envía a la DIAN y genera el PDF. Solo habilitación.
                var urlApi = Environment.GetEnvironmentVariable("DIAN_API_URL");
                if (!string.IsNullOrEmpty(urlApi))
                {
                    return EnviarPorApi(urlApi, Environment.GetEnvironmentVariable("DIAN_API_KEY") ?? string.Empty, json, numero, salida);
                }

                var invoice = DIAN.Traduccion.TraductorFactura.Factura(docErp, new DIAN.Traduccion.ParametrosEmision
                {
                    Ambiente = AmbienteDestino.PRUEBAS,
                    SoftwareNit = emisorCfg.Nit,
                    SoftwareIdentificador = software.Identificacion,
                    SoftwarePin = software.Pin
                });
                xml = invoice.SerializeXmlDocument().ToXmlString();
                identificador = invoice.UUID.Value;
                firmado = firma.FirmarFactura(xml, DateTimeHelper.GetColombianDate());
            }
            else if (tipo == "nota-credito-erp" || tipo == "nota-debito-erp")
            {
                // Nota armada como la entrega QuantyERP (DocumentoDian 91/92 con DocumentoRef) y traducida con TraductorNota.
                var esCredito = tipo == "nota-credito-erp";
                var refFactura = LeerReferencia(Path.Combine(salida, referenciaNumero + "-firmado.xml"));
                Console.WriteLine("Referencia: " + refFactura.Numero + " (" + refFactura.UUID.Substring(0, 12) + "...)");

                var responsabilidades = new List<FE.Models.Doc.responsabilidad>
                {
                    new FE.Models.Doc.responsabilidad { Codigo = rut.RegimenFiscal, CodigoFE = rut.RegimenFiscal, Descripcion = "Responsabilidad fiscal del emisor" }
                };
                if (rut.ResponsabilidadTributaria == "01")
                {
                    responsabilidades.Add(new FE.Models.Doc.responsabilidad { Codigo = "48", CodigoFE = "48", Descripcion = "Responsable de IVA" });
                }

                var responsabilidadesAdquiriente = new List<FE.Models.Doc.responsabilidad>
                {
                    new FE.Models.Doc.responsabilidad { Codigo = "R-99-PN", CodigoFE = "R-99-PN", Descripcion = "No aplica - Otros" }
                };

                var constructor = WS_V2_HttpClientLib.Builders.DianDocumentBuilder.Crear(esCredito ? "91" : "92", esCredito ? "NC" : "ND", numero, fecha)
                    .ConFormaPago(FE.Enum.MetodoPagoEnum.Contado, "10", 0, "")
                    .ConParametrosFiscales(0, 0, 0, 0.15m, 0.025m, 0.035m, 0.00966m)
                    .ConResolucion(rango.NumeroResolucion, prefijo, fechaResolucion, DateTime.Parse(rango.FechaHasta), rango.RangoDesde, rango.RangoHasta, software.ClaveTecnica)
                    .ConEmisorPersonaJuridica("EMP1", rut.MatriculaMercantil, emisorCfg.Nit, NitHelper.CalcularDigitoVerificador(emisorCfg.Nit).ToString(), emisorCfg.RazonSocial, emisorCfg.RazonSocial, "Contacto", rut.Correo, rut.Telefono, "Gerente", responsabilidades, "48")
                    .ConLocacionPrincipalEmisor(rut.Direccion, rut.Municipio.Codigo)
                    .ConLocacionComercialEmisor(rut.Direccion, rut.Municipio.Codigo)
                    .ConReceptorPersonaJuridica("CLI1", "00000", "900108281", "3", "ADQUIRIENTE DE EJEMPLO", "ADQUIRIENTE DE EJEMPLO", "", "pruebas@ejemplo.com", "3100000000", "", responsabilidadesAdquiriente, "48")
                    .ConLocacionPrincipalReceptor("CR 9 A N0 99 - 07 OF 802", "11001")
                    .ConLocacionComercialReceptor("CR 9 A N0 99 - 07 OF 802", "11001")
                    .ConDocumentoReferencia(refFactura.Numero, refFactura.UUID, refFactura.Fecha);

                if (esCredito)
                {
                    // Anulación total de la factura de prueba (mismas líneas).
                    constructor
                        .AgregarItem("PRUEBA001", "REF001", "Producto de prueba traductor", 2m, 50000m, 0.19m)
                        .AgregarItem("PRUEBA002", "REF002", "Producto con descuento de prueba", 1m, 120000m, 0.19m, 0.10m)
                        .AgregarNota("Nota crédito de prueba del traductor (ERP a UBL)");
                }
                else
                {
                    constructor
                        .AgregarItem("INTERES001", "INT001", "Intereses de mora de prueba", 1m, 10000m, 0.19m)
                        .AgregarNota("Nota débito de prueba del traductor (ERP a UBL)");
                }

                var docErp = constructor.Construir(validar: true);
                docErp.DocumentoRef.Concepto = esCredito ? "2" : "1";

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(docErp);
                docErp = Newtonsoft.Json.Linq.JObject.Parse(json).ToObject<FE.Models.Doc.DocumentoDian>();

                var parametrosEmision = new DIAN.Traduccion.ParametrosEmision
                {
                    Ambiente = AmbienteDestino.PRUEBAS,
                    SoftwareNit = emisorCfg.Nit,
                    SoftwareIdentificador = software.Identificacion,
                    SoftwarePin = software.Pin
                };

                if (esCredito)
                {
                    var nota = DIAN.Traduccion.TraductorNota.NotaCredito(docErp, parametrosEmision);
                    xml = nota.SerializeXmlDocument().ToXmlString();
                    identificador = nota.UUID.Value;
                    firmado = firma.FirmarNotaCredito(xml, DateTimeHelper.GetColombianDate());
                }
                else
                {
                    var nota = DIAN.Traduccion.TraductorNota.NotaDebito(docErp, parametrosEmision);
                    xml = nota.SerializeXmlDocument().ToXmlString();
                    identificador = nota.UUID.Value;
                    firmado = firma.FirmarNotaDebito(xml, DateTimeHelper.GetColombianDate());
                }
            }
            else if (tipo == "factura")
            {
                var generador = new GeneradorFactura(AmbienteDestino.PRUEBAS, TipoFactura.FACTURA_VENTA)
                    .ConNumero(numero)
                    .ConFecha(fecha)
                    .ConVencimiento(vencimiento)
                    .ConNota("Set de pruebas")
                    .ConMoneda(Moneda.PESO_COLOMBIANO)
                    .ConExtensionDian(extensionDian)
                    .ConEmisor(emisor)
                    .ConAdquiriente(adquiriente)
                    .AgregarLinea(linea)
                    .AgregarResumenImpuesto(resumenIva)
                    .ConTotales(totales)
                    .ConDetallePago(detallePago)
                    .AsignarCUFE(software.ClaveTecnica, software.Pin);

                var invoice = generador.Obtener();
                xml = invoice.SerializeXmlDocument().ToXmlString();
                identificador = invoice.UUID.Value;
                firmado = firma.FirmarFactura(xml, DateTimeHelper.GetColombianDate());
            }
            else
            {
                var referencia = LeerReferencia(Path.Combine(salida, referenciaNumero + "-firmado.xml"));
                Console.WriteLine("Referencia: " + referencia.Numero + " (" + referencia.UUID.Substring(0, 12) + "...)");

                if (tipo == "nota-credito")
                {
                    // Anulación total: mismas líneas y totales que la factura de prueba.
                    var generador = new GeneradorNotaCredito(AmbienteDestino.PRUEBAS, ConceptoNotaCredito.ANULACION_TOTAL, referencia)
                        .ConNumero(numero)
                        .ConFecha(fecha)
                        .ConNota("Set de pruebas")
                        .ConMoneda(Moneda.PESO_COLOMBIANO)
                        .ConExtensionDian(extensionDian)
                        .ConEmisor(emisor)
                        .ConAdquiriente(adquiriente)
                        .AgregarLinea(linea)
                        .AgregarResumenImpuesto(resumenIva)
                        .ConTotales(totales)
                        .ConDetallePago(detallePago)
                        .AsignarCUDE(software.Pin);

                    var nota = generador.Obtener();
                    xml = nota.SerializeXmlDocument().ToXmlString();
                    identificador = nota.UUID.Value;
                    firmado = firma.FirmarNotaCredito(xml, DateTimeHelper.GetColombianDate());
                }
                else
                {
                    // Nota débito por intereses: una línea de 10.000 con IVA 19 %.
                    var lineaDebito = new Linea
                    {
                        Cantidad = 1M,
                        CodigoProducto = new CodigoProducto
                        {
                            Tipo = TipoCodigoProducto.GetFromCodigo("ESTANDAR_INTERNO"),
                            Valor = "INTERES001"
                        },
                        Descripcion = "Intereses de mora de prueba",
                        PrecioUnitario = 10000,
                        BaseGeneral = 10000,
                        Impuestos =
                        {
                            new Impuesto
                            {
                                Tipo = TipoImpuesto.GetFromNombre("IVA"),
                                Porcentaje = 19.00m,
                                Importe = 1900,
                                BaseImponible = 10000
                            }
                        }
                    };

                    var resumenDebito = new ResumenImpuesto
                    {
                        Tipo = TipoImpuesto.GetFromNombre("IVA"),
                        Importe = 1900,
                        Detalles =
                        {
                            new Impuesto { Porcentaje = 19.00m, Importe = 1900, BaseImponible = 10000 }
                        }
                    };

                    var totalesDebito = new Totales
                    {
                        ValorBruto = 10000,
                        BaseImponible = 10000,
                        ValorBrutoConImpuestos = 11900,
                        Descuentos = 0,
                        Cargos = 0,
                        TotalPagable = 11900
                    };

                    var generador = new GeneradorNotaDebito(AmbienteDestino.PRUEBAS, ConceptoNotaDebito.INTERESES, referencia)
                        .ConNumero(numero)
                        .ConFecha(fecha)
                        .ConNota("Set de pruebas")
                        .ConMoneda(Moneda.PESO_COLOMBIANO)
                        .ConExtensionDian(extensionDian)
                        .ConEmisor(emisor)
                        .ConAdquiriente(adquiriente)
                        .AgregarLinea(lineaDebito)
                        .AgregarResumenImpuesto(resumenDebito)
                        .ConTotales(totalesDebito)
                        .ConDetallePago(detallePago)
                        .AsignarCUDE(software.Pin);

                    var nota = generador.Obtener();
                    xml = nota.SerializeXmlDocument().ToXmlString();
                    identificador = nota.UUID.Value;
                    firmado = firma.FirmarNotaDebito(xml, DateTimeHelper.GetColombianDate());
                }
            }

            File.WriteAllText(Path.Combine(salida, numero + "-sin-firmar.xml"), xml);
            Console.WriteLine(tipo + " generada: " + numero + "  CUFE/CUDE: " + identificador);
            File.WriteAllBytes(Path.Combine(salida, numero + "-firmado.xml"), firmado);
            Console.WriteLine("Firmada con FirmaXades nueva (" + firmado.Length + " bytes).");

            // La verificación independiente no se hace en este proceso: Microsoft.Xades registra sus propios
            // algoritmos en CryptoConfig al firmar y eso hace fallar a SignedXml dentro del mismo proceso.
            // Se verifica aparte con: FirmaXades.Pruebas verificar <archivo firmado>.
            Console.WriteLine("Verifica la firma aparte con: FirmaXades.Pruebas verificar \"" + Path.Combine(salida, numero + "-firmado.xml") + "\"");

            if (modo == "generar")
            {
                Console.WriteLine("Modo generar: no se envió nada a la DIAN.");
                return 0;
            }

            // ---- Envío al set de pruebas de habilitación ----
            if (!ClienteServicioDian.ENDPOINT_PRUEBAS.Contains("-hab."))
            {
                throw new InvalidOperationException("El endpoint de pruebas no es el de habilitación; se aborta el envío.");
            }

            Console.WriteLine("Enviando a HABILITACIÓN (" + (modo == "enviar-sync" ? "síncrono, un documento" : "set de pruebas") + "): " + ClienteServicioDian.ENDPOINT_PRUEBAS);
            var cliente = new ClienteServicioDian
            {
                Ambiente = AmbienteServicio.PRUEBAS,
                RutaCertificado = rutaCert,
                ClaveCertificado = clave
            };

            if (modo == "enviar-sync")
            {
                // SendBillSync: valida un solo documento y devuelve el resultado en la misma llamada.
                // Eventos y nómina tienen cada uno su propia operación síncrona en el servicio de la DIAN.
                var sync = (tipo == "evento") ? cliente.EnviarEvento(firmado)
                    : (tipo == "nomina" || tipo == "nomina-ajuste") ? cliente.EnviarNomina(firmado)
                    : cliente.EnviarDocumento(firmado);
                Console.WriteLine(sync.StatusCode + " - " + sync.StatusDescription + ": " + sync.StatusMessage);
                if (sync.ErrorMessage != null)
                {
                    Console.WriteLine(string.Join(Environment.NewLine, sync.ErrorMessage));
                }
                if (sync.XmlBase64Bytes != null)
                {
                    File.WriteAllText(Path.Combine(salida, numero + "-respuesta-dian.xml"), Encoding.UTF8.GetString(sync.XmlBase64Bytes));
                }

                return sync.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE ? 0 : 7;
            }

            var subida = cliente.EnviarSetPruebas(new List<byte[]> { firmado }, testSetId);
            Console.WriteLine("ZipKey: " + subida.ZipKey);
            if (subida.ErrorMessageList != null)
            {
                foreach (var item in subida.ErrorMessageList)
                {
                    Console.WriteLine("[" + item.DocumentKey + "] " + item.ProcessedMessage);
                }
            }

            if (string.IsNullOrEmpty(subida.ZipKey)) return 5;

            DianResponse respuesta = null;
            for (var intento = 1; intento <= 12; intento++)
            {
                Console.WriteLine("Consultando validaciones (intento " + intento + ")...");
                respuesta = cliente.ConsultarDocumentos(subida.ZipKey).FirstOrDefault();
                if (respuesta != null && (respuesta.XmlBase64Bytes != null || respuesta.ErrorMessage != null)) break;
                Thread.Sleep(10000);
            }

            if (respuesta == null)
            {
                Console.WriteLine("La DIAN no devolvió respuesta todavía.");
                return 6;
            }

            Console.WriteLine(respuesta.StatusCode + " - " + respuesta.StatusDescription + ": " + respuesta.StatusMessage);
            if (respuesta.ErrorMessage != null)
            {
                Console.WriteLine(string.Join(Environment.NewLine, respuesta.ErrorMessage));
            }
            if (respuesta.XmlBase64Bytes != null)
            {
                File.WriteAllText(Path.Combine(salida, numero + "-respuesta-dian.xml"), Encoding.UTF8.GetString(respuesta.XmlBase64Bytes));
            }

            return respuesta.StatusCode == RespuestaDian.PROCESADO_CORRECTAMENTE ? 0 : 7;
        }

    }

    // ---- Configuración local (config-local\*.json) ----
    internal class EmisorCfg
    {
        public string RazonSocial { get; set; }
        public string Nit { get; set; }
        public string Dv { get; set; }
        public string TestSetId { get; set; }
        public CertCfg Certificado { get; set; }
    }

    internal class CertCfg
    {
        public string Ruta { get; set; }
        public string VariableEntornoClave { get; set; }
    }

    internal class HabilitacionCfg
    {
        public RangoCfg RangoNumeracionAsignado { get; set; }
        public SoftwareCfg InformacionSoftware { get; set; }
    }

    internal class RangoCfg
    {
        public string Prefijo { get; set; }
        public string NumeroResolucion { get; set; }
        public long RangoDesde { get; set; }
        public long RangoHasta { get; set; }
        public string FechaDesde { get; set; }
        public string FechaHasta { get; set; }
    }

    internal class SoftwareCfg
    {
        public string Identificacion { get; set; }
        public string Nombre { get; set; }
        public string ClaveTecnica { get; set; }
        public string Pin { get; set; }
    }

    internal class NominaCfg
    {
        public string SoftwareIdentificacion { get; set; }
        public string SoftwarePin { get; set; }
        public string TestSetId { get; set; }
        public string Prefijo { get; set; }
    }

    internal class DsCfg
    {
        public string SoftwareIdentificacion { get; set; }
        public string SoftwarePin { get; set; }
        public string TestSetId { get; set; }
        public string Prefijo { get; set; }
        public string NumeroResolucion { get; set; }
        public long? RangoDesde { get; set; }
        public long? RangoHasta { get; set; }
        public string FechaResolucion { get; set; }
        public string VigenciaDesde { get; set; }
        public string VigenciaHasta { get; set; }
    }

    internal class CodigoNombre
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }

    internal class RutCfg
    {
        public CodigoNombre Departamento { get; set; }
        public CodigoNombre Municipio { get; set; }
        public string Direccion { get; set; }
        public string TipoPersona { get; set; }
        public string RegimenFiscal { get; set; }
        public string ResponsabilidadTributaria { get; set; }
        public string MatriculaMercantil { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
    }
}
