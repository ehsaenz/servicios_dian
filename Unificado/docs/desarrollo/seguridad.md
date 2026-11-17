# Seguridad

Última revisión: 2026-11-16

Modelo de amenazas y controles de la API (`Pos.Api`) y del portal (`Admin.Web`). Los controles de red (nginx, IIS, firewall) se describen en [../despliegue/README.md](../despliegue/README.md); aquí, lo que depende del código.

## 1. Activos y actores

| Activo | Por qué importa |
|---|---|
| **Certificado `.p12` y su clave** de cada cliente | Con ellos se firman documentos con la identidad tributaria del cliente. |
| **Software DIAN: identificador y PIN** | Habilitan la emisión ante la DIAN. |
| **Claves de API** | Permiten emitir documentos como el cliente. |
| **Llaves de Data Protection** (`Secretos:Llaves`) | Quien las lea puede descifrar PIN, claves de certificado y secretos de webhook. |
| **Credenciales del portal** | Operador: control de toda la plataforma. Cliente: su propio cliente. |
| **Documentos emitidos** (XML, PDF) | Datos tributarios y personales de los adquirientes. |

| Actor | Alcance legítimo |
|---|---|
| ERP del cliente | `api/*` con la clave de **su** cliente. |
| Usuario de cliente | Portal: solo su cliente (logo, sucursales, claves de API, webhook, documentos y PDF, su contraseña). |
| Operador | Portal: todos los clientes, certificados, software, producción, usuarios, auditoría. Solo desde la red interna o la VPN. |
| Internet | Solo `/salud`, `/api/*` (con clave válida) y el portal de clientes. |

## 2. Controles de la API

| Control | Dónde | Detalle |
|---|---|---|
| Clave de API por cliente | `TenantMiddleware` | Cabecera `x-api-key`. Sin clave, desconocida o de un cliente inactivo → 401 con mensaje genérico. Solo `/salud` es pública. |
| Claves no almacenadas | `ClaveApi` | 256 bits aleatorios; se guarda el hash SHA-256; la clave se muestra **una sola vez**. Se pueden tener varias por cliente y revocar cada una. |
| Aislamiento entre clientes | `ServiciosPos(tenant)`, `AlmacenDocumentos` | Todo acceso a datos pasa por el tenant de la petición; los documentos de cada cliente viven en su propia carpeta. Hay pruebas ("cada cliente solo ve sus documentos") y una prueba manual con dos certificados (`AislamientoRealTests`). |
| Producción bloqueada por defecto | `TenantConfig.EsHabilitacion` | Un documento va a producción **solo** si el software del cliente está marcado como producción **y** `PermiteProduccion` es verdadero. En cualquier otro caso, habilitación. |
| Validación previa para imprimir | `LectorDocumentoAprobado` | El PDF oficial solo existe para un documento con respuesta `02` de la DIAN y CUFE coincidente; el renderizador se niega a imprimir otro modelo. |
| Idempotencia | `DocumentoServicio.Agregar` | Reenviar un número ya recibido o aprobado no lo duplica ni toca la DIAN. |
| XML de entrada | `LectorDocumentoAprobado` | Rechaza XML con DTD (protección contra XXE). |
| Plantillas sin scripts | `RenderizadorPdf` | Los scripts de las `.repx` se eliminan antes de imprimir. |
| Sin URL de retorno por petición | Diseño del webhook | La URL del aviso la fija el cliente (o el operador) en el portal; una clave filtrada no puede desviar notificaciones. |
| Webhook firmado | `EntregaWebhookHttp` | `X-Dian-Firma: sha256=<HMAC-SHA256 del cuerpo con el secreto>`; sin seguir redirecciones; espera máxima 10 s; el aviso no lleva datos del documento más allá de lo mínimo. |
| Límite de tasa | nginx (despliegue) | Por dirección y por ruta; ver el despliegue. |
| Cabeceras de proxy | `UseForwardedHeaders` | Solo se confía en `X-Forwarded-*` que vienen del propio equipo (`127.0.0.1`). |

## 3. Controles del portal (`Admin.Web`)

