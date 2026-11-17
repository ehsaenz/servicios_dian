#!/usr/bin/env bash
# Certificado TLS de nginx para DIAN: estado, CA interna con renovación automática, o certificado de una autoridad externa.
# Uso (como root):
#   dian-tls estado                                    muestra de quién es el certificado, cuándo vence y cuántos días quedan
#   dian-tls ca-interna                                crea la CA interna (una sola vez) y muestra qué archivo repartir a los clientes
#   dian-tls emitir <nombre[,nombre...]> [ip[,ip...]]  emite el certificado del servidor con la CA interna, lo instala y recarga nginx
#   dian-tls renovar                                   renueva si faltan menos de 30 días (lo ejecuta el temporizador a diario)
#   dian-tls instalar-externo <crt> <key> [cadena]     instala un certificado emitido por una autoridad de confianza
#   dian-tls letsencrypt <dominio> <correo>            emite el certificado público con Let's Encrypt (se renueva solo con certbot)
set -euo pipefail

TLS=/etc/dian/tls
CA="$TLS/ca"
CRT="$TLS/dian.crt"
KEY="$TLS/dian.key"
UMBRAL_DIAS="${DIAN_TLS_UMBRAL_DIAS:-30}"   # solo se cambia en pruebas
umask 077

[ "$(id -u)" -eq 0 ] || { echo "Ejecute como root (sudo)."; exit 1; }

dias_restantes() {
  local fin
  fin="$(openssl x509 -in "$1" -noout -enddate | cut -d= -f2)"
  echo $(( ( $(date -d "$fin" +%s) - $(date +%s) ) / 86400 ))
}

origen() { [ -f "$TLS/origen.txt" ] && cat "$TLS/origen.txt" || echo "autofirmado"; }

recargar_nginx() {
  if nginx -t 2>/dev/null; then systemctl reload nginx; echo "nginx recargado."
  else echo "nginx rechazó la configuración; el certificado NO se aplicó."; nginx -t || true; return 1; fi
}

instalar_par() { # <crt nuevo> <key nueva> <origen>
  install -m 644 "$1" "$CRT.nuevo"
  install -m 600 "$2" "$KEY.nuevo"
  [ -f "$CRT" ] && cp -p "$CRT" "$CRT.anterior"
  [ -f "$KEY" ] && cp -p "$KEY" "$KEY.anterior"
  mv "$CRT.nuevo" "$CRT"; mv "$KEY.nuevo" "$KEY"
  echo "$3" > "$TLS/origen.txt"
  if ! recargar_nginx; then
    [ -f "$CRT.anterior" ] && mv "$CRT.anterior" "$CRT"; [ -f "$KEY.anterior" ] && mv "$KEY.anterior" "$KEY"
    echo "Se restauró el certificado anterior."; return 1
  fi
  rm -f "$CRT.anterior" "$KEY.anterior"
}

crear_ca() {
  if [ -f "$CA/ca.key" ]; then echo "La CA interna ya existe: $CA/ca.crt"; return; fi
  mkdir -p "$CA"; chmod 700 "$CA"
  openssl req -x509 -newkey rsa:4096 -nodes -days 3650 -sha256 -subj "/CN=DIAN Unificado CA interna" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign" \
    -keyout "$CA/ca.key" -out "$CA/ca.crt" 2>/dev/null
  chmod 600 "$CA/ca.key"
  echo "CA interna creada (vale 10 años). Reparta SOLO este archivo a los equipos que deben confiar en el servidor:"
  echo "  $CA/ca.crt"
  echo "La llave $CA/ca.key no debe salir del servidor."
}

emitir() { # <nombres,csv> [ips,csv]
  [ -f "$CA/ca.key" ] || { echo "Primero cree la CA interna: dian-tls ca-interna"; return 1; }
  local nombres="$1" ips="${2:-}" san="" primero tmp
  primero="${nombres%%,*}"
  IFS=',' read -ra N <<< "$nombres"; for n in "${N[@]}"; do san+="DNS:$n,"; done
  if [ -n "$ips" ]; then IFS=',' read -ra I <<< "$ips"; for i in "${I[@]}"; do san+="IP:$i,"; done; fi
  san="${san%,}"
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
  openssl req -new -newkey rsa:3072 -nodes -subj "/CN=$primero" -keyout "$tmp/s.key" -out "$tmp/s.csr" 2>/dev/null
  printf 'subjectAltName=%s\nbasicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n' "$san" > "$tmp/ext"
  openssl x509 -req -in "$tmp/s.csr" -CA "$CA/ca.crt" -CAkey "$CA/ca.key" -CAcreateserial -days 825 -sha256 -extfile "$tmp/ext" -out "$tmp/s.crt" 2>/dev/null
  instalar_par "$tmp/s.crt" "$tmp/s.key" "ca-interna"
  printf '%s\n%s\n' "$nombres" "$ips" > "$TLS/sans.txt"
  echo "Certificado emitido para: $san (vence $(openssl x509 -in "$CRT" -noout -enddate | cut -d= -f2))."
}

