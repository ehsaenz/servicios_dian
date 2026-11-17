# Instalación como servicio (systemd + nginx con HTTPS)

Instala la API de facturación (`Pos.Api`) y la app Admin (`Admin.Web`) en un servidor Linux como servicios de `systemd`, detrás de nginx con HTTPS. Probado en Ubuntu 24.04 (VM `quanty-lab`) el 2026-11-07.

## Qué instala

| Pieza | Dónde | Detalle |
|---|---|---|
| Binarios | `/opt/dian/pos-api`, `/opt/dian/admin-web` | De root y de solo lectura para el servicio. |
| Datos y secretos | `/var/lib/dian/{almacen,llaves,trabajo,cache}` | Del usuario `dian`, permisos 700. |
| Configuración | `/etc/dian/dian.env` | Solo root (600). Puede traer la cadena de MySQL. |
| Certificado TLS | `/etc/dian/tls/dian.crt` y `dian.key` | Autofirmado si no existe; **reemplazar en producción**. |
| Servicios | `dian-pos-api` y `dian-admin-web` | Usuario `dian` sin privilegios, reinicio automático, arranque con el sistema. |
| nginx | `/etc/nginx/conf.d/dian.conf` | No toca ningún otro sitio de nginx. |
| Comandos | `/usr/local/sbin/dian-admin`, `dian-tls`, `dian-respaldo`, `dian-firewall` | Administración de la app, certificado TLS, respaldos cifrados y reglas del firewall (secciones de abajo). |
| Temporizadores | `dian-tls.timer` (3:15) y `dian-respaldo.timer` (2:30) | Vigilancia/renovación del certificado y respaldo diario. |

## Puertos

| Puerto | Qué | Acceso |
|---|---|---|
| 443 | API pública | Quien el firewall deje llegar (ver `dian-firewall`) y tenga una clave de API válida. Límite de 20 solicitudes por segundo por dirección. |
| 8443 | App Admin | **Solo** este equipo, redes privadas (10/8, 172.16/12, 192.168/16) y la VPN (26/8). Cualquier otra dirección recibe 403. Ajuste las redes en `dian-nginx.conf`. |
| 5290 y 5280 | La API y la app Admin | Solo en `127.0.0.1`; nunca se exponen. |

## Instalar o actualizar

1. En la máquina de desarrollo, publicar y empaquetar (desde `Unificado`):

```bash
dotnet publish src/Pos/Pos.Api -c Release -r linux-x64 --self-contained true -o dist/servicios/Pos.Api
dotnet publish src/Admin/Admin.Web -c Release -r linux-x64 --self-contained true -o dist/servicios/Admin.Web
tar -czf dist/servicios-linux-x64.tar.gz -C dist servicios -C ../linux despliegue
```

2. Copiar el paquete al servidor y ejecutar el instalador como root:

```bash
mkdir -p ~/instalacion && tar -xzf servicios-linux-x64.tar.gz -C ~/instalacion
cd ~/instalacion/despliegue && sudo ./instalar.sh ~/instalacion/servicios
```

El instalador es repetible: **actualiza** los binarios (detiene los servicios antes de copiar), reinicia y no toca los datos, la configuración ni el certificado. Si nginx rechaza la configuración, deja la anterior. Al final comprueba los tres servicios y las dos URL.

**Actualice siempre con el instalador.** Copiar archivos a mano sobre una aplicación en marcha la tumba (se vio en las pruebas, y systemd la reinició sola a los 5 segundos).

## Después de instalar

```bash
printf '%s' '<contraseña de 12 o más caracteres>' | sudo dian-admin crear-admin <usuario>     # primer administrador
sudo dian-admin crear-esquema                                                                 # solo si usa MySQL
```

- Entrar a `https://<servidor>:8443` (por la VPN) y dar de alta los clientes. O por consola, con `importar-cliente` (la clave del certificado va por una variable de entorno, no por argumento; ver `docs/fase2-despliegue-linux.md`).
- Para usar **MySQL**, descomentar y completar `MySql__Cadena` en `/etc/dian/dian.env` y ejecutar `sudo systemctl restart dian-pos-api dian-admin-web`. `sudo dian-admin migrar-almacen` copia los clientes del almacén de archivos.
- Para ver qué pasa: `journalctl -u dian-pos-api -f` y `journalctl -u dian-admin-web -f`.

## Certificado TLS (`dian-tls`)

El instalador deja un certificado autofirmado, solo para pruebas. Hay dos caminos para algo mejor; ambos recargan nginx, y si nginx rechaza el certificado restauran el anterior.

```bash
sudo dian-tls estado                       # de quién es, quién lo emitió, cuándo vence y cuántos días quedan
```

**A. CA interna** (sin dominio público; sirve para la red interna y la VPN). Una sola CA firma el certificado del servidor y se renueva sola:

```bash
sudo dian-tls emitir <nombre[,nombre...]> [ip[,ip...]]      # ej.: servidor.interno,api.interno 172.31.50.10
```

