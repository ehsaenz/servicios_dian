# Autenticación: la clave de API

Última revisión: 2026-11-16

## Cómo se envía

Toda petición, salvo `GET /salud`, lleva la cabecera:

```
x-api-key: <SU_CLAVE_DE_API>
```

- El nombre de la cabecera no distingue mayúsculas de minúsculas (es una cabecera HTTP).
- No hay usuario/contraseña, ni tokens que expiren, ni la clave en la URL. Solo esa cabecera.
- Siempre por HTTPS. Una clave enviada por HTTP plano debe darse por comprometida.

## Qué identifica la clave

La clave es de **un cliente** (su empresa). Con ella el servicio sabe su emisor, su software registrado ante la DIAN, su certificado de firma, su webhook y el ambiente (habilitación o producción). Los documentos son por cliente: el número `SETP990000001` de otro cliente no existe para usted (responde 404) y usted no puede ver ni tocar los de otro.

Para comprobar con qué cliente quedó identificada su clave:

```
GET /api/Servicio/Quien
```

```json
{ "Id": "ACME", "Nit": "900999999", "RazonSocial": "EMPRESA DE EJEMPLO S.A.S." }
```

## Respuestas de error de autenticación

| Situación | Código | Cuerpo |
|---|---|---|
| Sin cabecera, clave desconocida, clave revocada o **cliente desactivado** | **401** | Texto plano: `Clave de API inválida.` |

El servicio **no** devuelve 403 por clave: no distingue "no existe" de "está revocada" (por seguridad). Si recibe 401 con una clave que antes funcionaba, pida al operador que revise si se revocó la clave o se desactivó el cliente.

Un 403 o un 404 con una página HTML que no viene de la API suele venir de la infraestructura (IIS o nginx), por ejemplo un cuerpo mayor de 5 MB. Ver [referencia-api.md](referencia-api.md#límites-de-infraestructura).

## Cómo se obtiene la clave

1. El **operador** de SISTEMAS 360 da de alta su cliente y le genera una clave, o
2. su usuario del **portal de clientes** entra a su cliente → sección **Claves de API** → *Generar clave nueva* (con una descripción, por ejemplo "ERP sucursal norte").

La clave completa **se muestra una sola vez**. La plataforma guarda solo su huella (hash SHA-256), no la clave; si la pierde no se puede recuperar: se revoca y se genera otra. Entréguela al equipo del ERP por un canal seguro y guárdela como secreto (variable de entorno, bóveda), nunca en el código ni en el repositorio.

## Rotación sin cortes

Un cliente puede tener **varias claves activas a la vez**, y por eso la rotación no interrumpe el servicio:

1. Genere una clave nueva en el portal (o pídala al operador).
2. Configure la clave nueva en el ERP y despliegue.
3. Compruebe con `GET /api/Servicio/Quien` y un documento de prueba.
4. **Revoque** la clave anterior en el portal (botón *Revocar*). Desde ese momento la anterior responde 401.

Cuándo rotar: cada cierto tiempo según su política, al salir una persona que la conocía, o de inmediato ante cualquier sospecha de filtración. Si usa varios sistemas (ERP, POS, sucursales), pida una clave por sistema (campo *Descripción*) para poder revocar solo la comprometida.

Las altas y bajas de claves quedan en la auditoría de la plataforma; solo se anotan los **4 últimos caracteres** de la clave para poder identificarla ("termina en ...").

## Buenas prácticas

- Una clave por sistema/ambiente de su lado; no comparta la de producción con el ambiente de pruebas de su ERP.
- No la escriba en registros (logs) ni en mensajes de error. El servicio tampoco la registra: solo el id del cliente.
- Límite de solicitudes: unas **20 por segundo por dirección IP** (con ráfagas hasta 40) en la entrada pública; superado, la infraestructura responde **503**. Reintente con espera.
