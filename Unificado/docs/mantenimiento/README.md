# Manual de mantenimiento de la plataforma DIAN

Última revisión: 2026-11-16

Alcance: código y documentación local de Unificado. Las URLs y estados del piloto son referencias históricas; confirme el ambiente actual con el operador. No se inspeccionaron servidores en esta revisión.

Para el personal de soporte y operación de SISTEMAS 360. No necesita saber programar: cada guía dice qué hacer, en qué pantalla y qué debe ver.

## Qué es la plataforma, en una línea

Un servicio que recibe los documentos de los ERP de los clientes (facturas, notas, documento soporte, POS...), los firma con el certificado del cliente, los envía a la DIAN, y devuelve el resultado, el PDF y un aviso al ERP. Este manual trata de **administrar a los clientes** de ese servicio desde el panel web.

## Los dos tipos de usuario

| | **Operador** (SISTEMAS 360) | **Cliente** (usuario del portal) |
|---|---|---|
| Quién es | Personal de SISTEMAS 360 | Una persona de la empresa cliente |
| Ve | Todos los clientes | Solo su propia empresa (si intenta ver otra, recibe "no encontrado") |
| Entra por | `https://localhost:8443` con un túnel SSH (red interna o VPN) | `https://services.sistemas360.com:47125` (dirección pública; en el futuro `https://dian.sistemas360.com`) |
| Puede hacer | Todo: crear clientes, emisor, software DIAN, certificado, estado, producción, usuarios del portal, auditoría | Solo: logo, sucursales (y su logo), claves de API, webhook, ver sus documentos y descargar PDF aprobados, cambiar su contraseña |

**El operador no puede entrar por la dirección pública**: si lo intenta, la pantalla responde "Usuario o contraseña incorrectos" aunque la clave sea correcta. Es a propósito.

## Guías

Siga este orden la primera vez que dé de alta a un cliente:

| # | Guía | Para qué sirve |
|---|---|---|
| 1 | [Acceso al panel](acceso-al-panel.md) | Crear su usuario de operador, abrir el túnel y entrar |
| 2 | [Crear un cliente](crear-cliente.md) | Dar de alta la empresa (id, NIT, DV, razón social) |
| 3 | [Configurar un cliente](configurar-cliente.md) | Emisor, software DIAN por tipo de documento, estado |
| 4 | [Subir la configuración de un cliente existente](subir-configuracion.md) | Alta rápida desde el archivo de la API anterior |
| 5 | [Certificado de firma](certificado.md) | Subirlo por primera vez y en cada renovación |
| 6 | [Logo y sucursales](logo.md) | Logo del cliente y de cada sucursal |
| 7 | [Claves de API](claves-api.md) | Generar, rotar y revocar las claves del ERP |
| 8 | [Webhook](webhook.md) | Avisar al ERP del resultado de cada documento |
| 9 | [Usuarios del portal](usuarios-del-portal.md) | Que el cliente entre a administrar lo suyo |
| 10 | [Habilitación y set de pruebas](habilitacion-y-set-de-pruebas.md) | Probar contra la DIAN de pruebas |
| 11 | [Paso a producción](paso-a-produccion.md) | Lista de verificación, autorización y cómo volver atrás |
| 12 | [Auditoría y respaldos](auditoria-y-respaldos.md) | Qué quedó registrado, copias de seguridad, qué hacer si algo falla |
| 13 | [Preguntas frecuentes](preguntas-frecuentes.md) | Mensajes de error y problemas comunes |

## Resumen del camino de un cliente nuevo

1. Crear el cliente → [crear-cliente.md](crear-cliente.md).
2. Completar el emisor y registrar el software de la DIAN en modo pruebas → [configurar-cliente.md](configurar-cliente.md).
3. Subir el certificado → [certificado.md](certificado.md).
4. Generar la clave de API y dársela al ERP → [claves-api.md](claves-api.md).
5. Validar envíos en habilitación y comprobar el SET según el módulo (factura no usa TestSetId) → [habilitacion-y-set-de-pruebas.md](habilitacion-y-set-de-pruebas.md).
6. Subir el logo, crear sucursales y webhook (opcional) → [logo.md](logo.md), [webhook.md](webhook.md).
7. Crear el usuario del portal → [usuarios-del-portal.md](usuarios-del-portal.md).
8. Con la confirmación expresa del cliente, pasar a producción → [paso-a-produccion.md](paso-a-produccion.md).

## Reglas de oro

- **Nada pasa a producción sin la autorización expresa del cliente.** Mientras no se autorice, todo envío va a la DIAN de pruebas (habilitación).
- **Los secretos se muestran una sola vez** (clave de API, secreto del webhook, contraseña de un usuario del portal). Cópielos al momento y entréguelos por un canal seguro. Si se pierden, se generan de nuevo.
- **No pegue contraseñas, PIN ni claves de API en chats, correos ni documentos.** La auditoría nunca los guarda; no los guarde usted tampoco.
- **Use datos ficticios en las pruebas.** No envíe a la DIAN documentos de personas reales para probar.

## Otros manuales

- Despliegue e instalación del servidor: [../despliegue/README.md](../despliegue/README.md)
- Para el equipo de desarrollo: [../desarrollo/README.md](../desarrollo/README.md)
- Integración de un ERP con la API: [../integracion/README.md](../integracion/README.md)

## Qué no se ha podido verificar

Cada guía termina con una sección «No verificado» cuando algo se describe por lo que dice el código y no por haberlo visto funcionar. Tómela como una invitación a comprobarlo la primera vez.


## Guía complementaria

- [Operación por módulo, configuración de ambientes y límites del SET](operacion-por-modulo.md).
