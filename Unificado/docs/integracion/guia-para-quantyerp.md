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

### 3.2 Campos y reglas de `DocumentoDian` frente a QuantyERP

Qué exige la API en `POST /api/documento/agregar` ([`TraductorFactura.cs`](../../src/Shared/Traductor/TraductorFactura.cs) y [`TraductorNota.cs`](../../src/Shared/Traductor/TraductorNota.cs)), qué envía u omite hoy `QuantyERP.DianWorker` (`DianRequestJsonBuilder.cs` y `DianDocumentDataService.cs`), y qué se observó en las muestras de desarrollo (`SETP-990000001`..`027`):

| Campo `DocumentoDian` | Lo que exige la API | Qué hace o omite QuantyERP hoy | Estado |
|---|---|---|---|
| `DocumentoRef` (`Numero`, `CUFE`, `Fecha`, `Concepto`) | Obligatorios `DocumentoRef.Numero` y `DocumentoRef.CUFE` en notas `91` y `92` (sin ellos responde **400**). Nota: la clave JSON exacta en `DocumentoDian` es **`DocumentoRef`**, no `documento_ref` ni `DocumentoReferencia`. | **No lo envía.** `DianRequestJsonBuilder` no llama `ConDocumentoReferencia` y `SQL_DOCUMENTO_SALIDA` no consulta `dian_envios.Cufe` de la factura origen (solo llena `OrdenRef` con `DocRefNumero`). | **Bloqueante** en `91`/`92` |
| `Resolucion.NumeroResolucion` (y `Prefijo`, rango, fechas) | Obligatorio y numérico (`long.Parse`) tanto en `01` como en notas `91` y `92`. En `01` exige además `Resolucion.ClaveTecnica`. | En **`91`/`92` no lo envía**: `AgregarResolucionAsync` hace `return` sin consultar resolución. En **`01`** consulta por `r.Activa`, `r.TipDoc` y `r.Sucursal`; en las 27 FV de muestra `Puesto = 0` deja `Sucursal` nulo y `resoluciones` sin `ClaveTecnica` en la exportación. | **Bloqueante** en `91`/`92` (y en `01` sin sucursal/`ClaveTecnica`) |
| `Emisor` y `Receptor` (`LocacionPrincipal.CodigoMunicipio`, `Direccion`) | Exige `CodigoMunicipio` DANE de 5 dígitos y `LocacionPrincipal` en ambos (sin él: **400**). `Emisor` se resuelve por `CodigoTienda` y usa `MatriculaMercantil` para el logo. | `SQL_DOCUMENTO_SALIDA` saca `CodigoTienda` vía `puestos -> sucursales`; cuando `Puesto = 0` (todas las 27 FV de muestra), `CodigoTienda` sale vacío (`""`). En `terceros_clientes` de muestra el cliente de `SETP-990000009` tiene `CodMunicipio` vacío (y el de `SETP-990000001` no tiene `Email`). | **Bloqueante** si falta `Puesto`/municipio DANE |
| `Receptor.TipoIdDian` | Tipo de documento DIAN (`31` NIT, `13` CC, `22` CE, `41` pasaporte, etc.; default `31` si falta). | `DianDocumentBuilder` fija `"13"` para toda persona natural y `"31"` para jurídica sin leer `contribuyentes.TipoDocu` (ni siquiera se selecciona en `SQL_RECEPTOR`). | **Brecha media** (CE/pasaportes salen como `"13"`) |
| `Emisor` / `Receptor` (`TipoPersona`, `IdTipoPersona`, `DV`, nombres) | `TipoPersona` en mayúsculas (`"JURIDICA"` fija `IdTipoPersona = "1"` con `RazonSocial`; `"NATURAL"` fija `"2"` con `PrimerNombre`/`PrimerApellido`). | Enviado mediante `UCASE(c.Persona)` y `DianDocumentBuilder`. Requiere que `contribuyentes` exista y tenga esos campos completos (la exportación de muestras solo incluye `terceros`). | **Compatible** (depende de datos en BD) |
| `Numero` y `FechaHoraDoc` | `Numero` no vacío con `Resolucion.Prefijo` (llave de idempotencia); `FechaHoraDoc` hora Colombia. | `NormalizeNumero` quita guiones (`SETP-990000001` → `SETP990000001`). En `SETP-990000001` de muestra el `Prefijo` de resolución coincide (`SETP`). | **Compatible** |
| `Items[]` (`Cant`, `ValorUnit`, `DtoDir`, `Detalle`, `Codigo`/`Referencia`, `Excluido`) | Mínimo 1 ítem. La API **no recalcula ni totaliza** por sí misma: usa `Cant`, `ValorUnit`, `DtoDir`, `BaseGral` e `impuestos[]`/`retenciones[]` del modelo. | `SQL_ITEMS_SALIDA` y `SQL_IMPUESTOS_SALIDA` filtran por `i.DocRelacionado = s.IdDocSal`. **Desfase detectado en muestra:** `SETP-990000001` tiene `0` líneas y `0` impuestos, y en `facturas_fv_lineas` las líneas existentes referencian `LPAD(s.ID, 10, '0')` (17 FV, solo 6 con impuestos materializados) en vez de `s.IdDocSal`. | **Bloqueante en pruebas** si `IdDocSal` ≠ `DocRelacionado` o faltan ítems/impuestos |
| `Items[].impuestos[].ID` | Códigos DIAN de `TipoImpuesto`: **`04`** para INC (`14` lanza excepción → **400**). | `SQL_IMPUESTOS_SALIDA` pasa `isi.ImpuestoId` directo y contempla `WHEN '14' THEN 'INC'`. La BD MARLIT usa `'04'`, no `'14'`. | **Decisión pendiente** (mapear `'14'` → `'04'` o aceptarlo en API) |
| `Items[].retenciones[]` y `ReadyDevTime` | Lee `Items[].retenciones[]` (`05`, `06`, `07`) directamente sin ejecutar `Totalizar()`. | `DianRequestJsonBuilder` no consulta `itemssalidas_retenciones`: depende de `DocumentoDian.Totalizar()` con `ModeloDian.ClaveDesarrollo` (`SecretosLocales`) para poblar `retenciones[]`. | **Riesgo** si el worker corre sin esa clave |
| `TipoDocDian` (`02` exportación, `03` contingencia) | `/api/documento/*` solo admite **`01`, `91` y `92`** (`02` y `03` responden **400**). | `MapTipoDocDian` mapea `"EX"` → `"02"` y `"TG"` → `"03"`, pero esos comprobantes no existen en MARLIT ni tienen `CREAR_ENVIO_DIAN`. | **No bloquea hoy** |

