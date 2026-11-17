// Cliente de ejemplo para la API de facturación electrónica (api/documento). Node 18 o superior (fetch incluido).
// Variables de entorno: DIAN_URL y DIAN_API_KEY. Uso: node cliente.mjs ../factura.json
// Los datos del JSON son ficticios. Probar primero en habilitación.
import { readFile, writeFile } from 'node:fs/promises';

const url = (process.env.DIAN_URL ?? '').replace(/\/$/, '');
const clave = process.env.DIAN_API_KEY;
if (!url || !clave) throw new Error('Faltan DIAN_URL y/o DIAN_API_KEY');

const archivo = process.argv[2] ?? 'factura.json';
const cuerpo = await readFile(archivo, 'utf8');
const numero = JSON.parse(cuerpo).Numero;
const cabeceras = { 'x-api-key': clave };
const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

// 1. Enviar. 202 = recibido (no aprobado). 400 = documento no válido (texto plano), consulte el número si el error indica un fallo de persistencia.
let r = await fetch(`${url}/api/documento/agregar`, {
  method: 'POST',
  headers: { ...cabeceras, 'Content-Type': 'application/json' },
  body: cuerpo,
  signal: AbortSignal.timeout(60_000),
});
if (r.status === 400) throw new Error('Documento no válido: ' + (await r.text()));
if (r.status === 401) throw new Error('Clave de API inválida.');
if (!r.ok) throw new Error(`Error ${r.status}: ${await r.text()}`);
let registro = await r.json();
console.log(`Recibido ${numero}: ${registro.Estado}, CUFE ${registro.CUFE}`);

// 2. Esperar el resultado (en producción real use el webhook y deje el polling como respaldo).
for (let i = 0; i < 60 && ['Recibido', 'Error'].includes(registro.Estado); i++) {
  await esperar(5000);
  r = await fetch(`${url}/api/documento/estado/${encodeURIComponent(numero)}`, { headers: cabeceras });
  registro = await r.json();
  console.log('  estado:', registro.Estado);
}

if (registro.Estado === 'Rechazado') {
  console.log('La DIAN lo rechazó:', registro.Mensaje, registro.Errores); // corrija y reenvíe el MISMO número
  process.exit(1);
}
if (registro.Estado !== 'Aprobado') {
  console.log('Sigue pendiente:', registro.Estado);
  process.exit(2);
}

// 3. Guardar lo oficial. Tras la aprobación el PDF puede tardar un instante (503): se reintenta.
async function descargar(ruta, destino) {
  for (let i = 0; i < 6; i++) {
    const res = await fetch(`${url}/${ruta}`, { headers: cabeceras });
    if (res.ok) {
      await writeFile(destino, Buffer.from(await res.arrayBuffer()));
      console.log('  guardado', destino);
      return;
    }
    if (res.status !== 503) { console.log(`  ${ruta}: ${res.status}`); return; }
    await esperar(3000);
  }
}

const seguro = encodeURIComponent(numero);
await descargar(`api/documento/pdf/${seguro}`, `${numero}.pdf`);
await descargar(`api/documento/xml/${seguro}`, `${numero}.xml`);
await descargar(`api/documento/respuesta-dian/${seguro}`, `${numero}-respuesta.xml`);
