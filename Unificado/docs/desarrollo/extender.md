# Cómo extender el sistema

Última revisión: 2026-11-16

Recetas para los cambios más comunes. Todas parten de los patrones que ya usa el código; antes de empezar, lea [arquitectura.md](arquitectura.md) y [convenciones.md](convenciones.md). Regla general: **cada cambio lleva su prueba** (ver [pruebas.md](pruebas.md)) y, si cambia el comportamiento visible, su documentación.

## Agregar un endpoint a la API

1. **Servicio primero.** La lógica va en `Pos.Servicios` (o `Nomina.Servicios`), no en el controlador. Los controladores son delgados: leen el cuerpo, llaman al servicio y traducen el resultado a HTTP.
2. **Controlador** en `src/Pos/Pos.Api/Controllers`. Dos estilos conviven: el nuevo (`DocumentoController`: `[Route("api/documento")]`, rutas por atributo `[HttpGet("estado/{numero}")]`) y el heredado de las APIs anteriores (`api/[controller]/[action]`, para conservar las rutas de los clientes). **Una ruta existente no se cambia**: los ERP ya la usan (decisión 5.5 del plan).
3. **Cliente de la petición:** `HttpContext.ObtenerTenant()` (el `TenantMiddleware` ya autenticó con `x-api-key`). Ningún dato de otro cliente debe ser alcanzable: acceda siempre por el tenant.
4. **Servicio por tenant:** si necesita un servicio nuevo, agréguele un método de fábrica en `ServiciosPos` (`ServiciosPos.Documentos(tenant)` es el ejemplo) y regístrelo donde corresponda en `Program.cs`.
5. **Rutas públicas:** solo `/salud`. Cualquier otra exige clave; no abra excepciones en el middleware.
6. **Respuestas:** conserve el formato JSON de la API (`PropertyNamingPolicy = null`, enumeraciones como texto, tildes sin escapar).
7. **Prueba:** `WebApplicationFactory<Program>` con la DIAN simulada (patrón de `DocumentoApiTests`), incluyendo el caso "otro cliente no lo ve".

## Agregar o ajustar un tipo de documento electrónico

Hoy `DocumentoServicio.Agregar` soporta `TipoDocDian` `01` (factura), `91` (nota crédito) y `92` (nota débito). Para añadir otro:

1. **Traductor.** Una función nueva en `src/Shared/Traductor` (o en `TraductorNota` si es una nota) que convierta `DocumentoDian` a los tipos UBL usando los generadores de `src/Shared/Documentos`. Reutilice los terceros, líneas e impuestos del traductor de factura; **no recalcule impuestos** (los importes salen de las cuentas del propio modelo). Rechace lo no soportado con `DocumentoNoTraducibleException`.
2. **Firma.** Revise que `FirmaElectronica` tenga el método para ese tipo (`FirmarFactura`, `FirmarNotaCredito`, `FirmarNotaDebito` hoy).
3. **Servicio.** Un `case` nuevo en el `switch (doc.TipoDocDian)` de `DocumentoServicio.Agregar`.
4. **Lector de impresión.** `LectorDocumentoAprobado` reconoce hoy el elemento raíz `Invoice`, `CreditNote` y `DebitNote`; un tipo nuevo exige ampliarlo (y su prueba de rechazo si la respuesta no es `02`).
5. **Plantilla PDF** (ver abajo) y su entrada en `GeneradorPdfDevExpress`.
6. **Pruebas:** ida y vuelta (JSON → XML → lector → modelo), pruebas de API con la DIAN simulada y, antes de darlo por bueno, **un envío real a habilitación** con `Habilitacion.Pruebas` (los generadores de la solución se validan contra la DIAN, no solo contra el esquema).
7. Documente el campo o tipo nuevo en [../integracion/README.md](../integracion/README.md).

## Agregar un campo al modelo del cliente (tenant)

Ejemplo real: el webhook (`WebhookUrl`, `WebhookSecretoProtegido`).

