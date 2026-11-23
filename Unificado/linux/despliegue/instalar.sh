#!/usr/bin/env bash
# Instala o actualiza la API DIAN y la app Admin como servicios de systemd detrás de nginx con HTTPS.
# Uso (como root):  sudo ./instalar.sh <carpeta con Pos.Api y Admin.Web publicados>
# Se puede repetir: actualiza los binarios y reinicia, sin tocar los datos, la configuración ni el certificado.
set -euo pipefail

ORIGEN="${1:-}"
AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

[ "$(id -u)" -eq 0 ] || { echo "Ejecute como root (sudo)."; exit 1; }
# Un paquete armado en Windows no conserva el permiso de ejecución: se lo damos a los dos ejecutables.
[ -n "$ORIGEN" ] && [ -f "$ORIGEN/Pos.Api/Pos.Api" ] && [ -f "$ORIGEN/Admin.Web/Admin.Web" ] &&   chmod +x "$ORIGEN/Pos.Api/Pos.Api" "$ORIGEN/Admin.Web/Admin.Web" || {
  echo "Uso: sudo ./instalar.sh <carpeta con Pos.Api/ y Admin.Web/ publicados para linux-x64>"; exit 1; }
command -v nginx >/dev/null || { echo "Falta nginx (apt install nginx)."; exit 1; }
command -v openssl >/dev/null || { echo "Falta openssl."; exit 1; }

echo "== Usuario y carpetas =="
id dian >/dev/null 2>&1 || useradd --system --home-dir /var/lib/dian --shell /usr/sbin/nologin dian
install -d -m 755 -o root -g root /opt/dian /opt/dian/pos-api /opt/dian/admin-web /etc/dian
install -d -m 700 -o dian -g dian /var/lib/dian /var/lib/dian/almacen /var/lib/dian/llaves /var/lib/dian/trabajo /var/lib/dian/cache
install -d -m 700 -o root -g root /etc/dian/tls
install -d -m 755 -o root -g root /var/www/dian-acme

echo "== Binarios =="
# En una actualización los servicios están en marcha: se detienen para poder reemplazar los ejecutables.
systemctl stop dian-pos-api dian-admin-web 2>/dev/null || true
# Los binarios son de root y de solo lectura para el usuario del servicio: un fallo en la aplicación no puede reescribirlos.
cp -a --no-preserve=ownership "$ORIGEN/Pos.Api/." /opt/dian/pos-api/
cp -a --no-preserve=ownership "$ORIGEN/Admin.Web/." /opt/dian/admin-web/
chown -R root:root /opt/dian
chmod -R go-w /opt/dian
chmod 755 /opt/dian/pos-api/Pos.Api /opt/dian/admin-web/Admin.Web

echo "== Configuración (/etc/dian/dian.env) =="
if [ ! -f /etc/dian/dian.env ]; then
  cat > /etc/dian/dian.env <<'ENV'
# Variables de la API y de la app Admin. Este archivo es solo de root: puede traer la cadena de MySQL.
Almacen__Carpeta=/var/lib/dian/almacen
Secretos__Llaves=/var/lib/dian/llaves
Datos__Carpeta=/var/lib/dian/trabajo
Cache__Carpeta=/var/lib/dian/cache
# Para usar MySQL en lugar del almacén de archivos, descomente y complete (la base en utf8mb4):
# MySql__Cadena=Server=127.0.0.1;Database=dian_api;Uid=dian;Pwd=CAMBIAR;CharSet=utf8mb4
ENV
  chmod 600 /etc/dian/dian.env
  echo "creado /etc/dian/dian.env"
else
  echo "se conserva /etc/dian/dian.env"
fi

echo "== Certificado TLS =="
if [ ! -f /etc/dian/tls/dian.crt ]; then
  NOMBRE="$(hostname -f 2>/dev/null || hostname)"
  openssl req -x509 -newkey rsa:3072 -nodes -days 825 -sha256 -subj "/CN=$NOMBRE" \
    -addext "subjectAltName=DNS:$NOMBRE,DNS:localhost,IP:127.0.0.1" \
    -keyout /etc/dian/tls/dian.key -out /etc/dian/tls/dian.crt 2>/dev/null
  chmod 600 /etc/dian/tls/dian.key
  echo "AVISO: se creó un certificado AUTOFIRMADO para pruebas. En producción reemplace /etc/dian/tls/dian.crt y dian.key"
  echo "       por uno emitido por una autoridad de confianza y ejecute: systemctl reload nginx"
