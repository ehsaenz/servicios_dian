# Acceso al panel del operador

Última revisión: 2026-11-16

El panel del operador administra a todos los clientes. **No está en internet**: solo se llega por la red interna, la VPN o un túnel SSH. Esta guía explica cómo crear su usuario y cómo entrar **desde su PC**, sin conectarse al servidor Windows (host).

## Antes de empezar

- Su PC debe tener el alias SSH `quanty-lab` configurado (usuario `sis360`, con salto por el servidor `servidor-sis360`). Ya está en `~/.ssh/config` del PC del equipo de soporte; si no lo tiene, pídalo al equipo de desarrollo.
- Estar conectado a la VPN (Radmin) si no está en la red local.
- Una terminal (Git Bash o PowerShell).

## 1. Crear su usuario de operador (una sola vez)

Reglas del usuario y la contraseña (las valida el sistema):

- Usuario: sin espacios, hasta 40 caracteres.
- Contraseña: **mínimo 12 caracteres**.

Ejecute este comando cambiando `miusuario` por el suyo. Le pedirá la contraseña **sin mostrarla**:

```bash
ssh -t quanty-lab 'read -rsp "Contraseña: " P; echo; printf "%s" "$P" | sudo dian-admin crear-admin miusuario'
```

Debe terminar con: `Administrador miusuario creado.`

Notas:

- La contraseña viaja por la entrada estándar, nunca como argumento del comando, para que no quede en el historial.
- Si el usuario ya existe, el comando lo reemplaza (útil para restablecer la contraseña de un operador).
- El primer operador de un servidor nuevo se crea con este mismo comando.

## 2. Abrir el túnel

El panel escucha dentro de la VM. El túnel conecta el puerto 8443 de **su PC** con el de la VM. En una terminal, ejecute y **déjela abierta**:

```bash
ssh -N -L 8443:127.0.0.1:8443 quanty-lab
```

**No mostrará nada. Es lo normal**: la opción `-N` significa «solo abre el túnel». Si cierra esa terminal, el túnel se cae y el panel deja de abrir.

Si el puerto 8443 de su PC ya lo usa otro programa, el comando avisa de un error («address already in use»). Use otro puerto local, por ejemplo:

```bash
ssh -N -L 18443:127.0.0.1:8443 quanty-lab
```

y entre luego por `https://localhost:18443`.

## 3. Entrar

1. En el navegador **de su PC**, abra `https://localhost:8443`.
2. Verá un aviso de «Conexión no segura» o «No es seguro»: el servidor usa un certificado propio. Acepte continuar (en Chrome: «Configuración avanzada» → «Continuar a localhost»).
3. Aparece la pantalla **Ingreso** con los campos **Usuario** y **Contraseña** y el botón **Entrar**.
4. Escriba su usuario y contraseña. Entra a la pantalla **Clientes**.

La parte superior muestra **DIAN · Administración de clientes**, el enlace **Auditoría**, **Mi cuenta · su usuario** y el botón **Salir**.

### Qué debe usar y qué no

| Use | No use |
|---|---|
| `https://localhost:8443` con el túnel abierto | `https://172.31.50.10:8443` desde su PC |

La dirección `172.31.50.10` es la red interna de la máquina virtual (Hyper-V); **solo la ve el servidor Windows (host)**. Desde su PC da «tiempo de espera agotado» (`ERR_CONNECTION_TIMED_OUT`). Esa dirección solo sirve si abre el navegador *dentro del host*.

## Cómo saber que está en la VM y no en su propio PC

- El puerto 8443 de su PC lo está escuchando el programa `ssh` (el túnel), no un servidor web propio.
- Verá los clientes reales de la VM. Una instancia de pruebas local solo mostraría clientes ficticios.
- Prueba definitiva: cierre la terminal del túnel y recargue la página. Deja de funcionar.

Mientras el túnel está abierto, **lo que haga en esa pantalla lo está haciendo en la VM real**.

## Sesión y bloqueo

- La sesión dura 30 minutos y se renueva mientras usted trabaja. Pasado ese tiempo sin actividad, vuelve a pedir usuario y contraseña.
- **Cinco contraseñas incorrectas** bloquean al usuario **15 minutos** («Demasiados intentos. El usuario quedó bloqueado por 15 minutos.»). Quedan registradas en la auditoría.
- Cambie su contraseña en **Mi cuenta** → **Cambiar contraseña**: pide la actual, la nueva (mínimo 12 caracteres) y su repetición.

## Entrar desde el servidor Windows (host)

También es posible, pero no es necesario: conéctese al host (Radmin o escritorio remoto), abra el navegador de ese equipo y vaya a `https://172.31.50.10:8443`. Ahí sí se ve esa red.

## Si algo falla

| Qué ve | Qué significa | Qué hacer |
|---|---|---|
| `localhost` «ha rechazado la conexión» (`ERR_CONNECTION_REFUSED`) | El túnel no está abierto, o está en otro puerto | Abra el túnel (paso 2) y use el mismo puerto en la dirección |
| La dirección salta a `localhost/Login` **sin puerto** | Era un error de la configuración del servidor, ya corregido | Avise al equipo de desarrollo si reaparece |
| «Tiempo de espera agotado» con `172.31.50.10` | Está usando la dirección interna desde su PC | Use `https://localhost:8443` con el túnel |
| «Usuario o contraseña incorrectos» con la clave correcta, entrando por la dirección pública | Los operadores no pueden entrar por la dirección pública | Entre por el túnel |
| El comando `ssh` pide una contraseña | Su llave SSH no está cargada | Avise al equipo de desarrollo |

## No verificado

- El aviso exacto del navegador al aceptar el certificado varía según el navegador.
- No se probó el procedimiento desde un PC distinto del equipo de desarrollo.
