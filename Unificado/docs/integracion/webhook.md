# Webhook de documentos

Última revisión: 2026-11-16

El webhook es **opcional**. Sin él, su ERP consulta por polling (`GET /api/documento/cambios`). Con él, el servicio le avisa cuando un documento llega a un **estado final**, y usted confirma y descarga.

> **El aviso es una pista, no la fuente de verdad.** Al recibirlo, confirme el estado con `GET /api/documento/estado/{numero}`. Así un aviso falsificado, repetido o fuera de orden no puede dañar sus datos.

## Cómo se configura

En el portal de clientes, en su cliente → sección **Webhook de documentos**:

- **URL:** `http` o `https`, **sin usuario ni clave**, de hasta 300 caracteres. Debe ser alcanzable **desde internet** (el servicio llama hacia usted). Use HTTPS.
- **Secreto de firma:** lo genera la plataforma (32 bytes aleatorios en Base64), se muestra **una sola vez** y se guarda protegido. Marque *Generar un secreto nuevo* para cambiarlo (el anterior deja de servir al instante). Entréguelo al equipo del ERP por un canal seguro.
- **Quitar el webhook:** deje la URL vacía. Vuelve el polling.

Cambios y altas quedan en la auditoría de la plataforma (sin guardar el secreto).

## Cuándo se envía

Cuando un documento llega a **`Aprobado`** o **`Rechazado`**. **No** se avisa de `Recibido` ni de `Error` (esos se resuelven solos con los reintentos del servicio; si se aprueban después, entonces sí hay aviso).

Con un PDF: el aviso de un documento aprobado se envía **después** de generar el PDF, así que normalmente trae `Pdf: true`. Si el generador falló, trae `Pdf: false` y el PDF se genera cuando usted lo pida.

## La petición

```
POST <su URL>
Content-Type: application/json; charset=utf-8
X-Dian-Evento: documento.estado
X-Dian-Firma: sha256=<64 caracteres hexadecimales en minúsculas>
```

Cuerpo:

```json
{
  "Evento": "documento.estado",
  "Numero": "SETP990001001",
  "Estado": "Aprobado",
  "CUFE": "3f0c...(96 hexadecimales)...a91b",
  "Mensaje": "La Factura electrónica SETP990001001, ha sido autorizada.",
  "Pdf": true,
  "SecuenciaCambio": 3
}
```

| Campo | Tipo | Significado |
|---|---|---|
| `Evento` | texto | Siempre `documento.estado`. |
| `Numero` | texto | Número del documento. |
| `Estado` | texto | `Aprobado` o `Rechazado`. |
| `CUFE` | texto | CUFE/CUDE del documento. |
| `Mensaje` | texto | Mensaje de la DIAN (en un rechazo, su resumen). El detalle de las reglas está en `Errores` del registro: pídalo con `estado/{numero}`. |
| `Pdf` | booleano | Si ya existe el PDF oficial. |
| `SecuenciaCambio` | entero | Cursor de cambios del documento. Sirve para identificar el aviso. |

El cuerpo se serializa con el serializador estándar de .NET: los caracteres no ASCII salen como secuencias `\uXXXX` (por ejemplo `ó` por "ó"). Es JSON válido y su biblioteca lo interpreta igual; solo importa que **la firma se calcula sobre esos bytes exactos**.

## Cómo verificar la firma

`X-Dian-Firma` = `"sha256=" + hex_minusculas( HMAC-SHA256( clave = secreto, mensaje = cuerpo ) )`

Reglas que fallan con frecuencia:

1. **La clave es el texto del secreto**, tal como lo mostró el portal (la cadena Base64), convertida a bytes **UTF-8**. **No** la decodifique de Base64.
2. El mensaje es el **cuerpo crudo**, byte a byte. **No** lo parsee y vuelva a serializar antes de calcular: cambiaría los bytes. Lea el cuerpo sin procesar.
3. Compare en **tiempo constante** (`timingSafeEqual`, `hmac.compare_digest`, `CryptographicOperations.FixedTimeEquals`).
4. Si la firma no coincide, responda **401** y no procese nada.

