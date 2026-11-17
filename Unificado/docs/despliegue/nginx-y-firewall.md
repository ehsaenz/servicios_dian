# nginx y firewall

Última revisión: 2026-11-16 · Volver al [índice](README.md)

Archivo fuente: [`linux/despliegue/dian-nginx.conf`](../../linux/despliegue/dian-nginx.conf). Se instala como `/etc/nginx/conf.d/dian.conf` y **no toca ningún otro sitio** de nginx (el de QuantyERP, `sites-available/quantyerp`, sigue aparte).

## Zonas de límite de solicitudes

| Zona | Límite por dirección | Se usa en |
|---|---|---|
| `dian_api` | 20 r/s (ráfaga 40) | rutas de la API |
| `dian_portal` | 10 r/s (ráfaga 20) | portal público |
| `dian_admin` | 5 r/s (ráfaga 20) | panel del operador |

Detrás del IIS todas las peticiones llegarían de `172.31.50.1`; por eso el bloque 8090 usa `set_real_ip_from 172.31.50.1` y `real_ip_header X-Forwarded-For`. **No verificado:** que el IIS (ARR) envíe `X-Forwarded-For` con la IP real del cliente; si no lo hace, el límite se comparte entre todos los clientes.

## Bloque por bloque

| # | `server` | Qué hace |
|---|---|---|
| 1 | `listen 80`, `dian.sistemas360.com` | Sirve `/.well-known/acme-challenge/` desde `/var/www/dian-acme` (Let's Encrypt) y redirige lo demás a HTTPS. |
| 2 | `listen 443 ssl`, `dian.sistemas360.com` | Dominio público: `/api/` y `/salud` → API `127.0.0.1:5290` (timeout 180 s); todo lo demás → portal `127.0.0.1:5280` con `X-Dian-Entrada: publica`. HSTS y `nosniff`. Cuerpo máximo 5 MB (3 MB en el portal). |
| 3 | `listen 443 ssl default_server` (cualquier otro nombre o la IP) | Solo la API (`127.0.0.1:5290`), con timeout de 180 s: el envío a la DIAN puede tardar. |
| 4 | `listen 8090` (HTTP) | Entrada desde el IIS. API y portal, con `X-Forwarded-Proto https` y `X-Dian-Entrada: publica`; en el portal fija `X-Forwarded-Host services.sistemas360.com:47125`. Usa la IP real que manda el IIS (solo de `172.31.50.1`). |
| 5 | `listen 8443 ssl` | Panel del operador. Solo `127.0.0.1`, `::1`, `10/8`, `172.16/12`, `192.168/16` y `26/8` (VPN); cualquier otra dirección recibe 403. `Host $http_host` y `X-Dian-Entrada: interna`. |

TLS 1.2 y 1.3 únicamente (TLS 1.0 y 1.1 se rechazan; verificado el 2026-11-07). El certificado de los bloques 2, 3 y 5 es `/etc/dian/tls/dian.crt` y `dian.key` (ver [tls-y-certificados.md](tls-y-certificados.md)). El bloque 4 no usa TLS: el HTTPS lo termina el IIS.

### Cosas que conviene saber

- **`X-Dian-Entrada` la fija nginx siempre** y descarta la del cliente. Prueba en vivo: una cabecera falsificada no deja entrar al operador.
- **Redes del 8443:** ajústelas a su red en el bloque 5 si cambian.
- **Cambiar la dirección pública** (por ejemplo a `dian.sistemas360.com` sin puerto) exige cambiar la línea `X-Forwarded-Host` del bloque 4 ([produccion.md](produccion.md)).
- Tras editar el archivo en el repositorio, vuelva a correr `instalar.sh` (valida con `nginx -t` y restaura si falla). Para probar a mano en la VM: `sudo nginx -t && sudo systemctl reload nginx`.

## Firewall: `dian-firewall`

Script: [`linux/despliegue/dian-firewall.sh`](../../linux/despliegue/dian-firewall.sh) (instalado como `/usr/local/sbin/dian-firewall`). Usa `ufw`. **Solo añade reglas con la etiqueta «DIAN»**: no cambia las políticas ni las reglas de otros servicios (SSH, 8080, 8081 de QuantyERP).

```bash
sudo dian-firewall estado
sudo dian-firewall aplicar --api <origen[,origen...]|any> --admin <origen[,origen...]> [--http <origen|any>] [--iis <origen[,origen...]>] [--habilitar]
```

| Opción | Puerto | Quién | Notas |
|---|---|---|---|
| `--api` | 443 | La API (exige clave de API). `any` abre a internet. | Obligatoria. |
| `--admin` | 8443 | El panel del operador. **Nunca `any`.** | Obligatoria. |
| `--http` | 80 | Desafío de Let's Encrypt y redirección. Normalmente `any`. | Opcional. |
| `--iis` | 8090 | El IIS del host. **Nunca `any`.** | Opcional. |
| `--habilitar` | — | Si `ufw` está apagado, lo enciende; antes permite SSH. | Opcional. |

Un origen es una dirección o red (`172.31.50.1`, `10.0.0.0/8`).

### Advertencia: pase siempre todas las opciones

`aplicar` **borra todas las reglas «DIAN» anteriores y las vuelve a crear** con lo que se le pasa. Si en una segunda ejecución olvida `--iis` (o `--http`), esa regla desaparece y la entrada deja de funcionar. Comando vigente en la VM `quanty-lab`:

```bash
sudo dian-firewall aplicar --api 172.31.50.1 --admin 172.31.50.1 --iis 172.31.50.1
```

Resultado esperado (`dian-firewall estado` / `ufw status`):

```
443/tcp   ALLOW   172.31.50.1   # DIAN API
8443/tcp  ALLOW   172.31.50.1   # DIAN Admin
8090/tcp  ALLOW   172.31.50.1   # DIAN IIS (proxy)
```

Antes de aplicar en un servidor nuevo, revise las reglas con `sudo ufw status numbered`.

## No verificado

- Las reglas `--http` (puerto 80) en una máquina real con acceso público.
- El comportamiento del límite de solicitudes con la IP real del cliente detrás del IIS.
