# Guía de integración: QuantyERP con la API de facturación electrónica

Última revisión: 2026-11-22
Destinatario: equipo de desarrollo de QuantyERP (DianWorker, módulos de facturación, POS, nómina y RADIAN).

Esta guía resume lo que QuantyERP necesita para enviar documentos a la API de SISTEMAS 360. El detalle campo por campo está en los demás archivos de esta carpeta; aquí se indica qué cambiar, en qué orden y qué decisiones siguen abiertas.

---

## 1. Qué cambia respecto al ERP actual

Hoy, `QuantyERP.DianWorker` habla con un servicio legado (`api/acceso/login`, usuario, clave, matrícula y token en `Authorization`). La API nueva **no usa login ni tokens**:

| Tema | Legado (DianWorker actual) | API nueva |
|---|---|---|
| Autenticación | `POST api/acceso/login` con usuario, clave y matrícula | Cabecera `x-api-key: <clave>` en cada petición |
| Transporte | `ApiScheme = "http"` por defecto | **Solo HTTPS**. Nunca enviar la clave por HTTP |
| Rutas de envío | `actualizardoc`, `consultarpdf` (rutas antiguas) | `POST /api/documento/agregar` (facturas, notas), `POST /api/DocumentoSoporte/EnviarDSNO`, `POST /api/DocumentoElectronico/EnviarDE`, `/api/v1/Nomina/*`, `/api/v1/Eventos/*` |
| Respuesta al enviar | Espera `IsValid`, `StatusCode`, `UUID`, `ZipKey` | Responde **202** con el registro (`Estado` = `Recibido`, `CUFE`). El resultado final llega después |
| Estado del documento | No consulta | `GET /api/documento/estado/{numero}` o `GET /api/documento/cambios?desde=N` |

**Acción:** reemplazar el login por la cabecera `x-api-key`, forzar HTTPS y cambiar la lectura de la respuesta de envío por el registro 202. Esto es bloqueante.

---

## 2. Autenticación y ambiente

- Una clave de API identifica **un cliente** (su NIT, su software DIAN, su certificado y su ambiente). Ver [autenticacion.md](autenticacion.md).
- `GET /api/Servicio/Quien` confirma con qué cliente quedó identificada la clave.
- Habilitación o producción **no se elige por URL ni por clave**: lo decide la configuración del cliente en la plataforma. Ver [ambientes.md](ambientes.md).
- Errores de autenticación: **401** con texto `Clave de API inválida.`

URL base hoy: `https://services.sistemas360.com:47125`. La confirma el operador.

---

## 3. Facturas, notas crédito y notas débito (`/api/documento/*`)

### 3.1 Flujo

1. `POST /api/documento/agregar` con el JSON `DocumentoDian`. Responde **202** con el registro (`Estado: "Recibido"`, `CUFE`).
2. El envío a la DIAN corre en segundo plano. Consultar con `GET /api/documento/cambios?desde=N` (preferido) o `GET /api/documento/estado/{numero}`.
3. Cuando el estado es `Aprobado` o `Rechazado`, descargar `pdf/{numero}`, `xml/{numero}` y `respuesta-dian/{numero}`.
4. Opcional: recibir un webhook firmado en lugar de consultar. Ver [webhook.md](webhook.md).

Reenviar el mismo `Numero` es **idempotente**: si ya está `Recibido` o `Aprobado`, devuelve el existente sin volver a enviar a la DIAN. Si está `Rechazado` o `Error`, se reprocesa con el contenido nuevo.

### 3.2 Reglas que hoy bloquean la emisión

| Regla | Qué hacer en QuantyERP | Estado |
|---|---|---|
| **Notas 91 y 92** deben traer la referencia al documento original con la clave `documento_ref` (`DocumentoRef`): `Numero` y `CUFE` del documento original | Enviar `documento_ref`. El nombre `DocumentoReferencia` no se acepta y responde 400 | **Bloqueante** |
| **Resolución** `Resolucion.NumeroResolucion` debe existir y ser **numérico** | Validar antes de enviar. Si falta o no es numérico, responde 400 | **Bloqueante** |
| **Impuesto INC** se identifica con el código **`04`** | El ERP manda `14` en algunas consultas. Mapear `14` → `04` o acordar con SISTEMAS 360 que la API lo acepte | **Decisión pendiente** |
| Tipos de documento admitidos en `/api/documento/*`: **01, 91, 92** | No enviar `02` (exportación) ni `03` (tarifa 0) por esta ruta | Hoy no aparecen en MARLIT |

