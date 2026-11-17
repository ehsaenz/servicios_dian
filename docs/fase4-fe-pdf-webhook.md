# Fase 4: facturación electrónica en la API unificada, PDF y notificación al ERP

**Fecha:** 2026-11-08 · **Estado:** acuerdos de diseño tomados con Eduardo en conversación. Nada implementado todavía. Los puntos marcados como "propuesta" no están confirmados.

## 1. Principios acordados

1. **Validación previa.** Un cliente solo recibe la representación gráfica de un documento que la DIAN ya aprobó. El software lo garantiza por construcción, no por disciplina del flujo.
2. **La API unificada genera todo documento electrónico**: XML, firma, envío a la DIAN, PDF, correo al cliente y notificación al ERP. El ERP solo envía los datos del documento (el JSON `DocumentoDian`, que hoy arma `DianRequestJsonBuilder` y no cambia).
3. **La API solo genera el PDF de documentos APROBADOS.** No hay endpoint de borrador en la API.
4. **El borrador vive en el ERP.** Si el documento no está aprobado, o la API no responde, el ERP puede imprimir un borrador con una marca muy visible: **"DOCUMENTO NO VALIDADO POR LA AUTORIDAD TRIBUTARIA"**. Se imprime con la plantilla de siempre; el bloque DIAN (QR, CUFE, validación) no sale porque no hay CUFE.
5. **PDF de un documento aprobado = siempre pedido a la API.** El ERP lo pide a la API (y puede guardarlo, ver 5).
6. **El PDF se arma a partir del XML aprobado** (`AttachedDocument` o factura firmada + `ApplicationResponse`), no del JSON del ERP.
7. **Correo al cliente:** desde la API, con el servicio EmailAPI que ya usan QuantyERP y el POS.

## 2. Flujo en la API

`Recibido -> XML generado -> Firmado -> Enviado a la DIAN -> Autorizado | Rechazado | Pendiente -> PDF generado -> Correo enviado -> ERP notificado`

- La petición responde **202** con un identificador. Clave de idempotencia: cliente + prefijo + número (un reintento del ERP no duplica el documento).
- **Autorizado** incluye "aceptado con notificaciones".
- **Rechazado:** no hay PDF ni correo; el ERP recibe los errores de la DIAN.
- **Pendiente** (la DIAN aún valida): la API consulta hasta resolver.
- La biblioteca que arma el modelo de impresión **se niega a trabajar** si el `ApplicationResponse` no es de aprobado, y el servicio de correo solo toma PDFs marcados como autorizados.

## 3. Notificación al ERP: consulta por defecto, webhook opcional

Hay clientes con el ERP alcanzable desde internet y otros detrás de una red local (muchos con RadminVPN, que no cubre bien un servidor Linux). Por eso:

- **Consulta de estado (obligatoria y por defecto).** El `DianWorker` del ERP, que ya consulta desde adentro, sigue haciéndolo: funciona detrás de cualquier red. Endpoints de consulta por documento y por rango (propuesta: cursor por fecha de actualización).
- **Webhook (opcional, por cliente).** Una URL fija y un secreto, registrados en la App Admin. Eventos `documento.autorizado` y `documento.rechazado`, con firma HMAC, identificador de evento, reintentos con espera creciente y bitácora de entregas en Admin. El payload lleva CUFE, estado, fecha de validación, errores y la referencia externa, sin incrustar archivos.
- **No se acepta una URL de retorno por petición** (una clave filtrada podría desviar notificaciones). Cada petición lleva una **referencia externa** (el `DocumentoId` del ERP) que la API devuelve.

## 4. Perfil de impresión por sucursal

Cada sucursal tiene su licencia de facturación electrónica y su logo (por ejemplo, un cliente con 4 sucursales tiene 4 licencias y 4 logos). La API necesita, por cliente y sucursal: nombre del establecimiento, nombre de la empresa, régimen fiscal, texto de encabezado y **logo**, cargados desde la App Admin.

La API vieja ya identifica la licencia con un dato que viaja en el XML (`CorporateRegistrationScheme/Name` del emisor), así que el XML basta para encontrar el perfil sin datos extra del ERP.

Tensión con la decisión 5.4 del plan ("Licencias y Maestros DIAN siguen separados"): la gestión de licencias puede seguir separada, pero la API necesita este perfil de sucursal. Se resuelve guardando el perfil en la API y manteniendo la licencia como su origen.

