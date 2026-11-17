"""Receptor de webhook de ejemplo (Python 3.9+ y `pip install flask`).

Variable de entorno: DIAN_WEBHOOK_SECRETO = el secreto de firma que le entregó el portal (se muestra una sola vez).
Ponga este servicio detrás de HTTPS (proxy inverso) y registre su URL en el portal de clientes.
"""
import hashlib
import hmac
import json
import os

from flask import Flask, abort, request

secreto = os.environ.get("DIAN_WEBHOOK_SECRETO")
if not secreto:
    raise SystemExit("Falta DIAN_WEBHOOK_SECRETO")

app = Flask(__name__)
procesados = set()  # idempotencia: en producción, guárdelo en su base de datos


def firma_valida(cuerpo_crudo: bytes, cabecera: str) -> bool:
    # La clave HMAC es el TEXTO del secreto en UTF-8 (no se decodifica de Base64).
    esperada = "sha256=" + hmac.new(secreto.encode("utf-8"), cuerpo_crudo, hashlib.sha256).hexdigest()
    return hmac.compare_digest(esperada, cabecera or "")


@app.post("/dian/aviso")
def aviso():
    crudo = request.get_data()  # la firma se calcula sobre los BYTES EXACTOS recibidos
    if not firma_valida(crudo, request.headers.get("X-Dian-Firma", "")):
        abort(401)  # firma inválida: no procesar

    datos = json.loads(crudo.decode("utf-8"))
    llave = (datos["Numero"], datos["Estado"], datos["SecuenciaCambio"])
    if llave not in procesados:
        procesados.add(llave)
        # El aviso es solo una pista: confirme el estado con GET /api/documento/estado/{numero}
        # antes de actuar, y baje el PDF/XML. Haga ese trabajo FUERA de esta petición (cola/trabajador).
        print("Aviso:", datos["Evento"], datos["Numero"], datos["Estado"], "Pdf=", datos["Pdf"])

    return "", 200  # cualquier 2xx = entregado; responda rápido (el servicio espera 10 s)


if __name__ == "__main__":
    app.run(port=8080)
