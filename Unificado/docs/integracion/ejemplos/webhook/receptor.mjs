// Receptor de webhook de ejemplo (Node 18+, sin dependencias).
// Variable de entorno: DIAN_WEBHOOK_SECRETO = el secreto de firma que le entregó el portal (se muestra una sola vez).
// Ponga este servicio detrás de HTTPS (proxy inverso) y registre su URL en el portal de clientes.
import { createServer } from 'node:http';
import { createHmac, timingSafeEqual } from 'node:crypto';

const secreto = process.env.DIAN_WEBHOOK_SECRETO;
if (!secreto) throw new Error('Falta DIAN_WEBHOOK_SECRETO');

// Idempotencia: el aviso puede llegar más de una vez. En producción guárdelo en su base de datos.
const procesados = new Set();

function firmaValida(cuerpoCrudo, cabecera) {
  // La clave HMAC es el TEXTO del secreto en UTF-8 (no se decodifica de Base64).
  const esperada = 'sha256=' + createHmac('sha256', secreto).update(cuerpoCrudo).digest('hex');
  const a = Buffer.from(esperada);
  const b = Buffer.from(cabecera ?? '');
  return a.length === b.length && timingSafeEqual(a, b);
}

createServer((req, res) => {
  if (req.method !== 'POST' || req.url !== '/dian/aviso') {
    res.writeHead(404).end();
    return;
  }

  const trozos = [];
  req.on('data', (t) => trozos.push(t));
  req.on('end', () => {
    const crudo = Buffer.concat(trozos); // la firma se calcula sobre los BYTES EXACTOS recibidos
    if (!firmaValida(crudo, req.headers['x-dian-firma'])) {
      res.writeHead(401).end(); // firma inválida: no procesar
      return;
    }

    const aviso = JSON.parse(crudo.toString('utf8'));
    const llave = `${aviso.Numero}|${aviso.Estado}|${aviso.SecuenciaCambio}`;
    if (!procesados.has(llave)) {
      procesados.add(llave);
      // El aviso es solo una pista: confirme el estado con GET /api/documento/estado/{numero}
      // antes de actuar, y baje el PDF/XML. Haga ese trabajo FUERA de esta petición (cola/trabajador).
      console.log('Aviso:', aviso.Evento, aviso.Numero, aviso.Estado, 'Pdf=' + aviso.Pdf);
    }

    res.writeHead(200).end(); // cualquier 2xx = entregado; responda rápido (el servicio espera 10 s)
  });
}).listen(8080, () => console.log('Receptor en :8080/dian/aviso'));
