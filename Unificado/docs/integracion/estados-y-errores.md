# Estados, ciclo de vida y errores

Última revisión: 2026-11-16

## Ciclo de vida de un documento (`api/documento`)

```
 agregar ──► (validación y traducción inmediatas) ──✗──► 400, no se guarda nada
              │ ok
              ▼
          Recibido  (XML generado y firmado, CUFE calculado)
              │  envío a la DIAN (segundo plano)
     ┌────────┼───────────────┐
     ▼        ▼               ▼
 Aprobado  Rechazado        Error
 (00)      (99)       (otro código, o falla de red / sin respuesta)
     │        │               │
     │        │               └── reintentos automáticos o POST reintentar ──► Aprobado / Rechazado / Error
     │        └── usted corrige y llama a agregar con el MISMO número ──► Recibido ...
     ▼
 PDF oficial + (correo) + aviso por webhook
```

| Estado | Significado | Qué hace usted |
|---|---|---|
| `Recibido` | El documento es válido, está firmado y guardado; aún no se envía a la DIAN o está en proceso. Es lo que devuelve `agregar` en producción. | Esperar (webhook o polling). Si pasan más de unos minutos, ver *Error*. |
| `Aprobado` | La DIAN respondió `00` (procesado correctamente). **Único estado con PDF oficial.** | Descargar PDF, XML y respuesta de la DIAN. Es definitivo: ese número no se vuelve a enviar. |
| `Rechazado` | La DIAN respondió `99` con errores de validación (`Errores`). | Leer `Errores`, corregir y volver a llamar a `agregar` con el **mismo número**. |
| `Error` | No hay decisión de la DIAN: falla de red, tiempo de espera, otro código de respuesta (por ejemplo, "en proceso") o excepción al enviar. `Mensaje` dice el motivo. | Normalmente nada: el servicio **reintenta solo**. Se puede forzar con `POST reintentar/{numero}`. |

### Reintentos automáticos

Un trabajo de fondo (cada 60 s por defecto) reenvía:

- Los `Recibido` sin enviar y sin cambios hace **más de 2 minutos** (por ejemplo, tras reiniciarse el servicio).
- Los `Error`, con espera creciente desde el último intento: 1, 2, 4, 8, 16 y luego 30 minutos, **hasta 10 intentos**. `Intentos` cuenta los envíos.

Pasados los 10 intentos el documento queda en `Error` y ya no se reenvía solo: use `POST /api/documento/reintentar/{numero}` o avise a SISTEMAS 360 si sospecha de la DIAN.

> Los reintentos son seguros: el servicio envía **el mismo XML firmado**, con el mismo CUFE. La DIAN no duplica un documento ya autorizado.

### Cómo se traduce la respuesta de la DIAN

| Código de la DIAN | Estado del documento |
|---|---|
| `00` (procesado correctamente) | `Aprobado` |
| `99` (validación con errores) | `Rechazado` (`Errores` con la lista) |
| Cualquier otro (por ejemplo `98`, en proceso) o una excepción | `Error` (reintentable) |

`Mensaje` trae el texto de la DIAN. `Errores` es una lista de textos de la DIAN; los de rechazo suelen tener la forma `Regla: XXXX, Rechazo: descripción`. El significado de cada regla está en el **anexo técnico de factura electrónica de la DIAN** (caja de herramientas vigente).

## Errores 400 al agregar

`POST /api/documento/agregar` responde 400 con un texto cuando el documento **no se puede traducir**; en ese caso **no se guarda nada** y `estado/{numero}` responde 404. Corrija y reenvíe.

