# Lista de verificación del integrador (antes de pedir producción)

Última revisión: 2026-11-16

Marque cada punto en **habilitación**. Cuando todo esté cumplido, pida el paso a producción al operador de SISTEMAS 360 (ver [ambientes.md](ambientes.md#paso-a-producción-lo-que-hace-usted-y-lo-que-hace-el-operador)). Guarde la evidencia (capturas, números de documento) para su propia auditoría.

## 1. Conexión y seguridad

- [ ] `GET /salud` responde 200 desde el servidor donde corre su ERP (no solo desde su PC).
- [ ] `GET /api/Servicio/Quien` con su clave devuelve **su** cliente y **su** NIT.
- [ ] La clave de API está en una bóveda o variable de entorno; no está en el código, en el repositorio ni en los registros.
- [ ] Todo el tráfico es por **HTTPS** y su cliente valida el certificado del servidor (sin desactivar la validación).
- [ ] Un 401 se trata como "clave inválida" (alerta al equipo), no como un reintento infinito.
- [ ] Sabe rotar la clave sin cortes (genera, despliega, revoca) y hay una **segunda persona** que conoce el procedimiento.
- [ ] Tiempos de espera: ≥ 180 s en rutas síncronas, 30 a 60 s en las demás.
- [ ] Hay **reintento con espera creciente** ante 5xx, 503, 504 y errores de red (y **no** ante 400).

## 2. Documentos (factura, nota crédito, nota débito)

- [ ] Envió **facturas** y quedaron `Aprobado` con `Habilitacion: true`, con valores distintos: con y sin descuento, con línea excluida de IVA (si usted las usa), crédito y contado, persona natural y jurídica.
- [ ] Envió al menos una **nota crédito** y una **nota débito** aprobadas, con `DocumentoRef.Numero` y `DocumentoRef.CUFE` de una factura aprobada y el **`Concepto`** correcto (ver [la tabla](documentos-soportados.md#documentoref-notas-91-y-92)).
- [ ] Los totales de su ERP **coinciden** con los del documento aprobado (bruto, IVA y total a pagar).
- [ ] La numeración respeta el prefijo y el rango de la resolución, sin saltos ni repetidos; el ERP **no reutiliza** un número aprobado.
- [ ] El NIT del `Emisor` es el de su cliente en la plataforma; la `MatriculaMercantil` del emisor corresponde a la sucursal correcta (si usa logos por sucursal).
- [ ] Los terceros llevan `TipoIdDian`, `TipoPersona` en MAYÚSCULAS, dirección con `CodigoMunicipio` DANE, `EmailContacto` válido y responsabilidades fiscales correctas.
- [ ] El ERP guarda el **`CUFE`** de cada factura (lo necesitan las notas).
- [ ] Las **fechas** se envían en hora de Colombia, ISO 8601.

## 3. Estados y errores

- [ ] **202 no es aprobado**: su ERP no marca un documento como válido hasta ver `Aprobado`.
- [ ] Probó un **400** (por ejemplo, sin `Items`) y su ERP muestra el motivo y no deja el documento "enviado".
- [ ] Probó un **`Rechazado`** (por ejemplo, número fuera de rango), lo corrigió y lo **reenvió con el mismo número**; quedó `Aprobado`.
- [ ] Su ERP maneja `Error` como "pendiente, se reintenta solo" (no como definitivo) y tiene una alerta si pasa de una hora en ese estado.
- [ ] Reenviar un número ya aprobado **no** duplica: su ERP acepta el registro existente sin errores.
- [ ] Un documento que usted no puede corregir (ya aprobado con error de datos) se corrige con **nota crédito/débito**, no reenviando.

## 4. Resultado: webhook y/o polling

- [ ] **Polling de respaldo** con `GET /api/documento/cambios?desde=<cursor>`: guarda el `Cursor` y no pierde ni repite cambios tras reiniciar.
- [ ] Si usa **webhook**: valida `X-Dian-Firma` (HMAC-SHA256 sobre el cuerpo crudo, clave = texto del secreto en UTF-8) con comparación en tiempo constante y responde 401 si no coincide.
- [ ] El receptor responde 2xx en menos de 10 s y procesa el trabajo pesado aparte.
- [ ] El receptor es **idempotente** (llave `Numero` + `Estado` + `SecuenciaCambio`) y **confirma** con `GET estado/{numero}` antes de actuar.
- [ ] Probó el receptor **caído**: el aviso quedó pendiente y llegó al volver; sabe que tras 10 intentos el servicio deja de insistir (por eso el polling de respaldo).
- [ ] Su receptor tiene un certificado TLS válido y la URL no hace redirecciones.

## 5. PDF, XML y correo

- [ ] Descarga y **guarda** el PDF, el XML firmado y la respuesta de la DIAN de cada documento aprobado (`pdf/{numero}`, `xml/{numero}`, `respuesta-dian/{numero}`).
- [ ] Reintenta el PDF ante **503** y entiende que **409** significa "no aprobado".
- [ ] El logo y la razón social del PDF son los correctos (cargados en el portal).
- [ ] **Correo:** su ERP envía el PDF y el XML al cliente (el servicio aún no lo hace) y no envía nada de documentos de habilitación a clientes reales.
- [ ] Para NC y ND (aún sin plantilla de PDF) tiene definida cómo entregará la representación gráfica.

## 6. Operación

- [ ] Hay **registro (log)** de cada llamada: número, código HTTP, estado, sin la clave de API.
- [ ] Hay **alertas** por: 401, documentos `Rechazado`, `Error` prolongado, webhook caído, certificado a punto de vencer.
- [ ] Sabe quién contactar en SISTEMAS 360 (operador) y qué datos dar: cliente, número de documento, hora, código HTTP y mensaje.
- [ ] Tiene un **plan para la caída de la plataforma**: sus documentos quedan pendientes y se envían al volver; el ERP no emite "contingencia" por su cuenta sin acordarlo.
- [ ] Conoce el límite de ~20 solicitudes por segundo por IP y no hace envíos masivos en ráfaga (use una cola con ritmo).

## 7. Para pedir la producción

- [ ] Entregó al operador, por canal seguro, el **identificador y PIN del software de producción** y los datos de su **resolución real**.
- [ ] Tiene autorización escrita de su empresa para pasar a producción.
- [ ] Preparó el cambio en su ERP de resolución, prefijo, rango y **clave técnica** (la URL y la clave de API no cambian).
- [ ] Definió el **primer documento real** y quién verifica que salga `Habilitacion: false` y `Aprobado`.
- [ ] Tiene un plan de **vuelta atrás** acordado con el operador.

Con todo marcado, avise al operador. Él revisa su lista de producción en la plataforma (emisor, certificado vigente, software, set de pruebas, clave de API, logo) y autoriza.