case "${1:-}" in
  estado)
    echo "Sujeto : $(openssl x509 -in "$CRT" -noout -subject | sed 's/^subject=//')"
    echo "Emisor : $(openssl x509 -in "$CRT" -noout -issuer | sed 's/^issuer=//')"
    echo "Origen : $(origen)"
    echo "Vence  : $(openssl x509 -in "$CRT" -noout -enddate | cut -d= -f2)"
    d="$(dias_restantes "$CRT")"; echo "Quedan : $d días"
    if [ "$(origen)" = "autofirmado" ]; then echo "AVISO: certificado autofirmado, solo para pruebas."; fi
    [ "$d" -ge "$UMBRAL_DIAS" ] || { echo "AVISO: vence en menos de $UMBRAL_DIAS días."; exit 1; }
    ;;
  ca-interna) crear_ca ;;
  emitir)
    [ -n "${2:-}" ] || { echo "Uso: dian-tls emitir <nombre[,nombre...]> [ip[,ip...]]"; exit 1; }
    crear_ca >/dev/null; emitir "$2" "${3:-}" ;;
  renovar)
    d="$(dias_restantes "$CRT")"
    if [ "$d" -ge "$UMBRAL_DIAS" ]; then echo "Certificado vigente ($d días). Nada que renovar."; exit 0; fi
    case "$(origen)" in
      ca-interna)
        [ -f "$TLS/sans.txt" ] || { echo "No hay sans.txt; emita de nuevo con: dian-tls emitir <nombres> [ips]"; exit 1; }
        echo "Quedan $d días: renovando con la CA interna."
        emitir "$(sed -n 1p "$TLS/sans.txt")" "$(sed -n 2p "$TLS/sans.txt")" ;;
      letsencrypt)
        # Lo renueva el temporizador de certbot (y su deploy-hook lo instala aquí). Si llegamos a este punto con menos de
        # 30 días, la renovación automática está fallando: se avisa en el registro.
        echo "ATENCIÓN: el certificado de Let's Encrypt vence en $d días y certbot no lo ha renovado. Revise: systemctl status certbot.timer; certbot renew --dry-run"
        exit 0 ;;
      *)
        echo "ATENCIÓN: el certificado ($(origen)) vence en $d días y no se renueva solo. Instale uno nuevo con: dian-tls instalar-externo <crt> <key> [cadena]"
        exit 0 ;;
    esac ;;
  letsencrypt)
    # Emite (o renueva) el certificado público con Let's Encrypt por el desafío HTTP-01. Requisitos: certbot instalado,
    # el DNS del dominio apuntando a este servidor y el puerto 80 abierto (dian-firewall aplicar ... --http any).
    dominio="${2:-}"; correo="${3:-}"
    [ -n "$dominio" ] && [ -n "$correo" ] || { echo "Uso: dian-tls letsencrypt <dominio> <correo del responsable>"; exit 1; }
    command -v certbot >/dev/null || { echo "Falta certbot (apt install certbot)."; exit 1; }
    install -d -m 755 /var/www/dian-acme
    # --deploy-hook: certbot lo ejecuta tras CADA emisión y renovación, también las automáticas de su propio temporizador.
    certbot certonly --webroot -w /var/www/dian-acme -d "$dominio" -m "$correo" --agree-tos --no-eff-email \
      --keep-until-expiring --non-interactive --deploy-hook "/usr/local/sbin/dian-tls aplicar-letsencrypt"
    RENEWED_LINEAGE="/etc/letsencrypt/live/$dominio" "$0" aplicar-letsencrypt ;;
  aplicar-letsencrypt)
    # Lo llama certbot (deploy-hook) con RENEWED_LINEAGE=/etc/letsencrypt/live/<dominio>.
    linaje="${RENEWED_LINEAGE:-}"
    [ -f "$linaje/fullchain.pem" ] && [ -f "$linaje/privkey.pem" ] || { echo "No encuentro el certificado en '$linaje'."; exit 1; }
    instalar_par "$linaje/fullchain.pem" "$linaje/privkey.pem" "letsencrypt"
    echo "Certificado de Let's Encrypt instalado (vence $(openssl x509 -in "$CRT" -noout -enddate | cut -d= -f2))." ;;
  instalar-externo)
    crt="${2:-}"; key="${3:-}"; cadena="${4:-}"
    [ -f "$crt" ] && [ -f "$key" ] || { echo "Uso: dian-tls instalar-externo <crt> <key> [cadena]"; exit 1; }
    a="$(openssl x509 -in "$crt" -noout -pubkey | openssl sha256)"; b="$(openssl pkey -in "$key" -pubout | openssl sha256)"
    [ "$a" = "$b" ] || { echo "La llave no corresponde al certificado."; exit 1; }
    openssl x509 -in "$crt" -noout -checkend 0 >/dev/null || { echo "El certificado ya está vencido."; exit 1; }
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
    cp "$crt" "$tmp/c.crt"; [ -n "$cadena" ] && { echo >> "$tmp/c.crt"; cat "$cadena" >> "$tmp/c.crt"; }
    cp "$key" "$tmp/c.key"
    instalar_par "$tmp/c.crt" "$tmp/c.key" "externo"
    echo "Certificado externo instalado (vence $(openssl x509 -in "$CRT" -noout -enddate | cut -d= -f2))." ;;
  *) sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