1. **Modelo.** Propiedad en `ClienteRegistro` (lo que se guarda; los secretos con sufijo `Protegido`) y, si el servicio lo necesita en ejecución, en `TenantConfig` (con el valor ya descifrado, solo en memoria).
2. **Resolución.** `StoreTenantConfigProvider` copia de `ClienteRegistro` a `TenantConfig` y desprotege los secretos.
3. **Almacén de archivos.** `FileTenantStore` serializa el registro a `cliente.json`: normalmente no requiere cambios, pero un JSON viejo sin el campo debe seguir leyéndose (valor por defecto razonable).
4. **Almacén de MySQL.** `MySqlTenantStore` y `src/Shared/Tenants/EsquemaMySql.sql`. El script usa **`CREATE TABLE IF NOT EXISTS`**: sirve para tablas nuevas, pero **no agrega columnas a una tabla que ya existe**. Para una columna nueva en una tabla existente hace falta una migración explícita (o una tabla nueva, como se hizo con `dian_sucursales` y `dian_webhooks`). El mecanismo para migrar columnas no está definido todavía: ver [problemas-conocidos.md](problemas-conocidos.md). El esquema no usa claves foráneas ni `CHARSET`/`COLLATE`; la llave de `dian_clientes` es `IdCliente` y su referencia `ClienteId`.
5. **Servicio de administración.** Un método en `ClienteAdminServicio` que valide, guarde y **registre en `Auditoria`** (sin valores secretos).
6. **Migrador.** Si el dato son archivos o filas, revise `MigradorMySql` para que la migración archivos → MySQL lo copie.
7. **Pruebas:** `Pos.Pruebas/AdminServicioTests` y, para MySQL, `Admin.Pruebas/MySqlTenantStoreTests` (manual, base desechable).

## Agregar una página al Admin

1. **Página Razor** en `src/Admin/Admin.Web/Pages` (`Nombre.cshtml` + `Nombre.cshtml.cs`). Todo exige sesión por defecto (política de autorización de respaldo); solo `/Login` es anónima.
2. **Rol.** Si es solo del operador: `[Authorize(Roles = Roles.Operador)]` en el modelo (como `Auditoria`). Si es de un cliente, **valide el alcance en el servidor** con `User.PuedeVer(clienteId)` y responda **404** (no 403) cuando no corresponde, para no revelar qué ids existen.
3. **Acciones reservadas al operador.** En `Cliente` hay una lista blanca (`AccionesDeCliente`) revisada en `OnPageHandlerExecuting`: toda acción `OnPost…` que no esté en la lista se rechaza (403) para un usuario de cliente, aunque se arme la petición a mano. Si agrega una acción que el cliente sí puede hacer, **inclúyala explícitamente**.
4. **Lógica y auditoría** en `ClienteAdminServicio`, no en la página. Nunca registre valores secretos en la auditoría ni en el registro.
5. **Secretos mostrados una sola vez** (claves de API, secreto del webhook, contraseñas generadas): se exponen como propiedades de la respuesta, no se guardan.
6. **Formularios** con token antifalsificación (los de Razor lo llevan). Todo texto se escapa al mostrarlo.
7. **Enlace** en `Pages/Shared/_Layout.cshtml` (el de Auditoría solo se muestra al operador).
8. **Pruebas** en `Admin.Pruebas/AdminWebTests.cs`: caso del operador, caso del cliente propio, caso de otro cliente (404) y caso de acción reservada (403).

## Agregar una plantilla PDF

1. **Diseño.** Parta de la plantilla de QuantyERP del tipo de documento y adáptela con `tools/Reportes/GeneradorReportes` (cambiar fuentes a Liberation Sans, bloque DIAN, quitar scripts). Edite el generador, no el `.repx` resultante.
2. **Datos.** La plantilla recibe el JSON de `DocumentoImpresion`; si necesita campos nuevos, agréguelos al modelo y llénelos en `LectorDocumentoAprobado`.
3. **Registro.** Entrada nueva en el diccionario de `GeneradorPdfDevExpress` (`"<TipoDocDian>" → "<archivo>.repx"`) y copia de la plantilla a la carpeta `Pdf:Plantillas` del paquete.
4. **Revisión.** Genere la muestra PDF/PNG, revísela a ojo y repita en Linux con `PruebaLinux` (las fuentes y los anchos cambian entre motores).
5. **Seguridad.** Las plantillas se cargan **sin scripts**: no dependa de ellos.
6. Detalle: [plantillas-pdf.md](plantillas-pdf.md).

## Agregar una implementación de correo (EmailAPI)

`IEnviadorCorreo` (en `Pos.Servicios/Documentos/Salidas.cs`) tiene un solo método, `Enviar(destinatario, asunto, cuerpo, adjuntos)`. Falta el adaptador del servicio EmailAPI (hoy solo se conoce `IMiddleEmail.EnviarMail`/`GetTracking`) y asignarlo en `Program.CrearSalidas` (`salidas.Correo = ...`). Un fallo del correo no debe cambiar el estado del documento (ya lo garantiza `DocumentoServicio.EnviarCorreo`).

## No verificado

- Los nombres de los métodos de `FirmaElectronica` y las clases de las firmas de nota se tomaron de `DocumentoServicio`; no se revisó la clase completa.
- El mecanismo de migración de columnas nuevas en MySQL: no existe un procedimiento documentado; la receta describe lo que se deduce del script (`CREATE TABLE IF NOT EXISTS`).
- `MigradorMySql` se cita por su nombre y propósito (copia de clientes, certificados y logos); no se leyó su código.
