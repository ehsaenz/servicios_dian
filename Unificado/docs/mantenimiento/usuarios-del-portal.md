# Usuarios del portal (accesos de los clientes)

Última revisión: 2026-11-16 · Rol: **operador** crea; el **cliente** usa

El portal es la misma aplicación que usa el operador, pero un **usuario de cliente** solo ve y administra **su empresa**.

## Qué puede y qué no puede hacer un usuario de cliente

| Puede | No puede |
|---|---|
| Cambiar el **logo** del cliente | Cambiar el emisor (NIT, razón social, correo...) |
| Crear/eliminar **sucursales** y subir su logo | Cambiar el software de la DIAN |
| Generar y revocar **claves de API** | Subir o cambiar el certificado |
| Configurar el **webhook** | Activar/desactivar el cliente |
| Ver **Documentos recientes** y descargar los PDF aprobados | Autorizar o retirar producción |
| Cambiar su propia contraseña (**Mi cuenta**) | Ver otros clientes, la auditoría o crear usuarios |

Esto se aplica en el servidor: aunque alguien arme una petición a mano, las acciones del operador se rechazan. Si intenta ver otro cliente, recibe «no encontrado».

Al entrar, el usuario cae **directo en su empresa**: no hay lista de clientes. En **Estado** ve solo una etiqueta (Habilitación / Producción / Desactivado) y el vencimiento de su certificado, con la nota «Los datos del emisor, el software, el certificado y el paso a producción los administra SISTEMAS 360.»

## Crear un usuario para el cliente (operador)

1. Página del cliente → sección **Usuarios del portal**.
2. En **Usuario nuevo (sin espacios)** escriba el nombre de usuario (hasta 40 caracteres, sin espacios; no puede repetir el de otro cliente ni el de un operador).
3. Pulse **Crear usuario**.
4. Arriba aparece: **«Contraseña de <usuario> — entréguela por un canal seguro. Es la última vez que se muestra.»** Cópiela ahora. Es aleatoria y de 16 caracteres.

Entregue al cliente, por un canal seguro:

- La dirección del portal: hoy **`https://services.sistemas360.com:47125/Login`** (en el futuro, `https://dian.sistemas360.com`).
- Su usuario y la contraseña generada.
- La indicación de cambiar la contraseña en **Mi cuenta** al entrar.

## Restablecer la contraseña (olvidó la suya)

En la tabla de usuarios, botón **Restablecer contraseña** del usuario: «Contraseña restablecida. Entréguela por un canal seguro: no se vuelve a mostrar.» La anterior deja de servir. No hay recuperación automática por correo (aún no existe).

## Eliminar un usuario

Botón **Eliminar** («Usuario eliminado.»). Úselo cuando una persona deja la empresa cliente. Una sesión ya abierta puede seguir hasta 30 minutos sin actividad.

## Lo que hace el cliente al entrar

1. Abre la dirección del portal. Verá la pantalla **Ingreso** (**Usuario**, **Contraseña**, **Entrar**).
2. Entra. Si se equivoca cinco veces, queda bloqueado 15 minutos.
3. **Mi cuenta** → **Cambiar contraseña**: contraseña actual, nueva (mínimo 12 caracteres) y repetirla.
4. Desde su página puede hacer lo de la tabla de arriba: [logo y sucursales](logo.md), [claves de API](claves-api.md), [webhook](webhook.md), y revisar sus documentos.

## Reglas de seguridad

- Contraseñas guardadas con PBKDF2-SHA256 y sal propia (no se pueden leer).
- **Cinco intentos fallidos = bloqueo de 15 minutos**; queda en la auditoría.
- La sesión caduca a los 30 minutos sin actividad.
- Los **operadores no pueden entrar por la dirección pública**. Si un operador intenta entrar ahí, recibe «Usuario o contraseña incorrectos».
- Los eventos (`usuario-portal-crear`, `usuario-portal-restablecer`, `usuario-portal-eliminar`, `clave-cambiar`) quedan en la [auditoría](auditoria-y-respaldos.md), nunca con contraseñas.

## No verificado

- **No hay verificación en dos pasos ni correo de bienvenida o recuperación** (están pendientes). Mientras tanto, entregue contraseñas por un canal seguro y use la restauración manual.
- No hay límite de usuarios por cliente en el panel.