## 5. Cambios en el ERP (cuando se implemente)

- `RealDianCloudClient`: cambiar la ruta y la autenticación hacia la API unificada (propuesta: `x-api-key` en vez de `api/acceso/login`).
- `DianWorker`: al detectar que un documento quedó autorizado, **descargar y guardar el PDF oficial** de la API. Así se puede reimprimir aunque la API esté caída.
- Impresión de FV, NC y ND: si está autorizado, entregar el PDF oficial guardado o pedirlo a la API; si no, imprimir el borrador con la marca. Quien decide "autorizado" es el estado que el ERP ya guarda en `dian_envios`.
- El ERP sigue imprimiendo con su motor los documentos que no son electrónicos.
- Caso conservador: si la DIAN aprobó pero el ERP aún no lo sabe (API caída en ese momento), imprime borrador con la marca. Nunca al revés.

## 6. Biblioteca XML -> modelo de impresión

- Entrada: `AttachedDocument` o par factura firmada + `ApplicationResponse`. Salida: un **modelo de impresión plano** con la forma del JSON que ya usa el `.repx` (no una instancia de `DocumentoDian`, que arrastra lógica de generación).
- Fuente de lectura: las clases `InvoiceType`, `CreditNoteType` y `DebitNoteType` de la biblioteca vieja (`PdfTools.CrearPDF` ya lee de ellas el QR, la resolución y los rangos). Hay que ver si se reutilizan o se portan a .NET 10.
- Lo que el XML no trae (logo, nombres de catálogo) se completa con el perfil de sucursal y las tablas de catálogos; las observaciones salen de `cbc:Note`.
- **Pruebas de ida y vuelta:** JSON -> XML (generadores existentes) -> biblioteca -> comparación contra el JSON original, con una lista explícita de campos que se pierden.
- Orden de alcance: FV, NC, ND; luego documento soporte; nómina aparte (otro esquema).
- Plantilla: `reportes/FacturaVentaElectronica0.repx` (ya hecha). La del ERP y la de la API deben salir de la misma fuente (a definir dónde vive la copia oficial).

### Estado de la biblioteca (2026-11-08): hecha para FV, NC y ND

- Proyecto `Unificado\src\Shared\Impresion` (.NET 10, sin dependencias): `LectorDocumentoAprobado` (`DesdeContenedor` y `DesdeDocumentoYRespuesta`), el modelo `DocumentoImpresion` y `DocumentoNoAprobadoException`. Está en `DIAN.Unificado.sln`.
- Lee el XML por nombre local (sin depender de prefijos ni de las clases generadas) y rechaza XML con DTD.
- **Regla legal en el código:** solo devuelve el modelo si hay un `ApplicationResponse` con `ResponseCode` 02 (validado) **y** su CUFE coincide con el del documento. Con código distinto de 02, sin respuesta o con la respuesta de otro documento, lanza `DocumentoNoAprobadoException`.
- Entrega lo que la plantilla necesita: CUFE, QR (el que trae la extensión DIAN), ambiente, fecha de validación con zona (`2026-11-08 10:15:32 (UTC-5)`), texto de la resolución, emisor y receptor, líneas con IVA, totales, retenciones y, en NC y ND, el documento que corrigen y el concepto. La matrícula mercantil del emisor (`CorporateRegistrationScheme/Name`) queda en `Emisor.MatriculaMercantil`: es la llave del perfil de sucursal y su logo.
- Pruebas (`tests\Shared.Pruebas\ImpresionTests.cs`, 9 casos, datos ficticios): FV, NC y ND armadas con los generadores de la API; contenedor real de `GeneradorContenedor` igual al par factura + respuesta; rechazo con código 04; respuesta de otro CUFE; contenedor sin respuesta; DTD; nombres del JSON. Se verificó además que el JSON del modelo pasa por la plantilla `.repx` y sale bien impreso.
- **Lo que el XML no trae** y por tanto queda vacío o aproximado: la "Referencia" del artículo del ERP (el XML solo lleva el código), el tipo de la orden de referencia, el `Excluido` y el porcentaje de descuento directo de la línea, las observaciones internas y el logo (viene del perfil de sucursal).
- **Pendiente de esta biblioteca:** prueba de ida y vuelta desde el JSON del ERP (necesita el núcleo de FE, paso 4); documento soporte y notas de ajuste (se leen, pero sin pruebas ni nombres de tipo revisados); XML y respuestas reales de la DIAN (cuando Eduardo indique cuáles).
- **Observación de la plantilla:** la columna "Codigo" es angosta (un código de 9 caracteres sale cortado, "PRUEBA0"); es herencia de la plantilla de QuantyERP y se ajusta al revisar el diseño.

