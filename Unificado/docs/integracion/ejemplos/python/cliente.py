"""Cliente de ejemplo para la API de facturación electrónica (api/documento).

Python 3.9+ y `pip install requests`.
Variables de entorno: DIAN_URL y DIAN_API_KEY. Uso: python cliente.py ../factura.json
Los datos del JSON son ficticios. Probar primero en habilitación.
"""
import json
import os
import sys
import time
from urllib.parse import quote

import requests

url = os.environ.get("DIAN_URL", "").rstrip("/")
clave = os.environ.get("DIAN_API_KEY")
if not url or not clave:
    sys.exit("Faltan DIAN_URL y/o DIAN_API_KEY")

archivo = sys.argv[1] if len(sys.argv) > 1 else "factura.json"
with open(archivo, "rb") as f:
    cuerpo = f.read()
numero = json.loads(cuerpo.decode("utf-8"))["Numero"]
cabeceras = {"x-api-key": clave}

# 1. Enviar. 202 = recibido (no aprobado). 400 = documento no válido (texto plano), consulte el número si el error indica un fallo de persistencia.
r = requests.post(
    f"{url}/api/documento/agregar",
    headers={**cabeceras, "Content-Type": "application/json"},
    data=cuerpo,
    timeout=60,
)
if r.status_code == 400:
    sys.exit("Documento no válido: " + r.text)
if r.status_code == 401:
    sys.exit("Clave de API inválida.")
r.raise_for_status()
registro = r.json()
print(f"Recibido {numero}: {registro['Estado']}, CUFE {registro['CUFE']}")

# 2. Esperar el resultado (en producción real use el webhook y deje el polling como respaldo).
for _ in range(60):
    if registro["Estado"] not in ("Recibido", "Error"):
        break
    time.sleep(5)
    registro = requests.get(f"{url}/api/documento/estado/{quote(numero, safe='')}", headers=cabeceras, timeout=30).json()
    print("  estado:", registro["Estado"])

if registro["Estado"] == "Rechazado":
    print("La DIAN lo rechazó:", registro["Mensaje"], registro["Errores"])  # corrija y reenvíe el MISMO número
    sys.exit(1)
if registro["Estado"] != "Aprobado":
    print("Sigue pendiente:", registro["Estado"])
    sys.exit(2)


# 3. Guardar lo oficial. Tras la aprobación el PDF puede tardar un instante (503): se reintenta.
def descargar(ruta: str, destino: str) -> None:
    for _ in range(6):
        res = requests.get(f"{url}/{ruta}", headers=cabeceras, timeout=60)
        if res.ok:
            with open(destino, "wb") as f:
                f.write(res.content)
            print("  guardado", destino)
            return
        if res.status_code != 503:
            print(f"  {ruta}: {res.status_code}")
            return
        time.sleep(3)


seguro = quote(numero, safe="")
descargar(f"api/documento/pdf/{seguro}", f"{numero}.pdf")
descargar(f"api/documento/xml/{seguro}", f"{numero}.xml")
descargar(f"api/documento/respuesta-dian/{seguro}", f"{numero}-respuesta.xml")
