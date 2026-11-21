# Anonimizador de muestras JSON

Herramienta de consola (.NET 10, sin conexión a base de datos) que toma los archivos JSON locales de `%LOCALAPPDATA%\Dian\muestras-dev` y genera una copia anonimizada en `tools/SimuladorErp/muestras-anonimas/` aplicando las reglas de [ejemplos-anonimizados.md](../../docs/integracion/ejemplos-anonimizados.md).

## Uso

Desde `Unificado/tools/Anonimizador`:

```bash
dotnet run
```

O especificando rutas explícitas:

```bash
dotnet run -- --origen C:\ruta\muestras-dev --destino C:\ruta\muestras-anonimas
```

## Garantías

- Usa HMAC-SHA256 con una sal aleatoria efímera generada en memoria en cada ejecución (nunca se escribe al disco).
- Filtra las muestras sin inventar importes ni rellenar faltantes: solo conserva facturas FV con resolución numérica en `resoluciones.json`, cliente con `CodMunicipio` de 5 dígitos, al menos una línea con `Cant > 0` y `ValorUnit > 0`, e impuestos completos en `facturas_fv_impuestos.json` (las facturas incompletas como `SETP-990000001` y terceros sin municipio se excluyen).
- Conserva el NIT `222222222222` (consumidor final, `DV = "7"`, `CONSUMIDOR FINAL`).
- Preserva la integridad referencial entre `facturas_fv.json`, `terceros_clientes.json`, `facturas_fv_lineas.json`, `productos_usados.json` y `sucursales.json`, así como importes, impuestos, cantidades y códigos DANE (`CodMunicipio`).
- Remueve o reemplaza `ClaveTecnica`, logos base64 (`Logo`, `LogoFact`), credenciales, correos, teléfonos, direcciones, notas libres y fechas de nacimiento.
