# Despliegue en Linux y piloto (paso 2.6)

**Fecha:** 2026-11-07 · **VM:** `quanty-lab` (Ubuntu 24.04, ver memoria `reference-vm-ubuntu-quanty-lab`) · **Resultado:** piloto superado en habilitación.

## Resultado del piloto

Con la API (`Pos.Api`) corriendo en Linux, un cliente (MARLIT) dado de alta con el comando `importar-cliente`, y su clave de API, se enviaron desde Windows por un túnel SSH:

| Documento | Resultado de la DIAN (habilitación) |
|---|---|
| Documento soporte SEDS984000510 | Autorizado |
| Tiquete POS EPOS9 | Autorizado |
| Nota de ajuste del POS NATP5 | Autorizado |

También se comprobó: sin clave de API la API responde 401, la app Admin arranca y redirige al login, y los registros de la API no tienen errores. La API usó unos 213 MB de memoria. Al terminar se detuvieron los procesos y se borraron de la VM el almacén, las llaves y los XML (con `shred`); quedaron solo los binarios.

Consecutivos libres tras el piloto: `SEDS` 984000511, `EPOS` 10, `NATP` 6, `SETP` 990000512.

## Cómo se repite

Todo se hace desde `Unificado`. Los paquetes son autocontenidos (no hace falta instalar .NET en el servidor) e incluyen la DLL propia de WCF.

1. **Publicar y empaquetar:**

```bash
dotnet publish src/Pos/Pos.Api -c Release -r linux-x64 --self-contained true -o dist/piloto/Pos.Api
dotnet publish src/Admin/Admin.Web -c Release -r linux-x64 --self-contained true -o dist/piloto/Admin.Web
tar -czf dist/piloto-linux-x64.tar.gz -C dist/piloto Pos.Api Admin.Web
```

2. **En el servidor**, descomprimir y preparar carpetas con permisos restringidos (el almacén y las llaves guardan secretos):

```bash
mkdir -p ~/dian-piloto/datos/{almacen,llaves,trabajo} && chmod 700 ~/dian-piloto/datos ~/dian-piloto/datos/*
tar -xzf piloto-linux-x64.tar.gz -C ~/dian-piloto && chmod +x ~/dian-piloto/*/Pos.Api ~/dian-piloto/*/Admin.Web
```

3. **Dar de alta un cliente** desde un archivo de configuración con el formato de la API anterior (puede traer `Modulos` con el software de cada tipo) y su certificado en la misma carpeta. La clave del certificado va por variable de entorno, sin dejarla en el historial; al terminar se borra el archivo de configuración:

```bash
read -rs MARLIT_P12 && export MARLIT_P12
cd ~/dian-piloto/Admin.Web && ./Admin.Web importar-cliente MARLIT <ruta>/config.json --clave-cert-env=MARLIT_P12 --generar-clave=true \
  --Almacen:Carpeta=$HOME/dian-piloto/datos/almacen --Secretos:Llaves=$HOME/dian-piloto/datos/llaves
```

   La última línea (`CLAVE_API=...`) es la clave para el ERP y no se vuelve a mostrar. El comando valida todo (software, certificado) **antes** de crear el cliente, y no sobrescribe uno existente. Las opciones propias van como `--opcion=valor`: el lector de configuración de .NET se comería el argumento siguiente si fueran `--opcion valor`.

4. **Arrancar** la API y, si se quiere, la app Admin, ambas con las mismas carpetas y solo en `127.0.0.1`:

```bash
D=$HOME/dian-piloto/datos
cd ~/dian-piloto/Pos.Api && ASPNETCORE_URLS=http://127.0.0.1:5290 Almacen__Carpeta=$D/almacen Secretos__Llaves=$D/llaves Datos__Carpeta=$D/trabajo ./Pos.Api
cd ~/dian-piloto/Admin.Web && ASPNETCORE_URLS=http://127.0.0.1:5280 Almacen__Carpeta=$D/almacen Secretos__Llaves=$D/llaves ./Admin.Web
```

5. **Probar desde otro equipo** por un túnel SSH (`ssh -N -L 15290:127.0.0.1:5290 quanty-lab`) con la prueba manual `PilotoRemotoTests`:

```bash
DIAN_PILOTO_URL=http://127.0.0.1:15290 DIAN_PILOTO_CLAVE=<clave> DIAN_PILOTO_NIT=<nit> DIAN_PILOTO_RAZON="<razón social>" \
DIAN_PILOTO_DS=<consecutivo> DIAN_PILOTO_POS=<consecutivo> DIAN_PILOTO_NOTA=<consecutivo> \
dotnet test tests/Pos.Pruebas --filter FullyQualifiedName~PilotoRemotoTests
```