### Estado del renderizador de PDF (2026-11-08): hecho y probado en Windows y Linux

- Proyecto `Unificado\src\Shared\Impresion.DevExpress` (.NET 10, **fuera de la solución** porque necesita DevExpress 24.1 instalado): `RenderizadorPdf.Generar(modelo, plantilla.repx, logo)` devuelve el PDF. Registra las fuentes de `reportes\fuentes` una vez por proceso, quita los scripts de la plantilla y pone el logo en el control `pictureBox1` si viene.
- **Guardia legal:** solo imprime un `DocumentoImpresion` marcado `Aprobado`, y esa marca solo la pone `LectorDocumentoAprobado` tras verificar la respuesta de la DIAN. Un modelo armado a mano lanza `DocumentoNoAprobadoException`.
- **Prueba de la cadena completa** (`tools\Reportes\PruebaLinux`): contenedor XML aprobado -> modelo -> PDF. En la VM `quanty-lab` (Ubuntu 24.04, .NET 10 autocontenido, como la API): PDF de 58 KB (66 KB con logo), 1,4 s el primero y unos 100 ms los siguientes. En Windows igual.
- **Lección para integrar en la API (Pos.Api):** DevExpress busca por nombre los ensamblados `DevExpress.Drawing.v24.1.Skia` y `DevExpress.Pdf.v24.1.SkiaRenderer`, que no son dependencia de compilación y no quedan en el `.deps.json`. Sin ellos cae a GDI+ y en Linux falla pidiendo `libgdiplus`. La biblioteca los copia a la salida y registra un resolvedor que los carga desde la carpeta de la aplicación; con eso no hace falta instalar nada en el servidor.
- **Para publicar la API con PDF** hay que tener DevExpress 24.1 en la máquina de compilación (o llevar sus DLL a una carpeta y apuntar `DevExpressBinPath`), y confirmar la licencia (decisión 5.9).

### Estado del perfil de sucursal (2026-11-08): hecho

- **Datos:** cada cliente tiene una lista de sucursales (`SucursalRegistro`): matrícula mercantil (la llave, y la que viaja en el XML), nombre del establecimiento, régimen fiscal impreso y logo. En MySQL es la tabla nueva `dian_sucursales` (el script del esquema es idempotente: `crear-esquema` la crea en una base existente); el almacén de archivos la guarda en el JSON del cliente. El migrador archivos -> MySQL también copia los logos de sucursal.
- **App Admin:** en la página del cliente hay una sección "Sucursales" para crear o actualizar (conserva el logo), subir o reemplazar el logo (PNG o JPG, hasta 1 MB, con firma de imagen verificada) y eliminar (borra también su logo). Todo queda en la auditoría. El logo se ve en `/logo/<cliente>/<matrícula>`.
- **Uso desde la API:** `PerfilImpresionResolver.Resolver(cliente, matricula, almacen)` entrega nombre, régimen y logo de la sucursal que corresponde a la matrícula del XML (`Emisor.MatriculaMercantil` del modelo de impresión). Sin sucursal, o sin logo propio, cae al logo y a la razón social del cliente.
- **Pruebas:** 3 casos nuevos en `Pos.Pruebas` (perfil y logo, validaciones y eliminación, resolución con respaldo) y 1 de MySQL en `Admin.Pruebas` (se omite sin la variable `DIAN_MYSQL`: **no se ha corrido contra una base MySQL**). La página de Admin compila, pero no se revisó a ojo en el navegador.

### Núcleo de FE (paso 4): avance y hallazgos (2026-11-08)

