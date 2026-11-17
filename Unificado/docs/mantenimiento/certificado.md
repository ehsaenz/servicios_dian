# Certificado de firma (.p12)

Última revisión: 2026-11-16 · Rol: **operador** (el cliente **no** puede subir su certificado)

Cada documento se firma con el certificado digital del cliente. Sin certificado el cliente no puede firmar ni enviar. Lo emite una entidad de certificación; normalmente es un archivo `.p12` (o `.pfx`) con una clave.

## Antes de empezar

Tenga a la mano:

- El archivo `.p12` / `.pfx` del cliente (hasta **1 MB**).
- La **clave del certificado**. Escríbala usted en la pantalla; no la guarde en chats ni correos.
- El certificado debe ser **del NIT del cliente**.

## Subirlo por primera vez

1. Abra la página del cliente, sección **Certificado de firma**. Si aún no hay certificado, verá en naranja: «Falta el certificado. Sin él este cliente no puede firmar documentos.»
2. En **Archivo .p12** elija el archivo (acepta `.p12` y `.pfx`).
3. En **Clave del certificado** escriba la clave (se ve como puntos).
4. Pulse **Subir certificado**.

El sistema **valida antes de guardar**. Si todo está bien aparece: «Certificado guardado: <sujeto>, vence AAAA-MM-DD.» y la sección muestra el sujeto del certificado y su fecha de vencimiento.

### Mensajes de validación

| Mensaje | Qué pasó | Qué hacer |
|---|---|---|
| «Seleccione el archivo .p12 (hasta 1 MB).» | No eligió archivo, está vacío o pesa más de 1 MB | Elija el archivo correcto |
| «No se pudo abrir el certificado: el archivo no es un .p12 válido o la clave es incorrecta.» | Clave errónea o archivo dañado | Verifique la clave con quien emitió el certificado |
| «El certificado no trae la llave privada, y sin ella no se puede firmar.» | Es un certificado público (`.cer`), no el `.p12` completo | Pida el `.p12` con llave privada |
| «El certificado está vencido desde …» | Ya venció | Pida el certificado renovado |

### Avisos (se guarda, pero lea el aviso)

Después del mensaje verde puede aparecer «Atención: …»:

- «El NIT … no aparece en el sujeto del certificado; verifique que sea el del cliente.» Puede ser un certificado de otra empresa. Confirme antes de enviar documentos.
- «El certificado vence pronto (AAAA-MM-DD).» Vence en menos de 30 días: gestione la renovación.

## Renovación (reemplazar)

Cuando el cliente recibe su certificado nuevo:

1. Suba el nuevo **antes** de que venza el anterior.
2. En el mismo bloque, el botón dice ahora **Reemplazar certificado**. Elija el nuevo `.p12`, escriba **su** clave y pulse el botón.
3. Verifique que la fecha de vencimiento mostrada sea la nueva.

Se conserva un solo certificado por cliente: el nuevo **reemplaza** al anterior. No necesita desactivar al cliente ni cambiar claves de API.

> Consejo: si el certificado nuevo trae una clave distinta, use la clave nueva al subirlo; la anterior deja de servir.

## Dónde y cómo se guarda

- El archivo se guarda en el almacén del servidor (archivo `certificado.p12` del cliente, o en la base de datos si se usa MySQL).
- La **clave del certificado se guarda cifrada**; nunca aparece en pantalla, en la auditoría ni en el registro.
- En la auditoría queda la acción `certificado` con el sujeto y la fecha de vencimiento, sin la clave.
- **Respaldos:** el certificado y las llaves de cifrado se respaldan **juntos**. Sin las llaves no se pueden recuperar los secretos guardados ([auditoria-y-respaldos.md](auditoria-y-respaldos.md)).

## Ver la fecha de vencimiento

- En **Clientes** (lista), columna **Certificado**: «Vence AAAA-MM-DD». Se muestra en **rojo** cuando faltan menos de 30 días; en naranja «Falta» si no hay certificado.
- En la página del cliente, sección **Certificado de firma**.
- En **Camino a producción** el ítem «Certificado de firma» pasa a ▲ (aviso) si vence en menos de 30 días y a ✖ (impide producción) si ya venció.

## Qué hacer antes de que venza

1. Revise la lista de clientes al menos una vez al mes: busque fechas en rojo.
2. Con 60 días de margen, avise al cliente para que gestione el certificado nuevo.
3. Al recibirlo, súbalo (sección «Renovación») y confirme la nueva fecha.
4. Si un certificado vence sin renovarse, los envíos de ese cliente fallan; suba el nuevo y reenvíe lo pendiente ([preguntas-frecuentes.md](preguntas-frecuentes.md)).

El panel **no envía avisos automáticos** de vencimiento: depende de revisar la lista.

## Comprobación tras una renovación

El código recarga el certificado cuando cambia la fecha de modificación del archivo (`CertificadoCache`). Verifique sujeto y vigencia en el panel y una operación de firma controlada. Los XML ya firmados no se vuelven a firmar por subir un certificado nuevo: `reintentar` procesa el XML almacenado. Si un documento pendiente requiere nueva firma, escale al equipo técnico antes de reenviar. No cambie su número para resolver una duda de estado.

## No verificado

- No se probó cuánto tarda el servicio en usar el certificado nuevo tras reemplazarlo cuando hay varias instancias.
- El tamaño real de los `.p12` de las entidades de certificación colombianas es normalmente pequeño; el límite de 1 MB lo aplica el panel.