else
  echo "se conserva el certificado de /etc/dian/tls"
fi

echo "== Servicios systemd =="
install -m 644 "$AQUI/dian-pos-api.service" /etc/systemd/system/dian-pos-api.service
install -m 644 "$AQUI/dian-admin-web.service" /etc/systemd/system/dian-admin-web.service
systemctl daemon-reload
systemctl enable dian-pos-api dian-admin-web >/dev/null
systemctl restart dian-pos-api dian-admin-web

echo "== Herramientas de TLS, respaldo y firewall =="
install -m 700 "$AQUI/dian-tls.sh" /usr/local/sbin/dian-tls
install -m 700 "$AQUI/dian-respaldo.sh" /usr/local/sbin/dian-respaldo
install -m 700 "$AQUI/dian-firewall.sh" /usr/local/sbin/dian-firewall
for u in dian-tls.service dian-tls.timer dian-respaldo.service dian-respaldo.timer; do install -m 644 "$AQUI/$u" "/etc/systemd/system/$u"; done
systemctl daemon-reload
systemctl enable --now dian-tls.timer dian-respaldo.timer >/dev/null
install -d -m 700 /var/backups/dian

echo "== nginx =="
CONF=/etc/nginx/conf.d/dian.conf
[ -f "$CONF" ] && cp "$CONF" "$CONF.anterior" || true
install -m 644 "$AQUI/dian-nginx.conf" "$CONF"
if nginx -t 2>/dev/null; then
  systemctl reload nginx
else
  echo "nginx rechazó la configuración; se deja la anterior."
  nginx -t || true
  if [ -f "$CONF.anterior" ]; then mv "$CONF.anterior" "$CONF"; else rm -f "$CONF"; fi
  exit 1
fi
rm -f "$CONF.anterior"

echo "== Comando de administración =="
cat > /usr/local/sbin/dian-admin <<'SH'
#!/usr/bin/env bash
# Ejecuta la app Admin en modo comando (crear-admin, telefono-admin, importar-cliente, crear-esquema, migrar-almacen) como el usuario del servicio.
# Uso: sudo dian-admin <comando> [argumentos]
set -euo pipefail
# Se lee línea a línea y SIN interpretación del shell: la cadena de MySQL lleva ";" y "source" la cortaría.
while IFS= read -r linea || [ -n "$linea" ]; do
  case "$linea" in ''|\#*) continue ;; esac
  export "${linea%%=*}=${linea#*=}"
done < /etc/dian/dian.env
exec runuser -u dian --preserve-environment -- /opt/dian/admin-web/Admin.Web "$@"
SH
chmod 700 /usr/local/sbin/dian-admin

echo "== Comprobación =="
sleep 4
for s in dian-pos-api dian-admin-web nginx; do printf '%-16s %s\n' "$s" "$(systemctl is-active "$s")"; done
printf 'API  https://127.0.0.1/salud       -> HTTP %s\n' "$(curl -sk -o /dev/null -w '%{http_code}' https://127.0.0.1/salud)"
printf 'Admin https://127.0.0.1:8443/Login -> HTTP %s\n' "$(curl -sk -o /dev/null -w '%{http_code}' https://127.0.0.1:8443/Login)"
echo "Listo. Siguientes pasos:"
echo "  sudo dian-admin crear-admin <usuario>          primer administrador"
echo "  sudo dian-admin telefono-admin <usuario> <celular>   celular del segundo factor"
echo "  sudo dian-tls estado                           revisar el certificado (hoy autofirmado)"
echo "  sudo dian-respaldo configurar                  activar los respaldos cifrados diarios"
echo "  sudo dian-firewall aplicar --api <origen> --admin <origen>   abrir 443 y 8443 solo a quien corresponde"
