# Ejemplos

Última revisión: 2026-11-16

Todos usan **datos ficticios** y la clave como variable de entorno. **No se han ejecutado contra la DIAN**: son el contrato del servicio traducido a código. Pruébelos primero en **habilitación**.

Variables que usan todos los ejemplos de cliente:

| Variable | Valor |
|---|---|
| `DIAN_URL` | URL base, por ejemplo `https://services.sistemas360.com:47125` (sin `/` final) |
| `DIAN_API_KEY` | Su clave de API (nunca la escriba en el código) |

| Ejemplo | Contenido |
|---|---|
| [factura.json](factura.json) | Factura de venta (`01`) completa: 2 líneas, una con descuento, IVA 19 %. Total: base 208.000 + IVA 39.520 = **247.520** |
| [nota-credito.json](nota-credito.json) | Nota crédito (`91`), concepto 1 (devolución parcial), que referencia la factura |
| [nota-debito.json](nota-debito.json) | Nota débito (`92`), concepto 1 (intereses), que referencia la factura |
| [curl.md](curl.md) | Los mismos pasos con `curl` (bash) y con `curl.exe`/PowerShell |
| [csharp/ClienteDian.cs](csharp/ClienteDian.cs) | Cliente en C# (`HttpClient`): enviar, esperar y descargar el PDF |
| [node/cliente.mjs](node/cliente.mjs) | Lo mismo en Node 18+ (`fetch`) |
| [python/cliente.py](python/cliente.py) | Lo mismo en Python 3 (`requests`) |
| [webhook/receptor.mjs](webhook/receptor.mjs) | Receptor de webhook en Node (verifica la firma HMAC) |
| [webhook/receptor.py](webhook/receptor.py) | Receptor de webhook en Python (Flask) |
| [webhook/ReceptorWebhook.cs](webhook/ReceptorWebhook.cs) | Receptor de webhook en C# (ASP.NET Core, API mínima) |

## Antes de enviar los JSON de ejemplo

- Cambie `Numero` por un consecutivo **libre** de su resolución (habilitación: el del set de pruebas de la DIAN).
- Cambie `Resolucion` por la suya, incluida la `ClaveTecnica` (solo la factura la usa).
- Cambie `Emisor` por **su** empresa (el NIT del emisor debe ser el de su cliente) y `Receptor` por un adquiriente real o de pruebas.
- En la nota crédito y débito, ponga en `DocumentoRef` el `Numero` y el `CUFE` de una **factura ya aprobada**; el CUFE es el campo `CUFE` de la respuesta de `agregar` / `estado` de esa factura.
- Los NIT, direcciones, correos y la numeración de los ejemplos son inventados: la DIAN los rechazará tal cual.

## Compilar y ejecutar

Desde la raíz de `Unificado`, configure `DIAN_URL` y `DIAN_API_KEY` en el entorno mediante su gestor de secretos o una entrada privada. No pegue la clave en los comandos versionados. Primero compile; estos comandos no envían documentos:

```bash
dotnet build docs/integracion/ejemplos/csharp/ClienteDian.csproj
dotnet build docs/integracion/ejemplos/webhook/ReceptorWebhook.csproj
node --check docs/integracion/ejemplos/node/cliente.mjs
node --check docs/integracion/ejemplos/webhook/receptor.mjs
```

Los proyectos C# incluidos usan .NET 10, igual que la solución. Son ejemplos de transporte HTTP, sin referencia al modelo del servidor; para otro runtime debe adaptar y comprobar su proyecto consumidor.

Después de adaptar el JSON y confirmar habilitación con el operador, elija **uno** de estos clientes (cada ejecución envía el documento):

```bash
dotnet run --project docs/integracion/ejemplos/csharp/ClienteDian.csproj -- docs/integracion/ejemplos/factura.json
node docs/integracion/ejemplos/node/cliente.mjs docs/integracion/ejemplos/factura.json
python docs/integracion/ejemplos/python/cliente.py docs/integracion/ejemplos/factura.json
```

Python requiere `requests`; cree un entorno virtual e instálelo allí. Las descargas se guardan en el directorio de ejecución con el número del documento.

Para probar recepción webhook, configure `DIAN_WEBHOOK_SECRETO` y elija un receptor:

```bash
dotnet run --project docs/integracion/ejemplos/webhook/ReceptorWebhook.csproj
node docs/integracion/ejemplos/webhook/receptor.mjs
python docs/integracion/ejemplos/webhook/receptor.py
```

El receptor Python requiere Flask. Todos atienden `/dian/aviso` en el puerto 8080; publique mediante HTTPS antes de registrar la URL. No arranque los tres sobre el mismo puerto. Para uso real agregue persistencia, límites de petición y manejo de errores: los ejemplos usan memoria para deduplicar y no son un servicio listo para producción.

## Alcance de las comprobaciones

Compilar o comprobar sintaxis no valida conectividad, firma DIAN ni aceptación del documento. No se enviaron los ejemplos a la DIAN en esta revisión. Consulte [ERP y SDK](../erp-y-sdk.md) para recuperación, aislamiento entre clientes y requisitos de entrega.