**Hecho:**
- Proyecto `Unificado\src\Shared\ModeloDian` (en la solución): copia del modelo `DocumentoDian` (`01_FE_V2_Models`) y de su constructor `DianDocumentBuilder` (`05_FE_V2_HttpClientLib`), con los mismos espacios de nombres y nombre de ensamblado (`DianModelDoc`) para que el JSON que envía QuantyERP siga siendo el mismo contrato. Compila en .NET 10; los proyectos originales no se tocaron.
- `tests\Shared.Pruebas\ModeloDianTests.cs`: un documento armado como lo hace QuantyERP (`DianRequestJsonBuilder`) no trae errores, totaliza bien (subtotal, base gravada con descuento, IVA) y su JSON se lee con `JObject.ToObject<DocumentoDian>()`, que es como lo leía la API vieja, con los mismos valores.

**Hallazgo que fija el camino:** el generador viejo del XML (`XmlGen.cs`, 2.293 líneas, arma el `InvoiceType` desde `DocumentoDian`) **no se puede portar de forma mecánica**: compilado contra los tipos UBL de la solución nueva da unos 900 errores, porque las dos familias de clases UBL difieren en lo estructural (por ejemplo `AccountingCustomerParty` es un arreglo en la nueva y uno solo en la vieja, `DianExtensions` está en otro tipo, faltan los tipos de proveedor de software) y depende de `AppConfig.licencia` y `MtxSoftware`. Por eso el camino es **escribir un traductor `DocumentoDian` -> generadores de la solución nueva** (`GeneradorFactura`, `GeneradorNotaCredito`, `GeneradorNotaDebito`, con los tipos de `Metadatos`), que son los mismos que ya validó la DIAN en habilitación para FV, NC y ND con la herramienta de pruebas.

**Traductor de FV (hecho, falta validarlo con la DIAN):** proyecto `Unificado\src\Shared\Traductor` (en la solución), `TraductorFactura.Factura(documentoDian, parametrosEmision)` devuelve el `InvoiceType` de la factura electrónica de venta usando `GeneradorFactura`.
- Los importes salen de las cuentas del propio modelo (`BaseGral` por línea, impuestos por línea): el traductor no recalcula impuestos. Las tarifas del modelo (fracción, 0,19) pasan a porcentaje (19,00). El descuento de línea va como `AllowanceCharge` con su base; `LineExtensionAmount` es el valor neto; el total de la factura es neto + impuestos (las retenciones se informan aparte y no lo reducen).
- Lleva emisor y adquiriente con sus responsabilidades fiscales unidas con `;`, ubicaciones por código de municipio (los nombres salen del catálogo si no vienen), resolución y clave técnica del documento, forma y medio de pago, orden de referencia, notas y el software DIAN de los parámetros de emisión.
- Rechaza con `DocumentoNoTraducibleException` lo que no es factura de venta (01), lo que no trae resolución, clave técnica, líneas o software.
- **Ida y vuelta probada** (`tests\Shared.Pruebas\TraductorFacturaTests.cs`, 3 casos): documento del ERP -> JSON -> `JObject.ToObject` -> XML -> `LectorDocumentoAprobado` -> modelo de impresión conserva emisor, receptor, líneas (con la línea con descuento: neto 108.000, descuento 12.000, IVA 20.520), subtotal 208.000, IVA 39.520, total 247.520; y las sumas del XML cuadran entre líneas, totales y resumen de impuestos.
- **Validado por la DIAN en habilitación (2026-11-08):** la herramienta de pruebas (`Habilitacion.Pruebas`, modo `--tipo factura-erp`) arma un documento del ERP con los datos de MARLIT, lo pasa por JSON, lo traduce, lo firma con el certificado de MARLIT y lo envía en modo síncrono. La DIAN respondió `00 - Procesado correctamente: La Factura electrónica SETP990000515 ha sido autorizada` (ApplicationResponse con código 02, "Documento validado por la DIAN"). Consumió el consecutivo **SETP 990000515**; el siguiente libre es el 990000516.
- **Ajuste que salió de armar ese documento:** los marcadores `48` y `49` del modelo del ERP (responsable de IVA o no) no son códigos de la DIAN: definen el tributo del tercero (IVA o ZZ) pero no deben ir en `TaxLevelCode`. El traductor ya los deja fuera y solo envía los códigos `O-xx` y `R-xx`.
- **El lector con un par real:** `LectorDocumentoAprobado` leyó bien el documento firmado y la respuesta real de la DIAN (validado 2026-11-08 09:02:32 UTC-5, con QR, resolución y total 247.520). Queda una prueba opcional para repetirlo con otros documentos reales: definir `DIAN_REAL_DOC` y `DIAN_REAL_AR` con las rutas (sin las variables no hace nada; los archivos no se versionan).
- **No probado todavía:** persona natural (el código existe, sin prueba); exportación y contingencia; líneas excluidas y retenciones con datos reales; otros impuestos distintos del IVA; crédito con plazo.

