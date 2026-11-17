# Solución de problemas

Última revisión: 2026-11-16 · Volver al [índice](README.md)

Errores reales que aparecieron durante las pruebas, con su causa y su solución.

## Instalación y compilación

### `MSB3026`/`MSB3027` al compilar o publicar
**Causa:** hay un `Admin.Web` o `Pos.Api` corriendo en la máquina de desarrollo y bloquea los archivos de salida.
**Solución:** detener esa instancia (por ejemplo la de pruebas locales en `http://127.0.0.1:5280`) y volver a compilar.

### La aplicación se cae después de copiar archivos «a mano»
**Causa:** se copiaron binarios sobre una aplicación en marcha; systemd la reinició a los 5 segundos.
**Solución:** actualizar **siempre** con `instalar.sh`: detiene los servicios antes de copiar.

### `nginx rechazó la configuración; se deja la anterior.`
**Causa:** el `dian-nginx.conf` nuevo tiene un error. El instalador restaura el anterior y sale con error.
**Solución:** en la VM, `sudo nginx -t` muestra la línea; corregir en el repositorio y repetir la instalación.

### `dian-admin` corta la cadena de MySQL
**Causa (corregida):** se leía `dian.env` con `source` y el `;` de la cadena la partía. Ahora se lee línea a línea.

### `dian-respaldo` se detenía en silencio con MySQL
**Causa (corregida):** la cadena sin `Port` abortaba el script. Ahora un campo ausente da un valor vacío.

## Acceso y redirecciones

### Tras iniciar sesión en el panel por túnel, el navegador va a `localhost/Login?...` (sin puerto) y da `ERR_CONNECTION_REFUSED`
**Causa (corregida):** nginx pasaba `Host $host`, que **no incluye el puerto**, y la app arma sus redirecciones absolutas con el host que recibe.
**Solución:** en el bloque 8443 se usa `Host $http_host`. Quien edite el archivo no debe volver a `$host` allí.

### La entrada pública redirige a `https://172.31.50.10/Login`
**Causa (corregida):** el IIS reenvía con el nombre interno de la VM.
**Solución:** nginx (bloque 8090) fija `X-Forwarded-Host services.sistemas360.com:47125`. Si cambia la URL pública, cambiar esa línea ([produccion.md](produccion.md)).

### `ERR_CONNECTION_TIMED_OUT` al abrir `https://172.31.50.10:8443` desde el PC
**Causa:** `172.31.50.10` está en la red interna de Hyper-V; el PC no llega a ella.
**Solución:** abrir un túnel y usar `https://localhost:8443`:

```bash
ssh -N -L 8443:127.0.0.1:8443 quanty-lab
```

(o abrir la dirección interna desde el navegador **del host**).

### `ssh -N -L ...` «no hace nada»
Es lo normal: `-N` no ejecuta comandos, solo abre el túnel. Déjelo abierto; si cierra la terminal, el túnel se cae. Si el puerto local ya está en uso, el túnel avisa de «address already in use»: pruebe otro puerto local (`-L 18443:127.0.0.1:8443` y `https://localhost:18443`).

### Un comando con `ssh -f -N -L ...` se queda colgado cuando va con una tubería (`| grep`)
**Causa:** `ssh -f` deja un proceso en segundo plano que mantiene abierta la salida y la tubería espera.
**Solución:** redirigir la salida y la entrada del propio `ssh` (`>/dev/null 2>&1 </dev/null`), o abrir el túnel en una terminal aparte.

### «Estoy en la VM o en mi PC» al entrar a `https://localhost:8443`
El puerto 8443 de su PC lo escucha el proceso `ssh` (el túnel). Cierre el túnel y recargue: la página deja de funcionar. Los datos que ve son los de la VM.

### El operador no puede iniciar sesión por `https://services.sistemas360.com:47125`
Es intencional: `X-Dian-Entrada: publica` bloquea a los operadores (aparece «Usuario o contraseña incorrectos»). Use el 8443.

### Certificado no confiable en el 8443
Es el certificado de la VM, autofirmado. Acéptelo en el navegador, o emita uno con la CA interna ([tls-y-certificados.md](tls-y-certificados.md)).

## Red y firewall

### `502`/`503` por `https://services.sistemas360.com:47125`
Revisar en este orden: `systemctl is-active dian-pos-api dian-admin-web nginx` en la VM; `curl http://127.0.0.1:8090/salud` en la VM; `sudo ufw status` (debe existir `8090/tcp ... 172.31.50.1`); desde el host, `curl http://172.31.50.10:8090/salud`.

