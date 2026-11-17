# Webhook de documentos

Última revisión: 2026-11-16 · Rol: **operador y cliente** (ambos pueden)

Un **webhook** es un aviso que el servicio hace al ERP del cliente cuando un documento ya tiene resultado final (**Aprobado** o **Rechazado**). Es **opcional**.

## ¿Webhook o consulta (polling)?

| | Webhook | Consulta (polling) |
|---|---|---|
| Cómo funciona | El servicio avisa al ERP en una URL | El ERP pregunta cada cierto tiempo (`api/documento/cambios`) |
| Cuándo sirve | El ERP es **accesible desde internet** | El ERP está detrás de una red privada (la mayoría) |
| Configuración | Aquí | Ninguna: siempre disponible |

**El aviso es solo una pista**: el ERP debe confirmar siempre con la consulta de estado del documento. Si el ERP no tiene URL pública, **no configure webhook**: use polling.

## Configurarlo

1. Página del cliente → sección **Webhook de documentos**.
2. En **URL** escriba la dirección del ERP, por ejemplo `https://erp.ejemplo.com/dian/aviso` (ejemplo ficticio).
   - Debe ser `http` o `https` (use `https`), **sin usuario ni contraseña en la dirección**, de hasta 300 caracteres.
   - Mensaje si no cumple: «La URL del webhook debe ser http o https, sin usuario ni clave, de hasta 300 caracteres.»
3. Pulse **Guardar webhook**.

La **primera vez** el sistema genera un **secreto de firma** y lo muestra en un cuadro: **«Secreto de firma — entréguelo al ERP por un canal seguro. Es la última vez que se muestra.»** Cópielo en ese momento: se guarda cifrado y **no se vuelve a ver**. Verá «Webhook guardado. Copie el secreto ahora: no se vuelve a mostrar.»

El ERP usa el secreto para comprobar que el aviso viene realmente del servicio.

### Cambiar la URL sin cambiar el secreto

Cambie la URL y pulse **Guardar webhook** con la casilla **Generar un secreto nuevo** desmarcada: queda «Webhook guardado.» y se conserva el secreto actual (la pantalla indica «Ya tiene un secreto guardado»).

### Generar un secreto nuevo

Marque **Generar un secreto nuevo** y pulse **Guardar webhook**. Aparece el secreto nuevo (una sola vez). Entréguelo al ERP **antes** de que llegue el próximo aviso o el ERP rechazará la firma.

### Quitar el webhook

Deje la **URL vacía** y pulse **Guardar webhook**: «Webhook quitado: el ERP consulta por polling.» También se borra el secreto.

## Qué recibe el ERP

Una llamada `POST` a la URL, con un JSON pequeño con estos datos: `Evento` (valor `documento.estado`), `Numero`, `Estado`, `CUFE`, `Mensaje`, `Pdf` y `SecuenciaCambio`. No lleva el documento completo.

Cabecera de firma: `X-Dian-Firma: sha256=<firma>`, donde la firma es el **HMAC-SHA256 del cuerpo** calculado con el secreto. El ERP debe recalcularla y compararla.

El servicio **no sigue redirecciones** y espera hasta **10 segundos** la respuesta. El ERP debe responder con un código **2xx**.

## Reintentos

Si el ERP no responde 2xx, el aviso queda pendiente y se reintenta con espera creciente (1, 2, 4… hasta 30 minutos), **hasta 10 intentos**. Un trabajador de fondo lo hace solo, cada minuto por defecto.

En **Documentos recientes**, columna **Avisos**:

| Marca | Significado |
|---|---|
| **webhook ✓** | El ERP respondió bien |
| **webhook pendiente** (naranja) | Aún se está reintentando |
| (nada) | El cliente no tiene webhook, o el documento aún no está en estado final |

Si tras 10 intentos sigue pendiente, el aviso se abandona. El ERP no pierde información: puede consultar el estado por polling.

## Qué puede hacer el cliente

El cliente puede ver y cambiar su URL, y regenerar su secreto desde el portal. Todo queda en la auditoría (`webhook`, `webhook-quitar`) sin el secreto.

## No verificado

- **Un aviso entregado a una URL real**: solo se probó con una entrega simulada.
- Los valores exactos de `Estado` en el JSON se describen en la guía de integración ([../integracion/README.md](../integracion/README.md)).
- Qué pasa con los avisos pendientes si se quita el webhook mientras reintentan.