Esto crea la CA (`/etc/dian/tls/ca/ca.crt`, vale 10 años) y emite el certificado del servidor (825 días) con esos nombres e IP. **Reparta solo `ca.crt`** a los equipos y ERP que deben confiar en el servidor; la llave `ca.key` no sale del servidor. Un temporizador diario (`dian-tls.timer`) renueva el certificado cuando faltan menos de 30 días. Un nombre o IP que no esté en la lista se rechaza.

**B. Certificado de una autoridad de confianza** (para clientes en internet): `sudo dian-tls instalar-externo <crt> <key> [cadena]`. Comprueba que la llave corresponda al certificado y que no esté vencido. Estos certificados **no se renuevan solos**: el temporizador solo deja una advertencia en el registro (`journalctl -u dian-tls`) cuando faltan menos de 30 días.

## Respaldos cifrados (`dian-respaldo`)

Respalda el almacén de clientes, las llaves de cifrado y los XML generados, y el volcado de las tablas `dian_*` si se usa MySQL (no incluye la caché de archivos, que se reconstruye sola). Es **siempre cifrado** con una llave pública guardada en el servidor: quien robe el respaldo no puede leerlo sin la llave privada, que no vive en el servidor.

```bash
sudo dian-respaldo configurar     # una sola vez: crea el par de llaves
```

`configurar` deja la llave **privada** en `/root/dian-respaldo-privada.pem`. **Cópiela fuera del servidor (caja fuerte o gestor de contraseñas) y bórrela de ahí** (`shred -u`): sin ella no se puede restaurar nada, y quien tenga el servidor no la necesita para respaldar.

- **Automático:** el temporizador `dian-respaldo.timer` corre cada día a las 2:30 y conserva los últimos 14 (cambie `RETENCION=` en `/etc/dian/respaldo.conf`). Mientras no se configure, se omite sin error.
- **A mano:** `sudo dian-respaldo ejecutar`, `sudo dian-respaldo listar`. Los respaldos quedan en `/var/backups/dian` con su suma de verificación.
- **Fuera del servidor:** un respaldo en el mismo equipo no protege de perder el equipo. Copie `/var/backups/dian` a otro lugar (por ejemplo con `rsync` o `scp` desde otra máquina, o una tarea programada). Esa copia no está automatizada.
- **Restaurar** (a una carpeta vacía, para revisar o para volver a poner en servicio):

```bash
sudo dian-respaldo restaurar /var/backups/dian/dian-<fecha>.tar.gz.enc /var/lib/dian-restauracion /ruta/llave-privada.pem
```

  Verifica la suma de verificación, descifra y extrae. Una llave que no corresponde o un archivo alterado se rechazan con un mensaje claro. Para volver a poner en servicio, el propio comando imprime los pasos: detener los servicios, copiar `almacen`, `llaves` y `trabajo` a `/var/lib/dian`, `chown -R dian:dian /var/lib/dian` y arrancar. Con MySQL, cargue además `mysql-dian.sql`. Respalde y restaure **siempre juntos** el almacén (o la base) y las llaves.

## Firewall (`dian-firewall`)

Solo **añade** reglas de `ufw` con la etiqueta DIAN: no cambia las políticas ni las reglas de otros servicios.

```bash
sudo dian-firewall estado
sudo dian-firewall aplicar --api <origen[,origen...]|any> --admin <origen[,origen...]> [--habilitar]
```

Un origen es una dirección o red (`172.31.50.1`, `10.0.0.0/8`). `--api` decide quién llega al 443 (con `any` se abre a internet; la API igualmente exige clave de API); `--admin` decide quién llega al 8443 y **nunca admite `any`**. Si `ufw` está apagado, `--habilitar` lo enciende y permite SSH antes para no dejarle fuera. Es repetible: reemplaza las reglas DIAN anteriores por las pedidas. En la VM `quanty-lab` el firewall ya estaba activo, solo permitía SSH y el host Hyper-V (`172.31.50.1`) hacia 8080 y 8081, y se aplicó `--api 172.31.50.1 --admin 172.31.50.1`.

## Lo que verificó la prueba del 2026-11-07

- Los servicios corren como `dian`, no pueden escribir en `/opt/dian` ni leer `/home`, y la carpeta de datos es 700.
- `kill -9` a la API: systemd la reinició solo a los 5 segundos y volvió a responder.
- TLS 1.2 y 1.3 aceptados; TLS 1.0 y 1.1 rechazados. HSTS y las demás cabeceras presentes; las cookies de la app Admin llevan `Secure`, `HttpOnly` y `SameSite=Strict`.
- Sin clave de API: 401. Un bloque de prueba con `deny all` respondió 403.
- El otro sitio de nginx de la VM (`quantyerp`, puertos 8080 y 8081) siguió igual.
- Un cliente (MARLIT) dado de alta en el servicio instalado envió por HTTPS un documento soporte, un tiquete POS y una nota de ajuste, y la DIAN autorizó los tres en habilitación. El registro no contiene secretos.

## Pendiente

