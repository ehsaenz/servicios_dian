# Paso del ERP: validar el set de pruebas antes de producción

Antes de que el operador pueda pasar un cliente a producción, la DIAN tiene que confirmar que el set de pruebas de cada bloque está aceptado. Ese paso lo hace el ERP enviando un documento de prueba a la API con el encabezado de set.

## Facturación electrónica (ya implementado)

`POST /api/documento/agregar` con el mismo cuerpo de siempre y el encabezado:

```
X-Envio-Set: true
```

- El documento **no** se guarda en el almacén: no entra en reintentos ni en la lista de documentos.
- La API lo firma y lo envía al set de pruebas del software (`TestSetId`) con `EnviarSetPruebas`, y consulta el resultado.
- Respuesta `200` con `{ estado, mensaje, zipKey, numero, fechaUtc }`:
  - `aceptado`: la DIAN respondió «se encuentra Aceptado» (código 2).
  - `rechazado`: código 99. Revisar `mensaje` y corregir el documento antes de reenviar.
  - `en_proceso`: la DIAN aún no decide. Reenviar otro documento más tarde.
- `400` si el software no tiene `TestSetId`, si el documento no tiene número o si la API está en producción.

El estado del set queda en `set-facturacion.json` de la carpeta del cliente. El panel de administración lo lee; no envía nada a la DIAN.

## Pendiente

Todavía no tienen el encabezado de set: POS (`/api/DocumentoElectronico/EnviarDE`), documento soporte (`/api/DocumentoSoporte/EnviarDSNO`), nómina (`/api/v1/Nomina/EnviarNE`) y eventos RADIAN (`/api/v1/Eventos/{accion}`). Se añadirán siguiendo el mismo contrato.

## Consecutivos

El ERP pone el consecutivo del documento de prueba dentro del rango de habilitación que la DIAN asignó a ese bloque. La API no inventa consecutivos.
