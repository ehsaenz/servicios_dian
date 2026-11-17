# Paso a producción

Última revisión: 2026-11-16 · Rol: **operador** (el cliente no ve esta sección)

**Producción** es enviar documentos **reales** a la DIAN. Tienen validez legal y consumen numeración real.

> **Regla de oro: nada pasa a producción sin la confirmación expresa del cliente.** Pida su autorización por escrito (correo o mensaje) antes de empezar y guárdela.

## Cómo protege el sistema

El servicio solo envía a producción si se cumplen **las dos** condiciones:

1. El software de facturación está **desmarcado** como «Ambiente de pruebas (habilitación)».
2. El cliente tiene **«Producción autorizada»**.

Si falta una, **todo va a habilitación**. Así ni un cliente nuevo ni un error de configuración llegan a producción por accidente.

## La lista «Camino a producción»

En la página del cliente, sección **Camino a producción**. Cada fila tiene un símbolo:

| Símbolo | Significado |
|---|---|
| ✔ | Cumplido |
| ▲ | Aviso: **no impide** producción, pero conviene resolverlo |
| ✖ | Pendiente: **impide** autorizar producción |

| Ítem | Qué revisa | ✖ si… | ▲ si… |
|---|---|---|---|
| **Datos del emisor** | NIT, DV, razón social | falta alguno de esos tres | falta matrícula, correo o teléfono |
| **Certificado de firma** | Certificado cargado y vigente | no hay, o ya venció | vence en menos de 30 días |
| **Software de facturación electrónica** | Existe el software de tipo facturación electrónica | no está registrado | — |
| **Set de pruebas de la DIAN** | Documentos aprobados en habilitación | ninguna factura aprobada | faltan notas crédito o débito |
| **Software de producción** | El software de facturación está marcado como producción | sigue marcado como pruebas | — |
| **Clave de API** | Al menos una clave activa | — | no hay clave |
| **Logo** | Logo del cliente o de alguna sucursal | — | no hay logo |

El orden habitual que sugiere la propia pantalla: emisor, certificado, software de habilitación, set de pruebas aprobado y, al final, software de producción y autorización.

## Alcance y pendientes antes del cambio

La lista exige facturación electrónica incluso si el cliente solo usa otro módulo. No eluda ese bloqueo: consulte [operación por módulo](operacion-por-modulo.md). La autorización es global para el cliente; revise todos sus módulos.

Pause nuevos envíos del ERP y concilie documentos pendientes antes de reemplazar software. Los reintentos de `/api/documento` conservan el ambiente del registro original.

## Procedimiento

1. **Confirme con el cliente** que quiere pasar a producción y que tiene su resolución de numeración de producción vigente (se configura en su ERP; el servicio no guarda rangos).
2. Revise que la lista esté **sin ✖**, salvo el ítem «Software de producción», que se resuelve en el paso 3.
3. **Registre el software de producción.** En el bloque **Software · Facturación electrónica**:
   - **Identificador del software (GUID)** y **PIN**: los de producción que entrega la DIAN.
   - **Desmarque «Ambiente de pruebas (habilitación)».**
   - **Guardar software.** Esto **reemplaza** el software de pruebas de ese tipo.
   - Haga lo mismo con cada tipo que el cliente usará en producción (documento soporte, POS, etc.).
4. Compruebe que **todo** quedó ✔ (los ▲ pueden quedar).
5. En la sección **Producción**: «Mientras no se autorice, todo envío va a habilitación aunque el software esté marcado como producción. Autorice solo con la confirmación expresa del cliente.»
6. Escriba exactamente `AUTORIZO PRODUCCION` (mayúsculas, sin tilde) en el campo de confirmación.
7. Pulse **Autorizar producción**. Verá: «Producción autorizada para este cliente.» y la etiqueta cambia a **Producción autorizada**.

Si el botón está gris o aparece «Aún falta: …», hay un ✖ pendiente: el sistema lo vuelve a comprobar en el servidor, así que no se puede saltar. Mensajes posibles:

| Mensaje | Qué hacer |
|---|---|
| «Para autorizar producción escriba exactamente: AUTORIZO PRODUCCION» | Escriba la frase tal cual |
| «Aún falta: … Complete la lista de verificación antes de autorizar producción.» | Resuelva los ✖ nombrados |
| «Antes de autorizar producción el cliente necesita certificado y software configurados.» | Cargue certificado y software |

Haga los pasos 3 a 7 **en la misma sesión y coordinado con el cliente**: entre el paso 3 y el 7 los envíos van a habilitación con el software de producción registrado, y esa combinación no se ha probado ([No verificado](#no-verificado)).

## Después de autorizar

- Pida al cliente que emita **un primer documento real** con calma y revise que aparezca **Aprobado** en **Documentos recientes**, **sin** la etiqueta «pruebas».
- Verifique el PDF descargado: QR, CUFE y logo.
- Cada cambio queda en la [auditoría](auditoria-y-respaldos.md): `software`, `produccion-autorizar`.

## Volver atrás

| Quiero | Qué hacer | Efecto |
|---|---|---|
| Retirar producción para documentos nuevos | Sección **Producción** → **Retirar autorización de producción** | «Producción retirada: el cliente vuelve a habilitación.» No pide confirmación. Los envíos nuevos van a la DIAN de pruebas |
| Bloquear nuevas peticiones del cliente | **Desactivar cliente** (sección Estado) | Sus claves dejan de funcionar |
| Volver a probar | Retire la autorización y, si es necesario, registre de nuevo el software de pruebas (**Ambiente de pruebas** marcado) | |

> **Cuidado:** un documento que la DIAN ya **aprobó en producción no se puede deshacer desde esta plataforma**. Se corrige con una **nota crédito** emitida por el cliente. Retirar la autorización cambia el ambiente de documentos nuevos; **no cancela tareas en curso ni cambia el ambiente de registros pendientes**. Desactivar tampoco cancela tareas iniciadas. Para contención inmediata coordine la detención de la API con infraestructura.

## No verificado

- **Nunca se ha autorizado producción para ningún cliente**: todo este procedimiento está basado en el código y las pruebas, no en una autorización real.
- Qué responde la DIAN de habilitación si recibe el software de **producción** (estado intermedio entre el paso 3 y el 7), y viceversa.
- El comportamiento de los tipos de documento distintos de la factura (documento soporte, POS, nómina) en producción.
- Si el cliente debe **registrar de nuevo** el rango de numeración en el portal de la DIAN: depende de la DIAN, no del panel.
