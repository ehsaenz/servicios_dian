#!/usr/bin/env bash
# Respaldo cifrado de los datos de DIAN: almacén de clientes, llaves de cifrado y XML generados (y MySQL, si se usa).
# Uso (como root):
#   dian-respaldo configurar                              crea el par de llaves del respaldo (una sola vez)
#   dian-respaldo ejecutar                                hace un respaldo cifrado y borra los más antiguos
#   dian-respaldo listar                                  muestra los respaldos guardados
#   dian-respaldo restaurar <archivo.enc> <carpeta> <llave-privada.pem>   descifra y extrae en una carpeta VACÍA
#
# El respaldo contiene los secretos de los clientes, así que SIEMPRE va cifrado con la llave PÚBLICA /etc/dian/respaldo.crt.
# La llave privada solo hace falta para restaurar: guárdela FUERA de este servidor. Quien robe el respaldo sin ella no puede leerlo.
set -euo pipefail
umask 077

DESTINO=/var/backups/dian
CERT=/etc/dian/respaldo.crt
CONF=/etc/dian/respaldo.conf
DATOS=/var/lib/dian
RETENCION=14
[ -f "$CONF" ] && . "$CONF"

[ "$(id -u)" -eq 0 ] || { echo "Ejecute como root (sudo)."; exit 1; }

cadena_mysql() { # imprime el valor de MySql__Cadena si está configurada
  [ -f /etc/dian/dian.env ] && grep -E '^MySql__Cadena=' /etc/dian/dian.env | head -1 | cut -d= -f2- || true
}

campo() { # <cadena> <nombre>: valor de un campo "Nombre=valor;" de la cadena de conexión
  # Un campo ausente (por ejemplo Port) no es un error: devuelve vacío.
  echo "$1" | tr ';' '\n' | grep -iE "^$2=" | head -1 | cut -d= -f2- || true
}

case "${1:-}" in
  configurar)
    if [ -f "$CERT" ]; then echo "Ya existe $CERT; no se cambia (cambiarla dejaría ilegibles los respaldos anteriores)."; exit 0; fi
    install -d -m 700 /etc/dian "$DESTINO"
    PRIV=/root/dian-respaldo-privada.pem
    openssl req -x509 -newkey rsa:4096 -nodes -days 3650 -sha256 -subj "/CN=DIAN respaldo" -keyout "$PRIV" -out "$CERT" 2>/dev/null
    chmod 600 "$PRIV"; chmod 644 "$CERT"
    echo "Listo. La llave PRIVADA quedó en: $PRIV"
    echo "  1) Cópiela AHORA fuera de este servidor (caja fuerte o gestor de contraseñas)."
    echo "  2) Bórrela de aquí:  shred -u $PRIV"
    echo "Sin esa llave no se puede restaurar ningún respaldo; este servidor solo conserva la pública."
    ;;
  ejecutar)
    [ -f "$CERT" ] || { echo "Falta configurar: dian-respaldo configurar"; exit 1; }
    install -d -m 700 "$DESTINO"
    ahora="$(date +%Y%m%d-%H%M%S)"
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
    mkdir "$tmp/contenido"
    # Se copia lo que cambia a mano (almacén, llaves, XML). La caché de archivos se reconstruye sola y no se respalda.
    for d in almacen llaves trabajo; do [ -d "$DATOS/$d" ] && cp -a "$DATOS/$d" "$tmp/contenido/$d"; done
    cad="$(cadena_mysql)"
    if [ -n "$cad" ]; then
      servidor="$(campo "$cad" Server)"; puerto="$(campo "$cad" Port)"; base="$(campo "$cad" Database)"
      usuario="$(campo "$cad" Uid)"; clave="$(campo "$cad" Pwd)"
      MYSQL_PWD="$clave" mysqldump --single-transaction --no-tablespaces -h "${servidor:-127.0.0.1}" -P "${puerto:-3306}" -u "$usuario" \
        "$base" dian_clientes dian_claves dian_software dian_archivos > "$tmp/contenido/mysql-dian.sql"
    fi
    tar -czf "$tmp/datos.tar.gz" -C "$tmp/contenido" .
    salida="$DESTINO/dian-$ahora.tar.gz.enc"
    openssl cms -encrypt -binary -aes256 -outform DER -in "$tmp/datos.tar.gz" -out "$salida" "$CERT"
    openssl cms -cmsout -inform DER -in "$salida" -noout   # comprueba que el archivo cifrado es válido
    ( cd "$DESTINO" && sha256sum "$(basename "$salida")" > "$(basename "$salida").sha256" )
    echo "Respaldo creado: $salida ($(du -h "$salida" | cut -f1))"
    # Retención: se conservan los más recientes.
    ls -1t "$DESTINO"/dian-*.tar.gz.enc 2>/dev/null | tail -n +$((RETENCION + 1)) | while read -r viejo; do rm -f "$viejo" "$viejo.sha256"; echo "Borrado por antigüedad: $(basename "$viejo")"; done
    ;;
  listar) ls -lh "$DESTINO"/dian-*.tar.gz.enc 2>/dev/null || echo "No hay respaldos en $DESTINO." ;;
  restaurar)
    arch="${2:-}"; dest="${3:-}"; priv="${4:-}"
    [ -f "$arch" ] && [ -n "$dest" ] && [ -f "$priv" ] || { echo "Uso: dian-respaldo restaurar <archivo.enc> <carpeta> <llave-privada.pem>"; exit 1; }
    [ -f "$arch.sha256" ] && { ( cd "$(dirname "$arch")" && sha256sum -c "$(basename "$arch").sha256" >/dev/null ) || { echo "La suma de verificación no coincide: el respaldo está dañado."; exit 1; }; }
    mkdir -p "$dest"
    [ -z "$(ls -A "$dest")" ] || { echo "La carpeta de destino no está vacía."; exit 1; }
    plano="$(mktemp -p /var/tmp dian-restaurar.XXXXXX)"; trap 'shred -u "$plano" 2>/dev/null || rm -f "$plano"' EXIT
    openssl cms -decrypt -binary -inform DER -in "$arch" -inkey "$priv" -out "$plano" 2>/dev/null       || { echo "La llave privada no corresponde a este respaldo (o el archivo está dañado)."; exit 1; }
    tar -xzf "$plano" -C "$dest"
    echo "Restaurado en $dest. Para volver a poner en servicio:"
    echo "  systemctl stop dian-pos-api dian-admin-web"
    echo "  copie almacen, llaves y trabajo a $DATOS y ejecute: chown -R dian:dian $DATOS"
    [ -f "$dest/mysql-dian.sql" ] && echo "  (MySQL) cargue mysql-dian.sql en la base: mysql <base> < $dest/mysql-dian.sql"
    echo "  systemctl start dian-pos-api dian-admin-web"
    ;;
  *) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