**Notas crédito y débito (hecho, validado por la DIAN, 2026-11-08):** `TraductorNota.NotaCredito` y `NotaDebito` (mismo proyecto `Traductor`) reutilizan terceros, líneas, impuestos y totales del traductor de factura y añaden el documento de referencia (`DocumentoRef`: número, CUFE, fecha; se exige) y el concepto.
- **Concepto:** se agregó al contrato el campo opcional `documento_ref.Concepto` (código DIAN). Crédito: 1 devolución parcial, 2 anulación, 3 rebaja o descuento, 4 ajuste de precio, 5 pronto pago, 6 volumen. Débito: 1 intereses, 2 gastos por cobrar, 3 cambio de valor, 4 otros. Si el ERP no lo envía, la crédito sale como devolución parcial (1) y la débito como otros (4); un código desconocido se rechaza. Es compatible hacia atrás (un JSON sin el campo se sigue leyendo). **QuantyERP debe empezar a enviarlo** para que la nota lleve el concepto correcto.
- Solo se referencian facturas (algoritmo CUFE); referenciar otra nota no está soportado.
- **Pruebas** (`TraductorNotaTests`, 4 casos): ida y vuelta por el lector, concepto por defecto, rechazo sin documento origen o con concepto inválido o tipo equivocado.
- **DIAN, habilitación (síncrono, `--tipo nota-credito-erp` / `nota-debito-erp`):** `SETP990000516` nota crédito (anulación) y `SETP990000517` nota débito (intereses), ambas referenciando `SETP990000515`: "Procesado correctamente ... ha sido autorizada". El lector las leyó con los pares reales. Siguiente consecutivo libre: **990000518**.
- Observación: en el modelo de impresión de las notas la resolución sale vacía (el XML de la nota no la imprime como la factura); la plantilla de NC/ND debe contemplarlo.

**Servicio de documentos, primera parte (hecha, con la DIAN simulada en pruebas):** `Pos.Servicios\Documentos` y `DocumentoController` (`api/documento`), autenticado con la misma clave `x-api-key` de las demás rutas (queda resuelta la decisión de autenticación).
- `POST agregar` (cuerpo: el JSON de `DocumentoDian`): valida y traduce al instante (si no es traducible responde 400 y no guarda nada), firma con el certificado del cliente, guarda y responde **202** con el registro (estado, CUFE). El envío a la DIAN corre en segundo plano (`Documentos:EnvioEnSegundoPlano`, por defecto true). **Idempotente por número:** reenviar un número ya recibido o aprobado devuelve el existente sin tocar la DIAN; uno rechazado o con error se reprocesa.
- Estados: `Recibido` → `Aprobado` (respuesta 00 de la DIAN; el único con PDF oficial), `Rechazado` (99, con la lista de errores) o `Error` (otra respuesta o falla de red; reintentable). Ambiente: habilitación salvo software en producción Y `PermiteProduccion`.
- Consulta: `GET estado/{numero}`, `GET cambios?desde=N` (cursor `SecuenciaCambio`, para el polling del ERP), `GET xml/{numero}` y `GET respuesta-dian/{numero}`, `POST reintentar/{numero}`.
- Almacén en archivos por cliente (`datos\{cliente}\documentos\{numero}.json|.xml|-respuesta.xml`); un solo proceso escribe.
- Los clientes necesitan un software de tipo 1 (facturación electrónica) además de los de DS/POS.
- 6 pruebas de API (`DocumentoApiTests`): FV firmada y aprobada, idempotencia, NC y ND, rechazo inmediato, aislamiento entre clientes, cambios por cursor y descarga del XML.