| Control | Detalle |
|---|---|
| Autenticación | Cookie `dian-admin`: `HttpOnly`, `SameSite=Strict`, `Secure` según la petición (`SameAsRequest`), caducidad de 30 min con renovación. |
| Contraseñas | PBKDF2-SHA256, 210 000 iteraciones, sal propia por usuario, mínimo **12** caracteres. Las contraseñas generadas por el sistema (usuarios de cliente) son aleatorias de 16 caracteres con alfabeto sin ambigüedades y se muestran una sola vez. |
| Bloqueo | 5 intentos fallidos bloquean al usuario **15 minutos**; queda en el registro. |
| Roles | **Operador** y **Cliente** (`Roles.cs`). Un usuario de cliente solo ve y opera **su** cliente: cualquier otro responde **404** (no revela ids). Las acciones reservadas al operador se rechazan en el **servidor** (`OnPageHandlerExecuting`, lista blanca `AccionesDeCliente`) aunque se arme la petición a mano. |
| Entrada pública vs. interna | nginx fija siempre la cabecera `X-Dian-Entrada` (`publica` o `interna`) y descarta la que mande el cliente; la app solo escucha en `127.0.0.1`, así que solo nginx puede ponerla. Con `Portal:OperadorSoloInterno` (verdadero por defecto) **un operador no puede iniciar sesión por la entrada pública** (recibe el mismo mensaje que una contraseña mala) y una cookie de operador usada ahí recibe **403**. Probado en vivo el 2026-11-09 (`linux/despliegue/pruebas/probar-entrada-publica.sh`). |
| Antifalsificación | Todos los formularios llevan token; cookie antifalsificación `HttpOnly`, `SameSite=Strict`. |
| Cabeceras | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`, `Content-Security-Policy` restrictiva, HSTS en HTTPS. Todo contenido se escapa al mostrarlo. |
| Cargas | Logo: PNG o JPG, hasta 1 MB, **firma de imagen verificada** (no solo la extensión). Certificado: se valida antes de guardar (que abra, que tenga llave privada y que no esté vencido; avisa si el NIT no aparece en el sujeto o si vence en menos de 30 días). Límite de cuerpo multipart de 2 MB. |
| Producción | Para autorizarla hay que cumplir la **lista de verificación** (`ListaProduccion`: emisor, certificado vigente, software de facturación, set de pruebas aprobado, software de producción, clave de API, logo) —los pendientes se revisan en el servidor—, y escribir exactamente la frase `AUTORIZO PRODUCCION`. Solo el operador. |
| Auditoría | `auditoria.jsonl`: quién, cuándo, qué acción, sobre qué cliente. **Nunca** valores secretos. |

## 4. Secretos: cómo se guardan

- PIN, clave del certificado y secreto del webhook se guardan **cifrados** (`ProtectorSecretos`, ASP.NET Data Protection) y se descifran **solo en memoria** al resolver el tenant.
- Las llaves de Data Protection quedan en disco **sin cifrar** (en Linux no hay DPAPI): quien lea la carpeta de llaves puede descifrar los secretos. Permisos restringidos al usuario del servicio y respaldo **junto con** el almacén (sin las llaves, los secretos no se recuperan). Mejora posible: cifrarlas con un certificado.
- La clave del certificado que se pasa a la herramienta de importación va en una **variable de entorno**, no en un argumento ni en un archivo.
- Las claves de API se guardan como hash; no hay forma de recuperarlas: se genera otra.

## 5. Lo que nunca se imprime, registra ni commitea

Contraseñas (de usuarios, de certificados, de bases de datos), PIN del software DIAN, claves de API (el registro solo lleva el id del cliente), secretos del webhook y llaves de Data Protection; tampoco el contenido de `MtxSoftware.xml` ni el de `config-local`. Si una herramienta necesita mostrar una clave nueva, lo hace **una sola vez** en su respuesta y no la guarda. Las contraseñas de prueba se generan aleatorias o se piden por consola; no se escriben en ejemplos ni en la documentación.

## 6. Lista de verificación de seguridad para un cambio

- ¿Puede un usuario o un ERP alcanzar datos de **otro** cliente? (acceda siempre por el tenant; en el portal, `PuedeVer` y 404)
- ¿Hay una acción nueva en el Admin? Si es del operador, ¿está **fuera** de la lista blanca de acciones del cliente?
- ¿Se imprime o registra algún secreto, incluso en un mensaje de error?
- ¿Se escapa todo lo que se muestra? ¿Los formularios llevan token?
- ¿Algo nuevo puede mandar a **producción** sin pasar por `EsHabilitacion` y la autorización del operador?
- ¿Algo nuevo acepta un archivo? Valide tamaño y contenido real.
- ¿Se agrega una petición saliente (como el webhook)? ¿Sin seguir redirecciones, con tiempo máximo, y con destino fijado por el cliente?

## 7. Riesgos conocidos

| Riesgo | Estado |
|---|---|
| El portal público **no tiene verificación en dos pasos** ni **correo de recuperación** de contraseña (falta el contrato del EmailAPI). | Abierto. Hasta tenerlo, contraseñas generadas largas. |
| Llaves de Data Protection sin cifrar en disco. | Abierto; mitigado con permisos y aislamiento del servicio (`systemd`). |
| La URL del webhook admite `http` o `https` sin usuario ni clave; **no se verificó** que se rechacen destinos de redes internas (SSRF) desde el servidor. | **No verificado.** Revisar antes de ofrecer webhook a clientes no confiables. |
| Los administradores y la auditoría están en archivos: con varias instancias de `Admin.Web` habría que pasarlos a MySQL. | Abierto (una instancia hoy). |
| Una caída o un error de aislamiento en la instancia compartida afectaría a todos sus clientes. | Mitigado con pruebas por cliente, registros por cliente e instancias dedicadas para clientes críticos (decisión 5.8 del plan). |
| `MtxSoftware.xml` apunta a producción. | Nunca usarlo para enviar. |

## No verificado

- Revisión de código completa de `Admin.Web`: las cifras (210 000 iteraciones, 5 intentos, 15 minutos, 12 caracteres, 30 minutos, 1 MB) se leyeron en `AdminUsuarios.cs`, `ClienteAdminServicio.cs`, `Program.cs` y `Roles.cs`; el comportamiento de cada página no se re-probó en esta revisión.
- La validación de la URL del webhook (`ConfigurarWebhookAsync`) se conoce por la documentación de la fase 4 ("http o https, sin usuario ni clave"); no se leyó el método.
- Límites de tasa de nginx y de acceso por red: ver el despliegue; no son parte del código.
