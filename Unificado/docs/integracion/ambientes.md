# Ambientes: habilitación y producción

Última revisión: 2026-11-16

## Cómo funciona para usted

**Hay una sola URL y una sola clave de API.** El ambiente no se elige en la petición: lo decide la **configuración de su cliente** en la plataforma. Cada documento sale a:

- **Habilitación** (el ambiente de pruebas de la DIAN) mientras su cliente no tenga producción autorizada, **o**
- **Producción** solo cuando se cumplen las **dos** condiciones: el software de DIAN del cliente está marcado como de producción **y** el operador de SISTEMAS 360 autorizó explícitamente la producción para su cliente.

Es decir, por defecto **todo va a habilitación**, aunque el software diga producción, hasta que alguien autorice el paso. Nadie pasa a producción por accidente.

> **Claves por ambiente:** el servicio **no** tiene claves distintas para habilitación y producción: es la misma clave de su cliente. Si quiere aislar por completo las pruebas de su ERP (otro NIT de pruebas, otra clave), pida al operador un **cliente de pruebas separado**. Esto es posible por la forma de administrar clientes, pero no está documentado como práctica estándar: consúltelo.

## Cómo saber en qué ambiente está

| Para | Cómo |
|---|---|
| Facturas, notas crédito y débito (`api/documento`) | Cada registro trae `Habilitacion` (`true` = habilitación, `false` = producción). Mírelo en la respuesta de `agregar` o `estado`. |
| Documento soporte y POS | `GET /api/Servicio/GetAmbientePruebas` (`true` = habilitación). Usa el software de documento soporte, o el de POS si no hay. |

**Los documentos enviados a habilitación no tienen validez tributaria.** No los entregue a su cliente final como factura válida, ni los contabilice como definitivos. Si su ERP está en pruebas, conviene que no envíe correos reales a adquirientes reales: use correos de prueba en `Receptor.EmailContacto`.

## Qué cambia entre habilitación y producción

| Aspecto | Habilitación | Producción |
|---|---|---|
| Validez | Sin validez tributaria | Documento electrónico válido |
| URL y clave de API | Las mismas | Las mismas |
| Resolución de numeración | La de pruebas de la DIAN (prefijo **SETP** para facturación, y la clave técnica de pruebas) | **Su resolución real** (su prefijo, su rango y **su clave técnica de producción**) |
| Software de la DIAN | Identificador y PIN de habilitación | Identificador y PIN de **producción** (los registra el operador) |
| Datos del adquiriente (tiquete POS) | Se completan solos `CodigoCliente` y `NombreComercial` si faltan | **Obligatorios** (400 si faltan) |
| Nota de ajuste (`94`) | `Emisor.PrefijoRangoNumeracion` y `DetallePago.Identificador` se completan solos | **Obligatorios** (400 si faltan) |
| Set de pruebas (`TestSetId`) | Si el software del cliente lo tiene, el tiquete POS y la nota de ajuste van al **set de pruebas** y el servicio consulta el resultado a los pocos segundos | Siempre síncrono, sin set |
| Consulta por identificador | En rutas con SET puede ser `ZipKey`; use el identificador devuelto por cada contrato. `/api/documento` consulta por número | Use el identificador devuelto por el contrato; `/api/documento` consulta por número |
| Consecutivos | Se consumen: un número autorizado en habilitación no se reutiliza ahí | Un número autorizado jamás se reutiliza |

## El set de pruebas (habilitación)

Antes de producción, la DIAN exige demostrar que su software emite documentos correctos. En esta plataforma, el criterio que revisa el operador antes de autorizar la producción es:

- **Al menos una factura de venta aprobada en habilitación** (obligatorio).
- **Al menos una nota crédito y una nota débito aprobadas en habilitación** (recomendado: si faltan, la lista de verificación del operador lo marca como aviso).

Cómo hacerlo: envíe sus documentos de prueba con `agregar` (ver [ejemplos](ejemplos/README.md)) y verifique que queden `Aprobado` con `Habilitacion: true`. Use los números del rango de pruebas de la DIAN y, para las notas, una factura de pruebas ya aprobada como `DocumentoRef`.

**Factura, NC, ND y soporte:** sus servicios llaman a `EnviarDocumento`, sin usar TestSetId. Aprobarlos en habilitación no demuestra su asociación al SET. **POS y nómina:** tienen una rama de envío al SET cuando están en habilitación y hay TestSetId. Consulte [operación por módulo](../mantenimiento/operacion-por-modulo.md) antes de declarar completado el SET.

## Tiempos de espera

| Qué | Tiempo |
|---|---|
| Respuesta del proxy de entrada | hasta **180 s** por petición |
| `agregar`, `estado`, `cambios`, `xml`, `pdf` | segundos; use 30 s a 60 s de tiempo de espera |
| `reintentar`, `EnviarDSNO`, `EnviarDE`, nómina, eventos (síncronos) | dependen de la DIAN; use **al menos 180 s** |
| Aviso de webhook | el servicio espera **10 s** a su receptor |
| Espera entre enviar al set de pruebas y consultar | 5 s por defecto (ajuste del servicio) |

En `/api/documento`, los registros persistidos en `Recibido`/`Error` pueden recuperarse con el trabajador de reintentos, si está habilitado y se usa el almacén administrado. No generalice esta garantía a las rutas síncronas, ni a una petición que falló antes de persistir. Ante timeout, consulte primero el identificador original.

## Paso a producción (lo que hace usted y lo que hace el operador)

El detalle técnico del operador está en `../mantenimiento/`. Para usted:

1. Complete la [lista de verificación](lista-de-verificacion.md) en habilitación.
2. Pídale al operador el paso a producción, con la **autorización de su empresa**. La plataforma exige una confirmación explícita y no avanza si faltan requisitos (emisor completo, certificado vigente, software de producción registrado, factura aprobada en habilitación, entre otros).
3. Entregue al operador, por canal seguro: **identificador y PIN del software de producción** y los datos de su **resolución de numeración real**.
4. Cambie en su ERP **la resolución** (prefijo, rango, número de resolución, fechas) y la **clave técnica** de producción. La URL y la clave de API no cambian.
5. Envíe un **primer documento real pequeño** y verifique `Habilitacion: false` y `Aprobado`. Guarde su PDF y XML.
6. Conserve el polling de respaldo y vigile el vencimiento del certificado de firma (si vence, el servicio deja de poder firmar; pida su renovación con tiempo).

El ambiente se cambia solo en la plataforma, nunca desde su ERP; si necesita volver a habilitación (por ejemplo, para probar un cambio grande), pídalo al operador y mejor use un cliente de pruebas separado.

## No verificado

- El proceso oficial de habilitación ante la DIAN (set de pruebas, RUT, registro del software) tiene pasos fuera de esta plataforma. Esta guía solo cubre lo que ocurre dentro de ella; para lo demás, consulte el anexo técnico vigente y a su contador.
- No se probó aún el paso de un cliente real a producción con esta plataforma.