**PDF y correo al aprobarse (hecho, con generador y correo simulados en pruebas):**
- Al pasar a `Aprobado`, el servicio lee el XML firmado y la respuesta de la DIAN con `LectorDocumentoAprobado` (que solo marca aprobado con respuesta 02 y CUFE coincidente), genera el PDF, lo guarda (`{numero}.pdf`) y, si hay correo del adquiriente y un enviador configurado, lo envía con PDF, XML y respuesta de la DIAN adjuntos. Un fallo del PDF o del correo **nunca cambia el estado de la DIAN**: queda en `PdfMensaje` y se reintenta cuando el ERP pida el PDF.
- `GET api/documento/pdf/{numero}`: 200 con el PDF; **409 si el documento no está aprobado** (no hay PDF oficial; el borrador con la marca "no validado" es del ERP); 503 con el motivo si el generador falló o no está configurado.
- Piezas enchufables: `IGeneradorPdf` (en `Shared\Impresion`, sin dependencias) y `IEnviadorCorreo` (en `Pos.Servicios`). El generador con DevExpress es `Impresion.DevExpress\GeneradorPdfDevExpress` (fuera de la solución, compila): la API lo carga por configuración con `Pdf:Generador = "DIAN.Impresion.DevExpress.GeneradorPdfDevExpress, Impresion.DevExpress"`, `Pdf:Plantillas` y `Pdf:Fuentes`; si está configurado y no carga, la API no arranca. Para desplegarlo hay que copiar sus DLL (y las de Skia) junto a la API.
- Perfil de impresión: con el almacén de clientes configurado, el logo y los datos salen de la sucursal por la matrícula del XML (`PerfilImpresionResolver`).
- Solo hay plantilla de factura de venta (tipo 01): NC y ND quedan aprobadas con `PdfMensaje` "no hay plantilla", sin PDF.
- 3 pruebas (`DocumentoPdfApiTests`): aprobada con PDF y correo; rechazada sin PDF (409); falla del generador sin perder la aprobación.
- **Sin probar:** el generador DevExpress cargado de verdad por la API (solo se probó el renderizador por separado con el documento real) y el correo (falta el contrato de EmailAPI: hoy solo conozco `IMiddleEmail.EnviarMail`).

**Webhook y reintentos (hecho, probado con la entrega simulada; la entrega HTTP real no se probó):**
- **Configuración por cliente en la app Admin** (sección "Webhook de documentos" de la página del cliente): una URL fija (http o https, sin usuario ni clave) y un secreto de firma que la app genera, guarda protegido (nunca en claro) y muestra una sola vez; se puede regenerar y quitar (sin URL el ERP vuelve al polling). Se audita. En MySQL, tabla nueva `dian_webhooks` (`Admin.Web crear-esquema` la crea; es idempotente).
- **Aviso:** cuando un documento llega a un estado final (`Aprobado` o `Rechazado`) y el cliente tiene webhook, el servicio hace POST de un JSON pequeño (`Evento`, `Numero`, `Estado`, `CUFE`, `Mensaje`, `Pdf`, `SecuenciaCambio`) con la cabecera `X-Dian-Firma: sha256=<HMAC-SHA256 del cuerpo con el secreto>`; no sigue redirecciones y espera 10 s. El aviso es solo una pista: el ERP confirma con `estado/{numero}`. Sin URL por solicitud y sin datos del documento más allá de lo mínimo.
- **Reintentos del aviso:** si no hay respuesta 2xx queda pendiente y se reintenta con espera creciente (1, 2, 4... hasta 30 minutos), máximo 10 intentos; el error queda en el registro. Anotar un intento no mueve el cursor de `cambios` del ERP.
- **Trabajador de fondo** (`ReintentosWorker`, cada `Documentos:ReintentoSegundos`, 60 por defecto, 0 lo apaga): recorre los clientes activos y reenvía a la DIAN los documentos `Recibido` que quedaron sin enviar (más de 2 minutos, por ejemplo tras un reinicio) y los `Error` con la misma espera creciente (máximo 10 intentos), y reintenta los avisos pendientes. Solo funciona con el almacén de clientes de la Admin (el índice de archivos antiguo no se puede enumerar).
- 5 pruebas del servicio (aviso con firma HMAC, webhook caído con espera creciente, error de la DIAN reenviado, recibido sin enviar, sin webhook) y 1 de la Admin (secreto protegido, regeneración, URL inválida, quitar). Falta la prueba contra una URL real.