### Después de correr `dian-firewall aplicar` la entrada dejó de funcionar
**Causa:** `aplicar` reemplaza todas las reglas «DIAN»; si se omitió `--iis` (o `--api`/`--admin`), esa regla se borró.
**Solución:** repetir con todas las opciones: `sudo dian-firewall aplicar --api 172.31.50.1 --admin 172.31.50.1 --iis 172.31.50.1`.

### El 47125 aparecía «cerrado» desde internet
**Causa:** el router reenvía el rango, pero antes de crear el sitio `DIAN.Linux` nada escuchaba ese puerto en el host. Ver [iis-proxy.md](iis-proxy.md).

### `dian.sistemas360.com` no responde o muestra otro sitio
**Causa:** el DNS apunta a `5.9.84.181` (IP del hosting), no a esta red. Hasta cambiar el DNS, use `services.sistemas360.com:47125`.

## Pruebas

### `probar-entrada-publica.sh` dice «Ya hay administradores...»
Es una protección: la prueba borraría el archivo de usuarios. Solo corre en un servidor sin administradores.

### Un `python` suelto en la terminal se queda colgado
No ejecute `python` sin archivo (espera entrada estándar). Use las herramientas de edición o un script con nombre.

## PDF

### PDF de la factura con el título cortado y sin logo
**Causa:** el PDF de ejemplo en la VM (`SETP990000518.pdf`) es anterior a la corrección del título; y el logo se carga por cliente desde el panel (en la VM no se había cargado el de MARLIT).
**Solución:** instalar el paquete nuevo (la plantilla corregida va incluida), cargar el logo en el panel y generar el PDF otra vez.

### La generación de PDF falla en Linux pidiendo `libgdiplus`
**Causa:** DevExpress no encuentra `DevExpress.Drawing.v24.1.Skia` y `DevExpress.Pdf.v24.1.SkiaRenderer` y cae a GDI+.
**Solución:** estas DLL deben estar en la carpeta `pdf/` de la API (el empaquetado ya las incluye); no hace falta instalar nada en el servidor. Revisar las variables `Pdf__*` de `dian.env` ([instalacion-linux.md](instalacion-linux.md)).

### El PDF responde `503` con «Data at the root level is invalid. Line 1, position 1» (la factura sí está aprobada)
**Síntoma:** `GET /api/documento/pdf/{numero}` responde `503`; `estado` muestra `Estado: Aprobado`, `Pdf: false` y el mensaje «No se pudo generar el PDF: Data at the root level is invalid...».
**Causa (caso MARLIT, 2026-11-17):** el error no venía del XML firmado ni de la respuesta de la DIAN (ambos estaban bien formados). La traza de `journalctl` mostraba que el fallo estaba al cargar el **logo** del cliente: `RenderizadorPdf.Generar` → `DXImage.FromStream` → `SvgLoader`. El archivo `logo.png` era un PNG válido, pero traía el bloque de metadatos **C2PA** (`caBX`, credenciales de contenido que agregan algunas herramientas al exportar). DevExpress intenta leer la imagen como SVG antes que como PNG y se cae con ese error. El sistema acepta el archivo al subirlo porque solo revisa la firma del PNG (ver [logo.md](../mantenimiento/logo.md)).
**Cómo se diagnosticó:**
```bash
sudo journalctl -u dian-pos-api --since "15 min ago" --no-pager | grep -i -A12 "pdf"   # traza: busque RenderizadorPdf / DXImage
sudo grep -a -c caBX /var/lib/dian/almacen/clientes/<cliente>/logo.png                   # 1 o más = tiene metadatos C2PA
```
**Solución aplicada:** se quitó el bloque `caBX` copiando solo los bloques de imagen del PNG (los píxeles no cambiaron), se respaldó el original como `logo.original.bak` y se reinició `dian-pos-api`. Después, `GET .../pdf/{numero}` respondió `200` con `%PDF`.
**Prevención:** desde el cambio de `ClienteAdminServicio` (`LimpiadorLogo`), el panel quita esos metadatos al subir un PNG o un JPG. Los logos subidos antes no cambian solos: vuelva a subirlos.
**Si vuelve a pasar:** no regenere a ciegas. Revise el logo del cliente con la comprobación de arriba antes de cualquier otro cambio, y pruebe el PDF después de cada cambio. Para revertir, copie `logo.original.bak` sobre `logo.png`.

## No verificado

- Todos los casos de arriba se vieron en las pruebas de `quanty-lab` y del PC de desarrollo; no se han repetido en otro servidor.
