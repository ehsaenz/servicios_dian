# Primer envío al set de pruebas (MARLIT, habilitación)

Fecha: 2026-11-22. Cliente: MARLIT GROUP S.A.S., NIT 901033307. Ambiente: habilitación (set de pruebas), no producción.

## Resultado

| Factura | Receptor | Total | Estado | CodigoDian | UUID (CUFE) | ZipKey |
|---|---|---|---|---|---|---|
| SETP990000028 | Persona natural (campos de nombre separados) | 48.000,00 | aceptado | 2 | `ba19068762239e5344a56df9bdd0063040dc146972d5b9033fe20a1b2bf57c46d69edbdaa82e67d7854291cdf52cf339` | `6804adb5-1953-41d8-b9a7-9f4a50f6c198` |
| SETP990000029 | Persona jurídica | 48.000,00 | aceptado | 2 | `6351fc2342fd74c006908b88a657c009f05969c0c4ed668558b0fa7d254c56bac578084297ce75b8a30e3d46b7d2fb65` | `7d83989b-5762-42d0-94fc-f38218c0c64e` |

## Cómo se interpreta

- `CodigoDian` `2` = set aceptado, según la sección 8 de [guia-para-quantyerp.md](guia-para-quantyerp.md).
- El mensaje "Set de prueba con identificador ..." repite el mismo valor en las dos respuestas. Es el `TestSetId` del software de MARLIT, fijo por cliente, no un identificador por documento. Cada factura tiene su propio CUFE y ZipKey.
- `GET /api/documento/estado/{id}` respondió 404 para ambos documentos, por número y por UUID. Lo más probable es que el envío al set no se guarde en la tabla de documentos, pero no está verificado. Por ahora la confirmación depende de la respuesta del envío.

## Cómo se envió

Herramienta `Unificado/tools/SimuladorErp`, con `DIAN_API_URL` en `https://` y la clave de API en `DIAN_API_KEY`. La clave no se registra en el repositorio ni en la documentación.

Comando de envío (ilustrativo, sin clave):

```
dotnet run --project tools/SimuladorErp -- enviar --tipo factura --archivo <ruta-al-json> --set --confirmar
```

## Pendiente

- Consultar el estado de ambos documentos en la DIAN por un canal que no sea la API de envío, si la DIAN lo permite.
- Factura 029 y 028 ya consumieron los consecutivos SETP990000028 y SETP990000029 del set. No reintentar sin revisar primero.
