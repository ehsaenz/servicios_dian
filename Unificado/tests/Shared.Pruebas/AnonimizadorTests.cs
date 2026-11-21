using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anonimizador;
using DIAN.Impresion;
using DIAN.Traduccion;
using eFacturacionColombia_NETCore.Documentos;
using eFacturacionColombia_NETCore.Util;
using FacturacionColombia_Metadatos;
using FE.Models.Doc;
using Newtonsoft.Json.Linq;

namespace Shared.Pruebas;

public class AnonimizadorTests : IDisposable
{
    private readonly string _raizTemporal = Path.Combine(
        Path.GetTempPath(),
        "dian-anonimizador-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raizTemporal))
        {
            Directory.Delete(_raizTemporal, recursive: true);
        }
    }

    [Fact]
    public void Anonimiza_tablas_ERP_elimina_PII_conserva_222222222222_e_integridad_referencial()
    {
        var origen = Path.Combine(_raizTemporal, "origen");
        var destino = Path.Combine(_raizTemporal, "destino");
        Directory.CreateDirectory(origen);

        var nitEmpresa = "900555111";
        var cedulaNatural = "1098765432";
        var correoEmpresa = "contabilidad@empresa-real.com.co";
        var correoPersona = "ana.gomez@correo-real.co";
        var telEmpresa = "6024445566";
        var celPersona = "3158889900";
        var dirReal = "AVENIDA SIEMPREVIVA 742 LOCAL 9";

        File.WriteAllText(Path.Combine(origen, "terceros_clientes.json"), $$"""
        [
          {
            "IdTercero": "{{AnonimizadorMuestras.NitConsumidorFinal}}",
            "CodigoContrib": "{{AnonimizadorMuestras.NitConsumidorFinal}}",
            "IdSinc": "{{AnonimizadorMuestras.NitConsumidorFinal}}",
            "EsEstablecimiento": 0,
            "Nombre": "CUANTIAS MENORES REAL",
            "Apellido": "",
            "Direccion": "DIR REAL CF 123",
            "CodMunicipio": "11001",
            "Telefono": "3112223344",
            "Contacto": "CAJERO REAL",
            "Email": "cf@real-erp.co",
            "FechaNac": null,
            "Clave": "secreto_tercero"
          },
          {
            "IdTercero": "{{nitEmpresa}}",
            "CodigoContrib": "{{nitEmpresa}}",
            "IdSinc": "{{nitEmpresa}}",
            "EsEstablecimiento": -1,
            "Nombre": "COMERCIALIZADORA REAL S.A.S.",
            "Apellido": "",
            "Direccion": "{{dirReal}}",
            "CodMunicipio": "76001",
            "Telefono": "{{telEmpresa}}",
            "Contacto": "GERENTE REAL",
            "Email": "{{correoEmpresa}}",
            "EmailFE": "fe-real@empresa-real.com.co",
            "FechaNac": null
          },
          {
            "IdTercero": "{{cedulaNatural}}",
            "CodigoContrib": "{{cedulaNatural}}",
            "IdSinc": "{{cedulaNatural}}",
            "EsEstablecimiento": 0,
            "Nombre": "ANA MILENA",
            "Apellido": "GOMEZ RUIZ",
            "Direccion": "CALLE FALSA 99-88",
            "CodMunicipio": "05001",
            "Telefono": "{{celPersona}}",
            "Contacto": "",
            "Email": "{{correoPersona}}",
            "FechaNac": "1985-03-14T00:00:00"
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "facturas_fv.json"), $$"""
        [
          {
            "ID": 10,
            "IdDocSal": "0000000010",
            "TipoDocumento": "FV",
            "NumeroDoc": "SETP-990000001",
            "CodTercero": "{{AnonimizadorMuestras.NitConsumidorFinal}}",
            "Vendedor": "JPEREZ",
            "NumeroRef": "MG-000123",
            "Nota": "Entregar a Sr. Real cel 3158889900",
            "Guia": "GUIA998877"
          },
          {
            "ID": 11,
            "IdDocSal": "0000000011",
            "TipoDocumento": "FV",
            "NumeroDoc": "SETP-990000002",
            "CodTercero": "{{nitEmpresa}}",
            "Vendedor": "JPEREZ",
            "NumeroRef": "MG-000123",
            "Nota": "Llamar a contabilidad@empresa-real.com.co",
            "Guia": "GUIA554433"
          },
          {
            "ID": 12,
            "IdDocSal": "0000000012",
            "TipoDocumento": "FV",
            "NumeroDoc": "SETP-990000003",
            "CodTercero": "{{cedulaNatural}}",
            "Vendedor": "MLOPEZ",
            "NumeroRef": null,
            "Nota": null
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "facturas_fv_lineas.json"), """
        [
          {
            "IdItemSal": 1,
            "DocRelacionado": "0000000010",
            "CodProducto": "PROD99",
            "Detalle": "ZAPATO MARCA REAL EXCLUSIVA",
            "Cant": 1.0,
            "ValorUnit": 50000.0,
            "DtoDir": 0.0,
            "Excluido": 0
          },
          {
            "IdItemSal": 2,
            "DocRelacionado": "0000000011",
            "CodProducto": "PROD99",
            "Detalle": "ZAPATO MARCA REAL EXCLUSIVA",
            "Cant": 2.0,
            "ValorUnit": 150000.0,
            "DtoDir": 0.0,
            "Excluido": 0
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "facturas_fv_impuestos.json"), """
        [
          {
            "Id": 101,
            "ItemSalId": 1,
            "ImpuestoId": "01",
            "TarifaId": "0132",
            "BaseGravada": 42016.81,
            "BaseNoGravada": 0.0,
            "TarifaAplicada": 0.19,
            "ValorImpuesto": 7983.19,
            "IncluidoEnPrecio": 1,
            "OrdenAplicacion": 1
          },
          {
            "Id": 102,
            "ItemSalId": 2,
            "ImpuestoId": "01",
            "TarifaId": "0132",
            "BaseGravada": 252100.84,
            "BaseNoGravada": 0.0,
            "TarifaAplicada": 0.19,
            "ValorImpuesto": 47899.16,
            "IncluidoEnPrecio": 1,
            "OrdenAplicacion": 1
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "productos_usados.json"), """
        [
          {
            "IdProd": "PROD99",
            "NomProd": "ZAPATO MARCA REAL EXCLUSIVA",
            "NombreComercial": "ZAPATO MARCA REAL EXCLUSIVA",
            "Descripcion": "DETALLE INTERNO CLIENTE",
            "Referencia": "REF-REAL-99"
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "sucursales.json"), $$"""
        [
          {
            "IdSucursal": 1,
            "Nombre": "SEDE PRINCIPAL REAL",
            "Prefijo": "PR",
            "Direccion": "CARRERA REAL 45-67",
            "Telefonos": "6017778899",
            "CodigoTienda": "{{nitEmpresa}}",
            "Logo": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk",
            "LogoFact": "/9j/4AAQSkZJRgABAQEASABIAAD/2wBD",
            "Email": "tienda@empresa-real.com.co"
          }
        ]
        """);

        File.WriteAllText(Path.Combine(origen, "resoluciones.json"), """
        [
          {
            "ID": 1,
            "TipDoc": "FV",
            "NumeroRes": "187640999888",
            "Prefijo": "SETP",
            "ConsecutivoIni": 990000000,
            "ConsecutivoFin": 995000000,
            "ClaveTecnica": "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
          }
        ]
        """);

        var anonimizador = new AnonimizadorMuestras();
        var resumen = anonimizador.AnonimizarDirectorio(origen, destino);

        Assert.Equal(7, resumen.ArchivosProcesados);
        Assert.Equal(2, resumen.FacturasAceptadas);
        Assert.Equal(1, resumen.FacturasDescartadas);

        var textoSalidaTotal = string.Join(
            "\n",
            Directory.GetFiles(destino, "*.json").Select(f => File.ReadAllText(f, Encoding.UTF8)));

        foreach (var secretoOPii in new[]
        {
            nitEmpresa, cedulaNatural, correoEmpresa, "fe-real@empresa-real.com.co", correoPersona,
            "cf@real-erp.co", "tienda@empresa-real.com.co", telEmpresa, celPersona, "3112223344",
            "6017778899", dirReal, "CALLE FALSA 99-88", "CARRERA REAL 45-67",
            "COMERCIALIZADORA REAL S.A.S.", "ANA MILENA", "GOMEZ RUIZ", "SEDE PRINCIPAL REAL",
            "ZAPATO MARCA REAL EXCLUSIVA", "187640999888",
            "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c", "secreto_tercero",
            "iVBORw0KGgo", "/9j/4AAQSkZJRg", "1985-03-14"
        })
        {
            Assert.DoesNotContain(secretoOPii, textoSalidaTotal, StringComparison.OrdinalIgnoreCase);
        }

        var tercerosOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "terceros_clientes.json")))!.AsArray();
        var facturasOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "facturas_fv.json")))!.AsArray();
        var lineasOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "facturas_fv_lineas.json")))!.AsArray();
        var productosOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "productos_usados.json")))!.AsArray();
        var sucursalesOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "sucursales.json")))!.AsArray();
        var resolucionesOut = JsonNode.Parse(File.ReadAllText(Path.Combine(destino, "resoluciones.json")))!.AsArray();

        // La factura sin lineas (ID=12) fue descartada sin inventar importes.
        Assert.Equal(2, facturasOut.Count);
        Assert.Equal(2, lineasOut.Count);

        // Consumidor final 222222222222 se conserva intacto en terceros y en su factura.
        Assert.Equal(AnonimizadorMuestras.NitConsumidorFinal, tercerosOut[0]!["IdTercero"]!.GetValue<string>());
        Assert.Equal("CONSUMIDOR FINAL", tercerosOut[0]!["Nombre"]!.GetValue<string>());
        Assert.Equal(AnonimizadorMuestras.NitConsumidorFinal, facturasOut[0]!["CodTercero"]!.GetValue<string>());

        // Integridad referencial y conservacion de municipio DANE.
        var nitEmpresaAnon = tercerosOut[1]!["IdTercero"]!.GetValue<string>();
        Assert.Equal(nitEmpresaAnon, tercerosOut[1]!["CodigoContrib"]!.GetValue<string>());
        Assert.Equal(nitEmpresaAnon, tercerosOut[1]!["IdSinc"]!.GetValue<string>());
        Assert.Equal(nitEmpresaAnon, facturasOut[1]!["CodTercero"]!.GetValue<string>());
        Assert.Equal(nitEmpresaAnon, sucursalesOut[0]!["CodigoTienda"]!.GetValue<string>());
        Assert.Equal("76001", tercerosOut[1]!["CodMunicipio"]!.GetValue<string>());

        // IdProd == CodProducto tras anonimizar e importes intactos.
        var skuAnon = productosOut[0]!["IdProd"]!.GetValue<string>();
        Assert.Equal(skuAnon, lineasOut[1]!["CodProducto"]!.GetValue<string>());
        Assert.Equal(150000.0m, lineasOut[1]!["ValorUnit"]!.GetValue<decimal>());

        // ClaveTecnica reemplazada por marcador seguro y logos limpiados.
        Assert.Equal(AnonimizadorMuestras.MarcadorClaveTecnica, resolucionesOut[0]!["ClaveTecnica"]!.GetValue<string>());
        Assert.Equal("", sucursalesOut[0]!["Logo"]!.GetValue<string>());
        Assert.Equal("", sucursalesOut[0]!["LogoFact"]!.GetValue<string>());
    }

    [Fact]
    public void Anonimiza_DocumentoDian_recalcula_DV_y_oculta_ClaveTecnica()
    {
        var origen = Path.Combine(_raizTemporal, "origen-doc");
        var destino = Path.Combine(_raizTemporal, "destino-doc");
        Directory.CreateDirectory(origen);

        var nitEmisorReal = "890123456";
        var docJson = $$"""
        {
          "TipoDocDian": "91",
          "Numero": "SETP99000555",
          "Resolucion": {
            "NumeroResolucion": "187640111222",
            "Prefijo": "SETP",
            "ClaveTecnica": "clave-real-super-secreta"
          },
          "DocumentoRef": {
            "Numero": "SETP99000500",
            "CUFE": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "Descripcion": "Nota de prueba cliente real"
          },
          "Emisor": {
            "TipoPersona": "JURIDICA",
            "NIT": "{{nitEmisorReal}}",
            "DV": "1",
            "RazonSocial": "EMPRESA REAL S.A.S.",
            "EmailContacto": "real@empresa-secreta.co",
            "TelefonoContacto": "6025551234",
            "LocacionPrincipal": { "Direccion": "CALLE REAL 10 # 20-30", "CodigoMunicipio": "76001" }
          },
          "Receptor": {
            "TipoPersona": "NATURAL",
            "NIT": "{{AnonimizadorMuestras.NitConsumidorFinal}}",
            "DV": "7",
            "PrimerNombre": "CONSUMIDOR",
            "PrimerApellido": "FINAL",
            "EmailContacto": "cf@real.co",
            "TelefonoContacto": "3001112233",
            "LocacionPrincipal": { "Direccion": "SIN DIRECCION REAL", "CodigoMunicipio": "76001" }
          },
          "Items": [
            { "Codigo": "REAL-01", "Detalle": "ITEM REAL", "Cant": 1, "ValorUnit": 10000 }
          ],
          "Notas": [ "Observacion con cedula 12345678" ]
        }
        """;
        File.WriteAllText(Path.Combine(origen, "nota.json"), docJson);

        new AnonimizadorMuestras().AnonimizarDirectorio(origen, destino);

        var salidaTexto = File.ReadAllText(Path.Combine(destino, "nota.json"), Encoding.UTF8);
        foreach (var sensible in new[]
        {
            nitEmisorReal, "187640111222", "clave-real-super-secreta", "EMPRESA REAL S.A.S.",
            "real@empresa-secreta.co", "cf@real.co", "6025551234", "3001112233",
            "CALLE REAL 10 # 20-30", "12345678", "aaaaaaaaaaaa"
        })
        {
            Assert.DoesNotContain(sensible, salidaTexto, StringComparison.OrdinalIgnoreCase);
        }

        var docOut = JsonNode.Parse(salidaTexto)!.AsObject();
        Assert.Equal(AnonimizadorMuestras.MarcadorClaveTecnica, docOut["Resolucion"]!["ClaveTecnica"]!.GetValue<string>());

        var nitAnon = docOut["Emisor"]!["NIT"]!.GetValue<string>();
        var dvAnon = docOut["Emisor"]!["DV"]!.GetValue<string>();
        Assert.Equal(NitHelper.CalcularDigitoVerificador(nitAnon).ToString(), dvAnon);
        Assert.Equal("76001", docOut["Emisor"]!["LocacionPrincipal"]!["CodigoMunicipio"]!.GetValue<string>());

        Assert.Equal(AnonimizadorMuestras.NitConsumidorFinal, docOut["Receptor"]!["NIT"]!.GetValue<string>());
        Assert.Equal("7", docOut["Receptor"]!["DV"]!.GetValue<string>());
    }

    [Fact]
    public void Muestras_anonimas_del_repositorio_no_conservan_ningun_NIT_correo_telefono_ni_credencial_original()
    {
        var origenLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dian",
            "muestras-dev");

        var carpetaMuestrasRepo = ResolverCarpetaMuestrasRepo();
        if (carpetaMuestrasRepo == null || !Directory.Exists(origenLocal))
        {
            return;
        }

        var salidaTextos = Directory.GetFiles(carpetaMuestrasRepo, "*.json")
            .Select(f => File.ReadAllText(f, Encoding.UTF8))
            .ToArray();
        Assert.NotEmpty(salidaTextos);
        var todoTextoSalida = string.Join("\n", salidaTextos);

        var resolucionesTexto = File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "resoluciones.json"), Encoding.UTF8);
        Assert.DoesNotContain("ClaveTecnica", resolucionesTexto, StringComparison.OrdinalIgnoreCase);

        var originalesSensibles = ExtraerValoresSensibles(origenLocal);
        Assert.NotEmpty(originalesSensibles);

        foreach (var valorOriginal in originalesSensibles)
        {
            Assert.DoesNotContain(valorOriginal, todoTextoSalida, StringComparison.OrdinalIgnoreCase);
        }

        var sucAnon = JsonNode.Parse(File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "sucursales.json")))!.AsArray();
        Assert.All(sucAnon, s =>
        {
            Assert.Equal("", s!["Logo"]?.GetValue<string>() ?? "");
            Assert.Equal("", s!["LogoFact"]?.GetValue<string>() ?? "");
        });

        // Verifica filtrado sin invencion: solo las 6 FV completas (9 lineas, 9 impuestos) y 9 terceros con CodMunicipio.
        var fvsAnon = JsonNode.Parse(File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "facturas_fv.json")))!.AsArray();
        var lineasAnon = JsonNode.Parse(File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "facturas_fv_lineas.json")))!.AsArray();
        var impsAnon = JsonNode.Parse(File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "facturas_fv_impuestos.json")))!.AsArray();
        var tercAnon = JsonNode.Parse(File.ReadAllText(Path.Combine(carpetaMuestrasRepo, "terceros_clientes.json")))!.AsArray();

        Assert.Equal(6, fvsAnon.Count);
        Assert.Equal(9, lineasAnon.Count);
        Assert.Equal(9, impsAnon.Count);
        Assert.Equal(9, tercAnon.Count);
        Assert.All(fvsAnon, f => Assert.Null(f!["Resolucion"]));
        Assert.All(tercAnon, t => Assert.Matches(@"^\d{5}$", t!["CodMunicipio"]?.GetValue<string>() ?? ""));
    }

    [Fact]
    public void Factura_qa_anonimizada_se_traduce_a_UBL_sin_errores_y_sin_credenciales()
    {
        var carpetaMuestrasRepo = ResolverCarpetaMuestrasRepo();
        Assert.NotNull(carpetaMuestrasRepo);

        var rutaFacturaQa = Path.Combine(carpetaMuestrasRepo!, "factura-qa-fv016.json");
        Assert.True(File.Exists(rutaFacturaQa), $"Falta {rutaFacturaQa}");

        var json = File.ReadAllText(rutaFacturaQa, Encoding.UTF8);
        var doc = JObject.Parse(json).ToObject<DocumentoDian>()!;

        Assert.Equal("01", doc.TipoDocDian);
        Assert.Equal(AnonimizadorMuestras.MarcadorClaveTecnica, doc.Resolucion.ClaveTecnica);
        Assert.Equal(NitHelper.CalcularDigitoVerificador(doc.Emisor.NIT).ToString(), doc.Emisor.DV);
        Assert.Equal(NitHelper.CalcularDigitoVerificador(doc.Receptor.NIT).ToString(), doc.Receptor.DV);
        Assert.Equal(3, doc.Items.Count);

        var parametros = new ParametrosEmision
        {
            Ambiente = AmbienteDestino.PRUEBAS,
            SoftwareNit = doc.Emisor.NIT,
            SoftwareIdentificador = "00000000-0000-0000-0000-000000000000",
            SoftwarePin = "12345"
        };

        var factura = TraductorFactura.Factura(doc, parametros);
        var xml = XDocument.Parse(factura.SerializeXmlDocument().ToXmlString());

        decimal ParseValor(XElement el) => decimal.Parse(el.Value, CultureInfo.InvariantCulture);

        var lineasXml = xml.Root!.Elements(Datos.Cac + "InvoiceLine").ToList();
        Assert.Equal(3, lineasXml.Count);

        var sumaLineas = lineasXml.Sum(l => ParseValor(l.Element(Datos.Cbc + "LineExtensionAmount")!));
        var total = xml.Root.Element(Datos.Cac + "LegalMonetaryTotal")!;
        var impuestosXml = xml.Root.Elements(Datos.Cac + "TaxTotal").Sum(t => ParseValor(t.Element(Datos.Cbc + "TaxAmount")!));

        Assert.Equal(39159.66m, sumaLineas);
        Assert.Equal(39159.66m, ParseValor(total.Element(Datos.Cbc + "LineExtensionAmount")!));
        Assert.Equal(7440.34m, impuestosXml);
        Assert.Equal(46600.00m, ParseValor(total.Element(Datos.Cbc + "PayableAmount")!));
    }

    private static List<string> ExtraerValoresSensibles(string origen)
    {
        var resultados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Agregar(JsonNode? n, int minLen = 4)
        {
            if (n is JsonValue v && v.TryGetValue<string>(out var s))
            {
                var t = s.Trim();
                if (t.Length >= minLen && t != AnonimizadorMuestras.NitConsumidorFinal)
                {
                    resultados.Add(t);
                }
            }
        }

        foreach (var archivo in Directory.GetFiles(origen, "*.json"))
        {
            var nombre = Path.GetFileName(archivo).ToLowerInvariant();
            var nodo = JsonNode.Parse(File.ReadAllText(archivo, Encoding.UTF8));
            if (nodo is not JsonArray arr) continue;

            foreach (var fila in arr.OfType<JsonObject>())
            {
                switch (nombre)
                {
                    case "terceros_clientes.json":
                        Agregar(fila["IdTercero"]);
                        Agregar(fila["CodigoContrib"]);
                        Agregar(fila["IdSinc"]);
                        Agregar(fila["Nombre"]);
                        Agregar(fila["Apellido"]);
                        Agregar(fila["Direccion"], 5);
                        Agregar(fila["Telefono"], 6);
                        Agregar(fila["Email"], 5);
                        Agregar(fila["EmailFE"], 5);
                        break;
                    case "sucursales.json":
                        Agregar(fila["CodigoTienda"]);
                        Agregar(fila["Nombre"]);
                        Agregar(fila["Direccion"], 5);
                        Agregar(fila["Telefonos"], 6);
                        Agregar(fila["Email"], 5);
                        if (fila["Logo"] is JsonValue lv && lv.TryGetValue<string>(out var l) && l.Length >= 24)
                        {
                            resultados.Add(l[..24]);
                        }
                        if (fila["LogoFact"] is JsonValue lfv && lfv.TryGetValue<string>(out var lf) && lf.Length >= 24)
                        {
                            resultados.Add(lf[..24]);
                        }
                        break;
                    case "resoluciones.json":
                        Agregar(fila["NumeroRes"], 5);
                        break;
                    case "facturas_fv.json":
                        Agregar(fila["CodTercero"]);
                        Agregar(fila["Nota"], 6);
                        Agregar(fila["Guia"], 5);
                        break;
                }
            }
        }

        return resultados.ToList();
    }

    private static string? ResolverCarpetaMuestrasRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidato = Path.Combine(dir.FullName, "tools", "SimuladorErp", "muestras-anonimas");
            if (Directory.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }
        return null;
    }
}