## Instalación como servicio (2026-11-07)

El piloto se arrancó a mano. Ahora hay una instalación como servicio, detrás de nginx con HTTPS, en `Unificado\linux\despliegue` (guía en `LEEME.md`): unidades de `systemd` con usuario `dian` sin privilegios y aislamiento, nginx con TLS 1.2/1.3 (API en el 443; app Admin en el 8443 solo desde redes internas y la VPN), un instalador repetible (`instalar.sh`) y el comando `dian-admin`. Se probó en la VM `quanty-lab` (reinicio automático, permisos, TLS, cabeceras, 401/403) y por ese camino se autorizaron en habilitación un documento soporte (SEDS984000511), un tiquete POS (EPOS10) y una nota de ajuste (NATP6) de MARLIT. Los datos de MARLIT se borraron de la VM al terminar; los servicios quedaron instalados y en marcha con el almacén vacío.

La API y la app Admin ahora aceptan las cabeceras `X-Forwarded-*` solo del proxy local, la app Admin envía HSTS y todas sus cookies llevan `Secure` detrás de HTTPS.

Consecutivos libres: `SEDS` 984000512, `EPOS` 11, `NATP` 7, `SETP` 990000512.

## TLS, respaldos y firewall (2026-11-07)

Se agregaron tres herramientas al despliegue (detalle y uso en `Unificado\linux\despliegue\LEEME.md`) y se probaron en la VM `quanty-lab`:

- **`dian-tls`**: CA interna con emisión y **renovación automática** (temporizador diario), instalación de certificados de una autoridad externa y estado. Pruebas: el certificado de la CA valida sin `-k` por nombre y por IP, se rechaza un nombre no incluido y a un cliente sin la CA; la renovación cambia la serie del certificado; se rechazan una llave que no corresponde y un certificado vencido; nginx siguió sirviendo y, si nginx rechaza el certificado, se restaura el anterior.
- **`dian-respaldo`**: respaldo diario **cifrado con una llave pública** (la privada vive fuera del servidor), con retención, suma de verificación y volcado de las tablas `dian_*` si se usa MySQL. Pruebas: nada del cliente aparece en claro dentro del archivo; la restauración deja el almacén y las llaves idénticos; una segunda instancia de la API levantada con los datos restaurados atendió con la clave del cliente (lo que prueba que las llaves de cifrado se recuperaron); se rechazan una llave privada equivocada, un archivo alterado y una carpeta de destino con contenido; se conservaron solo los 2 respaldos pedidos; la rama de MySQL incluyó al cliente en el volcado y sin el PIN en claro; el servicio de systemd corre sin error.
- **`dian-firewall`**: solo añade reglas de `ufw` con la etiqueta DIAN, no toca las demás y es repetible. En la VM `ufw` ya estaba activo (denegar todo lo entrante; solo SSH y, desde el host `172.31.50.1`, los puertos 8080 y 8081), de modo que el 443 y el 8443 de DIAN **no eran alcanzables desde fuera** de la VM, y se comprobó desde el host: antes, sin respuesta; después de `aplicar --api 172.31.50.1 --admin 172.31.50.1`, HTTP 200 en los dos. La app Admin no admite `any`; el otro sitio de la VM siguió igual.

**Errores que encontró la prueba y quedaron corregidos:** `dian-admin` leía `/etc/dian/dian.env` con `source`, y el `;` de la cadena de MySQL cortaba el valor (ahora lo lee línea a línea, sin interpretación del shell); el respaldo se detenía en silencio si la cadena de conexión no traía el puerto; con una llave privada equivocada la restauración mostraba un error crudo de `tar`.

Al terminar se borraron de la VM el cliente de prueba, los respaldos y la llave privada de prueba; quedó el certificado emitido por la CA interna (`dian-tls estado`), el firewall con las reglas DIAN, los temporizadores activos y los servicios en marcha con el almacén vacío. El respaldo queda sin configurar hasta que se genere el par de llaves propio.

## Lo que sigue

- **Servidor de producción**: instalar con `instalar.sh`, emitir o instalar el certificado con los nombres que usarán los clientes, abrir el 443 solo a quien corresponda, y configurar el respaldo (guardando la llave privada fuera del servidor).
- **Copia de los respaldos fuera del servidor**: no está automatizada; depende de a dónde quiera llevarlos.
- **Primer cliente real** en habilitación.
- **Producción**: ningún cliente tiene producción autorizada. Pasar uno a producción es una decisión aparte, con su autorización expresa, empezando por un cliente piloto.
- Se dejan archivos `*.xml.lock` junto a las notas de ajuste; son inofensivos, pero conviene una limpieza periódica.