Los campos completos de `DocumentoDian` están en [documentos-soportados.md](documentos-soportados.md) y los ejemplos en [ejemplos/](ejemplos/README.md).

### 3.3 Cómo leer el resultado

| `Estado` | Significado | Qué hace el ERP |
|---|---|---|
| `Recibido` | Aceptado por la API, pendiente de la DIAN | Esperar |
| `Aprobado` | La DIAN autorizó el documento | Guardar CUFE, descargar PDF y XML |
| `Rechazado` | La DIAN lo rechazó (código 99) | Leer `Errores`, corregir y reenviar el mismo número |
| `Error` | Falla de transporte o de la DIAN; se reintenta | Reintentar con `reintentar/{numero}` si hace falta |

---

## 4. Set de pruebas (habilitación)

Antes de pasar a producción, la DIAN tiene que aprobar el **set de pruebas** de cada bloque. Esto se hace con el encabezado `X-Envio-Set: true` sobre la misma ruta del bloque. Ver [set-de-pruebas-produccion.md](set-de-pruebas-produccion.md).

- El documento de prueba **lo genera el ERP** con datos reales de la habilitación: NIT y razón social del emisor, consecutivos del rango de habilitación que la DIAN asignó a ese bloque, y terceros reales de la empresa. **No se deben inventar cédulas ni NIT.**
- El documento **no se guarda** en el almacén operativo: no entra en reintentos ni en la lista de documentos.
- Respuesta: `estado` (`aceptado`, `rechazado`, `en_proceso`), `codigoDian` (statusCode de la DIAN), `uuid` (CUFE, CUDE, CUDS o CUNE según el bloque), `mensaje`, `zipKey` y `numero`.
- Si `estado` es `en_proceso`, la DIAN aún no decidió: el ERP puede enviar otro documento de prueba. Cada envío cuenta para el set.
- El estado queda en `set-{bloque}.json` de la carpeta del cliente. El panel de administración lo lee para autorizar producción. **El panel no envía nada a la DIAN.**

Bloques y su archivo de set:

| Bloque | Rutas con `X-Envio-Set` | Archivo de estado |
|---|---|---|
| Facturación (01, 91, 92) | `POST /api/documento/agregar` | `set-facturacion.json` |
| POS (20, 94) | `POST /api/DocumentoElectronico/EnviarDE` | `set-pos.json` |
| Documento soporte (05, 95) | `POST /api/DocumentoSoporte/EnviarDSNO` | `set-ds.json` |
| Nómina (102, 103) | `POST /api/v1/Nomina/EnviarNE` | `set-nomina.json` |
| RADIAN (030 a 033) | `POST /api/v1/Eventos/{accion}` | `set-radian.json` |

**Importante:** el envío al set usa el método `SendTestSetAsync` de la DIAN y devuelve un **ZipKey**; ese resultado tarda unos 20 segundos y se consulta después. El envío normal (producción y los envíos sueltos en habilitación) usa `SendBillSync` y responde al instante. No mezclar los dos identificadores.

---

## 5. Documento soporte y nota de ajuste (05 y 95)

- Ruta: `POST /api/DocumentoSoporte/EnviarDSNO`, cuerpo `MetaDatosDE`. Si `TipoDocumento` falta, se asume `05`.
- El **adquiriente** debe ser la propia empresa (mismo NIT y razón social).
- La nota de ajuste `95` usa la misma ruta con `TipoDocumento` = `95`. Debe referenciar un documento soporte `05` por su **CUDS**.
- Consultar con `POST /api/DocumentoSoporte/ConsultarDSNO?track_id={id}`. Se consulta por el CUDS del documento.