#### 3.2.1 Diagnóstico de las 27 facturas FV de muestra (sin datos personales)

Revisión de la exportación local de desarrollo (`facturas_fv.json`, `facturas_fv_lineas.json`, `facturas_fv_impuestos.json`, `terceros_clientes.json` y `resoluciones.json`):

- **Confirmación sobre `SETP-990000001`:** `ID = 11`, `IdDocSal = "0000000018"`, `DocumentoOrigen = "0000000017"`. Tiene **`0` líneas** bajo cualquiera de los tres identificadores (`IdDocSal`, `LPAD(ID, 10, '0') = "0000000011"` y `DocumentoOrigen`) y **`0` impuestos**; además `Puesto = 0`, `Resolucion = null` en cabecera y cliente sin `Email`. **No sirve como factura de prueba.**
- **Faltantes comunes sin inventar en origen (27 de 27 FV):** todas traen `Puesto = 0` (lo que impide resolver `CodigoTienda` y `Sucursal` en `SQL_DOCUMENTO_SALIDA`), `facturas_fv.Resolucion = null`, `TotalDoc = 0.0` y `TotalIVA = 0.0`. Aunque `resoluciones.json` sí tiene resolución `FV` activa y numérica (`Prefijo = "SETP"`, rango `990000000`–`995000000`), la exportación omite `ClaveTecnica`.
- **Desfase de enlace en líneas:** las 357 filas de `facturas_fv_lineas.json` referencian `DocRelacionado = LPAD(s.ID, 10, '0')` (no `s.IdDocSal`, con el que consulta `SQL_ITEMS_SALIDA`).
- **IVA incluido en `ValorUnit` (`IncluidoEnPrecio = 1`):** en las 6 FV con impuestos materializados, `itemssalidas.ValorUnit` contiene el precio **con IVA incluido** (`BaseGravada + ValorImpuesto == Cant * ValorUnit * (1 - DtoDir)`), mientras `SQL_ITEMS_SALIDA` selecciona `i.ValorUnit` directo sin desglosar IVA y `TraductorFactura` espera valor unitario **antes de IVA y de descuento**.

