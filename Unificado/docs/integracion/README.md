# Integración con la API de facturación electrónica DIAN (SISTEMAS 360)

Última revisión: 2026-11-16

Alcance: código y documentación local de Unificado. Las URLs y estados del piloto son referencias históricas; confirme el ambiente actual con el operador. No se inspeccionaron servidores en esta revisión.

Esta carpeta es para quien **consume** la API desde un ERP, un POS o un SDK propio. No hace falta conocer cómo está hecho el servicio por dentro. Si necesita eso, vea la documentación de desarrollo (`../desarrollo/`); si va a instalarlo, `../despliegue/`; si administra clientes, `../mantenimiento/`.

## Qué hace el servicio

Usted envía los datos del documento en JSON. El servicio los traduce a UBL, los firma con **el certificado de su empresa** (cargado una sola vez por el operador), los envía a la DIAN, guarda el resultado, genera el PDF oficial (solo de documentos aprobados) y le avisa por webhook o usted consulta por polling.

Una misma instalación atiende a muchos clientes. Su **clave de API** lo identifica: con ella solo ve y usa sus propios documentos, su emisor, su software DIAN y su certificado.

## URL base

| Ambiente de la plataforma | URL base | Estado |
|---|---|---|
| Hoy (pruebas, VM interna publicada por el IIS de SISTEMAS 360) | `https://services.sistemas360.com:47125` | En uso |
| Futuro | `https://dian.sistemas360.com` (sin puerto) | Pendiente de reenvío en el router y DNS |

Todo va por HTTPS, con `Content-Type: application/json` en los cuerpos. La URL base se la confirma el operador; los ejemplos de esta carpeta usan `https://services.sistemas360.com:47125`.

> **Habilitación vs producción** no se elige por URL ni por clave: lo decide la configuración de su cliente en la plataforma. Ver [ambientes.md](ambientes.md).

## Flujo recomendado en 5 pasos (facturas, notas crédito y débito)

1. **Obtenga su clave de API** del operador o de su portal de clientes y pruebe `GET /salud` (sin clave) y `GET /api/Servicio/Quien` (con clave). Ver [autenticacion.md](autenticacion.md).
2. **Arme el JSON del documento** (`DocumentoDian`). Ver [documentos-soportados.md](documentos-soportados.md) y los [ejemplos](ejemplos/README.md).
3. **Envíelo** con `POST /api/documento/agregar`. Valida, traduce y firma antes de responder **202** con el registro y su CUFE. Normalmente queda `Recibido` y el envío DIAN corre en segundo plano; un duplicado puede devolver el estado existente y el modo síncrono puede devolver un resultado final.
4. **Espere el resultado**: por **webhook** (aviso firmado cuando queda `Aprobado` o `Rechazado`, ver [webhook.md](webhook.md)) o por **polling** (`GET /api/documento/estado/{numero}` o, mejor, `GET /api/documento/cambios?desde=N`). Ver [estados-y-errores.md](estados-y-errores.md).
5. **Descargue lo oficial**: `GET /api/documento/pdf/{numero}` (solo aprobados), `xml/{numero}` y `respuesta-dian/{numero}`. Guárdelo en su sistema. Ver [pdf-y-correo.md](pdf-y-correo.md).
   Si el servicio no tiene local el XML y el acuse de un documento (p. ej. facturas anteriores a esta versión o enviadas desde otra instancia), envíe el CUFE: `GET /api/documento/pdf/{numero}?cufe={CUFE}`, `xml/{numero}?cufe=...` o `respuesta-dian/{numero}?cufe=...`. El servicio consulta la DIAN por ese CUFE, valida que el acuse sea de aprobación (02) del mismo CUFE, lo guarda en local y responde. Si la DIAN no lo devuelve o no lo aprueba, responde **502** con el motivo. Sin `cufe`, solo responde con lo local.

Antes de pedir el paso a producción, recorra la [lista de verificación](lista-de-verificacion.md).

## Contenido

| Archivo | Para qué |
|---|---|
| [autenticacion.md](autenticacion.md) | Cabecera `x-api-key`, cómo se obtiene y se rota, errores 401 |
| [referencia-api.md](referencia-api.md) | Cada ruta de la API: método, parámetros, cuerpos, respuestas y códigos HTTP |
| [documentos-soportados.md](documentos-soportados.md) | Factura de venta, notas, documento soporte, tiquete POS, nota de ajuste, nómina y eventos RADIAN; campos y reglas |
| [estados-y-errores.md](estados-y-errores.md) | Ciclo de vida, códigos de la DIAN, errores de validación y qué hacer |
| [pdf-y-correo.md](pdf-y-correo.md) | PDF oficial, plantillas, logo y correo |
| [webhook.md](webhook.md) | Formato exacto del aviso, firma HMAC, reintentos, idempotencia |
| [guia-para-quantyerp.md](guia-para-quantyerp.md) | Guía para el equipo de QuantyERP: qué cambiar, reglas bloqueantes, set de pruebas y decisiones abiertas |
| [ambientes.md](ambientes.md) | Habilitación y producción, tiempos de espera, set de pruebas |
| [set-de-pruebas-produccion.md](set-de-pruebas-produccion.md) | Paso del ERP para validar el set antes de producción (encabezado `X-Envio-Set`) |
| [lista-de-verificacion.md](lista-de-verificacion.md) | Checklist de certificación del integrador |
| [primer-envio-set.md](primer-envio-set.md) | Primer envío aceptado al set de pruebas de MARLIT (habilitación): CUFE, ZipKey e interpretación de `CodigoDian` |
| [ejemplos/](ejemplos/README.md) | JSON completos y código en curl, C#, Node y Python |

## Qué rutas existen (resumen)

| Familia | Rutas | Documentos |
|---|---|---|
| Documentos del ERP (nuevo, asíncrono) | `/api/documento/*` | Factura de venta `01`, nota crédito `91`, nota débito `92` |
| Documento soporte | `/api/DocumentoSoporte/EnviarDSNO`, `ConsultarDSNO` | `05` |
| POS | `/api/DocumentoElectronico/EnviarDE`, `EnviarDEJson`, `ConsultarDE` | `05`, tiquete POS `20`, nota de ajuste `94` |
| Nómina electrónica | `/api/v1/Nomina/*` | Nómina individual |
| Eventos RADIAN | `/api/v1/Eventos/*` | Acuse, reclamo, recibo, aceptación |
| Servicio | `/salud`, `/api/Servicio/*` | Diagnóstico |

El detalle completo está en [referencia-api.md](referencia-api.md).

## Fuente de verdad y límites de esta documentación

El contrato sale del **código** del servicio (`src/Pos/Pos.Api`, `src/Pos/Pos.Servicios`, `src/Shared/*`) y de sus pruebas. Cuando algo no se pudo confirmar en el código aparece marcado como **No verificado**. Los ejemplos usan datos ficticios y **no se han enviado a la DIAN**: ajústelos a su numeración y a sus terceros reales y pruébelos en habilitación.

Un integrador en .NET 10 puede reutilizar el modelo y el constructor `DianDocumentBuilder` de `src/Shared/ModeloDian` (espacio de nombres `WS_V2_HttpClientLib.Builders`), que es el que usa QuantyERP para armar el JSON. Los demás lenguajes arman el JSON a mano según [documentos-soportados.md](documentos-soportados.md).


## Guía complementaria

- [Integración de cualquier ERP y construcción de SDK](erp-y-sdk.md).