- Que el servidor de producción tenga su certificado (CA interna o de una autoridad), con los nombres que usarán los clientes.
- Una copia de los respaldos **fuera** del servidor (no automatizada) y la llave privada del respaldo guardada aparte.
- Abrir el 443 solo a quien corresponda en el servidor real (`dian-firewall aplicar`).

## Dominio público: portal de clientes y API (`dian.sistemas360.com`)

Con el dominio `dian.sistemas360.com` apuntando al servidor, el mismo nginx publica **por HTTPS** (puerto 443) dos cosas:

| Ruta | Va a | Quién entra |
|---|---|---|
| `/api/...` y `/salud` | la API (`127.0.0.1:5290`) | los ERP, con su clave de API |
| todo lo demás | el portal (`127.0.0.1:5280`) | los usuarios de cada cliente, con usuario y contraseña |

El **operador no entra por el dominio público**: nginx marca cada petición con `X-Dian-Entrada` (`publica` en el dominio, `interna` en el puerto 8443) y la app rechaza el inicio de sesión de un operador cuando llega marcada como pública (también si trae una cookie de operador). El operador sigue entrando por `https://<servidor>:8443`, solo desde redes internas o la VPN (o por túnel SSH). Se puede desactivar con `Portal__OperadorSoloInterno=false` en `/etc/dian/dian.env` (no se recomienda).

### Puesta en marcha (en este orden)

1. **DNS:** un registro A de `dian.sistemas360.com` hacia la IP pública del servidor.
2. **Actualizar** con el instalador (instala la configuración de nginx nueva y crea `/var/www/dian-acme`):

```bash
cd ~/instalacion/despliegue && sudo ./instalar.sh ~/instalacion/servicios
```

3. **Firewall:** abrir 443 al mundo (la API ya exige clave y el portal usuario y contraseña) y el 80 para el desafío del certificado; el 8443 sigue solo para tu red interna o la VPN:

```bash
sudo dian-firewall aplicar --api any --http any --admin <red interna o VPN>
```

4. **Certificado público** (necesita `certbot`: `sudo apt install certbot`):

```bash
sudo dian-tls letsencrypt dian.sistemas360.com <correo del responsable>
sudo dian-tls estado          # Origen: letsencrypt
```

   certbot renueva solo con su temporizador (`systemctl status certbot.timer`) y, tras cada renovación, instala el certificado nuevo y recarga nginx (el `deploy-hook`). Compruebe una vez la renovación con `sudo certbot renew --dry-run`.
5. **Prueba:** `https://dian.sistemas360.com/Login` debe mostrar el inicio de sesión del portal y `https://dian.sistemas360.com/salud` responder `ok`. Un usuario de operador debe recibir "Usuario o contraseña incorrectos" en ese dominio.

### Crear el acceso de un cliente

En el panel del operador (8443), página del cliente → «Usuarios del portal» → crear usuario. Se entrega al cliente la dirección `https://dian.sistemas360.com`, su usuario y la contraseña generada (se muestra una sola vez; la cambia en «Mi cuenta»).

### Antes de abrirlo a clientes reales

- Verificación en dos pasos (pendiente) y correo de bienvenida/recuperación (falta el contrato del EmailAPI).
- Una copia de los respaldos fuera del servidor (ver «Respaldos cifrados»).
- Si el servidor de producción es otro, repetir los pasos 1 a 4 allí; `quanty-lab` es de pruebas.

### Entrada por el IIS del host (la que está en uso hoy): `https://services.sistemas360.com:47125`

La red tiene una sola IP pública y el 80 lo usa otro IIS, así que DIAN sale como QuantyERP (puertos 47122/47123): el IIS del host termina el HTTPS con el certificado comodín `*.sistemas360.com` y reenvía a la VM.

```
Internet → 190.242.61.214:47125 → IIS del host (sitio DIAN.Linux, ARR/URL Rewrite)
        → 172.31.50.10:8090 (nginx, solo desde 172.31.50.1) → API 5290 / portal 5280
```

- `https://services.sistemas360.com:47125/salud` y `/api/...`: la API. `https://services.sistemas360.com:47125/Login`: el portal de clientes.
- En la VM: el bloque `listen 8090` de `dian-nginx.conf` y `dian-firewall aplicar --api 172.31.50.1 --admin 172.31.50.1 --iis 172.31.50.1`. Marca la entrada como `publica` y declara `X-Forwarded-Proto https`, así que la app emite cookies seguras y **no deja entrar a un operador**; el operador sigue entrando por el 8443 (VPN o túnel SSH).
- En el host Windows: `iis/crear-sitio-iis.ps1` (repetible) crea el sitio `DIAN.Linux` con el certificado y el grupo de aplicaciones del sitio `QuantyERP.Linux.API`, una regla de URL Rewrite hacia `http://172.31.50.10:8090/` y la regla de Windows Firewall del 47125. Se ejecuta con `powershell -ExecutionPolicy Bypass -File`. El router ya reenvía ese rango (el 47125 llega al host).
- Para pasar a `https://dian.sistemas360.com` sin puerto basta con reenviar el 443 del router al IIS y agregar un enlace con ese nombre al sitio (el comodín ya lo cubre); la VM no cambia.