Estado por factura (`LineasIdDocSal` = cruce real de `DianDocumentDataService`; `LineasID` = cruce observado en `facturas_fv_lineas` por `LPAD(ID, 10, '0')`; `Resoluciones.FV` activa = `Sí` para todas por catálogo `resoluciones.json`, cabecera `facturas_fv.Resolucion` = `null` en todas):

| `NumeroDoc` | `ID` | `IdDocSal` | `Puesto` | `Res. cab.` | `LineasIdDocSal` | `LineasID` (`Val>0`) | `Imps` | `CodMunicipio` | Resultado en muestra |
|---|---|---|---|---|---:|---:|---:|---|---|
| `SETP-990000001` | 11 | `0000000018` | 0 | `null` | 0 | 0 (0) | 0 | `68001` | Descartada (`sin_lineas`, `sin_impuestos`) |
| `SETP-990000002` | 13 | `0000000020` | 0 | `null` | 0 | 1 (1) | 0 | `68001` | Descartada (`sin_impuestos`) |
| `SETP-990000003` | 50 | `0000000093` | 0 | `null` | 0 | 1 (0) | 0 | `76001` | Descartada (`valor_unitario_cero`, `sin_impuestos`) |
| `SETP-990000004` | 51 | `0000000094` | 0 | `null` | 0 | 0 (0) | 0 | `76001` | Descartada (`sin_lineas`) |
| `SETP-990000005` | 52 | `0000000095` | 0 | `null` | 0 | 1 (0) | 0 | `76001` | Descartada (`valor_unitario_cero`, `sin_impuestos`) |
| `SETP-990000006` | 53 | `0000000096` | 0 | `null` | 0 | 0 (0) | 0 | `76001` | Descartada (`sin_lineas`) |
| `SETP-990000007` | 54 | `0000000097` | 0 | `null` | 0 | 0 (0) | 0 | `76001` | Descartada (`sin_lineas`) |
| `SETP-990000008` | 55 | `0000000098` | 0 | `null` | 0 | 0 (0) | 0 | `76001` | Descartada (`sin_lineas`) |
| `SETP-990000009` | 56 | `0000000101` | 0 | `null` | 0 | 4 (0) | 0 | vacío | Descartada (`sin_cod_municipio`, `valor_unitario_cero`, `sin_impuestos`) |
| `SETP-990000010` | 57 | `0000000102` | 0 | `null` | 0 | 0 (0) | 0 | `05001` | Descartada (`sin_lineas`) |
| `SETP-990000011` | 58 | `0000000109` | 0 | `null` | 0 | 1 (0) | 0 | `05001` | Descartada (`valor_unitario_cero`, `sin_impuestos`) |
| `SETP-990000012` | 60 | `0000000111` | 0 | `null` | 0 | 0 (0) | 0 | `66001` | Descartada (`sin_lineas`) |
| `SETP-990000013` | 64 | `0000000115` | 0 | `null` | 0 | 0 (0) | 0 | `66001` | Descartada (`sin_lineas`) |
| `SETP-990000014` | 65 | `0000000116` | 0 | `null` | 1* | 0 (0) | 0 | `66001` | Descartada (*colisión de ID con `022`, `ID=65` tiene 0 líneas) |
| `SETP-990000015` | 67 | `0000000118` | 0 | `null` | 0 | 0 (0) | 0 | `66001` | Descartada (`sin_lineas`) |
| **`SETP-990000016`** | 71 | `0000000122` | 0 | `null` | 0 | **3 (3)** | **3** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |
| `SETP-990000017` | 72 | `0000000123` | 0 | `null` | 0 | 2 (2) | 0 | `66001` | Descartada (`sin_impuestos`) |
| **`SETP-990000018`** | 79 | `0000000132` | 0 | `null` | 0 | **1 (1)** | **1** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |
| `SETP-990000019` | 83 | `0000000136` | 0 | `null` | 0 | 1 (1) | 0 | `66001` | Descartada (`sin_impuestos`) |
| `SETP-990000020` | 86 | `0000000139` | 0 | `null` | 2* | 334 (334) | 0 | `66001` | Descartada (`sin_impuestos`; *colisión `0000000139` pertenece a `024`) |
| `SETP-990000021` | 91 | `0000000146` | 0 | `null` | 1* | 1 (1) | 0 | `66001` | Descartada (`sin_impuestos`; *colisión `0000000146` pertenece a `025`) |
| **`SETP-990000022`** | 116 | `0000000174` | 0 | `null` | 0 | **1 (1)** | **1** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |
| `SETP-990000023` | 138 | `0000000203` | 0 | `null` | 0 | 1 (1) | 0 | `66001` | Descartada (`sin_impuestos`) |
| **`SETP-990000024`** | 139 | `0000000204` | 0 | `null` | 0 | **2 (2)** | **2** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |
| **`SETP-990000025`** | 146 | `0000000214` | 0 | `null` | 0 | **1 (1)** | **1** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |
| `SETP-990000026` | 147 | `0000000215` | 0 | `null` | 0 | 1 (0) | 0 | `66001` | Descartada (`valor_unitario_cero`, `sin_impuestos`) |
| **`SETP-990000027`** | 151 | `0000000220` | 0 | `null` | 0 | **1 (1)** | **1** | `66001` | **Completa en líneas/impuestos/mun/catálogo res.** (sin `Puesto` ni `Res. cab.`) |

