# Fase 4: publicación del portal y la API de DIAN

Estado: **publicado en pruebas** (VM `quanty-lab`), 2026-11-09. Commits en `dev` del repositorio `Unificado` (sin subir al remoto).
Detalle de instalación y scripts: `Unificado/linux/despliegue/LEEME.md`. Infraestructura general: `QuantyERP/docs/infraestructura.md` (sección 3.2).

## 1. Qué hay publicado

| Qué | URL |
|---|---|
| API de facturación (la usan los ERP, con su clave de API) | `https://services.sistemas360.com:47125/api/...` y `/salud` |
| Portal de clientes (usuario y contraseña de cada cliente) | `https://services.sistemas360.com:47125/Login` |
| Panel del operador (administración de toda la plataforma) | `https://<IP de la VM>:8443` solo por VPN o túnel SSH. **No** sale a internet. |

## 2. Cómo llega el tráfico

```
Internet → 190.242.61.214:47125 (router; reenvía el rango 471xx)
        → IIS del host, sitio DIAN.Linux (HTTPS con el certificado comodín *.sistemas360.com)
        → 172.31.50.10:8090 (nginx en la VM; el firewall solo deja entrar a 172.31.50.1)
        → /api/ y /salud → Pos.Api 127.0.0.1:5290
        → todo lo demás  → Admin.Web 127.0.0.1:5280  (nginx le pone X-Dian-Entrada: publica)
```

Por qué un puerto y no `dian.sistemas360.com` sin puerto: la red tiene una sola IP pública, el puerto 80 lo atiende otro IIS y el 443 está cerrado en el router (no hay acceso al router por ahora). Se usa el mismo patrón que QuantyERP (47122/47123).

## 3. Qué se cambió

| Dónde | Cambio |
|---|---|
| Repositorio | `dian-nginx.conf`: bloque `listen 8090` (API y portal, con `X-Dian-Entrada: publica` y `X-Forwarded-Proto https`). `dian-firewall.sh`: opción `--iis` (nunca `any`). `iis/crear-sitio-iis.ps1`. `pruebas/probar-entrada-publica.sh`. `LEEME.md`. |
| VM `quanty-lab` | Instalado con `instalar.sh` (binarios nuevos, incluida la plantilla de factura con el título corregido); nginx con el 8090; `ufw` con `8090` solo desde `172.31.50.1`. No se tocaron datos, certificado ni `dian.env`. |
| Host Windows | Sitio IIS `DIAN.Linux` (`C:\inetpub\dian-linux`, puerto 47125) con el certificado y el grupo de aplicaciones de `QuantyERP.Linux.API`, regla de URL Rewrite hacia `http://172.31.50.10:8090/` y regla de Windows Firewall `DIAN IIS 47125`. Los demás sitios no se tocaron. |
| QuantyERP | `docs/infraestructura.md` actualizado (sitio, puerto 8090, sección 3.2, pendientes). Sin commit. |

Comando de firewall vigente en la VM: `sudo dian-firewall aplicar --api 172.31.50.1 --admin 172.31.50.1 --iis 172.31.50.1`.
Importante: `aplicar` reemplaza las reglas "DIAN"; hay que pasar siempre las tres opciones.

## 4. Qué se probó

Pruebas con el nombre público y certificado válido (desde internet y por VPN):

| Petición | Resultado |
|---|---|
| `/Login` | 200, portal |
| `/salud` | 200, API |
| `/api/documento` sin clave | 401 |

Prueba en vivo del operador (`pruebas/probar-entrada-publica.sh`, con un administrador temporal de clave aleatoria, borrado al terminar):

| # | Caso | Resultado esperado | Obtenido |
|---|---|---|---|
| 1 | Operador con clave correcta por la entrada **pública** (8090) | Rechazado como una contraseña mala | 200 con "Usuario o contraseña incorrectos" |
| 2 | Operador con clave correcta por la entrada **interna** (8443) | Entra | 302 a `/` |
| 3 | Cookie de sesión de operador usada en la entrada pública | 403 | 403 |
| 4 | Esa misma cookie en la interna | Sirve | 200 |
| 5 | Cliente falsifica la cabecera `X-Dian-Entrada: interna` en la pública | Rechazado | 200 (no entra; nginx la sobrescribe) |

Tras la prueba, la VM quedó **sin administradores** (se borró el archivo temporal). Pruebas automáticas del Admin: 32 correctas, 11 omitidas.

## 4b. Error encontrado al probar el panel por túnel (corregido)

Al entrar a `https://localhost:8443` por el túnel SSH, el navegador caía en `localhost/Login?ReturnUrl=%2F` (sin puerto, `ERR_CONNECTION_REFUSED`). Causa: nginx pasaba `Host $host`, que no incluye el puerto, y la app arma sus redirecciones absolutas con el host que recibe. Lo mismo pasaba en la entrada pública, donde el IIS reenvía con el nombre interno de la VM (`172.31.50.10`). Corrección en `dian-nginx.conf`: el 8443 usa `Host $http_host`; el 8090 fija `X-Forwarded-Host services.sistemas360.com:47125`. **Si la dirección pública cambia** (por ejemplo a `dian.sistemas360.com` sin puerto), hay que cambiar esa línea. Verificado: `https://services.sistemas360.com:47125/` redirige a `.../Login` con el host y puerto correctos.

### Cómo entrar al panel del operador desde su PC

1. Crear el usuario: `ssh -t quanty-lab 'read -rsp "Contraseña: " P; echo; printf "%s" "$P" | sudo dian-admin crear-admin <usuario>'`
2. Abrir el túnel y dejarlo abierto (no muestra nada, es normal): `ssh -N -L 8443:127.0.0.1:8443 quanty-lab`
3. En el navegador del mismo PC: `https://localhost:8443` (aceptar el aviso del certificado autofirmado). No sirve `172.31.50.10` desde el PC: esa red solo la ve el host.

## 5. Cómo operar

- **Crear el primer administrador** (la clave por entrada estándar, nunca por argumento):
  `printf '%s' '<clave de 12+ caracteres>' | sudo dian-admin crear-admin <usuario>`
- **Ver los servicios:** `systemctl status dian-pos-api dian-admin-web nginx` y `journalctl -u dian-pos-api -f`.
- **Reinstalar o actualizar:** publicar con `dotnet publish ... -r linux-x64 --self-contained`, empaquetar y correr `instalar.sh` (ver `LEEME.md`). Siempre con el instalador.
- **Repetir el sitio IIS:** `powershell -ExecutionPolicy Bypass -File iis\crear-sitio-iis.ps1` en el host.
- **Certificado:** el comodín `*.sistemas360.com` lo renueva usted a mano en el IIS; el 47125 usa el mismo.

## 6. Pendiente

1. **URL sin puerto** (`https://dian.sistemas360.com`): reenviar el 443 del router al IIS, agregar un enlace con ese nombre al sitio `DIAN.Linux` y cambiar el DNS de `dian` (hoy `5.9.84.181`, del hosting) a `190.242.61.214`.
2. **Servidor definitivo**: `quanty-lab` es de pruebas.
3. **Verificación en dos pasos** y **correo de recuperación** del portal (el correo depende del contrato del EmailAPI, aún sin conocer).
4. En la VM: cargar el logo de MARLIT y regenerar el PDF de la factura (el `SETP990000518.pdf` es anterior al arreglo del título).
5. Respaldo fuera del servidor y llave del respaldo guardada aparte.
6. Los commits de `dev` están sin subir (los sube el usuario).
