# Ejemplos con curl

Última revisión: 2026-11-16

## bash

```bash
export DIAN_URL="https://services.sistemas360.com:47125"
export DIAN_API_KEY="<SU_CLAVE_DE_API>"

# 1. ¿Está vivo? (no necesita clave)
curl -s "$DIAN_URL/salud"
# {"estado":"ok"}

# 2. ¿Con qué cliente me identifica mi clave?
curl -s -H "x-api-key: $DIAN_API_KEY" "$DIAN_URL/api/Servicio/Quien"

# 3. Enviar una factura (responde 202 con el registro; Estado normalmente "Recibido")
curl -s -i -X POST "$DIAN_URL/api/documento/agregar" \
  -H "x-api-key: $DIAN_API_KEY" \
  -H "Content-Type: application/json" \
  --data-binary @factura.json

# 4. Consultar el estado (repita hasta Aprobado o Rechazado), o use el webhook
curl -s -H "x-api-key: $DIAN_API_KEY" "$DIAN_URL/api/documento/estado/SETP990001001"

# 5. Polling por cursor: lo que cambió desde la última vez
curl -s -H "x-api-key: $DIAN_API_KEY" "$DIAN_URL/api/documento/cambios?desde=0&limite=100"

# 6. Descargar el PDF oficial (solo aprobados; 409 si no lo está), el XML firmado y la respuesta de la DIAN
curl -s -f -H "x-api-key: $DIAN_API_KEY" -o SETP990001001.pdf "$DIAN_URL/api/documento/pdf/SETP990001001"
curl -s -f -H "x-api-key: $DIAN_API_KEY" -o SETP990001001.xml "$DIAN_URL/api/documento/xml/SETP990001001"
curl -s -f -H "x-api-key: $DIAN_API_KEY" -o SETP990001001-respuesta.xml "$DIAN_URL/api/documento/respuesta-dian/SETP990001001"

# 7. Forzar el reintento de un documento Recibido/Rechazado/Error (síncrono: espera a la DIAN)
curl -s -m 200 -X POST -H "x-api-key: $DIAN_API_KEY" "$DIAN_URL/api/documento/reintentar/SETP990001001"

# 8. Nota crédito y nota débito: igual que la factura
curl -s -i -X POST "$DIAN_URL/api/documento/agregar" -H "x-api-key: $DIAN_API_KEY" -H "Content-Type: application/json" --data-binary @nota-credito.json
curl -s -i -X POST "$DIAN_URL/api/documento/agregar" -H "x-api-key: $DIAN_API_KEY" -H "Content-Type: application/json" --data-binary @nota-debito.json
```

## PowerShell (Windows)

Use `curl.exe` (no el alias `curl`, que en Windows PowerShell 5.1 es `Invoke-WebRequest`).

```powershell
$env:DIAN_URL = "https://services.sistemas360.com:47125"
$env:DIAN_API_KEY = "<SU_CLAVE_DE_API>"

curl.exe -s -i -X POST "$env:DIAN_URL/api/documento/agregar" `
  -H "x-api-key: $env:DIAN_API_KEY" `
  -H "Content-Type: application/json" `
  --data-binary "@factura.json"

curl.exe -s -H "x-api-key: $env:DIAN_API_KEY" "$env:DIAN_URL/api/documento/estado/SETP990001001"
curl.exe -s -f -H "x-api-key: $env:DIAN_API_KEY" -o SETP990001001.pdf "$env:DIAN_URL/api/documento/pdf/SETP990001001"
```

## Cómo leer los códigos de salida

| Resultado | Significado |
|---|---|
| `202` en `agregar` | Recibido y en cola hacia la DIAN. No es aprobación. |
| `400` en `agregar` | El documento no es válido; el cuerpo trae el motivo. No se guardó nada: corríjalo y reenvíe. |
| `401` | Clave inválida. |
| `404` en `estado` | Ese número no existe para su cliente (o `agregar` lo rechazó al instante). |
| `409` en `pdf` | El documento no está aprobado. |
| `503` en `pdf` | Aprobado, pero el PDF no se pudo generar; reintente luego. |
