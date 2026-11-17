# Auditoría y respaldos

Última revisión: 2026-11-16 · Rol: **operador** (la auditoría y los respaldos no los ve el cliente)

## Auditoría: quién hizo qué

Menú superior → **Auditoría** (solo para operadores). Muestra los **últimos 200 cambios** con las columnas **Fecha (UTC)**, **Usuario**, **Acción**, **Cliente** y **Detalle**. Nunca incluye claves, PIN ni contraseñas.

> Las horas están en **UTC**: en Colombia son 5 horas menos (UTC-5).

### Acciones que verá

| Acción | Qué es |
|---|---|
| `ingreso` / `ingreso-fallido` | Entradas al panel y contraseñas incorrectas (con «usuario bloqueado» si llegó al límite) |
| `ingreso-operador-bloqueado` | Un operador intentó entrar por la dirección pública (rechazado a propósito) |
| `admin-crear` | Se creó un operador desde el servidor |
| `crear` | Cliente nuevo (con su NIT) |
| `emisor` | Se guardó el emisor |
| `software` | Se guardó un software (dice el tipo, si es pruebas y si cambió el PIN) |
| `certificado` | Se subió un certificado (sujeto y vencimiento) |
| `logo`, `sucursal`, `sucursal-logo`, `sucursal-eliminar` | Cambios de logos y sucursales |
| `clave-crear`, `clave-revocar` | Claves de API (solo sus últimos 4 caracteres) |
| `webhook`, `webhook-quitar` | Webhook (solo el dominio, «secreto nuevo» si hubo) |
| `activar`, `desactivar` | Estado del cliente |
| `produccion-autorizar`, `produccion-quitar` | Paso a producción y vuelta atrás |
| `usuario-portal-crear`, `usuario-portal-restablecer`, `usuario-portal-eliminar` | Usuarios del portal |
| `clave-cambiar`, `clave-cambiar-fallido` | Cambio de la contraseña propia |

### Para qué sirve

- Saber quién cambió el software o el certificado de un cliente.
- Revisar intentos fallidos de ingreso o un bloqueo.
- Comprobar cuándo se autorizó producción.

El archivo está en el servidor en `/var/lib/dian/almacen/auditoria.jsonl` (una línea por evento) y crece con el tiempo; la pantalla solo muestra los 200 últimos.

## Respaldos

El servidor hace **un respaldo cifrado cada día a las 2:30**. Incluye los datos de los clientes, las llaves de cifrado, los XML generados y, si se usa MySQL, las tablas `dian_*`.

Puntos clave:

- **Siempre cifrados**: el respaldo solo se puede leer con una **llave privada** que **no vive en el servidor**.
- Se conservan los **últimos 14**.
- Quedan en `/var/backups/dian` del servidor, con su suma de verificación.
- **Un respaldo en el mismo equipo no protege de perder el equipo.** La copia de `/var/backups/dian` hacia **otro lugar** hoy **no está automatizada**: debe hacerse a mano o programarse.
- **La llave privada** debe estar guardada fuera del servidor (caja fuerte o gestor de contraseñas). Sin ella **no se puede restaurar nada**.
- Respalde y restaure **siempre juntos** los datos y las llaves de cifrado.

### Comandos (en el servidor, por SSH)

```bash
ssh quanty-lab
sudo dian-respaldo listar          # ver los respaldos guardados
sudo dian-respaldo ejecutar        # hacer un respaldo ahora
```

Configurar por primera vez y restaurar los hacen el equipo de desarrollo o un administrador con la llave privada ([../despliegue/README.md](../despliegue/README.md)).

## Qué hacer ante un fallo

| Situación | Qué hacer |
|---|---|
| El panel no abre | Revise que el túnel esté abierto ([acceso-al-panel.md](acceso-al-panel.md)). Si sí, avise al equipo de desarrollo |
| Un cliente dice que su ERP recibe 401 | Mire en el panel: ¿cliente **Desactivado**? ¿la clave **Revocada**? ([claves-api.md](claves-api.md)) |
| Un cliente dice que no recibe documentos aprobados | Revise **Documentos recientes**: estado y mensaje. Revise el certificado ([certificado.md](certificado.md)) |
| Documentos en **Error** que no se resuelven | Espere los reintentos automáticos; si pasan horas, avise al equipo de desarrollo |
| Se filtró una clave | Revóquela de inmediato y genere otra ([claves-api.md](claves-api.md)) |
| Se perdió o dañó un cliente | Avise al equipo de desarrollo: se restaura desde un respaldo |
| Sospecha de acceso indebido | Revise la auditoría, cambie su contraseña en **Mi cuenta** y avise |

Antes de tocar nada en un incidente, **no borre datos**: el almacén guarda certificados y secretos cifrados que solo se recuperan desde un respaldo.

## No verificado

- La restauración completa de un respaldo en una máquina nueva, vista por el personal de soporte (la hace el equipo de desarrollo).
- Que el respaldo diario esté **configurado** en el servidor actual: en la VM de pruebas se configura una sola vez con `dian-respaldo configurar`; confirme con el equipo de desarrollo si ya se hizo.