Solo **6 de 27** facturas (`016`, `018`, `022`, `024`, `025`, `027`: **9 líneas**, **9 impuestos**) tienen importes, impuestos y municipio completos en origen; aun así requieren en QuantyERP asignar un `Puesto` válido (> 0), unificar `itemssalidas.DocRelacionado = s.IdDocSal` (en lugar de `LPAD(ID, 10, '0')`) y ajustar el desglose de IVA incluido cuando `IncluidoEnPrecio = 1`.

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

## 8.1 Receptores persona natural (requisito nuevo)

Cuando el receptor es una **persona natural**, el ERP debe enviar el nombre **separado en cuatro campos**, además de la razón social:

```json
"Receptor": {
  "TipoPersona": "NATURAL",
  "TipoIdDian": "13",
  "PrimerNombre": "HAROLD",
  "SegundoNombre": "",
  "PrimerApellido": "ARENA",
  "SegundoApellido": ""
}
```

- `TipoIdDian` es el código del documento: `13` cédula de ciudadanía, `11` registro civil, `12` tarjeta de identidad, `22` cédula de extranjería, `41` pasaporte.
- Los cuatro campos del nombre pueden ir vacíos, pero **no deben faltar**: el XML los usa para `FirstName`, `MiddleName` y `FamilyName`.
- En la base de MatrixSQL/QuantyERP el nombre de un tercero natural suele venir en **un solo campo** (`terceros.Nombre`, con apellido y nombre juntos). El ERP tiene que partirlo antes de enviar; la API no adivina.
- Para persona natural, `DV` no aplica: envíe `"0"`.

Verificado el 2026-11-21: antes de esta fecha, cualquier receptor persona natural hacía fallar la API con `NullReferenceException`. Está corregido desde el commit `97affe8`.

## 8.2 Importes de las líneas

- `Items[].BaseGeneral` **la calcula el ERP**, no la API: `(Cantidad × PrecioUnitario) + cargos − descuentos`. Si llega en 0, el documento se firma y se transmite con importe 0 y la línea marcada como gratuita, **sin aviso**.
- Si su precio unitario incluye el IVA (en MatrixSQL, `itemssalidas_impuestos.IncluidoEnPrecio = 1`), **desglóselo antes de enviar**: la API espera el precio antes de impuestos.

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
- [ ] Enviar `DocumentoRef` (`Numero` y `CUFE`) en las notas 91 y 92 (además de `Resolucion.NumeroResolucion` y `Prefijo`).
- [ ] Validar que `NumeroResolucion` sea numérico antes de enviar.
- [ ] Resolver el código de INC (`04` frente a `14`).
- [ ] Probar `GET /salud` y `GET /api/Servicio/Quien` con la clave de habilitación.
- [ ] Enviar al set de pruebas de cada bloque con `X-Envio-Set` y guardar `codigoDian` y `uuid`.
- [ ] Verificar con el operador que el estado `aceptado` aparece en cada `set-{bloque}.json`.
- [ ] Solo después, pedir el paso a producción.

---

## 11. Decisiones abiertas (responsable: SISTEMAS 360 y QuantyERP)

1. ¿La API acepta `DocumentoReferencia` además de `DocumentoRef`, o el ERP cambia? (Recomendación: el ERP cambia.)
2. ¿INC se envía como `04` (recomendado) o la API acepta `14`?
3. ¿La nota 95 de documento soporte también se expone en `EnviarDE`?
4. ¿Cuándo se renueva la resolución de documento soporte?
5. ¿Cuándo se hace la primera prueba real del set con un cliente de habilitación?

Referencia completa de rutas: [referencia-api.md](referencia-api.md). Glosario y estados: [estados-y-errores.md](estados-y-errores.md).
