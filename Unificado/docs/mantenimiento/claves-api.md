# Claves de API

Última revisión: 2026-11-16 · Rol: **operador y cliente** (ambos pueden generar y revocar)

La **clave de API** es la «contraseña» con que el ERP del cliente se identifica ante el servicio. El ERP la envía en la cabecera HTTP `x-api-key` en cada llamada. Cada clave pertenece a **un solo cliente**: con ella el ERP solo ve y envía documentos de ese cliente.

## Cómo se guarda

- El sistema guarda solo un **resumen (hash SHA-256)** de la clave, nunca la clave completa.
- Por eso **la clave completa se muestra una sola vez**, justo al generarla. Después solo se ve en qué termina («…ab12»). No existe forma de recuperarla.

## Generar una clave

1. Página del cliente → sección **Claves de API**.
2. En **Descripción** escriba para qué es (por ejemplo, «ERP sucursal norte»; hasta 80 caracteres). Ayuda a saber de quién es cada clave.
3. Pulse **Generar clave nueva**.
4. Arriba aparece un cuadro: **«Clave de API nueva — entréguela al cliente por un canal seguro. Es la última vez que se muestra.»** con la clave y la nota «El ERP la envía en la cabecera x-api-key».
5. **Cópiela en ese momento.** Si recarga la página, ya no está.

Entréguela por un canal seguro (gestor de contraseñas, o llamada). No la mande por correo ni WhatsApp en claro si puede evitarlo.

La tabla de claves muestra: **Descripción**, **Termina en**, **Creada** (UTC), **Estado** (Activa / Revocada) y el botón **Revocar**.

## Rotar una clave (cambiarla sin cortar el servicio)

Un cliente puede tener **varias claves activas a la vez**; eso permite cambiar sin interrupción:

1. Genere una clave **nueva** (descripción sugerida: «ERP, rotación 2026-11»).
2. Entréguela al cliente y pídale que la configure en su ERP.
3. Cuando confirme que el ERP ya funciona con la nueva, **revoque la anterior**.

## Revocar una clave

En la fila de la clave, pulse **Revocar** («Clave revocada.»). La clave deja de funcionar **de inmediato** y el estado pasa a **Revocada**. No se puede reactivar: se genera otra.

Revoque de inmediato cuando:

- La clave se filtró o se envió por un medio inseguro.
- Una persona con acceso a la clave dejó la empresa.
- Se terminó una rotación.

## Qué ve el ERP si la clave falla

El servicio responde **401** (no autorizado) si la clave no existe, fue revocada o el cliente está **Desactivado**. Ver [preguntas-frecuentes.md](preguntas-frecuentes.md).

## Qué puede hacer el cliente

El usuario del portal también puede generar y revocar las claves **de su empresa** ([usuarios-del-portal.md](usuarios-del-portal.md)). Todo queda en la auditoría (`clave-crear`, `clave-revocar`, con los últimos 4 caracteres, nunca la clave).

## En producción

En **Camino a producción**, «Clave de API» es un aviso (▲) si el cliente no tiene ninguna activa: «Aún no tiene clave de API: su ERP no podrá enviar.» No impide autorizar producción.

## No verificado

- El límite de cuántas claves puede tener un cliente (el panel no lo restringe).
- Cuánto tarda en dejar de funcionar una clave revocada cuando hay varias instancias del servicio (el almacén se relee cada pocos segundos).
