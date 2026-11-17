#!/usr/bin/env bash
# Reglas de ufw para DIAN. Solo AÑADE reglas con la etiqueta "DIAN": no cambia las políticas ni toca las reglas de otros servicios.
# Uso (como root):
#   dian-firewall estado
#   dian-firewall aplicar --api <origen[,origen...]|any> --admin <origen[,origen...]> [--http <origen|any>] [--habilitar]
#
# Un origen es una dirección o una red (172.31.50.1, 10.0.0.0/8). "any" abre a todo internet (solo tiene sentido para la API).
#   --api     quién puede llegar al 443 (la API; igual exige clave de API)
#   --admin   quién puede llegar al 8443 (la app Admin: nunca "any")
#   --http    quién puede llegar al 80 (solo el desafío de Let's Encrypt y la redirección a HTTPS; normalmente "any")
#   --iis     quién puede llegar al 8090 (el IIS del host que hace de proxy inverso; nunca "any")
#   --habilitar  si ufw está apagado, lo enciende; ANTES permite SSH para no dejarle fuera.
# Es repetible: reemplaza las reglas DIAN anteriores por las pedidas.
set -euo pipefail

[ "$(id -u)" -eq 0 ] || { echo "Ejecute como root (sudo)."; exit 1; }
command -v ufw >/dev/null || { echo "Falta ufw (apt install ufw)."; exit 1; }

borrar_reglas_dian() {
  # Se borran de la última a la primera para que la numeración no cambie a mitad de camino.
  local n
  for n in $(ufw status numbered | grep -F "DIAN" | sed -E 's/^\[ *([0-9]+)\].*/\1/' | sort -rn); do
    ufw --force delete "$n" >/dev/null
  done
}

permitir() { # <origen> <puerto> <etiqueta>
  if [ "$1" = "any" ]; then ufw allow "$2/tcp" comment "$3" >/dev/null
  else ufw allow from "$1" to any port "$2" proto tcp comment "$3" >/dev/null; fi
}

case "${1:-}" in
  estado) ufw status verbose ;;
  aplicar)
    shift; api=""; admin=""; http=""; iis=""; habilitar=0
    while [ $# -gt 0 ]; do
      case "$1" in
        --iis) iis="${2:-}"; shift 2 ;;
        --http) http="${2:-}"; shift 2 ;;
        --api) api="${2:-}"; shift 2 ;;
        --admin) admin="${2:-}"; shift 2 ;;
        --habilitar) habilitar=1; shift ;;
        *) echo "Opción desconocida: $1"; exit 1 ;;
      esac
    done
    [ -n "$api" ] && [ -n "$admin" ] || { echo "Indique --api y --admin."; exit 1; }
    case ",$admin," in *,any,*) echo "La app Admin no puede abrirse a 'any'."; exit 1 ;; esac
    if ! ufw status | grep -q "Status: active"; then
      [ "$habilitar" -eq 1 ] || { echo "ufw está apagado. Con --habilitar lo enciendo (permitiendo SSH antes)."; exit 1; }
      ufw allow OpenSSH >/dev/null
      echo "SSH permitido antes de encender el firewall."
    fi
    borrar_reglas_dian
    IFS=',' read -ra A <<< "$api";   for o in "${A[@]}"; do permitir "$o" 443 "DIAN API"; done
    IFS=',' read -ra D <<< "$admin"; for o in "${D[@]}"; do permitir "$o" 8443 "DIAN Admin"; done
    # Puerto 80: solo para el desafío de Let's Encrypt y la redirección a HTTPS del dominio público.
    if [ -n "$http" ]; then IFS=',' read -ra H <<< "$http"; for o in "${H[@]}"; do permitir "$o" 80 "DIAN HTTP (certificado)"; done; fi
    # Puerto 8090: entrada desde el IIS del host (reverse proxy). Nunca "any".
    if [ -n "$iis" ]; then
      case ",$iis," in *,any,*) echo "--iis no puede ser 'any'."; exit 1 ;; esac
      IFS=',' read -ra I <<< "$iis"; for o in "${I[@]}"; do permitir "$o" 8090 "DIAN IIS (proxy)"; done
    fi
    ufw --force enable >/dev/null
    echo "Reglas DIAN aplicadas:"; ufw status | grep -E "Status|DIAN" ;;
  *) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
