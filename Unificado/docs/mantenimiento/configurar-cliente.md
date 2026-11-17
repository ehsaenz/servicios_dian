# Configurar un cliente

Última revisión: 2026-11-16 · Rol: **operador**

Todo se hace en la página del cliente: en la lista **Clientes**, pulse el id del cliente (enlace azul). La página muestra, de arriba abajo: **Estado**, **Camino a producción**, **Producción**, **Emisor**, cinco bloques de **Software**, **Certificado de firma**, **Usuarios del portal**, **Logo**, **Sucursales**, **Documentos recientes**, **Webhook de documentos** y **Claves de API**.

Cada vez que guarda algo aparece arriba un cuadro verde con el resultado (por ejemplo «Datos del emisor guardados.»). Si algo falla, un cuadro rojo con el motivo.

## Estado: activar y desactivar

En la sección **Estado** hay una etiqueta y un botón:

| Etiqueta | Significado |
|---|---|
| **Habilitación (los envíos van a la DIAN de pruebas)** | Estado normal de un cliente nuevo |
| **Producción autorizada** | Puede enviar documentos reales ([paso-a-produccion.md](paso-a-produccion.md)) |
| **Desactivado** | No puede usar la API |

- **Desactivar cliente**: «Cliente desactivado: sus claves dejan de funcionar.» El ERP del cliente recibirá rechazo (401) hasta que lo reactive. Úselo cuando un cliente deja el servicio o ante una clave comprometida que no puede revocar.
- **Activar cliente**: lo vuelve a habilitar. No borra nada: claves, certificado y documentos siguen ahí.

## Emisor

Datos de la empresa que emite los documentos. Pulse **Guardar emisor** al terminar («Datos del emisor guardados.»).

| Campo | Qué es | Obligatorio |
|---|---|---|
| **NIT** | Solo números, sin DV | Sí |
| **DV** | Dígito de verificación (un carácter) | Sí para producción |
| **Matrícula mercantil** | Matrícula de la matriz | No bloquea, pero sale en el PDF. Un aviso en la lista de producción si falta |
| **Razón social** | Nombre legal | Sí |
| **Correo** | Correo del emisor (sale en el PDF) | No bloquea; aviso si falta |
| **Teléfono** | Teléfono del emisor (sale en el PDF) | No bloquea; aviso si falta |
| **Perfil de correo** | Nombre del perfil con que el servicio de correo enviará los mensajes de este cliente | No. Ver «No verificado» |

## Software DIAN (uno por tipo de documento)

El «software» es el que el cliente registró en el portal de la DIAN: la DIAN le entrega un **identificador (GUID)**, y el cliente define un **PIN**. Hay **un bloque por tipo de documento**:

| Bloque en pantalla | Para qué documentos |
|---|---|
| **Software · Facturación electrónica (factura, notas crédito y débito)** | Factura de venta, nota crédito, nota débito |
| **Software · Documento soporte** | Documento soporte y su nota de ajuste |
| **Software · Documento equivalente POS** | Tiquete POS y su nota de ajuste |
| **Software · Nómina electrónica** | Nómina |
| **Software · Eventos RADIAN (acuse, recibo, aceptación)** | Eventos RADIAN |

Solo configure los bloques de los documentos que el cliente va a emitir. Cada uno tiene:

| Campo | Qué escribir |
|---|---|
| **Identificador del software (GUID)** | El identificador que entrega la DIAN, por ejemplo `00000000-0000-0000-0000-000000000000` (ejemplo ficticio). Debe ser un GUID válido |
| **PIN** | El PIN del software. Se escribe como contraseña (puntos). Una vez guardado, **nunca se vuelve a mostrar**; al editar, déjelo vacío para conservar el actual |
| **TestSetId (habilitación)** | El identificador del set de pruebas que da la DIAN al habilitar. Solo se usa en pruebas |
| **Ambiente de pruebas (habilitación)** | Casilla. **Marcada** = el software es de habilitación (DIAN de pruebas). **Desmarcada** = es el software de producción |

Pulse **Guardar software** («Software guardado.»).

Cosas que debe saber:

- Guardar un bloque **reemplaza** el software de ese tipo: no se guardan dos a la vez (uno de pruebas y otro de producción) para el mismo tipo de documento. Al pasar a producción se guarda el de producción en lugar del de pruebas ([paso-a-produccion.md](paso-a-produccion.md)).
- El PIN se guarda **cifrado**. Tampoco aparece en la auditoría (solo «PIN nuevo» cuando lo cambia).
- Mensajes de error: «El identificador del software debe ser un GUID, como el que entrega la DIAN.» y «El PIN del software es obligatorio.» (si es un bloque nuevo y no escribió PIN).

## Ambientes: habilitación y producción

No hay URL que escribir. El servicio elige a qué DIAN enviar con **dos condiciones**, que se leen en el servidor:

- Va a **producción** solo si el software está **desmarcado** como «Ambiente de pruebas» **y** el cliente tiene **Producción autorizada**.
- En cualquier otro caso va a **habilitación** (DIAN de pruebas), aunque el software esté marcado como producción.

Por eso un cliente nuevo no puede enviar a producción por error.

## Resoluciones y prefijos

**No se configuran en el panel.** El servicio no guarda los rangos de numeración: el ERP del cliente los envía en cada documento (resolución, prefijo, rango). Si la DIAN rechaza por resolución o prefijo, el dato que hay que revisar está en el ERP del cliente.

## Sucursales y matrícula

Se configuran en la sección **Sucursales** ([logo.md](logo.md)).

## Los demás bloques de la página

| Bloque | Guía |
|---|---|
| Certificado de firma | [certificado.md](certificado.md) |
| Usuarios del portal | [usuarios-del-portal.md](usuarios-del-portal.md) |
| Logo y Sucursales | [logo.md](logo.md) |
| Claves de API | [claves-api.md](claves-api.md) |
| Webhook de documentos | [webhook.md](webhook.md) |
| Documentos recientes | [habilitacion-y-set-de-pruebas.md](habilitacion-y-set-de-pruebas.md) |
| Camino a producción y Producción | [paso-a-produccion.md](paso-a-produccion.md) |

## No verificado

- **Perfil de correo:** el campo se guarda, pero el envío de correo del servicio depende de un servicio de correo (EmailAPI) cuyo contrato aún no está integrado; hoy no se ha probado un envío real. Déjelo vacío salvo que el equipo de desarrollo le indique un valor.
- Para nómina, RADIAN, POS y soporte consulte [operación por módulo](operacion-por-modulo.md); su resultado no se comprueba con la lista de facturas.
- Qué ocurre si se envía un software de producción a la DIAN de habilitación (por ejemplo, tras retirar la autorización de producción): no se probó.
