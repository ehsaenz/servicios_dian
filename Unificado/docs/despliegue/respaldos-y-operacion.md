# Respaldos y operación

Última revisión: 2026-11-16 · Volver al [índice](README.md)

## Respaldos cifrados (`dian-respaldo`)

Script: [`linux/despliegue/dian-respaldo.sh`](../../linux/despliegue/dian-respaldo.sh), instalado como `/usr/local/sbin/dian-respaldo` (como root).

**Qué respalda:** `/var/lib/dian/almacen` (clientes), `/var/lib/dian/llaves` (llaves de cifrado) y `/var/lib/dian/trabajo` (XML y PDF generados); y, si `MySql__Cadena` está en `dian.env`, el volcado de las tablas `dian_clientes`, `dian_claves`, `dian_software` y `dian_archivos`. **No** respalda la caché (`/var/lib/dian/cache`), que se reconstruye sola.

**Cómo protege:** siempre cifra con la **llave pública** `/etc/dian/respaldo.crt` (CMS, AES-256). La privada solo hace falta para restaurar y **no debe vivir en el servidor**. Cada respaldo lleva su suma `sha256`. Retención: 14 respaldos (variable `RETENCION=` en `/etc/dian/respaldo.conf`). Los archivos quedan en `/var/backups/dian` como `dian-<fecha>.tar.gz.enc`.

### Configurar (una sola vez)

```bash
sudo dian-respaldo configurar
```

Crea el par de llaves; la privada queda en `/root/dian-respaldo-privada.pem`. **Cópiela AHORA fuera del servidor (caja fuerte o gestor de contraseñas) y bórrela de ahí:**

```bash
sudo shred -u /root/dian-respaldo-privada.pem
```

Sin esa llave no se puede restaurar ningún respaldo. Si ya existe `/etc/dian/respaldo.crt`, `configurar` no la cambia (cambiarla dejaría ilegibles los respaldos anteriores). Mientras no se configure, el temporizador lo omite sin error.

### Usar

```bash
sudo dian-respaldo ejecutar     # hace un respaldo, comprueba que sea válido y borra los más antiguos
sudo dian-respaldo listar
sudo dian-respaldo restaurar /var/backups/dian/dian-<fecha>.tar.gz.enc /var/lib/dian-restauracion /ruta/llave-privada.pem
```

`restaurar` verifica la suma, descifra y extrae en una carpeta **vacía**. Después imprime los pasos para volver a poner en servicio: detener `dian-pos-api` y `dian-admin-web`, copiar `almacen`, `llaves` y `trabajo` a `/var/lib/dian`, `chown -R dian:dian /var/lib/dian` y arrancar; con MySQL, cargar además `mysql-dian.sql`. **Restaure siempre juntos el almacén (o la base) y las llaves.**

### Fuera del servidor

Un respaldo en el mismo equipo no protege de perder el equipo. Copie `/var/backups/dian` a otro lugar (por ejemplo con `rsync` o `scp` desde otra máquina, o una tarea programada). **Esa copia no está automatizada.**

## Temporizadores

| Unidad | Hora | Hace |
|---|---|---|
| `dian-respaldo.timer` | todos los días 02:30 (hasta 10 min de retraso; `Persistent`) | `dian-respaldo ejecutar` (servicio `dian-respaldo.service`) |
| `dian-tls.timer` | todos los días 03:15 (hasta 15 min de retraso; `Persistent`) | `dian-tls renovar` (servicio `dian-tls.service`) |

```bash
systemctl list-timers dian-respaldo.timer dian-tls.timer
systemctl status dian-respaldo.service dian-tls.service
```

(El inventario de infraestructura menciona ~03:20 para TLS; el archivo del temporizador dice 03:15 más un retraso aleatorio de hasta 15 min, así que ambos son compatibles.)

## Registros

```bash
journalctl -u dian-pos-api -f          # la API
journalctl -u dian-admin-web -f        # el portal y el panel
journalctl -u dian-tls                 # avisos del certificado
journalctl -u dian-respaldo            # resultado de los respaldos
sudo tail -f /var/log/nginx/error.log  # nginx (ruta por defecto de Ubuntu)
```

La auditoría de acciones del panel (quién creó, cambió o autorizó qué) queda en `/var/lib/dian/almacen/auditoria.jsonl` y se ve en la página «Auditoría» del panel (solo operador). El registro no contiene secretos.

## Salud

- `GET /salud` → `ok`. Es público, sin clave.
- `systemctl is-active dian-pos-api dian-admin-web nginx`.
- Reinicio automático: si un servicio muere, systemd lo levanta a los 5 segundos (probado con `kill -9`).
- Memoria: la API usó unos 213 MB en las pruebas del piloto.

## Qué revisar a diario

1. `curl -s https://services.sistemas360.com:47125/salud` responde `ok`.
2. Los tres servicios activos: `systemctl is-active dian-pos-api dian-admin-web nginx`.
3. El respaldo de la noche existe y es reciente: `sudo dian-respaldo listar`.
4. Sin errores nuevos: `journalctl -u dian-pos-api --since yesterday -p err`.
5. Vencimientos: `sudo dian-tls estado` y la fecha del comodín del IIS.
6. Espacio en disco: `df -h /var/lib/dian /var/backups/dian`.

## Qué revisar cada semana o cada mes

- La copia de los respaldos **fuera** del servidor.
- Certificados de los clientes próximos a vencer (el panel los muestra en la columna «Certificado»).
- Archivos `*.xml.lock` junto a las notas de ajuste: son inofensivos, pero conviene una limpieza periódica (`DIAN/docs/fase2-despliegue-linux.md`).
- Actualizaciones del sistema operativo de la VM (**No verificado**: no hay una política definida).

## Administradores del panel

```bash
printf '%s' '<clave de 12+ caracteres>' | sudo dian-admin crear-admin <usuario>
```

La contraseña por entrada estándar, nunca por argumento. Las cuentas de operador se bloquean 15 minutos tras 5 intentos fallidos. Las contraseñas se guardan con PBKDF2-SHA256 (210 000 iteraciones) en `admin-usuarios.json` dentro del almacén. **No verificado:** un comando para eliminar o restablecer a un operador (los usuarios de los clientes sí se gestionan desde el panel; ver [`../mantenimiento/`](../mantenimiento/)).

## No verificado

- Respaldo configurado en `quanty-lab` (las pruebas dejaron «sin configurar» hasta generar el par de llaves propio).
- Una política de actualizaciones del sistema operativo y de rotación de registros más allá de la de systemd/nginx.