**Prueba en la VM `quanty-lab` (2026-11-08, a medias):**
- Hecho: el paquete nuevo (API con `api/documento`, Admin con el webhook y el generador de PDF) se publicó para `linux-x64` y se instaló con `instalar.sh` (actualización; datos y configuración intactos): los tres servicios activos y las dos URL responden 200.
- **Cambio que salió de armar el paquete:** el generador DevExpress necesita `System.Drawing.Common` 4.7.2 y la API fija la 10.0.14 (por seguridad, vía `Tenants`), así que no pueden convivir en una carpeta. La API ahora carga el generador desde su propia carpeta y en su propio contexto de carga (`Pdf:Carpeta`; solo comparte `Impresion`, la interfaz). Empaquetado: `Pos.Api\pdf\` con las DLL de DevExpress/Skia, `plantillas\` y `fuentes\`.
- **Configuración que falta en `/etc/dian/dian.env`** (el instalador no la agrega): `Pdf__Generador=DIAN.Impresion.DevExpress.GeneradorPdfDevExpress`, `Pdf__Carpeta=/opt/dian/pos-api/pdf`, `Pdf__Plantillas=/opt/dian/pos-api/pdf/plantillas`, `Pdf__Fuentes=/opt/dian/pos-api/pdf/fuentes`.
- **Resultado (con permiso de Eduardo, 2026-11-08):** MARLIT dado de alta en la VM con `importar-cliente` y una factura armada como la entrega QuantyERP enviada **a la API que corre en Linux** por un túnel SSH (`Habilitacion.Pruebas --tipo factura-erp` con `DIAN_API_URL` y `DIAN_API_KEY`). La API tradujo, firmó y envió: **SETP990000518 autorizada por la DIAN** ("La Factura electrónica SETP990000518, ha sido autorizada"), y `api/documento/pdf/SETP990000518` devolvió **un PDF de 59 KB generado en Linux con DevExpress** (el generador cargado en su propio contexto de carga funcionó). Siguiente consecutivo libre: **990000519**.
- **Errores que encontró la prueba (corregidos):** (1) ni la Admin ni `importar-cliente` aceptaban el software de facturación electrónica (tipo 1): ahora sí, y la página del cliente tiene su sección; (2) un error de comillas dejó en `dian.env` las variables `Pdf__*` en una sola línea y la API no arrancaba (se vio que un generador mal configurado tumba el servicio, como se quiso). Quedan además dos detalles de uso: el instalador no agrega las variables `Pdf__*` a `dian.env` y `importar-cliente` deja un cliente a medias si falla a mitad (hubo que borrarlo a mano).
- **Observación:** al consultar el estado justo tras la aprobación puede verse `Aprobado` con `Pdf: false` unos instantes, porque el PDF se genera después de guardar el estado; el webhook se envía ya con el PDF listo.
- **No probado en la VM:** el correo (sin EmailAPI), el webhook contra una URL real, notas crédito y débito.
- **Pendiente de limpieza:** los datos de MARLIT (cliente, certificado y XML) siguen en la VM; el borrado automático no se ejecutó (lo bloqueó el control de permisos) y queda por hacer.

**Falta, en orden:**
4. Del servicio: contenedor `AttachedDocument` (el correo hoy adjunta XML y respuesta por separado), adaptador de EmailAPI (falta el contrato), plantillas de NC y ND, almacén de documentos en MySQL (hoy archivos en un solo servidor) y el cupo de matrículas/sucursales (propuesta de abajo).
5. Cambios en QuantyERP: enviar `documento_ref.Concepto`, cliente HTTP de `api/documento`, descarga y caché del PDF oficial y el borrador con la marca "no validado".

**Por decidir:**
- La API vieja valida la **licencia por matrícula mercantil** antes de generar (`AppConfig.licencia(matricula).EsValida`). En la nueva, la equivalente es la sucursal. Propuesta: si el cliente tiene sucursales configuradas, el documento se rechaza cuando su matrícula no corresponde a ninguna; si no tiene ninguna, se acepta y se imprime con el perfil general.
- Autenticación del ERP: clave de API en la cabecera `x-api-key` (propuesta) frente a mantener `api/acceso/login` con usuario y clave.

## 7. Orden de trabajo propuesto

1. ~~Prueba de DevExpress en Linux~~ (hecha, funciona; falta resolver las fuentes, ver sección 8). El servicio Windows solo para PDFs no hace falta.
2. ~~Biblioteca XML -> modelo de impresión~~ (hecha para FV, NC y ND; ver "Estado de la biblioteca" en la sección 6). Falta la ida y vuelta desde el JSON del ERP.
3. ~~Perfil de sucursal y logo en el almacén de la API y en la App Admin~~ (hecho; ver "Estado del perfil de sucursal" en la sección 6).
4. Portar el núcleo de FE (XML, firma, envío) y exponer `api/documento/agregar` con clave de API.
5. PDF automático, correo por EmailAPI y estado/webhook.
6. Cambios en el ERP (sección 5) y pruebas con MARLIT en habilitación.

Nada pasa a producción sin autorización expresa de Eduardo.

## 8. Resultado de la prueba de DevExpress en Linux (2026-11-08)

Herramienta: `Unificado\tools\Reportes\PruebaLinux` (net8.0, fuera de la solución; carga una `.repx`, la llena con un JSON y exporta PDF y PNG). Se publicó para `linux-x64` y se ejecutó en la VM `quanty-lab` (Ubuntu 24.04, .NET 8.0.33) con la plantilla `FacturaVentaElectronica0.repx` y el JSON de ejemplo.

- **Funciona.** DevExpress 24.1 elige solo el motor Skia en Linux y genera el PDF (68 KB). Primer PDF 1,6 s (arranque); desde el segundo, unos 110 ms.
- **Qué hay que desplegar con la API:** `DevExpress.Drawing.v24.1.Skia`, `DevExpress.Pdf.v24.1.SkiaRenderer`, SkiaSharp 2.88.9, SkiaSharp.HarfBuzz, los nativos `SkiaSharp.NativeAssets.Linux.NoDependencies` y `HarfBuzzSharp.NativeAssets.Linux`, y `System.Drawing.Common` 4.7.2 (sin él la carga del reporte falla).
- **QR y CUFE salen bien.** No apareció ninguna marca de evaluación en la muestra. Eso es una comprobación técnica; que la licencia cubra el despliegue en el servidor (decisión 5.9) es un tema contractual con la suscripción de DevExpress y lo confirma Eduardo.
- **Problema encontrado y resuelto: las fuentes.** La plantilla usaba `Segoe UI` (de Windows) y la VM solo trae DejaVu, más ancha; el texto se partía y algunas cifras se cortaban.
  - **Solución:** la plantilla ahora usa **Liberation Sans** (licencia libre OFL, mismas medidas que Arial) y los archivos de la fuente viajan con la aplicación, en `reportes\fuentes` (Regular, Bold, Italic y BoldItalic, más su licencia). Se cargan con `DXFontRepository.Instance.AddFont` antes de abrir el reporte. Descargada de la versión 2.1.5 del proyecto oficial (liberationfonts/liberation-fonts en GitHub, SHA-256 del archivo `7191c669bf38899f73a2094ed00f7b800553364f90e2637010a69c0e268f25d0`).
  - **Resultado en Linux:** el diseño queda igual que en Windows, sin cortes (`reportes\FacturaVentaElectronica0.muestra-linux.png`). 1,6 s el primer PDF y unos 95 ms los siguientes.
  - **Cuidado en Windows:** con DirectWrite, registrar las cuatro variantes hace que todo el texto salga en negrita. En Windows hay que registrar solo `LiberationSans-Regular.ttf` (la negrita se sintetiza y se ve aceptable). Aplica al generador de la plantilla y al borrador que imprime el ERP. En Linux (la API) se registran las cuatro. Si se quiere fidelidad total en Windows, la alternativa es instalar la fuente en el sistema.
  - El generador cambia la fuente de todos los controles de la plantilla de QuantyERP, así que otras plantillas derivadas salen con la misma fuente.

## 9. Pendiente por definir

- Contrato exacto de EmailAPI (solo se vio `IMiddleEmail.EnviarMail(parametros)` y `GetTracking`).
- Autenticación del ERP contra la API (clave de API frente a login compatible).
- Si las clases de la biblioteca vieja se reutilizan o se portan.
- Dónde vive la copia oficial de las plantillas `.repx`.
- Licencia de DevExpress para el despliegue en la API.
