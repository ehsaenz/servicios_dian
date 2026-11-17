# Habilitación y set de pruebas

Última revisión: 2026-11-16 · Rol: **operador** (el cliente ve sus documentos)

**Habilitación** es el ambiente de pruebas de la DIAN. Todo cliente nuevo empieza ahí: sus documentos se envían a la DIAN **de pruebas**, no valen legalmente y sirven para demostrar que el software emite bien. Al superar el **set de pruebas** (el conjunto de documentos que la DIAN pide a cada software), el cliente queda listo para pedir su paso a producción ([paso-a-produccion.md](paso-a-produccion.md)).

## Lo que debe estar listo antes de enviar el primer documento

Marque cada punto en la página del cliente:

| # | Qué | Dónde | Guía |
|---|---|---|---|
| 1 | Cliente creado, **activo** | Estado | [crear-cliente.md](crear-cliente.md) |
| 2 | Emisor con NIT, DV y razón social | Emisor | [configurar-cliente.md](configurar-cliente.md) |
| 3 | **Software de facturación electrónica** con su GUID y PIN, **«Ambiente de pruebas (habilitación)» marcado** y el **TestSetId** | Software · Facturación electrónica | [configurar-cliente.md](configurar-cliente.md) |
| 4 | **Certificado** vigente | Certificado de firma | [certificado.md](certificado.md) |
| 5 | **Clave de API** entregada al ERP | Claves de API | [claves-api.md](claves-api.md) |

El software, el GUID, el PIN y el TestSetId los obtiene el cliente en el portal de la DIAN al registrarse como facturador en **habilitación** (esos pasos son del portal de la DIAN y no se describen aquí).

## Iniciar el set de pruebas

No hay un botón de «iniciar el set» en el panel. **Enviar a habilitación y enviar a un SET son operaciones diferentes**: `/api/documento/agregar` no usa TestSetId. Los pasos siguientes prueban facturas en habilitación; no acreditan por sí solos completar un SET. POS y nómina sí utilizan TestSetId en sus rutas cuando corresponde. Consulte [operación por módulo](operacion-por-modulo.md).

Para validar facturación en habilitación:

1. Confirme con el cliente que su ERP apunta al servicio con su clave de API y que está en modo de pruebas.
2. Pida que emita, con datos **ficticios**, los documentos que exige el set, en este orden recomendado:
   1. **Factura de venta (FV).**
   2. **Nota crédito (NC)** que referencie una factura ya aprobada.
   3. **Nota débito (ND)** que referencie una factura ya aprobada.
   4. Si el cliente usa otros tipos (documento soporte, documento equivalente POS, nómina, eventos RADIAN), uno de cada tipo, con su software configurado.
3. La cantidad y el tipo exactos de documentos de cada set los define el portal de la DIAN por software; consúltelos allí.

> Mientras el cliente **no** tenga «Producción autorizada», todo envío va a habilitación aunque el software esté marcado como producción.

### Ejemplo de una prueba completa (datos ficticios)

El equipo de desarrollo validó el servicio con una empresa de prueba: una factura (`SETP990000518`), una nota crédito por anulación (`SETP990000516`) y una nota débito por intereses (`SETP990000517`), las tres referenciando facturas ya aprobadas. La DIAN respondió «ha sido autorizada» en las tres. Los consecutivos `SETP` son de habilitación y se consumen al enviar: el ERP debe usar el rango de pruebas que la DIAN asignó.

## Leer los resultados

Abra el cliente y baje a **Documentos recientes** (los 50 más recientes). Columnas:

| Columna | Qué muestra |
|---|---|
| **Número** | El consecutivo. La etiqueta **pruebas** indica que se envió a la DIAN de pruebas |
| **Tipo** | El tipo de documento del ERP |
| **Estado** | Ver tabla de abajo |
| **Actualizado (UTC)** | Última vez que cambió |
| **PDF** | **Descargar** (solo si fue aprobado). «Sin PDF» si fue aprobado pero no se pudo generar |
| **Avisos** | «correo ✓», «webhook ✓» o «webhook pendiente» |

### Estados

| Estado | Color | Significado | Qué hacer |
|---|---|---|---|
| **Recibido** | naranja | El servicio lo recibió y está procesándolo o a punto de enviarlo | Espere. Si pasan varios minutos, revise que no quede atascado |
| **Aprobado** | verde | La DIAN lo autorizó (respuesta 00). **Es el único estado con PDF oficial** | Nada; descargue el PDF |
| **Rechazado** | rojo | La DIAN lo rechazó. Debajo aparece el mensaje con los errores | Corregir el dato indicado en el ERP y **reenviar con el mismo número** |
| **Error** | rojo | Falla de red o respuesta inesperada. Se **reintenta solo** | Normalmente se resuelve solo; si persiste, avise al equipo de desarrollo |

Detalles útiles:

- **Idempotencia:** reenviar un número ya recibido o aprobado **no lo duplica**. Uno rechazado o con error se reprocesa.
- **Reintentos automáticos:** los documentos en **Error** se reenvían a la DIAN con espera creciente (hasta 10 intentos); los que quedaron en **Recibido** sin enviarse (por ejemplo, tras un reinicio del servidor) se retoman pasados unos 2 minutos.
- Un documento recién aprobado puede mostrar un instante «Sin PDF» mientras el PDF se genera.
- El mensaje de un rechazo es el de la DIAN. Los más comunes son datos del ERP: ciudad o departamento mal escritos, tarifas, código postal, resolución o prefijo.

## Cuándo se considera superado

La lista **Camino a producción** aplica un criterio local, no confirma el SET oficial: el ítem **Set de pruebas de la DIAN** cuenta los aprobados en habilitación entre los **1.000 registros más recientes** de `/api/documento`:

| Resultado | Significa |
|---|---|
| ✖ pendiente | Ninguna factura aprobada todavía |
| ▲ aviso | Hay facturas aprobadas pero faltan notas crédito y/o débito por probar |
| ✔ cumplido | Al menos una factura, una nota crédito y una nota débito aprobadas |

El detalle indica «Aprobados en habilitación: N factura(s), N nota(s) crédito, N nota(s) débito». Esto es el criterio **del panel**; la habilitación definitiva la confirma la DIAN en su portal según su propio set.

## Ver el PDF

Con el documento aprobado, use **Descargar**. Es un PDF con QR y CUFE del ambiente de pruebas. El logo sale si ya lo subió ([logo.md](logo.md)).

## No verificado

- La cantidad de documentos que pide el set de la DIAN por tipo de software.
- Un envío real de **nómina**, **eventos RADIAN** o **documento equivalente POS** desde el panel de un cliente en la VM: solo se validaron factura, nota crédito y nota débito en el servicio instalado (y documento soporte, POS y nota de ajuste en el piloto anterior).
- Solo hay **plantilla de PDF para factura de venta**: las notas crédito y débito aprobadas quedan **sin PDF** («no hay plantilla») hasta que se desarrolle.
- Cómo se ve la lista cuando hay más de 50 documentos: solo se muestran los 50 más recientes.
