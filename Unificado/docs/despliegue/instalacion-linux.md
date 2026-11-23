# Instalación y actualización en Linux

Última revisión: 2026-11-16 · Volver al [índice](README.md)

Instala `Pos.Api` y `Admin.Web` como servicios de `systemd`, detrás de nginx con HTTPS. Probado en Ubuntu 24.04 (VM `quanty-lab`). Los paquetes son **autocontenidos**: no hace falta instalar .NET en el servidor.

## Requisitos del servidor

- Linux de 64 bits con glibc (probado en Ubuntu 24.04), `nginx`, `openssl`, `curl`, `systemd`.
- Para el firewall: `ufw`. Para Let's Encrypt: `certbot`. Para respaldar MySQL: `mysqldump`.
- Salida a internet hacia la DIAN (ver [arquitectura-de-red.md](arquitectura-de-red.md)).
- Paquete `tzdata` y biblioteca ICU; si .NET no arranca por ICU, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (según [`linux/LEEME-LINUX.md`](../../linux/LEEME-LINUX.md)).

## 1. Publicar y empaquetar (en la máquina de desarrollo)

Desde la carpeta `Unificado`:

```bash
dotnet publish src/Pos/Pos.Api -c Release -r linux-x64 --self-contained true -o dist/servicios/Pos.Api
dotnet publish src/Admin/Admin.Web -c Release -r linux-x64 --self-contained true -o dist/servicios/Admin.Web
# Antes de empaquetar, dist/servicios/Pos.Api/pdf debe existir (generador DevExpress; ver plantillas-pdf.md).
# publish NO la crea, y borrar dist/servicios la elimina: conserve una copia de pdf/ fuera de dist.
tar -czf dist/servicios-linux-x64.tar.gz -C dist servicios -C ../linux despliegue
```

Notas:

- Si hay un `Admin.Web` o `Pos.Api` corriendo localmente, `dotnet build/publish` falla (MSB3026/MSB3027): ver [solucion-de-problemas.md](solucion-de-problemas.md).
- **PDF con DevExpress:** la API carga el generador desde su carpeta `pdf/` (`Pdf:Carpeta`). Para publicarla con PDF hace falta DevExpress 24.1 en la máquina de compilación (o sus DLL en una carpeta y `DevExpressBinPath`). La licencia de servidor es una decisión pendiente ([produccion.md](produccion.md)).
- `dist/` está fuera del control de versiones.

## 2. Copiar e instalar (en el servidor)

```bash
scp dist/servicios-linux-x64.tar.gz quanty-lab:~/
ssh quanty-lab 'mkdir -p ~/instalacion && tar -xzf ~/servicios-linux-x64.tar.gz -C ~/instalacion'
# Un paquete armado en Windows no conserva el permiso de ejecución de los .sh: se restaura aquí.
ssh quanty-lab 'chmod +x ~/instalacion/despliegue/*.sh && cd ~/instalacion/despliegue && sudo ./instalar.sh ~/instalacion/servicios'
```

(El paquete puede dejarse en cualquier carpeta; lo importante es que `instalar.sh` reciba la carpeta que contiene `Pos.Api/` y `Admin.Web/`.) Si el paquete se armó en Windows, el instalador le da permiso de ejecución a los dos binarios.

### Qué hace `instalar.sh`

| Paso | Detalle |
|---|---|
| Usuario y carpetas | Crea el usuario de sistema `dian` (sin shell, home `/var/lib/dian`). `/opt/dian/{pos-api,admin-web}` y `/etc/dian` (755, root). `/var/lib/dian/{almacen,llaves,trabajo,cache}` (700, `dian`). `/etc/dian/tls` (700, root). `/var/www/dian-acme` (755). |
| Binarios | **Detiene** los servicios, copia los binarios, los deja de root y sin escritura para el resto. |
| Configuración | Crea `/etc/dian/dian.env` (600) **solo si no existe**. |
| Certificado TLS | Crea uno **autofirmado** (825 días) en `/etc/dian/tls/dian.crt` y `dian.key` **solo si no existe**. |
| Servicios | Instala `dian-pos-api.service` y `dian-admin-web.service`, los habilita y reinicia. |
| Herramientas | `/usr/local/sbin/dian-tls`, `dian-respaldo`, `dian-firewall` y `dian-admin`; unidades y temporizadores de TLS y respaldo; `/var/backups/dian`. |
| nginx | Instala `/etc/nginx/conf.d/dian.conf`; si `nginx -t` falla, **restaura el anterior** y sale con error. No toca otros sitios. |
| Comprobación | Muestra el estado de los tres servicios y la respuesta de `https://127.0.0.1/salud` y `https://127.0.0.1:8443/Login`. |

Es **repetible**: actualizar es volver a correr los pasos 1 y 2. No toca los datos, `dian.env` ni el certificado.

> **Actualice siempre con el instalador.** Copiar archivos a mano sobre una aplicación en marcha la tumba (se vio en las pruebas; systemd la reinició a los 5 segundos).

### Rollback

El instalador no guarda versiones anteriores de los binarios. Para volver atrás: publicar de nuevo la versión anterior (por ejemplo desde su commit de `dev`) y repetir la instalación. nginx sí conserva el archivo previo si el nuevo es rechazado. **No verificado:** un procedimiento de rollback ensayado.

## Servicios de systemd

| Unidad | Escucha | Ejecuta |
|---|---|---|
| `dian-pos-api` | `http://127.0.0.1:5290` | `/opt/dian/pos-api/Pos.Api` |
| `dian-admin-web` | `http://127.0.0.1:5280` | `/opt/dian/admin-web/Admin.Web` |

