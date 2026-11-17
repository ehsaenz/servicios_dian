# Referencia de la API

Última revisión: 2026-11-16

Convenciones:

- URL base: `https://services.sistemas360.com:47125` (ver [README](README.md#url-base)). Cabecera `x-api-key` en todo, salvo `/salud` ([autenticacion.md](autenticacion.md)).
- Las respuestas JSON usan **los nombres de campo tal cual** (PascalCase en los registros de documentos; los de la DIAN y `ResponseDian` conservan su forma, p. ej. `statusCode`), **las enumeraciones como texto** (`"Aprobado"`) y las tildes sin escapar.
- Las fechas de respuesta (`FechaRecibido`, `FechaActualizado`, `WebhookEntregado`) están en **UTC**.
- Los errores de validación y de negocio de `api/documento` y de las rutas de POS/Documento soporte llegan como **texto plano** en el cuerpo (no JSON).
- `{numero}` es el número completo del documento, con prefijo (`SETP990000001`). Si lleva caracteres especiales, codifíquelo en la URL.
- Todo documento es **por cliente**: un número que no es suyo responde 404.

Contenido: [salud y diagnóstico](#salud-y-diagnóstico) · [api/documento](#apidocumento-factura-nota-crédito-y-nota-débito) · [Documento soporte](#documento-soporte) · [POS](#pos-tiquete-documento-soporte-y-nota-de-ajuste) · [Eventos RADIAN](#eventos-radian) · [Nómina](#nómina-electrónica) · [ResponseDian](#responsedian) · [Límites](#límites-de-infraestructura)

---

## Salud y diagnóstico

### `GET /salud`

Sin clave y sin datos. Para monitoreo.

| Código | Cuerpo |
|---|---|
| 200 | `{"estado":"ok"}` |

### `GET /api/Servicio/Quien`

Con qué cliente identificó su clave. 200 con `{"Id","Nit","RazonSocial"}`; 401 si la clave no sirve.

### `GET /api/Servicio/GetAmbientePruebas`

`true` si los envíos van a **habilitación** y `false` si van a producción. Se calcula con el software de **documento soporte** del cliente y, si no tiene, con el de **documento equivalente (POS)**; si no tiene ninguno responde `true`. **No** considera el software de facturación electrónica (para las facturas use el campo `Habilitacion` de cada registro; ver [ambientes.md](ambientes.md)).

| Código | Cuerpo |
|---|---|
| 200 | `true` o `false` |

---

## api/documento: factura, nota crédito y nota débito

Es la familia nueva y recomendada. Es **asíncrona e idempotente por número**. El detalle del cuerpo está en [documentos-soportados.md](documentos-soportados.md); el ciclo de vida, en [estados-y-errores.md](estados-y-errores.md).

Requisito del cliente: tener configurado el **software de facturación electrónica** (tipo 1) y su certificado. Si no, `agregar` responde 400 (ver más abajo).

### El registro del documento

Casi todas las rutas devuelven (o contienen) este objeto:

| Campo | Tipo | Significado |
|---|---|---|
| `Secuencia` | entero | Consecutivo interno del documento en su cliente (no cambia). |
| `Numero` | texto | Número enviado (con prefijo). |
| `TipoDocDian` | texto | `01`, `91` o `92`. |
| `TipoDocErp` | texto | Lo que usted mandó en `TipoDocErp` (`FV`, `NC`, ...). Vacío si no lo mandó. |
| `Matricula` | texto | Matrícula mercantil del emisor (`Emisor.MatriculaMercantil`); decide el logo del PDF. |
| `Estado` | texto | `Recibido`, `Aprobado`, `Rechazado` o `Error`. Ver [estados-y-errores.md](estados-y-errores.md). |
| `CUFE` | texto | CUFE (facturas) o CUDE (notas), de 96 caracteres hexadecimales. Lo calcula el servicio. |
| `Mensaje` | texto | Mensaje de la DIAN o del servicio. |
| `Errores` | lista de texto | Mensajes de rechazo de la DIAN (vacía si no hay). |
| `Habilitacion` | booleano | `true` si el documento se envió a habilitación; `false` si a producción. |
| `Pdf` | booleano | Existe el PDF oficial. |
| `PdfMensaje` | texto | Por qué no hay PDF (sin plantilla, falla del generador...). Vacío si hay o aún no se intenta. |
| `CorreoEnviado` | booleano | Se envió el correo al adquiriente. **Hoy siempre `false`** (ver [pdf-y-correo.md](pdf-y-correo.md)). |
| `CorreoDestino` | texto | Correo del adquiriente tomado del XML aprobado. |
| `WebhookPendiente` | booleano | Hay un aviso de webhook sin entregar. |
| `WebhookIntentos` | entero | Intentos de entrega del aviso. |
| `WebhookError` | texto | Último error de entrega. |
| `WebhookEntregado` | fecha UTC o `null` | Cuándo se entregó el aviso. |
| `Intentos` | entero | Veces que se intentó enviar a la DIAN. |
| `FechaRecibido` / `FechaActualizado` | fecha UTC | Cuándo llegó y cuándo cambió por última vez. |
| `SecuenciaCambio` | entero | Cursor de cambios (ver `cambios`). Sube cada vez que el documento cambia para usted. |

Ejemplo:

```json
{
  "Secuencia": 1,
  "Numero": "SETP990001001",
  "TipoDocDian": "01",
  "TipoDocErp": "FV",
  "Matricula": "00001",
  "Estado": "Aprobado",
  "CUFE": "3f0c...(96 caracteres hexadecimales)...a91b",
  "Mensaje": "La Factura electrónica SETP990001001, ha sido autorizada.",
  "Errores": [],
  "Habilitacion": true,
  "Pdf": true,
  "PdfMensaje": "",
  "CorreoEnviado": false,
  "CorreoDestino": "compras@cliente.example",
  "WebhookPendiente": false,
  "WebhookIntentos": 1,
  "WebhookError": "",
  "WebhookEntregado": "2026-11-16T15:04:12.51Z",
  "Intentos": 1,
  "FechaRecibido": "2026-11-16T15:04:09.12Z",
  "FechaActualizado": "2026-11-16T15:04:12.51Z",
  "SecuenciaCambio": 3
}
```

### `POST /api/documento/agregar`

Recibe un documento. Lo valida, traduce y firma antes de responder; un fallo de traducción responde 400 antes de guardar el documento. El controlador también convierte otros fallos de recepción en 400: ante un fallo de escritura no asuma ausencia de efectos parciales; consulte el número. Si es válido, lo firma, lo guarda y responde 202; el envío a la DIAN corre en segundo plano (a menos que el operador haya apagado `Documentos:EnvioEnSegundoPlano`, que es un ajuste de pruebas).

- **Cuerpo:** el JSON de `DocumentoDian` (ver [documentos-soportados.md](documentos-soportados.md)), `Content-Type: application/json`.
- **Idempotente por `Numero`:** si el número ya existe y su estado es `Recibido` o `Aprobado`, devuelve el registro existente **sin volver a tocar la DIAN y sin comparar el contenido**. Si el estado es `Rechazado` o `Error`, el documento se **reprocesa** con el contenido nuevo (así se corrige y reenvía el mismo número).

| Código | Cuándo | Cuerpo |
|---|---|---|
| **202** | Aceptado (nuevo o ya existente) | El [registro](#el-registro-del-documento). En producción lo normal es `Estado: "Recibido"` con `CUFE`; el resultado llega después. |
| **400** | El cuerpo no es un documento válido, falta un dato obligatorio, el tipo no está soportado o el cliente no tiene software de FE | Texto plano con el motivo. Mensajes del servicio: ver [estados-y-errores.md](estados-y-errores.md#errores-400-al-agregar). |
| 401 | Clave inválida | `Clave de API inválida.` |

Importante: **202 no significa aprobado.** Significa "recibido, firmado y en cola hacia la DIAN".

### `GET /api/documento/estado/{numero}`

El registro actual del documento.

| Código | Cuerpo |
|---|---|
| 200 | El [registro](#el-registro-del-documento) |
| 404 | El documento no existe (para su cliente), o fue rechazado al instante por `agregar` (esos no se guardan) |

### `GET /api/documento/cambios?desde={n}&limite={m}`

Polling por cursor: los documentos que **cambiaron** después de `desde`, en orden de cambio.

| Parámetro | Tipo | Por defecto | Notas |
|---|---|---|---|
| `desde` | entero | `0` | Última `SecuenciaCambio` (o `Cursor`) que usted ya procesó. |
| `limite` | entero | `100` | Máximo `500`; un valor menor que 1 se toma como 1. |

Respuesta **200**:

```json
{
  "Cursor": 7,
  "Documentos": [ { "...": "registros completos, ver arriba" } ]
}
```

- `Cursor` es la mayor `SecuenciaCambio` devuelta (o `desde` si no hay cambios). **Guárdelo y úselo como `desde` en la siguiente llamada.**
- Un documento aparece cada vez que **cambia** (recibido, aprobado, rechazado, PDF generado...). Procese por `Numero` y estado, no cuente apariciones.
- El **intento de entrega del webhook no mueve el cursor**: no verá cambios solo porque se reintentó un aviso.

### `POST /api/documento/reintentar/{numero}`

Pide **ya mismo** el reenvío a la DIAN de un documento `Recibido`, `Rechazado` o `Error`. Es **síncrono**: la llamada espera la respuesta de la DIAN (puede tardar; use un tiempo de espera de al menos 180 s). Un documento `Aprobado` devuelve el registro sin reenviar. No cambia el contenido: para corregir un rechazo, vuelva a llamar a `agregar` con el documento corregido.

| Código | Cuerpo |
|---|---|
| 200 | El registro actualizado |
| 404 | No existe |

### `GET /api/documento/pdf/{numero}`

PDF oficial. **Solo existe para documentos aprobados por la DIAN.**

| Código | Cuándo | Cuerpo |
|---|---|---|
| 200 | Aprobado y PDF disponible | `application/pdf`, archivo `{numero}.pdf` |
| 404 | El documento no existe | vacío |
| **409** | El documento **no está aprobado** (`Recibido`, `Rechazado`, `Error`) | Texto: `El documento no está aprobado por la DIAN: no hay PDF oficial.` |
| **503** | Aprobado, pero no se pudo generar el PDF (sin plantilla para ese tipo, generador caído o sin configurar) | Texto con el motivo (`PdfMensaje`). El documento **sigue aprobado**; reintente más tarde. |

Si el PDF aún no existe pero el documento está aprobado, esta llamada lo genera. Ver [pdf-y-correo.md](pdf-y-correo.md).

### `GET /api/documento/xml/{numero}`

El XML UBL **firmado por usted** que se envió a la DIAN. Sirve aunque el documento no esté aprobado (para auditar).

| Código | Cuerpo |
|---|---|
| 200 | `application/xml`, archivo `{numero}.xml` |
| 404 | No existe |

### `GET /api/documento/respuesta-dian/{numero}`

El `ApplicationResponse` (XML) que devolvió la DIAN. Es el comprobante de la aprobación o del rechazo. Solo existe después de que la DIAN respondió.

| Código | Cuerpo |
|---|---|
| 200 | `application/xml`, archivo `{numero}-respuesta.xml` |
| 404 | No existe (aún no hay respuesta de la DIAN) |

---

## Documento soporte

Rutas heredadas de la API anterior (mismas rutas y cuerpos). Son **síncronas**: responden cuando la DIAN respondió.

### `POST /api/DocumentoSoporte/EnviarDSNO`

- **Cuerpo:** `MetaDatosDE` (ver [documentos-soportados.md](documentos-soportados.md#documento-soporte-05)). Si `TipoDocumento` falta, se asume documento soporte (`05`).
- **Reglas:** el **adquiriente** debe ser **su propia empresa** (mismo NIT y misma razón social, sin importar mayúsculas); el servicio rellena los datos del software DIAN y firma con su certificado.

| Código | Cuerpo |
|---|---|
| 200 | [`ResponseDian`](#responsedian) (también cuando la DIAN rechaza; mire `statusCode` y `Errors`) |
| 400 | Texto: `Ocurrió un error inesperado el documento no se envió: ...` (incluye "Este servicio no puede enviar Documentos Soporte para el adquiriente ...") |

### `POST /api/DocumentoSoporte/ConsultarDSNO?track_id={id}`

Consulta el resultado. `track_id` va en la **query string**. En habilitación es el `ZipKey` del envío; en producción, el UUID (CUDS) del documento.

| Código | Cuerpo |
|---|---|
| 200 | `ResponseDian` |
| 400 | Texto: `Ocurrió un error inesperado en la consulta: ...` |

---

## POS: tiquete, documento soporte y nota de ajuste

Rutas `api/DocumentoElectronico/*`, también heredadas. Un solo punto de entrada que decide por `TipoDocumento.Codigo`.

### `POST /api/DocumentoElectronico/EnviarDE`

- **Cuerpo:** `MetaDatosDE` con `TipoDocumento`: `05` documento soporte, `20` tiquete POS electrónico, `94` nota de ajuste crédito del POS. Cualquier otro código → 400 `Este servicio no está configurado para enviar documentos electrónicos de tipo: [xx]`.

| Código | Cuándo | Cuerpo |
|---|---|---|
| 200 | La DIAN respondió (aprobado o rechazado; revise `statusCode`) | `ResponseDian` |
| 400 | Sin tipo de documento, datos inválidos o error al enviar (`05` y `20`: `Ocurrió un error inesperado el documento no se envió: ...`; `94`: el motivo de la validación) | Texto |
| 409 | Solo `94`: ya existe una nota con ese número y otros datos, sin resolver | Texto |
| 502 | Solo `94`: no se pudo confirmar el envío; **consulte el estado antes de reintentar** | Texto |

- **Tiquete POS (`20`) y nota de ajuste (`94`)** son idempotentes: reenviar uno ya autorizado devuelve el resultado en vez de duplicarlo; solo un rechazo explícito permite volver a transmitirlo.

### `POST /api/DocumentoElectronico/EnviarDEJson`

Igual que `EnviarDE`, pero el `MetaDatosDE` viaja en un parámetro `JsonDE` codificado en **Base64 de UTF-8**. Respuestas: las de `EnviarDE`; 400 `No se ha recibido un objeto válidi` si falta y `Ocurrió un error desserializando el ojeto` si no se puede leer (los textos conservan las erratas de la API anterior).

**No verificado:** si `JsonDE` se espera en la query string o en el cuerpo. Por las reglas de enlace de ASP.NET para un `string` simple en un controlador `[ApiController]`, sería **query string** (con el límite de longitud de URL que eso implica). Para documentos grandes use `EnviarDE`.

### `POST /api/DocumentoElectronico/ConsultarDE?track_id={id}`

Consulta el resultado de un tiquete POS (`20`) o de una nota (`94`). `track_id` en la query string. 200 con `ResponseDian`; 400 con `Ocurrió un error inesperado en la consulta: ...`.

---

## Eventos RADIAN

Eventos sobre **facturas recibidas** (usted como adquiriente). El cliente debe tener configurado el software de **eventos RADIAN**.

Rutas (todas `POST`, `/api/v1/Eventos/...`) y el `Codigo` que cada una exige en el cuerpo:

| Ruta | `Codigo` |
|---|---|
| `AcuseFactura` | `030` |
| `ReclamoFactura` | `031` |
| `ReciboBienesServicios` | `032` |
| `AceptacionExpresa` | `033` |
| `AceptacionTacita` | `034` |

Cuerpo (`EventoRadian`):

| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `Codigo` | texto | Sí | Debe coincidir con la ruta. |
| `UUID` | texto | Sí | CUFE de la factura recibida. |
| `Causa` | objeto `{Codigo, Significado}` | Solo en `031` | Ambos textos son obligatorios en el reclamo. |
| `Numero` | texto | No | Número del evento (p. ej. `EVT15`). Si falta, el servicio usa el consecutivo del cliente. |

| Código | Cuándo | Cuerpo |
|---|---|---|
| 200 | `statusCode` entre `00` y `99` (incluye rechazos de la DIAN con su detalle) | `ResponseDian` |
| 400 | Cualquier otro: `101` (código de evento que no corresponde a la ruta o falta la causa), `108` (el cliente no tiene el software de eventos configurado u otro error de negocio), `99` (error inesperado) | `ResponseDian` con `statusCode` y `statusMessage` |

---

## Nómina electrónica

Rutas `api/v1/Nomina/*`. El cliente debe tener configurado el software de **nómina electrónica**.

| Ruta | Método | Para qué |
|---|---|---|
| `EnviarNE` | POST | Genera, firma y envía la nómina individual a la DIAN. |
| `CrearXML` | POST | Solo genera y firma el XML, sin enviarlo (código `100`). |
| `ConsultarNE/{TractID}` | GET | Consulta el resultado por el `TrackID`. |

- **Cuerpo de `EnviarNE` y `CrearXML`:** `MetaDatosNEI` (modelo extenso; ver `src/Nomina/MetaDatosNE/MetaDatosNEI.cs` y [fase3-nomina-radian.md](../../../docs/fase3-nomina-radian.md)). `Empleador.NIT` **debe ser el NIT de su cliente**; si no, 400 con `statusCode` `108`: *Este servicio no está registrado al NIT del emisor de éste documento*.
- **Códigos HTTP:** 200 si `statusCode` está entre `00` y `99`, entre `SP00` y `SP99` (envío al set de pruebas) o es `100` (solo `CrearXML`); **400** para los demás (`108`, errores). En `ConsultarNE`: 404 si no se encuentra el documento (error `DB_NOT_FOUND`).
- Respuesta: [`ResponseDian`](#responsedian).

---

## ResponseDian

Respuesta común de las rutas de documento soporte, POS, eventos y nómina.

| Campo | Tipo | Significado |
|---|---|---|
| `statusCode` | texto | `00` procesado correctamente (autorizado); `99` validación con errores (rechazo); `SPxx` envío al set de pruebas; `101`, `108`, `199`...: errores del servicio. |
| `statusMessage` | texto | Mensaje legible. |
| `TrackID` | texto | En habilitación, identificador del envío (ZipKey); en producción, el UUID. Úselo para consultar. |
| `UUID` | texto | CUFE, CUDE, CUDS o CUNE del documento. |
| `StrQR` | texto | Texto para el código QR. |
| `FechaValidacion` | fecha | Cuándo validó la DIAN. |
| `Errors` | lista | Rechazos de la DIAN. Cada uno: `Source`, `Codigo` (la regla, p. ej. `FAJ44`), `Descripcion`. |
| `Warnings` | lista | Notificaciones de la DIAN (no impiden la autorización). Misma forma. |

Los mensajes de la DIAN con formato `Regla: XXX, Rechazo: ...` se clasifican en `Errors`; los de notificación, en `Warnings`. Algo que no tenga ese formato se pone en `Warnings` con código `0001`.

---

## Límites de infraestructura

| Límite | Valor | Qué ve usted |
|---|---|---|
| Tamaño máximo del cuerpo (entrada pública) | 5 MB | 404 o 413 de la infraestructura (IIS/nginx), con una página que no es de la API |
| Solicitudes por dirección IP | ~20 por segundo, ráfaga de 40 | 503 |
| Tiempo máximo de espera de una respuesta en el proxy | 180 s | 504 o corte de la conexión |
| Tiempo de espera de la DIAN | depende de la DIAN | `Estado: Error` en `api/documento`; reintente |

Use en su cliente HTTP un tiempo de espera **de al menos 180 s** para las rutas síncronas (documento soporte, POS, nómina, eventos y `reintentar`). Para `agregar`, `estado`, `cambios` bastan 30 s.

## No verificado en esta referencia

- El esquema campo por campo de `MetaDatosDE` (POS, DS) y `MetaDatosNEI` (nómina) no se reproduce aquí: es extenso y se mantiene en el código y en `docs/fase2-diseno-api-pos-dsno.md`.
- El texto exacto de algunos errores de los modelos de la DIAN (`Source` en `Errors`).
- La forma exacta (query o cuerpo) en que `EnviarDEJson`, `ConsultarDE` y `ConsultarDSNO` reciben sus parámetros de texto; se deduce de las reglas de ASP.NET (query string).
- Los códigos HTTP que devuelve la infraestructura (IIS/nginx) ante cuerpos grandes o exceso de solicitudes dependen de su configuración.
