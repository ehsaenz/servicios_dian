# PDF oficial y correo

Última revisión: 2026-11-16

## Reglas de fondo

1. **Solo hay PDF oficial de un documento `Aprobado` por la DIAN.** Sin aprobación, `GET pdf/{numero}` responde 409. El servicio nunca entrega un "borrador" del PDF: si su ERP necesita imprimir algo antes de la aprobación (por ejemplo, una proforma), lo hace su ERP y debe marcarlo como **no válido como factura**.
2. **El PDF se arma a partir del XML aprobado** (el XML firmado más la respuesta de la DIAN), no del JSON que usted envió. Por eso refleja exactamente lo que la DIAN validó, con su CUFE, su código QR y su resolución.
3. **Un fallo del PDF nunca cambia el estado ante la DIAN.** El documento sigue `Aprobado`; solo queda anotado en `PdfMensaje` y se reintenta cuando usted pida el PDF.

## Cómo obtener el PDF

```
GET /api/documento/pdf/{numero}      →  200 application/pdf  (archivo {numero}.pdf)
```

- Normalmente el PDF **ya está listo** cuando el documento se aprueba (se genera en ese momento y se guarda). Si el aviso de webhook trae `Pdf: true`, está.
- Si consulta `estado/{numero}` justo tras la aprobación puede ver un instante `Aprobado` con `Pdf: false`: el PDF se genera un momento después de guardar el estado. Reintente.
- Si el PDF no existe aún, la llamada a `pdf/{numero}` **lo genera** en ese momento.
- **Guárdelo en su sistema.** Así puede reimprimir aunque la plataforma esté caída, y no depende de nuestro almacenamiento.
- También puede bajarlo una persona de su empresa desde el **portal de clientes** (sección *Documentos recientes*, columna *PDF*), que muestra los 50 documentos más recientes.

Respuestas: ver [referencia-api.md](referencia-api.md#get-apidocumentopdfnumero) (200, 404, 409, 503).

## Plantilla por tipo de documento

| Documento | Plantilla de PDF |
|---|---|
| Factura electrónica de venta (`01`) | **Sí** (`FacturaVentaElectronica0`) |
| Nota crédito (`91`) y nota débito (`92`) | **No todavía.** Quedan `Aprobado` con `Pdf: false` y `PdfMensaje` que dice que no hay plantilla; `GET pdf/{numero}` responde 503. |
| Documento soporte, tiquete POS, nota de ajuste, nómina | No pasan por esta ruta; no tienen PDF generado por el servicio. |

Mientras no existan las plantillas de notas, el integrador debe generar su propia representación gráfica de NC/ND (con los datos del XML aprobado) o esperar a SISTEMAS 360. Pregunte por la fecha.

## El logo y los datos de impresión

El PDF lleva la **razón social** y el **logo** de su empresa. Se configuran en el portal de clientes (o los configura el operador):

- **Logo del cliente** (PNG o JPG, hasta 2 MB) y, opcionalmente, **un logo por sucursal**, identificada por su **matrícula mercantil**.
- El servicio toma la matrícula del **emisor del documento** (`Emisor.MatriculaMercantil` que usted envió) y busca la sucursal con esa matrícula; si la encuentra, usa su logo y sus datos; si no, usa los del cliente. Si no hay logo, el PDF sale sin imagen.
- Por eso **envíe siempre `Emisor.MatriculaMercantil`** si su empresa tiene varias sucursales con distinto logo.

El cambio de logo aplica a los PDFs que se generen **desde ese momento**; los ya generados quedan guardados como estaban.

## Correo al adquiriente

**Estado actual: el envío de correo NO está conectado.** El servicio tiene el punto de enganche, pero falta el adaptador al servicio de correo de SISTEMAS 360 (EmailAPI). Mientras tanto:

- `CorreoEnviado` es siempre `false`.
- **Su ERP debe enviar el PDF (y el XML) por correo a su cliente**, tomando los archivos de `pdf/{numero}`, `xml/{numero}` y `respuesta-dian/{numero}`. Es lo más seguro de todos modos: usted controla el remitente y el texto.

Qué hará el servicio cuando se conecte (diseñado, **no activo**):

- Destinatario: `Receptor.EmailContacto` del documento aprobado (queda en `CorreoDestino`).
- Asunto: `{razón social del emisor}: documento electrónico {numero}`.
- Adjuntos: `{numero}.pdf`, `{numero}.xml` (firmado) y `{numero}-respuesta-dian.xml`.
- Solo se enviará si hay PDF, hay un correo de destino y no se envió antes. Un fallo del correo no cambia el estado de la DIAN.

Para esto sirve tener **`Receptor.EmailContacto`** correcto en cada documento.

## No verificado

- El PDF se probó con la plantilla de factura contra documentos aprobados en habilitación; no hay plantilla de notas.
- El correo no se probó (no hay adaptador).