| Mensaje (texto del cuerpo) | Causa | Cómo corregirlo |
|---|---|---|
| `El cuerpo no es un documento válido: ...` | El cuerpo no es JSON o no se puede leer como `DocumentoDian`. | Revise el JSON (comillas, comas, tipos: números sin comillas, fechas ISO 8601). |
| `El cliente no tiene configurado el software de facturación electrónica.` | Falta el software de FE (tipo 1) en su cliente. | Pida al operador que lo registre (Identificador y PIN del software en la DIAN). |
| `El documento no trae número.` | `Numero` vacío (o cuerpo `null`). | Envíe `Numero` con prefijo. |
| `El tipo de documento X no está soportado (solo 01, 91 y 92).` | `TipoDocDian` desconocido, `02` o `03`. | Use `01`, `91` o `92`. |
| `Se esperaba el tipo 91; llegó el tipo ...` / `Por ahora solo se traduce la factura de venta (01); ...` | Tipo inconsistente con el traductor. | Corrija `TipoDocDian`. |
| `El documento necesita emisor y receptor.` | Falta `Emisor` o `Receptor`. | Envíe ambos. |
| `El documento no trae la resolución de numeración.` | Sin `Resolucion` o sin `NumeroResolucion`. | Envíe la resolución completa. |
| `La resolución no trae la clave técnica.` | Factura (`01`) sin `Resolucion.ClaveTecnica`. | Envíe la clave técnica de la resolución. |
| `El documento no tiene líneas.` | `Items` vacío. | Envíe al menos una línea. |
| `El documento no trae la forma de pago.` | Sin `FormaPago`. | Envíe `FormaPago`. |
| `Faltan los datos del software DIAN del cliente.` | El software del cliente no tiene Identificador o PIN, o falta el NIT del emisor del cliente. | Avise al operador: configuración del cliente incompleta. |
| `Falta la locación (dirección y código de municipio) de un tercero.` | Un tercero sin `LocacionPrincipal.CodigoMunicipio`. | Envíe el código DANE de 5 dígitos. |
| `La nota debe referenciar el documento origen (número y CUFE).` | NC/ND sin `DocumentoRef.Numero` o `DocumentoRef.CUFE`. | Envíe ambos (CUFE de la factura aprobada). |
| `Concepto de nota crédito desconocido: x.` / `... de nota débito ...` | `DocumentoRef.Concepto` fuera de la tabla. | Use los códigos de [documentos-soportados.md](documentos-soportados.md#documentoref-notas-91-y-92). |
| `No se pudo procesar el documento: ...` | Error al construir el documento: por ejemplo `NumeroResolucion` no numérico, un código de impuesto inexistente, o un dato `null` donde se esperaba un valor. El texto posterior trae el detalle. | Corrija el dato que menciona el detalle. |

## Otros códigos HTTP de `api/documento`

| Código | Ruta | Significado |
|---|---|---|
| 202 | `agregar` | Recibido (nuevo o ya existente). No es aprobación. |
| 200 | `estado`, `cambios`, `reintentar`, `pdf`, `xml`, `respuesta-dian` | Correcto. |
| 401 | todas | Clave inválida. |
| 404 | `estado`, `reintentar`, `pdf`, `xml`, `respuesta-dian` | El número no existe para su cliente, o aún no hay ese archivo (`respuesta-dian` antes de que la DIAN responda). |
| 409 | `pdf` | El documento no está aprobado. |
| 503 | `pdf` | Aprobado pero sin PDF (sin plantilla para ese tipo, generador caído). Se anota en `PdfMensaje`; no cambia el estado de la DIAN. |

Las rutas de documento soporte, POS, nómina y eventos tienen su propio esquema de errores; ver [referencia-api.md](referencia-api.md).

## Estados del PDF y del correo

| `Pdf` | `PdfMensaje` | Qué pasa |
|---|---|---|
| `true` | vacío | PDF oficial disponible. |
| `false` | vacío | Documento no aprobado, o el PDF aún no se genera (por unos instantes tras aprobarse). |
| `false` | `no hay plantilla...` | Ese tipo de documento no tiene plantilla de PDF todavía (hoy, notas crédito y débito). |
| `false` | `No se pudo generar el PDF: ...` | Falla del generador; se reintenta cuando pida `pdf/{numero}`. |
| `false` | `El servicio no tiene generador de PDF configurado.` | La instalación no tiene el generador; avise al operador. |

`CorreoEnviado` hoy no cambia a `true`: el envío por correo **no está conectado** (ver [pdf-y-correo.md](pdf-y-correo.md)).

## Qué hacer cuando...

| Situación | Acción |
|---|---|
| `agregar` respondió 202 y después de varios minutos sigue `Recibido` | Pida `estado/{numero}`; si sigue igual, `POST reintentar/{numero}` y revise `Mensaje`. |
| Quedó `Error` con "No se pudo enviar a la DIAN: ..." | Es una falla de red o de la DIAN. Espere los reintentos automáticos o use `reintentar`. |
| `Rechazado` | Lea `Errores`, corrija datos y reenvíe con el mismo número. Si el rechazo es por numeración (fuera de rango, prefijo, resolución vencida), corrija la resolución y el número; un número rechazado puede reutilizarse mientras no haya sido aprobado. |
| `Aprobado` pero su ERP no lo sabe (se perdió el aviso) | `GET cambios?desde=<cursor>` y `estado/{numero}`. |
| Envió un número aprobado con datos distintos | Recibirá el registro del documento ya aprobado **sin cambios**. Para corregir un documento aprobado use una **nota crédito/débito**. |
| 401 de repente | Clave revocada o cliente desactivado: pida al operador. |
| 503 de la infraestructura | Exceso de solicitudes o servicio caído: reintente con espera creciente. |