Ambas: usuario `dian`, `EnvironmentFile=/etc/dian/dian.env`, `Restart=on-failure` cada 5 s, `UMask=0077`, `NoNewPrivileges`, `PrivateTmp`, `ProtectSystem=strict`, `ProtectHome`, `ReadWritePaths=/var/lib/dian` y otras restricciones de kernel. Empiezan después de `network-online.target` y `mysql.service`.

## `/etc/dian/dian.env` (solo nombres de variables)

Archivo de root (600). Lo lee systemd para los dos servicios y el comando `dian-admin`. **No debe versionarse ni copiarse a documentos.**

| Variable | Para qué | Quién la usa |
|---|---|---|
| `Almacen__Carpeta` | Almacén de clientes (`/var/lib/dian/almacen`) | API y Admin |
| `Secretos__Llaves` | Llaves de cifrado de secretos (`/var/lib/dian/llaves`) | API y Admin |
| `Datos__Carpeta` | XML y PDF generados (`/var/lib/dian/trabajo`) | API (escribe), Admin (lee) |
| `Cache__Carpeta` | Caché de archivos (`/var/lib/dian/cache`) | API y Admin |
| `MySql__Cadena` | Opcional: cadena de MySQL para usar la base en vez del almacén de archivos (viene comentada) | API y Admin |
| `Portal__OperadorSoloInterno` | Opcional: `false` desactiva el bloqueo de operadores en la entrada pública (no se recomienda) | Admin |
| `Whatsapp__ClaveApi` | Clave del gateway de WhatsApp. **Sin ella no hay segundo factor** y el operador solo entra por la red interna | Admin |
| `Whatsapp__Url`, `Whatsapp__Sesion` | Opcionales: gateway y sesión (`https://services.sistemas360.com:47112`, `QuantyERPWhatsapp`) | Admin |
| `Portal__SegundoFactorOperador` | Opcional: `false` apaga el código de WhatsApp (vuelve a regir solo `Portal__OperadorSoloInterno`) | Admin |
| `Dian__EsperaSetSegundos` | Opcional: espera del resultado del set de pruebas | API |
| `Documentos__EnvioEnSegundoPlano` | Opcional: envío en segundo plano | API |
| `Documentos__ReintentoSegundos` | Opcional: intervalo de reintentos | API |
| `Pdf__Generador`, `Pdf__Carpeta`, `Pdf__Plantillas`, `Pdf__Fuentes` | Generación de PDF con DevExpress | API |

Valores de las cuatro variables `Pdf__*` según la bitácora de la fase 4 (`DIAN/docs/fase4-fe-pdf-webhook.md`): generador `DIAN.Impresion.DevExpress.GeneradorPdfDevExpress` y carpetas `/opt/dian/pos-api/pdf`, `.../pdf/plantillas` y `.../pdf/fuentes`. **El instalador no las agrega.** No verificado: si ya están en el `dian.env` de `quanty-lab`.

Tras cambiar el archivo: `sudo systemctl restart dian-pos-api dian-admin-web`.

### MySQL (opcional)

Por defecto todo vive en archivos (`/var/lib/dian`). Para MySQL, siga [configuración y migración](configuracion-y-migracion.md): pause los ERP, respalde y **detenga ambos servicios antes de cambiar `MySql__Cadena`**. Con la base creada y la conexión configurada, ejecute antes de arrancarlos:

```bash
sudo dian-admin crear-esquema        # crea las tablas si no existen
sudo dian-admin migrar-almacen       # copia los clientes del almacén de archivos a MySQL
```

El respaldo incluye en ese caso el volcado de `dian_clientes`, `dian_claves`, `dian_software` y `dian_archivos`. La API de DIAN no necesita compatibilidad con MySQL 5.1.73 (esa restricción es del ERP).

## Después de instalar

```bash
# Primer administrador (la contraseña por entrada estándar, nunca por argumento; 12 o más caracteres)
ssh -t quanty-lab 'read -rsp "Contraseña: " P; echo; printf "%s" "$P" | sudo dian-admin crear-admin <usuario>'

sudo dian-tls estado          # de quién es el certificado y cuándo vence
sudo dian-respaldo configurar # una sola vez (ver respaldos-y-operacion.md)
sudo dian-firewall aplicar --api 172.31.50.1 --admin 172.31.50.1 --iis 172.31.50.1   # ver nginx-y-firewall.md
```

`dian-admin` ejecuta la app Admin en modo comando como el usuario `dian`, con las variables de `dian.env`, leídas línea a línea sin interpretación del shell (la cadena de MySQL lleva `;`). Comandos: `crear-admin`, `telefono-admin`, `importar-cliente`, `crear-esquema`, `migrar-almacen`.

## Verificación rápida tras instalar

```bash
systemctl is-active dian-pos-api dian-admin-web nginx
curl -sk https://127.0.0.1/salud                # API
curl -sk -o /dev/null -w '%{http_code}\n' https://127.0.0.1:8443/Login      # panel (200)
curl -s  -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8090/api/documento   # 401 sin clave
```

Prueba en vivo del bloqueo del operador en la entrada pública: [`linux/despliegue/pruebas/probar-entrada-publica.sh`](../../linux/despliegue/pruebas/probar-entrada-publica.sh). Crea un administrador temporal y lo borra; **se niega a correr si ya existen administradores**, para no borrarlos.

## No verificado

- Instalación en un servidor distinto de `quanty-lab` (Debian, Rocky, etc.).
- Rollback ensayado.
- Contenido actual de `dian.env` en la VM (incluidas las variables `Pdf__*`).
