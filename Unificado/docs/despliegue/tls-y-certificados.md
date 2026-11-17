# TLS y certificados

Última revisión: 2026-11-16 · Volver al [índice](README.md)

Hay **dos certificados distintos** que no deben confundirse:

| Certificado | Dónde vive | Quién lo ve | Cómo se renueva |
|---|---|---|---|
| Comodín `*.sistemas360.com` | Almacén de certificados del IIS del host (el mismo que usa el sitio `:47110`) | Los navegadores y ERP que entran por `https://services.sistemas360.com:47125` | **A mano**, en el IIS (el 47125 usa el mismo que los demás sitios) |
| Certificado de la VM | `/etc/dian/tls/dian.crt` y `dian.key` | El panel del operador por el 8443 y, en un servidor con acceso público directo, la API y el portal por el 443 | Con `dian-tls` (ver abajo) |

Como hoy el HTTPS público lo termina el IIS, el certificado de la VM solo se ve en el 8443 (panel del operador, por túnel o VPN). Por eso el navegador muestra un aviso: es autofirmado.

## `dian-tls` (en la VM)

Script: [`linux/despliegue/dian-tls.sh`](../../linux/despliegue/dian-tls.sh), instalado como `/usr/local/sbin/dian-tls`. Todos los comandos recargan nginx y, si nginx rechaza la configuración, **restauran el certificado anterior**. Debe ejecutarse como root.

```bash
sudo dian-tls estado
```

Muestra sujeto, emisor, origen (`autofirmado`, `ca-interna`, `externo` o `letsencrypt`), fecha de vencimiento y días restantes. Termina con error (código 1) si faltan menos de 30 días (variable `DIAN_TLS_UMBRAL_DIAS`, solo para pruebas).

### 1. Autofirmado (el que crea `instalar.sh`)

Vale 825 días y es solo para pruebas. `dian-tls estado` lo avisa.

### 2. CA interna (red interna y VPN, sin dominio público)

```bash
sudo dian-tls ca-interna                                   # crea la CA (10 años); una sola vez
sudo dian-tls emitir <nombre[,nombre...]> [ip[,ip...]]     # ej.: servidor.interno,api.interno 172.31.50.10
```

- Emite el certificado del servidor (825 días) con esos nombres e IP, lo instala y recarga nginx. Un nombre o IP que no esté en la lista se rechaza.
- Reparta **solo** `/etc/dian/tls/ca/ca.crt` a los equipos y ERP que deben confiar. La llave `ca.key` **no sale del servidor**.
- **Se renueva solo:** el temporizador `dian-tls.timer` ejecuta `dian-tls renovar` cada día (03:15, con hasta 15 min de retraso) y re-emite el certificado cuando faltan menos de 30 días, con los nombres guardados en `/etc/dian/tls/sans.txt`.

### 3. Certificado de una autoridad de confianza

```bash
sudo dian-tls instalar-externo <crt> <key> [cadena]
```

Comprueba que la llave corresponda al certificado y que no esté vencido. **No se renueva solo:** el temporizador solo deja una advertencia en el registro cuando faltan menos de 30 días (`journalctl -u dian-tls`). Aquí también podría instalarse el comodín `*.sistemas360.com` si el servidor con acceso público fuera la propia VM (**No verificado**: no se ha hecho).

### 4. Let's Encrypt (solo con dominio público directo al servidor)

Requiere `certbot` (`sudo apt install certbot`), el DNS del dominio apuntando al servidor y el puerto 80 abierto desde internet (`dian-firewall aplicar ... --http any`).

```bash
sudo dian-tls letsencrypt dian.sistemas360.com <correo del responsable>
sudo dian-tls estado          # Origen: letsencrypt
sudo certbot renew --dry-run  # comprobar la renovación una vez
```

certbot renueva con su propio temporizador (`systemctl status certbot.timer`) y, tras **cada** emisión y renovación, ejecuta `dian-tls aplicar-letsencrypt` (el `--deploy-hook`), que instala el certificado nuevo y recarga nginx. Si algún día la renovación falla, `dian-tls renovar` deja un aviso en el registro.

> **Hoy no se usa:** la red tiene una sola IP pública y el puerto 80 lo atiende otro servidor, así que el desafío HTTP-01 no llega a la VM. La opción que se eligió es el comodín del IIS. El camino de Let's Encrypt solo está **sin probar** (se revisó la sintaxis; nunca se emitió un certificado real).

## El comodín del IIS (renovación manual)

- Es el certificado de `*.sistemas360.com`. El sitio `DIAN.Linux` lo toma del sitio `QuantyERP.Linux.API` (mismo almacén `MY`) al crearse con [`iis/crear-sitio-iis.ps1`](../../linux/despliegue/iis/crear-sitio-iis.ps1).
- La renovación la hace quien administra el comodín en el IIS (hoy a mano, como la de `services.sistemas360.com`). Cuando se cambia el certificado, hay que volver a asociarlo al enlace HTTPS de cada sitio que lo usa, **incluido `DIAN.Linux`** (**No verificado**: el procedimiento exacto de renovación y quién lo ejecuta no está documentado en el repositorio).
- Si el certificado vence, los ERP y navegadores dejan de confiar en `https://services.sistemas360.com:47125`, aunque la VM siga bien.
- Calendario sugerido: anotar la fecha de vencimiento del comodín y revisarla con 30 días de anticipación.

## No verificado

- Emisión real con Let's Encrypt.
- Quién renueva el comodín del IIS, con qué frecuencia y cómo se reasocia a `DIAN.Linux`.
