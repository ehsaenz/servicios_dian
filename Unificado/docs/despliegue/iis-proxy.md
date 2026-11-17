# Proxy en el IIS del host

Última revisión: 2026-11-16 · Volver al [índice](README.md)

El IIS del host Windows (`SERVIDOR-SISTEMAS360`) recibe el tráfico de internet y lo reenvía a la VM, igual que ya hacía con QuantyERP (`QuantyERP.Linux.WEB` en el 47122 y `QuantyERP.Linux.API` en el 47123). ARR (Application Request Routing) y URL Rewrite ya están habilitados a nivel de servidor.

## El sitio `DIAN.Linux`

| Dato | Valor |
|---|---|
| Nombre | `DIAN.Linux` |
| Carpeta | `C:\inetpub\dian-linux` (solo contiene `web.config`) |
| Enlace | HTTPS, puerto **47125** |
| Certificado | El del sitio `QuantyERP.Linux.API` (comodín `*.sistemas360.com`, almacén `MY`) |
| Grupo de aplicaciones | El de `QuantyERP.Linux.API` |
| Regla | `ProxyDianVm`: todo (`(.*)`) se reescribe a `http://172.31.50.10:8090/{R:1}` |
| Límite de cuerpo | 5 242 880 bytes (5 MB) |
| Windows Firewall | Regla de entrada `DIAN IIS 47125` (TCP 47125) |

No se tocó ningún otro sitio del IIS.

## Crear o repetir el sitio

Script: [`linux/despliegue/iis/crear-sitio-iis.ps1`](../../linux/despliegue/iis/crear-sitio-iis.ps1). **Se ejecuta en el host Windows**, como administrador, y es repetible: si el sitio ya existe lo dice y no lo cambia; la regla del firewall solo se crea si falta.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File iis\crear-sitio-iis.ps1
```

Qué hace, en orden: toma el grupo de aplicaciones y el certificado de `QuantyERP.Linux.API` (falla si no encuentra el certificado), crea la carpeta con el `web.config`, crea el sitio con enlace HTTPS en el 47125 y le asocia el certificado, y crea la regla de Windows Firewall.

Desde el PC de desarrollo se ejecutó por `ssh servidor-sis360`. En esa conexión el perfil de PowerShell del usuario falla por la política de ejecución (un error inofensivo); conviene usar `-ExecutionPolicy Bypass` y, para scripts largos, `-EncodedCommand` en vez de pasar el texto entre comillas.

## El router

- Reenvía el rango de puertos `471xx` de la IP pública `190.242.61.214` al host. El puerto 47125 llega al host (comprobado el 2026-11-09 con `https://services.sistemas360.com:47125/salud` → 200).
- Antes de crear el sitio, el 47125 aparecía «cerrado» en un escaneo, no porque el router lo bloqueara, sino porque ningún programa lo escuchaba en el host.
- **No hay acceso al router por ahora.** Cualquier cambio (por ejemplo reenviar el 443) está pendiente de eso ([produccion.md](produccion.md)).

## Probar el proxy

Desde cualquier equipo:

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://services.sistemas360.com:47125/salud            # 200
curl -s -o /dev/null -w '%{http_code}\n' https://services.sistemas360.com:47125/Login            # 200
curl -s -o /dev/null -w '%{http_code}\n' https://services.sistemas360.com:47125/api/documento    # 401 sin clave
curl -si https://services.sistemas360.com:47125/ | grep -i '^location'   # .../Login con el host y el puerto públicos
```

Por la VPN, con la IP Radmin: `curl -sk https://26.190.225.205:47125/Login` (el certificado no coincide con la IP, por eso `-k`).

## Si algo falla

| Síntoma | Causa probable |
|---|---|
| 502/503 del IIS | La VM o nginx no responden en `172.31.50.10:8090`, o `ufw` no tiene la regla `--iis`. Probar desde el host: `curl http://172.31.50.10:8090/salud`. |
| Aviso de certificado en el navegador | El comodín vencido o no asociado al enlace del sitio. |
| Redirecciones a una dirección interna | La línea `X-Forwarded-Host` de nginx no coincide con la URL pública ([solucion-de-problemas.md](solucion-de-problemas.md)). |
| No llega desde internet pero sí por la VPN | Router o Windows Firewall (`DIAN IIS 47125`). |

## No verificado

- Si ARR envía `X-Forwarded-For` con la IP real del cliente.
- El procedimiento para quitar el sitio (no hay script de eliminación).