Receptores completos y listos: [Node](ejemplos/webhook/receptor.mjs), [Python](ejemplos/webhook/receptor.py), [C#](ejemplos/webhook/ReceptorWebhook.cs).

Pseudocódigo:

```
cuerpo = leer_cuerpo_crudo(peticion)
esperada = "sha256=" + hex(hmac_sha256(utf8(SECRETO), cuerpo))
si no comparar_tiempo_constante(esperada, cabecera("X-Dian-Firma")): responder 401
```

El servicio **no incluye marca de tiempo** en la firma: un aviso capturado podría reenviarse. Por eso la regla de oro es **confirmar con `estado/{numero}`** antes de actuar, y tratar el aviso como idempotente.

## Cómo responder

- **Cualquier código 2xx** = entregado. Cualquier otro (o un tiempo de espera, o un error de conexión) = no entregado y se reintenta.
- El servicio **espera 10 segundos** como máximo. Responda de inmediato y haga el trabajo pesado (descargar PDF, actualizar su sistema) en segundo plano.
- **No siga redirecciones:** el servicio no las sigue. Una respuesta 301/302/307/308 cuenta como fallo. Registre la URL final.
- Mantenga el certificado TLS de su receptor válido (el servicio verifica el certificado de su URL).

## Reintentos

Si el aviso no se entregó, queda **pendiente** y un trabajo de fondo lo reintenta, con espera creciente medida **desde el intento anterior**:

| Intento | Espera antes de este intento |
|---|---|
| 1 | inmediato (al llegar el documento a su estado final) |
| 2 | 1 minuto |
| 3 | 2 minutos |
| 4 | 4 minutos |
| 5 | 8 minutos |
| 6 | 16 minutos |
| 7 a 10 | 30 minutos cada uno |

- **Máximo 10 intentos** (cerca de 2,5 horas en total). Después de eso **el servicio deja de insistir**.
- El trabajo de fondo corre cada 60 s por defecto, así que las esperas son "al menos" esos minutos (con una granularidad de cerca de un minuto).
- El estado de cada documento muestra `WebhookPendiente`, `WebhookIntentos`, `WebhookError` y `WebhookEntregado` (en `estado/{numero}` y en el portal).
- **Si agotó los 10 intentos, recupere con polling:** `GET /api/documento/cambios?desde=<su cursor>` devuelve los documentos cambiados. Por eso conviene **mantener siempre un polling de respaldo** (por ejemplo cada 5 a 15 minutos).
- Si el documento se rechaza, usted lo corrige y lo reenvía con el mismo número, y se aprueba, habrá un aviso nuevo para ese estado.

## Idempotencia

El servicio entrega **al menos una vez**: puede repetir un aviso (por ejemplo si su receptor respondió 2xx pero la respuesta se perdió). Trate cada aviso como idempotente. Una llave sencilla y suficiente: **`Numero` + `Estado` + `SecuenciaCambio`**. Si ya la procesó, responda 200 y no haga nada.

No asuma orden de llegada entre avisos de documentos distintos.

## Prueba rápida

1. Levante uno de los [receptores de ejemplo](ejemplos/webhook/) detrás de HTTPS y registre su URL en el portal.
2. Envíe un documento en habilitación.
3. Verifique en el portal que el documento muestre `webhook ✓`, y en su receptor, la línea de registro.
4. Apague el receptor, envíe otro documento y compruebe que el portal muestre `webhook pendiente` y que, al volver a encenderlo, llegue el reintento.

## No verificado

- La entrega contra una **URL real por internet** no se probó (solo con entrega simulada en las pruebas automáticas). Si ve diferencias, avise a SISTEMAS 360.
- Si su receptor está detrás de un firewall que filtra por dirección IP de origen, pida a SISTEMAS 360 la IP de salida del servicio (varía según el despliegue).
