#!/usr/bin/env bash
# Prueba de las bibliotecas DIAN.Unificado (.NET 10) en Linux.
#
#   ./probar-linux.sh diagnostico    Mira el entorno. No usa el certificado ni la red.
#   ./probar-linux.sh local          Genera y firma documentos aquí y verifica las firmas. No envía nada.
#   ./probar-linux.sh habilitacion   Hace lo de "local" y además envía a la HABILITACIÓN de la DIAN (pide confirmación).
#
# Requisitos: carpeta config-local junto a este script (ver LEEME-LINUX.md) y la variable MARLIT_P12 con la
# clave del certificado. La clave nunca se imprime ni se guarda.
#
# Deja el resultado completo en resultado-linux.txt para poder compartirlo.

set -u
cd "$(dirname "$0")" || exit 1

ETAPA="${1:-}"
LOG="resultado-linux.txt"
FALLAS=0
PASOS=0

HAB="./Habilitacion.Pruebas"
FIR="./FirmaXades.Pruebas"

registrar() { echo "$*" | tee -a "$LOG"; }

# Ejecuta un comando, guarda su salida en el registro y cuenta si falló.
paso() {
    local titulo="$1"; shift
    PASOS=$((PASOS + 1))
    registrar ""
    registrar "=== $titulo"
    local salida
    salida="$("$@" 2>&1)"
    local codigo=$?
    echo "$salida" | tee -a "$LOG"
    if [ "$codigo" -ne 0 ]; then
        registrar ">>> FALLÓ (código $codigo)"
        FALLAS=$((FALLAS + 1))
    else
        registrar ">>> correcto"
    fi
    return "$codigo"
}

verificar_firma() {
    paso "Verificación independiente de la firma: $1" "$FIR" verificar "config-local/salida/$1-firmado.xml"
}

diagnostico() {
    registrar "=== DIAGNÓSTICO DEL ENTORNO ($(date -u '+%Y-%m-%d %H:%M:%S') UTC)"
    registrar "Sistema:      $(uname -srm)"
    if [ -r /etc/os-release ]; then registrar "Distribución: $(. /etc/os-release; echo "$PRETTY_NAME")"; fi
    registrar "Hora local:   $(date '+%Y-%m-%d %H:%M:%S %z') (zona: ${TZ:-$(cat /etc/timezone 2>/dev/null || echo 'sin definir')})"
    if [ -e /usr/share/zoneinfo/America/Bogota ]; then
        registrar "tzdata:       America/Bogota disponible"
    else
        registrar "tzdata:       FALTA America/Bogota (instalar el paquete tzdata)"
    fi
    if command -v ldconfig >/dev/null 2>&1 && ldconfig -p 2>/dev/null | grep -qi 'libicuuc'; then
        registrar "ICU:          instalada"
    else
        registrar "ICU:          no se encontró libicu; si .NET falla al iniciar, definir DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"
    fi
    if command -v openssl >/dev/null 2>&1; then registrar "OpenSSL:      $(openssl version)"; else registrar "OpenSSL:      no encontrado"; fi
    registrar "Globalización invariante: ${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-no definida}"
    registrar "Certificado:  $([ -f config-local/certificado.p12 ] && echo 'config-local/certificado.p12 presente' || echo 'FALTA config-local/certificado.p12')"
    registrar "Clave:        $([ -n "${MARLIT_P12:-}" ] && echo 'MARLIT_P12 definida' || echo 'FALTA la variable MARLIT_P12')"
    for archivo in emisor-marlit habilitacion-sw-propio emisor-rut documento-soporte nomina pos; do
        registrar "Config:       $archivo.json $([ -f "config-local/$archivo.json" ] && echo presente || echo FALTA)"
    done
}

requisitos() {
    local falta=0
    [ -f config-local/certificado.p12 ] || { echo "Falta config-local/certificado.p12"; falta=1; }
    [ -n "${MARLIT_P12:-}" ] || { echo "Falta la variable MARLIT_P12 (clave del certificado)"; falta=1; }
    [ -f config-local/emisor-marlit.json ] || { echo "Falta config-local/emisor-marlit.json"; falta=1; }
    [ "$falta" -eq 0 ] || exit 2
    chmod +x "$HAB" "$FIR" 2>/dev/null || true
}

local_() {
    requisitos
    paso "Revisión del certificado en Linux" env FIRMA_PFX_CLAVE="$MARLIT_P12" "$FIR" certificado-info config-local/certificado.p12

    paso "Generar y firmar FACTURA SETP990000509 (sin enviar)" "$HAB" generar --tipo factura --consecutivo 990000509
    verificar_firma SETP990000509

    paso "Generar y firmar DOCUMENTO SOPORTE SEDS984000507 (sin enviar)" "$HAB" generar --tipo documento-soporte --consecutivo 984000507
    verificar_firma SEDS984000507

    paso "Generar y firmar DOCUMENTO EQUIVALENTE POS EPOS4 (sin enviar)" "$HAB" generar --tipo documento-equivalente --consecutivo 4
    verificar_firma EPOS4

    paso "Generar y firmar NÓMINA NOM1003 (sin enviar)" "$HAB" generar --tipo nomina --consecutivo 1003
    verificar_firma NOM1003
}

habilitacion() {
    local_

    registrar ""
    registrar "Ahora se enviarán 4 documentos de PRUEBA a la HABILITACIÓN de la DIAN (no a producción):"
    registrar "factura SETP990000509, documento soporte SEDS984000507, documento equivalente POS EPOS4 y nómina NOM1003."
    printf 'Escribe SI para continuar: '
    read -r respuesta
    if [ "$respuesta" != "SI" ]; then
        registrar "Envío cancelado."
        return
    fi

    paso "Consulta de rangos (solo lectura)" "$HAB" rangos
    paso "Enviar FACTURA SETP990000509 a habilitación" "$HAB" enviar-sync --tipo factura --consecutivo 990000509 --confirmar
    paso "Enviar DOCUMENTO SOPORTE SEDS984000507 a habilitación" "$HAB" enviar-sync --tipo documento-soporte --consecutivo 984000507 --confirmar
    paso "Enviar DOCUMENTO EQUIVALENTE POS EPOS4 a habilitación" "$HAB" enviar-sync --tipo documento-equivalente --consecutivo 4 --confirmar
    paso "Enviar NÓMINA NOM1003 al set de pruebas de nómina" "$HAB" enviar --tipo nomina --consecutivo 1003 --confirmar
}

: > "$LOG"
case "$ETAPA" in
    diagnostico) diagnostico ;;
    local)       diagnostico; local_ ;;
    habilitacion) diagnostico; habilitacion ;;
    *)
        echo "Uso: $0 diagnostico | local | habilitacion"
        exit 2
        ;;
esac

if [ "$PASOS" -gt 0 ]; then
    registrar ""
    registrar "=== RESUMEN: $PASOS pasos, $FALLAS con fallas."
fi
registrar "Resultado completo en: $(pwd)/$LOG"
[ "$FALLAS" -eq 0 ]