**Pendiente:** la resolución de documento soporte de la base de desarrollo (prefijo S3) venció el 13 de septiembre de 2026. Hay que renovarla antes de emitir documento soporte real.

---

## 6. POS (tiquete 20, nota de ajuste 94)

- Ruta: `POST /api/DocumentoElectronico/EnviarDE` con `MetaDatosDE` y `TipoDocumento` 20 o 94. Cualquier otro código responde 400.
- `EnviarDEJson` recibe el mismo contenido como texto JSON.
- Consultar con `POST /api/DocumentoElectronico/ConsultarDE?track_id={id}`.
- Tiquete y nota son idempotentes: reenviar uno ya autorizado devuelve el resultado sin duplicarlo.

**Decisión pendiente:** si la nota 95 de documento soporte también se expone en esta ruta. Hoy solo está en `EnviarDSNO`.

---

## 7. Nómina electrónica y RADIAN

- Nómina: `POST /api/v1/Nomina/EnviarNE` (genera, firma y envía) y `CrearXML` (solo genera). `Empleador.NIT` debe ser el NIT del cliente; si no, responde 400 con `statusCode` 108.
- Consulta: `GET /api/v1/Nomina/ConsultarNE/{TrackID}`. El `TrackID` de un envío síncrono es el CUNE.
- RADIAN: `POST /api/v1/Eventos/{accion}` con los códigos de evento. Los acuses de recibo aplican solo a facturas a crédito.

Detalle en [referencia-api.md](referencia-api.md).

---

## 8. Identificadores: qué es cada uno

- **UUID** (campo `UUID` en las respuestas y en la base): es el identificador único del documento. Según el tipo: **CUFE** (factura), **CUDE** (notas crédito y débito), **CUDS** (documento soporte y su nota), **CUNE** (nómina).
- **ZipKey**: solo el envío al set de pruebas. Se consulta con `GetStatusZip`.
- **TrackID**: identificador del envío que se usa para consultar. Para el set es el ZipKey; para el envío síncrono es el UUID.
- **CodigoDian**: el código de respuesta de la DIAN (`00` autorizado, `99` rechazado, `2` set aceptado).

---

## 9. Qué no debe hacer el ERP

- No enviar la clave de API por HTTP ni en la URL.
- No guardar la clave en el código: usar variables de entorno o un almacén seguro.
- No inventar NIT, cédulas ni consecutivos. Los datos de habilitación son reales.
- No subir datos de clientes reales a repositorios ni a canales compartidos.
- No reenviar el mismo número con contenido distinto sin antes ver por qué falló (`Errores`).

---

## 10. Lista de verificación para QuantyERP

- [ ] Reemplazar el login legado por la cabecera `x-api-key` y forzar HTTPS.
- [ ] Leer la respuesta 202 de `agregar` y consultar después con `cambios`.
- [ ] Enviar `documento_ref` (Numero y CUFE) en las notas 91 y 92.
- [ ] Validar que `NumeroResolucion` sea numérico antes de enviar.
- [ ] Resolver el código de INC (`04` frente a `14`).
- [ ] Probar `GET /salud` y `GET /api/Servicio/Quien` con la clave de habilitación.
- [ ] Enviar al set de pruebas de cada bloque con `X-Envio-Set` y guardar `codigoDian` y `uuid`.
- [ ] Verificar con el operador que el estado `aceptado` aparece en cada `set-{bloque}.json`.
- [ ] Solo después, pedir el paso a producción.

---

## 11. Decisiones abiertas (responsable: SISTEMAS 360 y QuantyERP)

1. ¿La API acepta `DocumentoReferencia` además de `documento_ref`, o el ERP cambia? (Recomendación: el ERP cambia.)
2. ¿INC se envía como `04` (recomendado) o la API acepta `14`?
3. ¿La nota 95 de documento soporte también se expone en `EnviarDE`?
4. ¿Cuándo se renueva la resolución de documento soporte?
5. ¿Cuándo se hace la primera prueba real del set con un cliente de habilitación?

Referencia completa de rutas: [referencia-api.md](referencia-api.md). Glosario y estados: [estados-y-errores.md](estados-y-errores.md).
