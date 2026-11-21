using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using eFacturacionColombia_NETCore.Documentos;

namespace Anonimizador;

/// <summary>
/// Resultado de ejecutar la anonimización sobre un directorio de muestras JSON.
/// </summary>
public sealed record ResumenAnonimizacion(int ArchivosProcesados, int RegistrosProcesados, string CarpetaDestino);

/// <summary>
/// Anonimiza archivos JSON exportados desde QuantyERP y documentos <c>DocumentoDian</c>
/// de forma determinística por ejecución mediante HMAC-SHA256 con sal efímera en memoria,
/// siguiendo <c>docs/integracion/ejemplos-anonimizados.md</c>.
/// </summary>
public sealed class AnonimizadorMuestras
{
    public const string NitConsumidorFinal = "222222222222";
    public const string DvConsumidorFinal = "7";
    public const string MarcadorClaveTecnica = "<CLAVE_TECNICA_DE_LA_RESOLUCION>";

    private static readonly string[] PrimerosNombres =
        ["CARLOS", "MARIA", "ANDRES", "LAURA", "LUIS", "SOFIA", "CAMILO", "VALERIA"];
    private static readonly string[] SegundosNombres =
        ["EDUARDO", "FERNANDA", "DAVID", "PAOLA", "FELIPE", "NATALIA", "ALEJANDRO", "DANIELA"];
    private static readonly string[] PrimerosApellidos =
        ["PEREZ", "GOMEZ", "RODRIGUEZ", "MARTINEZ", "LOPEZ", "TORRES", "RAMIREZ", "VARGAS"];
    private static readonly string[] SegundosApellidos =
        ["DIAZ", "MORENO", "CASTRO", "ROJAS", "HERRERA", "RUIZ", "MENDOZA", "SILVA"];
    private static readonly string[] Vias =
        ["CALLE", "CARRERA", "AVENIDA", "TRANSVERSAL", "DIAGONAL"];

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly HashSet<string> CamposCredencial = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClaveTecnica", "Clave", "Password", "Contrasena", "Secret", "Secreto", "ApiKey", "Token", "PIN"
    };

    private readonly byte[] _sal;
    private readonly Dictionary<(string Dominio, string Original), string> _mapa = new();
    private readonly Dictionary<string, int> _contadores = new(StringComparer.Ordinal);
    private readonly HashSet<string> _originalesBloqueados = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _asignados = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Crea un anonimizador con una sal efímera aleatoria en memoria (o una sal explícita para pruebas reproducibles).
    /// </summary>
    public AnonimizadorMuestras(byte[]? sal = null)
    {
        _sal = sal is { Length: > 0 } ? sal.ToArray() : RandomNumberGenerator.GetBytes(32);
    }

    /// <summary>
    /// Anonimiza todos los archivos <c>*.json</c> de <paramref name="origen"/> y escribe las copias limpias en <paramref name="destino"/>.
    /// </summary>
    public ResumenAnonimizacion AnonimizarDirectorio(string origen, string destino)
    {
        if (!Directory.Exists(origen))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de origen: {origen}");
        }

        var archivos = Directory.GetFiles(origen, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (archivos.Length == 0)
        {
            throw new InvalidOperationException($"No se encontraron archivos JSON en: {origen}");
        }

        Directory.CreateDirectory(destino);
        var nodosPorArchivo = new List<(string NombreArchivo, JsonNode Nodo)>(archivos.Length);

        foreach (var ruta in archivos)
        {
            var texto = File.ReadAllText(ruta, Encoding.UTF8);
            var nodo = JsonNode.Parse(texto)
                ?? throw new InvalidDataException($"El archivo JSON está vacío o es nulo: {Path.GetFileName(ruta)}");
            RegistrarOriginalesSensibles(nodo);
            nodosPorArchivo.Add((Path.GetFileName(ruta), nodo));
        }

        var totalRegistros = 0;
        foreach (var (nombre, nodo) in nodosPorArchivo)
        {
            totalRegistros += AnonimizarArchivo(nombre, nodo);
            var salida = Path.Combine(destino, nombre);
            File.WriteAllText(salida, nodo.ToJsonString(OpcionesJson) + Environment.NewLine, new UTF8Encoding(false));
        }

        return new ResumenAnonimizacion(nodosPorArchivo.Count, totalRegistros, destino);
    }

    private void RegistrarOriginalesSensibles(JsonNode nodo)
    {
        if (nodo is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null) RegistrarOriginalesSensibles(item);
            }
            return;
        }

        if (nodo is not JsonObject obj) return;

        foreach (var kv in obj)
        {
            if (kv.Value is JsonValue val && val.TryGetValue<string>(out var texto))
            {
                var limpio = texto.Trim();
                if (limpio.Length >= 4 && limpio != NitConsumidorFinal)
                {
                    _originalesBloqueados.Add(limpio);
                }
            }
            else if (kv.Value != null)
            {
                RegistrarOriginalesSensibles(kv.Value);
            }
        }
    }

    private int AnonimizarArchivo(string nombreArchivo, JsonNode nodo)
    {
        if (nodo is JsonObject obj)
        {
            if (obj.ContainsKey("Emisor") || obj.ContainsKey("Receptor") || obj.ContainsKey("DocumentoRef"))
            {
                AnonimizarDocumentoDian(obj);
                return 1;
            }

            AnonimizarObjetoPorNombre(nombreArchivo, obj);
            return 1;
        }

        if (nodo is not JsonArray arr) return 0;

        foreach (var elemento in arr)
        {
            if (elemento is JsonObject fila)
            {
                AnonimizarObjetoPorNombre(nombreArchivo, fila);
            }
        }
        return arr.Count;
    }

    private void AnonimizarObjetoPorNombre(string nombreArchivo, JsonObject obj)
    {
        LimpiarCredencialesGenericas(obj);
        var nombre = nombreArchivo.ToLowerInvariant();

        switch (nombre)
        {
            case "terceros_clientes.json":
                AnonimizarTerceroSql(obj);
                break;
            case "sucursales.json":
                AnonimizarSucursalSql(obj);
                break;
            case "facturas_fv.json":
                AnonimizarFacturaSql(obj);
                break;
            case "facturas_fv_lineas.json":
                AnonimizarLineaFacturaSql(obj);
                break;
            case "productos_usados.json":
                AnonimizarProductoSql(obj);
                break;
            case "resoluciones.json":
                AnonimizarResolucionSql(obj);
                break;
            case "facturas_fv_impuestos.json":
                break;
            default:
                AnonimizarGenerico(obj);
                break;
        }
    }

    private void AnonimizarTerceroSql(JsonObject obj)
    {
        var originalId = LeerTexto(obj, "IdTercero") ?? LeerTexto(obj, "CodigoContrib") ?? string.Empty;
        var es222 = string.Equals(originalId.Trim(), NitConsumidorFinal, StringComparison.Ordinal);

        if (es222)
        {
            ReemplazarSiPresente(obj, "IdTercero", NitConsumidorFinal);
            ReemplazarSiPresente(obj, "CodigoContrib", NitConsumidorFinal);
            ReemplazarSiPresente(obj, "Codigo", NitConsumidorFinal);
            ReemplazarSiTieneTexto(obj, "IdSinc", NitConsumidorFinal);
            ReemplazarSiTieneTexto(obj, "Nombre", "CONSUMIDOR FINAL");
            ReemplazarSiPresente(obj, "Apellido", "");
            ReemplazarSiPresente(obj, "Contacto", "");
        }
        else
        {
            var sintetico = string.IsNullOrWhiteSpace(originalId) ? null : MapearIdentificacion("tercero", originalId, esJuridica: EsTerceroJuridico(obj, originalId));
            if (sintetico != null)
            {
                ReemplazarSiPresente(obj, "IdTercero", sintetico);
                MapearCampoIdentificacion(obj, "CodigoContrib", "tercero", EsTerceroJuridico(obj, LeerTexto(obj, "CodigoContrib")));
                MapearCampoIdentificacion(obj, "Codigo", "tercero", EsTerceroJuridico(obj, LeerTexto(obj, "Codigo")));
                MapearCampoIdentificacion(obj, "IdSinc", "tercero", esJuridica: null);
            }

            if (EsTerceroJuridico(obj, originalId))
            {
                MapearTextoSiNoVacio(obj, "Nombre", "razon_tercero", k => $"CLIENTE EJEMPLO {SiguienteNumero("cli")} S.A.S.");
                ReemplazarSiTieneTexto(obj, "Apellido", "");
            }
            else
            {
                AnonimizarNombrePersona(obj, "Nombre", "Apellido", originalId);
            }

            if (!string.IsNullOrWhiteSpace(LeerTexto(obj, "Contacto")))
            {
                var h = HmacBytes("contacto", originalId);
                obj["Contacto"] = $"{PrimerosNombres[h[0] % PrimerosNombres.Length]} {PrimerosApellidos[h[1] % PrimerosApellidos.Length]}";
            }
        }

        MapearDireccionSiNoVacia(obj, "Direccion");
        LimpiarTextoSiNoVacio(obj, "Barrio");
        LimpiarTextoSiNoVacio(obj, "Comuna");
        MapearTelefonoSiNoVacio(obj, "Telefono");
        MapearCorreoSiNoVacio(obj, "Email", "cliente");
        MapearCorreoSiNoVacio(obj, "EmailFE", "fe-cliente");
        MapearTextoSiNoVacio(obj, "MMercantil", "mmercantil", _ => SiguienteNumero("mm").ToString("D5"));
        MapearTextoSiNoVacio(obj, "CtaCorriente", "cta", k => $"CTA-{HexCorto("cta", k, 6)}");

        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, "FechaNac")))
        {
            var anio = 1975 + (HmacBytes("fechanac", originalId)[0] % 25);
            obj["FechaNac"] = $"{anio}-05-15T00:00:00";
        }
    }

    private void AnonimizarSucursalSql(JsonObject obj)
    {
        var tienda = LeerTexto(obj, "CodigoTienda") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(tienda))
        {
            var idTienda = MapearIdentificacion("tercero", tienda, esJuridica: true);
            obj["CodigoTienda"] = idTienda;
        }

        MapearTextoSiNoVacio(obj, "Nombre", "sucursal", _ => $"SUCURSAL EJEMPLO {SiguienteNumero("suc")}");
        MapearDireccionSiNoVacia(obj, "Direccion");
        MapearTelefonoSiNoVacio(obj, "Telefonos");
        MapearCorreoSiNoVacio(obj, "Email", "sucursal");
        LimpiarTextoSiNoVacio(obj, "Logo");
        LimpiarTextoSiNoVacio(obj, "LogoFact");
        MapearTextoSiNoVacio(obj, "Empresa", "empresa", _ => $"EMP{SiguienteNumero("emp"):D2}");

        var prefijo = LeerTexto(obj, "Prefijo");
        if (!string.IsNullOrWhiteSpace(prefijo) && !string.Equals(prefijo, "SETP", StringComparison.OrdinalIgnoreCase))
        {
            obj["Prefijo"] = $"S{SiguienteNumero("prefs"):D1}";
        }
        var prefijoNe = LeerTexto(obj, "PrefijoNE");
        if (!string.IsNullOrWhiteSpace(prefijoNe))
        {
            obj["PrefijoNE"] = "NE";
        }
    }

    private void AnonimizarFacturaSql(JsonObject obj)
    {
        MapearCampoIdentificacion(obj, "CodTercero", "tercero", esJuridica: null);
        MapearCampoIdentificacion(obj, "Tercero2", "tercero", esJuridica: null);
        MapearCampoIdentificacion(obj, "TerceroM", "tercero", esJuridica: null);

        MapearTextoSiNoVacio(obj, "Vendedor", "vendedor", _ => $"V{SiguienteNumero("ven"):D2}");
        MapearTextoSiNoVacio(obj, "CodUsuario", "usuario", _ => $"USR-{SiguienteNumero("usr"):D3}");
        MapearTextoSiNoVacio(obj, "AnuladoPor", "usuario", _ => $"USR-{SiguienteNumero("usr"):D3}");
        MapearTextoSiNoVacio(obj, "NumeroRef", "orden_ref", _ => $"ORD-{SiguienteNumero("ord"):D6}");
        MapearTextoSiNoVacio(obj, "Guia", "guia", k => $"GUIA-{HexCorto("guia", k, 8)}");
        MapearTextoSiNoVacio(obj, "CodTransportadora", "transp", _ => $"TRN-{SiguienteNumero("trn"):D2}");

        ReemplazarSiTieneTexto(obj, "Nota", "Documento de ejemplo anonimizado.");
        ReemplazarSiTieneTexto(obj, "NotaAnulacion", "Anulacion de ejemplo anonimizada.");
        ReemplazarSiTieneTexto(obj, "Motivo", "Motivo de ejemplo anonimizado.");
        MapearTextoSiNoVacio(obj, "IdentificadorPago", "pago", k => $"PAG-{HexCorto("pago", k, 6)}");

        var numeroDoc = LeerTexto(obj, "NumeroDoc");
        if (!string.IsNullOrWhiteSpace(numeroDoc))
        {
            obj["NumeroDoc"] = ObtenerOMapear("numero_fv", numeroDoc, _ => $"SETP-{990001000 + SiguienteNumero("fv")}");
        }

        var cufe = LeerTexto(obj, "Cufe");
        if (!string.IsNullOrWhiteSpace(cufe))
        {
            obj["Cufe"] = ObtenerOMapear("cufe", cufe, CalcularCufeSintetico);
        }
    }

    private void AnonimizarLineaFacturaSql(JsonObject obj)
    {
        var codOriginal = LeerTexto(obj, "CodProducto");
        string? skuSintetico = null;
        if (!string.IsNullOrWhiteSpace(codOriginal))
        {
            skuSintetico = ObtenerOMapear("producto", codOriginal, _ => $"SKU-{SiguienteNumero("sku"):D4}");
            obj["CodProducto"] = skuSintetico;
        }

        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, "Detalle")))
        {
            obj["Detalle"] = skuSintetico != null ? $"Producto de ejemplo {skuSintetico}" : "Producto de ejemplo";
        }

        MapearTextoSiNoVacio(obj, "Serial", "serial", k => $"SER-{HexCorto("ser", k, 8)}");
        MapearTextoSiNoVacio(obj, "Manifiesto", "manifiesto", k => $"MAN-{HexCorto("man", k, 6)}");
    }

    private void AnonimizarProductoSql(JsonObject obj)
    {
        var idOriginal = LeerTexto(obj, "IdProd");
        if (string.IsNullOrWhiteSpace(idOriginal)) return;

        var skuSintetico = ObtenerOMapear("producto", idOriginal, _ => $"SKU-{SiguienteNumero("sku"):D4}");
        obj["IdProd"] = skuSintetico;

        var refOriginal = LeerTexto(obj, "Referencia");
        if (!string.IsNullOrWhiteSpace(refOriginal))
        {
            obj["Referencia"] = ObtenerOMapear("referencia_prod", refOriginal, _ => $"REF-{SiguienteNumero("ref"):D4}");
        }

        ReemplazarSiTieneTexto(obj, "NomProd", $"Producto de ejemplo {skuSintetico}");
        ReemplazarSiTieneTexto(obj, "NombreComercial", $"Producto de ejemplo {skuSintetico}");
        ReemplazarSiTieneTexto(obj, "Descripcion", $"Descripcion de ejemplo {skuSintetico}");
        ReemplazarSiTieneTexto(obj, "NombreEspMin", $"Producto {skuSintetico}");
        ReemplazarSiTieneTexto(obj, "NombreFiscal", $"Producto fiscal {skuSintetico}");
        ReemplazarSiTieneTexto(obj, "Alias", $"Alias {skuSintetico}");
        LimpiarTextoSiNoVacio(obj, "DataSheet");
        LimpiarTextoSiNoVacio(obj, "Referencia_Fab");
        LimpiarTextoSiNoVacio(obj, "Ubicacion");
        LimpiarTextoSiNoVacio(obj, "GrupoEM");
        MapearTextoSiNoVacio(obj, "CodBarras", "barras", k => $"770000{HexNumerico("barras", k, 7)}");
        MapearTextoSiNoVacio(obj, "CodigoSP", "codigosp", _ => $"SP{SiguienteNumero("sp"):D2}");

        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, "LlavePrecio")))
        {
            var llave = LeerTexto(obj, "LlavePrecio")!;
            obj["LlavePrecio"] = _mapa.TryGetValue(("producto", llave), out var skuMapped)
                ? skuMapped
                : ObtenerOMapear("llave_precio", llave, _ => $"LP-{SiguienteNumero("lp"):D4}");
        }

        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, "LlaveBOM")))
        {
            var bom = LeerTexto(obj, "LlaveBOM")!;
            obj["LlaveBOM"] = ObtenerOMapear("llave_bom", bom, _ => $"BOM-{SiguienteNumero("bom"):D4}");
        }
    }

    private void AnonimizarResolucionSql(JsonObject obj)
    {
        var numRes = LeerTexto(obj, "NumeroRes");
        if (!string.IsNullOrWhiteSpace(numRes))
        {
            obj["NumeroRes"] = ObtenerOMapear("resolucion", numRes, _ => GenerarResolucionSintetica());
        }

        var prefijo = LeerTexto(obj, "Prefijo");
        if (!string.IsNullOrWhiteSpace(prefijo) && !string.Equals(prefijo, "SETP", StringComparison.OrdinalIgnoreCase))
        {
            obj["Prefijo"] = ObtenerOMapear("prefijo_res", prefijo, _ => $"EJ{SiguienteNumero("pr"):D2}");
        }

        ReemplazarSiTieneTexto(obj, "ClaveTecnica", MarcadorClaveTecnica);
    }

    private void AnonimizarDocumentoDian(JsonObject doc)
    {
        LimpiarCredencialesGenericas(doc);

        var num = LeerTexto(doc, "Numero");
        if (!string.IsNullOrWhiteSpace(num))
        {
            doc["Numero"] = ObtenerOMapear("numero_doc", num, _ => $"SETP{990001000 + SiguienteNumero("doc")}");
        }

        if (doc["Resolucion"] is JsonObject res)
        {
            LimpiarCredencialesGenericas(res);
            var nRes = LeerTexto(res, "NumeroResolucion");
            if (!string.IsNullOrWhiteSpace(nRes))
            {
                res["NumeroResolucion"] = ObtenerOMapear("resolucion", nRes, _ => GenerarResolucionSintetica());
            }
            ReemplazarSiTieneTexto(res, "ClaveTecnica", MarcadorClaveTecnica);
        }

        if (doc["DocumentoRef"] is JsonObject docRef)
        {
            var refNum = LeerTexto(docRef, "Numero");
            if (!string.IsNullOrWhiteSpace(refNum))
            {
                docRef["Numero"] = ObtenerOMapear("numero_doc", refNum, _ => $"SETP{990001000 + SiguienteNumero("doc")}");
            }
            var refCufe = LeerTexto(docRef, "CUFE");
            if (!string.IsNullOrWhiteSpace(refCufe))
            {
                docRef["CUFE"] = ObtenerOMapear("cufe", refCufe, CalcularCufeSintetico);
            }
            ReemplazarSiTieneTexto(docRef, "Descripcion", "Referencia de ejemplo anonimizada.");
        }

        if (doc["OrdenRef"] is JsonObject ordRef)
        {
            MapearTextoSiNoVacio(ordRef, "Numero", "orden_ref", _ => $"ORD-{SiguienteNumero("ord"):D6}");
            ReemplazarSiTieneTexto(ordRef, "Descripcion", "Orden de ejemplo anonimizada.");
        }

        if (doc["Emisor"] is JsonObject emisor) AnonimizarPersonaDian(emisor, esEmisor: true);
        if (doc["Receptor"] is JsonObject receptor) AnonimizarPersonaDian(receptor, esEmisor: false);

        if (doc["Items"] is JsonArray items)
        {
            var idx = 1;
            foreach (var it in items.OfType<JsonObject>())
            {
                var cod = LeerTexto(it, "Codigo");
                var sku = !string.IsNullOrWhiteSpace(cod)
                    ? ObtenerOMapear("producto", cod, _ => $"SKU-{SiguienteNumero("sku"):D4}")
                    : $"SKU-{idx:D4}";
                if (!string.IsNullOrWhiteSpace(cod)) it["Codigo"] = sku;
                MapearTextoSiNoVacio(it, "Referencia", "referencia_prod", _ => $"REF-{SiguienteNumero("ref"):D4}");
                ReemplazarSiTieneTexto(it, "Detalle", $"Producto de ejemplo {sku}");
                idx++;
            }
        }

        if (doc["Notas"] is JsonArray notas)
        {
            for (var i = 0; i < notas.Count; i++)
            {
                if (notas[i] is JsonValue nv && nv.TryGetValue<string>(out var textoNota) && !string.IsNullOrWhiteSpace(textoNota))
                {
                    notas[i] = "Documento de ejemplo. Datos ficticios.";
                }
            }
        }
    }

    private void AnonimizarPersonaDian(JsonObject persona, bool esEmisor)
    {
        var nitOriginal = LeerTexto(persona, "NIT") ?? string.Empty;
        var es222 = string.Equals(nitOriginal.Trim(), NitConsumidorFinal, StringComparison.Ordinal);
        var esJuridica = string.Equals(LeerTexto(persona, "TipoPersona"), "JURIDICA", StringComparison.OrdinalIgnoreCase);

        if (es222)
        {
            persona["NIT"] = NitConsumidorFinal;
            if (persona.ContainsKey("DV")) persona["DV"] = DvConsumidorFinal;
            ReemplazarSiTieneTexto(persona, "RazonSocial", "CONSUMIDOR FINAL");
            ReemplazarSiTieneTexto(persona, "NombreComercial", "CONSUMIDOR FINAL");
            ReemplazarSiPresente(persona, "PrimerNombre", "CONSUMIDOR");
            ReemplazarSiPresente(persona, "PrimerApellido", "FINAL");
            LimpiarTextoSiNoVacio(persona, "SegundoNombre");
            LimpiarTextoSiNoVacio(persona, "SegundoApellido");
            LimpiarTextoSiNoVacio(persona, "NombreContacto");
        }
        else
        {
            var nitSintetico = MapearIdentificacion("tercero", nitOriginal, esJuridica);
            persona["NIT"] = nitSintetico;
            if (persona.ContainsKey("DV"))
            {
                persona["DV"] = NitHelper.CalcularDigitoVerificador(nitSintetico).ToString();
            }

            if (esJuridica)
            {
                var rol = esEmisor ? "EMPRESA" : "ADQUIRIENTE";
                var razon = ObtenerOMapear("razon_social", LeerTexto(persona, "RazonSocial") ?? nitOriginal,
                    _ => $"{rol} DE EJEMPLO {SiguienteNumero(esEmisor ? "em" : "adq")} S.A.S.");
                ReemplazarSiPresente(persona, "RazonSocial", razon);
                ReemplazarSiTieneTexto(persona, "NombreComercial", razon);
            }
            else
            {
                AnonimizarNombrePersona(persona, "PrimerNombre", "PrimerApellido", nitOriginal);
                if (!string.IsNullOrWhiteSpace(LeerTexto(persona, "SegundoNombre")))
                {
                    var h = HmacBytes("nom2", nitOriginal);
                    persona["SegundoNombre"] = SegundosNombres[h[0] % SegundosNombres.Length];
                }
                if (!string.IsNullOrWhiteSpace(LeerTexto(persona, "SegundoApellido")))
                {
                    var h = HmacBytes("ape2", nitOriginal);
                    persona["SegundoApellido"] = SegundosApellidos[h[0] % SegundosApellidos.Length];
                }
                var fullName = $"{LeerTexto(persona, "PrimerNombre")} {LeerTexto(persona, "PrimerApellido")}".Trim();
                ReemplazarSiTieneTexto(persona, "RazonSocial", fullName);
                ReemplazarSiTieneTexto(persona, "NombreComercial", fullName);
            }

            ReemplazarSiTieneTexto(persona, "NombreContacto", "Contacto Ejemplo");
        }

        MapearTextoSiNoVacio(persona, "Codigo", "codigo_terc", _ => $"{(esEmisor ? "EMP" : "CLI")}-{HexCorto("cod", nitOriginal, 4)}");
        MapearTextoSiNoVacio(persona, "MatriculaMercantil", "mmercantil", _ => SiguienteNumero("mm").ToString("D5"));
        MapearCorreoSiNoVacio(persona, "EmailContacto", esEmisor ? "facturacion" : "compras");
        MapearTelefonoSiNoVacio(persona, "TelefonoContacto");

        if (persona["LocacionPrincipal"] is JsonObject locP) MapearDireccionSiNoVacia(locP, "Direccion");
        if (persona["LocacionComercial"] is JsonObject locC) MapearDireccionSiNoVacia(locC, "Direccion");
    }

    private void AnonimizarGenerico(JsonObject obj)
    {
        foreach (var clave in obj.Select(k => k.Key).ToList())
        {
            if (obj[clave] is JsonObject hijo) AnonimizarGenerico(hijo);
            else if (obj[clave] is JsonArray arr)
            {
                foreach (var it in arr.OfType<JsonObject>()) AnonimizarGenerico(it);
            }
            else if (clave.Contains("Email", StringComparison.OrdinalIgnoreCase) || clave.Contains("Correo", StringComparison.OrdinalIgnoreCase))
            {
                MapearCorreoSiNoVacio(obj, clave, "contacto");
            }
            else if (clave.Contains("Telefono", StringComparison.OrdinalIgnoreCase))
            {
                MapearTelefonoSiNoVacio(obj, clave);
            }
            else if (clave.Equals("Direccion", StringComparison.OrdinalIgnoreCase))
            {
                MapearDireccionSiNoVacia(obj, clave);
            }
        }
    }

    private void LimpiarCredencialesGenericas(JsonObject obj)
    {
        foreach (var clave in obj.Select(k => k.Key).ToList())
        {
            if (CamposCredencial.Contains(clave))
            {
                if (clave.Equals("ClaveTecnica", StringComparison.OrdinalIgnoreCase))
                {
                    ReemplazarSiTieneTexto(obj, clave, MarcadorClaveTecnica);
                }
                else
                {
                    obj[clave] = null;
                }
            }
        }
    }

    private bool EsTerceroJuridico(JsonObject obj, string? id)
    {
        var apellido = LeerTexto(obj, "Apellido");
        if (!string.IsNullOrWhiteSpace(apellido)) return false;
        if (obj.TryGetPropertyValue("EsEstablecimiento", out var estNode) && estNode is JsonValue ev && ev.TryGetValue<int>(out var est) && est == -1)
        {
            return true;
        }
        var digitos = SoloDigitos(id ?? string.Empty);
        return digitos.Length == 9;
    }

    private void MapearCampoIdentificacion(JsonObject obj, string propiedad, string dominio, bool? esJuridica)
    {
        var valor = LeerTexto(obj, propiedad);
        if (string.IsNullOrWhiteSpace(valor)) return;
        obj[propiedad] = MapearIdentificacion(dominio, valor, esJuridica);
    }

    private string MapearIdentificacion(string dominio, string original, bool? esJuridica)
    {
        var limpio = original.Trim();
        if (limpio == NitConsumidorFinal) return NitConsumidorFinal;

        return ObtenerOMapear(dominio, limpio, clave =>
        {
            var digitos = SoloDigitos(clave);
            var pareceJuridica = esJuridica ?? (digitos.Length == 9);
            var h = HmacBytes(dominio, clave);
            var semilla = BitConverter.ToUInt32(h, 0);

            for (var intento = 0; intento < 10_000; intento++)
            {
                string candidato;
                if (digitos.Length == 0 || !digitos.Equals(clave, StringComparison.Ordinal))
                {
                    candidato = $"EXT-{HexCorto(dominio, $"{clave}:{intento}", 6)}";
                }
                else if (pareceJuridica)
                {
                    var numero = 900100000u + ((semilla + (uint)intento * 7919u) % 899999u);
                    candidato = numero.ToString();
                }
                else
                {
                    var numero = 1000100000u + ((semilla + (uint)intento * 7919u) % 99899999u);
                    candidato = numero.ToString();
                }

                if (!_originalesBloqueados.Contains(candidato) && !_asignados.Contains(candidato))
                {
                    return candidato;
                }
            }

            return $"ID-{SiguienteNumero("id_fallback"):D8}";
        });
    }

    private void AnonimizarNombrePersona(JsonObject obj, string propNombre, string propApellido, string semillaClave)
    {
        var nomOrig = LeerTexto(obj, propNombre);
        var apeOrig = LeerTexto(obj, propApellido);
        var h = HmacBytes("persona", $"{semillaClave}|{nomOrig}|{apeOrig}");

        if (!string.IsNullOrWhiteSpace(nomOrig))
        {
            obj[propNombre] = PrimerosNombres[h[0] % PrimerosNombres.Length];
        }
        if (!string.IsNullOrWhiteSpace(apeOrig))
        {
            obj[propApellido] = PrimerosApellidos[h[1] % PrimerosApellidos.Length];
        }
    }

    private void MapearTelefonoSiNoVacio(JsonObject obj, string propiedad)
    {
        var original = LeerTexto(obj, propiedad);
        if (string.IsNullOrWhiteSpace(original)) return;

        obj[propiedad] = ObtenerOMapear("telefono", original, clave =>
        {
            var digitos = SoloDigitos(clave);
            var esMovil = digitos.Length == 10 && digitos.StartsWith('3');
            for (var i = 0; i < 1000; i++)
            {
                var sufijo = HexNumerico("tel", $"{clave}:{i}", 4);
                var candidato = esMovil ? $"300000{sufijo}" : $"602000{sufijo}";
                if (!_originalesBloqueados.Contains(candidato) && !_asignados.Contains(candidato))
                {
                    return candidato;
                }
            }
            return $"300000{SiguienteNumero("tel"):D4}";
        });
    }

    private void MapearCorreoSiNoVacio(JsonObject obj, string propiedad, string rol)
    {
        var original = LeerTexto(obj, propiedad);
        if (string.IsNullOrWhiteSpace(original)) return;

        obj[propiedad] = ObtenerOMapear("correo", original.Trim().ToLowerInvariant(), clave =>
            $"{rol}-{HexCorto("correo", clave, 6)}@ejemplo.example");
    }

    private void MapearDireccionSiNoVacia(JsonObject obj, string propiedad)
    {
        var original = LeerTexto(obj, propiedad);
        if (string.IsNullOrWhiteSpace(original)) return;

        obj[propiedad] = ObtenerOMapear("direccion", original.Trim(), clave =>
        {
            var h = HmacBytes("dir", clave);
            var via = Vias[h[0] % Vias.Length];
            var n1 = (h[1] % 98) + 1;
            var n2 = (h[2] % 98) + 1;
            var n3 = (h[3] % 98) + 1;
            return $"{via} {n1} # {n2}-{n3}";
        });
    }

    private void MapearTextoSiNoVacio(JsonObject obj, string propiedad, string dominio, Func<string, string> generador)
    {
        var valor = LeerTexto(obj, propiedad);
        if (string.IsNullOrWhiteSpace(valor)) return;
        obj[propiedad] = ObtenerOMapear(dominio, valor, generador);
    }

    private string ObtenerOMapear(string dominio, string original, Func<string, string> generador)
    {
        var key = (dominio, original);
        if (_mapa.TryGetValue(key, out var existente))
        {
            return existente;
        }

        var nuevo = generador(original);
        _mapa[key] = nuevo;
        _asignados.Add(nuevo);
        return nuevo;
    }

    private string CalcularCufeSintetico(string original)
    {
        var bytes = SHA384.HashData(HmacBytes("cufe", original));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private byte[] HmacBytes(string dominio, string valor)
    {
        var payload = Encoding.UTF8.GetBytes($"{dominio}|{valor}");
        return HMACSHA256.HashData(_sal, payload);
    }

    private string HexCorto(string dominio, string valor, int longitud)
    {
        return Convert.ToHexString(HmacBytes(dominio, valor)).ToLowerInvariant()[..longitud];
    }

    private string HexNumerico(string dominio, string valor, int digitos)
    {
        var num = BitConverter.ToUInt32(HmacBytes(dominio, valor), 0);
        var modulo = (uint)Math.Pow(10, digitos);
        return (num % modulo).ToString($"D{digitos}");
    }

    private int SiguienteNumero(string serie)
    {
        _contadores.TryGetValue(serie, out var actual);
        actual++;
        _contadores[serie] = actual;
        return actual;
    }

    private string GenerarResolucionSintetica()
    {
        while (true)
        {
            var candidato = (18760001000L + SiguienteNumero("res")).ToString();
            if (!_originalesBloqueados.Contains(candidato) && !_asignados.Contains(candidato))
            {
                return candidato;
            }
        }
    }

    private static string? LeerTexto(JsonObject obj, string propiedad)
    {
        if (!obj.TryGetPropertyValue(propiedad, out var node) || node is not JsonValue val) return null;
        return val.TryGetValue<string>(out var texto) ? texto : null;
    }

    private static void ReemplazarSiPresente(JsonObject obj, string propiedad, string nuevoValor)
    {
        if (obj.ContainsKey(propiedad)) obj[propiedad] = nuevoValor;
    }

    private static void ReemplazarSiTieneTexto(JsonObject obj, string propiedad, string nuevoValor)
    {
        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, propiedad))) obj[propiedad] = nuevoValor;
    }

    private static void LimpiarTextoSiNoVacio(JsonObject obj, string propiedad)
    {
        if (!string.IsNullOrWhiteSpace(LeerTexto(obj, propiedad))) obj[propiedad] = "";
    }

    private static string SoloDigitos(string texto) => Regex.Replace(texto, "[^0-9]", "");
}
