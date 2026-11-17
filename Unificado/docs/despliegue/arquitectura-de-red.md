# Arquitectura de red

Última revisión: 2026-11-16 · Volver al [índice](README.md)

## Las máquinas

| Pieza | Dato |
|---|---|
| Servidor físico | `SERVIDOR-SISTEMAS360`, Windows 11 Pro. IP LAN `192.168.200.250`; IP de la VPN Radmin `26.190.225.205`; IP del switch Hyper-V `LAB-NAT` `172.31.50.1`. Es host de Hyper-V e IIS (reverse proxy y sitios Windows). SSH con el alias `servidor-sis360`. |
| VM de pruebas | `quanty-lab`, Ubuntu Server 24.04, red interna `LAB-NAT`, IP `172.31.50.10` (con NAT en el host). SSH con el alias `quanty-lab`, que **salta por el host** (`ProxyJump servidor-sis360`). |
| IP pública | **Una sola:** `190.242.61.214`. Varios servicios comparten esa IP mediante puertos distintos que el router reenvía al host. |
| Dominio | `sistemas360.com` está en latinoamericahosting. `services.sistemas360.com` apunta a la IP pública; `dian.sistemas360.com` hoy apunta a `5.9.84.181` (IP del hosting, **no** a esta red). |

La VM aloja dos grupos de servicios: QuantyERP (puertos 8080/8081) y DIAN (este documento). No deben tocarse entre sí.

## Cómo llega el tráfico

```
Internet
  → 190.242.61.214:47125  (router; reenvía el rango 471xx hacia el host)
  → IIS del host, sitio DIAN.Linux  (termina el HTTPS con el certificado comodín *.sistemas360.com)
  → 172.31.50.10:8090  (nginx de la VM; el firewall solo deja entrar a 172.31.50.1)
       ├─ /api/...  y  /salud  → Pos.Api    127.0.0.1:5290   (la usan los ERP, con su clave de API)
       └─ todo lo demás         → Admin.Web  127.0.0.1:5280   (portal de clientes; nginx añade X-Dian-Entrada: publica)
```

Lo que se ve desde afuera:

| Qué | URL |
|---|---|
| API de facturación | `https://services.sistemas360.com:47125/api/...` y `/salud` |
| Portal de clientes | `https://services.sistemas360.com:47125/Login` |

### Por qué un puerto y no `dian.sistemas360.com` sin puerto

La red solo tiene una IP pública, el puerto 80 lo atiende otro IIS y el 443 está cerrado en el router, y no hay acceso al router por ahora. Se usa el mismo patrón que QuantyERP (puertos 47122 y 47123): el IIS del host hace de proxy hacia la VM. Los puertos `47101`–`47104`, `47110`–`47117`, `47122` y `47123` ya tienen sitios en el IIS; el `47125` quedó libre y el router lo reenvía (el rango completo). Pasar a la URL sin puerto está en [produccion.md](produccion.md).

## Entrada del operador

El panel del operador (crear clientes, certificados, claves, autorizar producción) **no sale a internet**.

```
PC del operador ── ssh -N -L 8443:127.0.0.1:8443 quanty-lab ──▶ nginx 8443 (VM) ──▶ Admin.Web 127.0.0.1:5280
navegador del mismo PC:  https://localhost:8443
```

- El alias `quanty-lab` ya pasa por el host: no hace falta entrar al host.
- `ssh -N` no muestra nada mientras el túnel está abierto: es normal. Si se cierra esa terminal, el túnel se cae.
- Desde el PC **no** sirve abrir `https://172.31.50.10:8443`: esa red solo la ve el host. Sí sirve desde el navegador del propio host.
- Alternativa: la VPN Radmin (`26.0.0.0/8` está permitido en nginx) o una red privada (`10/8`, `172.16/12`, `192.168/16`).
- El certificado del 8443 es el de la VM (autofirmado en las pruebas): el navegador mostrará un aviso que hay que aceptar.

## Las tres cabeceras que importan

| Cabecera | Quién la pone | Para qué |
|---|---|---|
| `X-Dian-Entrada` (`publica` o `interna`) | nginx, **siempre**; descarta la que mande el cliente | La app rechaza el inicio de sesión de un operador si la entrada es `publica` (opción `Portal__OperadorSoloInterno`, activa por defecto). Si llega una cookie de operador por la entrada pública, responde 403. |
| `X-Forwarded-Proto` | nginx | En el 8090 se fija en `https` (el IIS ya terminó el TLS), así la app emite cookies `Secure`. |
| `X-Forwarded-Host` | nginx, solo en el 8090 | Fija `services.sistemas360.com:47125`. El IIS reenvía con el nombre interno (`172.31.50.10`) y la app arma sus redirecciones con el host que ve; sin esto mandaba a los usuarios a una dirección interna. **Si la dirección pública cambia, hay que cambiar esta línea** (ver [produccion.md](produccion.md)). |

Las apps solo escuchan en `127.0.0.1` y solo aceptan las cabeceras `X-Forwarded-*` del proxy local; por eso nadie de afuera puede falsificarlas. En el 8443 el `Host` se pasa con `$http_host` para conservar el puerto (ver [solucion-de-problemas.md](solucion-de-problemas.md)).

## Puertos de la VM

| Puerto | Qué | Quién llega (ufw) |
|---|---|---|
| 22 | sshd | cualquiera (dentro de la red) |
| 443 | nginx: API (y el vhost `dian.sistemas360.com`) | `172.31.50.1` |
| 8090 | nginx: entrada desde el IIS (API y portal) | `172.31.50.1` |
| 8443 | nginx: panel del operador | `172.31.50.1` |
| 80 | nginx: desafío de Let's Encrypt y redirección | no abierto hoy (solo con `--http`) |
| 8080 / 8081 | nginx de QuantyERP | `172.31.50.1` |
| 5290 | Kestrel `Pos.Api` | solo local |
| 5280 | Kestrel `Admin.Web` | solo local |
| 3306 | MySQL 8.4 (de QuantyERP; DIAN usa archivos salvo que se configure `MySql__Cadena`) | solo local |

Reglas vigentes de `ufw` en la VM (comprobadas el 2026-11-09): `OpenSSH` desde cualquiera; `8080` y `8081` desde `172.31.50.1`; `443` (`DIAN API`), `8443` (`DIAN Admin`) y `8090` (`DIAN IIS (proxy)`) desde `172.31.50.1`.

## Salida a internet

La API necesita salir al servicio web de la DIAN. En habilitación: `vpfe-hab.dian.gov.co` por el 443 (ver [`linux/LEEME-LINUX.md`](../../linux/LEEME-LINUX.md)). En producción la DIAN usa otro host (**No verificado** aquí: no hay producción autorizada). Además la API puede llamar al webhook que registre cada cliente.

## No verificado

- La configuración actual del router (solo se comprobó que el 47125 llega al host y que el 443 está cerrado).
- Qué atiende exactamente el puerto 80 de la IP pública (se vio `Microsoft-IIS/10.0`).
- La salida a internet hacia el host de producción de la DIAN.
