#!/usr/bin/env bash
# Prueba en vivo de la entrada pública: un OPERADOR no debe poder iniciar sesión por el puerto 8090 (el que usa el IIS),
# y sí por el 8443 (interno). Ejecutar EN la VM del servicio, con sudo disponible: bash probar-entrada-publica.sh
# Crea un administrador temporal con una clave aleatoria (no se imprime) y lo borra al final.
# Se NIEGA a correr si ya existen administradores: borraría el archivo de usuarios.
if sudo -n test -f /var/lib/dian/almacen/admin-usuarios.json; then
  echo "Ya hay administradores en este servidor; esta prueba no se ejecuta para no tocarlos."; exit 1
fi
U=prueba-tmp
P="$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 18)Aa1"
printf '%s' "$P" | sudo -n dian-admin crear-admin "$U" >/dev/null 2>&1 && echo "admin temporal creado"
login() { # <url base> <jar> [extra curl args]
  local base="$1" jar="$2"; shift 2
  local t; t=$(curl -sk -c "$jar" "$base/Login" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | sed 's/.*value="//;s/"$//')
  curl -sk -b "$jar" -c "$jar" -o /tmp/resp.html -w "%{http_code} %{redirect_url}" --data-urlencode "usuario=$U" --data-urlencode "clave=$P" --data-urlencode "__RequestVerificationToken=$t" "$@" "$base/Login"
}
echo "1) Operador por entrada PUBLICA (8090): $(login http://127.0.0.1:8090 /tmp/j1) ; mensaje: $(grep -o 'incorrectos' /tmp/resp.html | head -1)"
echo "2) Operador por entrada INTERNA (8443):  $(login https://127.0.0.1:8443 /tmp/j2)"
echo "3) Cookie de operador (interna) usada en la PUBLICA: $(curl -sk -b /tmp/j2 -o /dev/null -w '%{http_code}' http://127.0.0.1:8090/)"
echo "4) Misma cookie en la INTERNA: $(curl -sk -b /tmp/j2 -o /dev/null -w '%{http_code}' https://127.0.0.1:8443/)"
echo "5) Cabecera X-Dian-Entrada falsificada desde afuera (interna) en la PUBLICA: $(login http://127.0.0.1:8090 /tmp/j3 -H 'X-Dian-Entrada: interna')"
# limpieza: el almacén no tenía administradores antes de esta prueba
sudo -n rm -f /var/lib/dian/almacen/admin-usuarios.json && echo "admin temporal borrado"
rm -f /tmp/j1 /tmp/j2 /tmp/j3 /tmp/resp.html
