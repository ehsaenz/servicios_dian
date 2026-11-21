# Paso del ERP: validar el set de pruebas antes de producción

Antes de que el operador pueda pasar un cliente a producción, la DIAN tiene que confirmar que el set de pruebas de cada bloque está aceptado. Ese paso lo hace el ERP enviando un documento de prueba a la API con el encabezado de set.

## Rutas habilitadas por bloque

Todas aceptan el mismo cuerpo JSON habitual junto con el encabezado `X-Envio-Set: true`:

| Bloque | Ruta | Archivo generado en carpeta del cliente |
|---|---|---|
| Facturación electrónica (`01`, `91`, `92`) | `POST /api/documento/agregar` | `set-facturacion.json` |
| POS (`20` tiquete, `94` nota de ajuste) | `POST /api/DocumentoElectronico/EnviarDE` (o `EnviarDEJson`) | `set-pos.json` |
| Documento soporte (`05`, nota de ajuste `95`) | `POST /api/DocumentoSoporte/EnviarDSNO` (y `EnviarDE` para `05`) | `set-ds.json` |
| Nómina electrónica (`102`, `103`) | `POST /api/v1/Nomina/EnviarNE` | `set-nomina.json` |
| Eventos RADIAN (`030`..`033`) | `POST /api/v1/Eventos/{accion}` | `set-radian.json` |

El archivo queda en la carpeta de datos del cliente; el panel de administración lo lee en modo solo lectura y no envía nada a la DIAN.

## Comportamiento común y respuesta

- El documento **no** se guarda en el almacén de operaciones ni entra en reintentos o idempotencia local.
- La API valida los datos del bloque, firma con el certificado del cliente y lo envía con `EnviarSetPruebas` usando el `TestSetId` configurado para ese módulo, consultando el resultado del lote (`GetStatusZip`).
- Respuesta `200` con `{ Estado, Mensaje, ZipKey, Numero, FechaUtc, CodigoDian, Uuid }`:
  - `aceptado`: la DIAN respondió «se encuentra Aceptado» (código 2). Solo entonces el bloque cumple el requisito de set para pasar a producción.
  - `rechazado`: código 99 (o errores devueltos al recibir el lote sin `ZipKey`). Revisar `Mensaje` (incluye las reglas fallidas), corregir el documento en el ERP y reenviar otro consecutivo.
  - `en_proceso`: la DIAN aún no decide o autorizó el documento individual pero el set aún no completa sus requisitos. Reenviar el siguiente documento o probar más tarde.
- `400` si falta `TestSetId`, el bloque ya está en producción, el cuerpo es inválido o el identificador no corresponde.
- `502` si ocurrió un error de comunicación inesperado al contactar la DIAN.

Documento soporte: `POST /api/DocumentoSoporte/EnviarDSNO` con `TipoDocumento` 05 (documento soporte) o 95 (nota de ajuste del documento soporte, que referencia un 05 por su CUDS). Ambos escriben `set-ds.json`.

## Consecutivos

El ERP pone el consecutivo del documento de prueba dentro del rango de habilitación que la DIAN asignó a ese bloque. La API no inventa consecutivos.
