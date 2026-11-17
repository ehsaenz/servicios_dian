# Problemas conocidos, pendientes y deuda técnica

Última revisión: 2026-11-16

Lista viva de lo que **no está hecho o no está verificado**. Cada punto sale del código o de las bitácoras en `DIAN\docs` (dentro del repositorio raíz DIAN). Cuando resuelva uno, bórrelo de aquí y anote el cambio en el commit.

## 1. Servicio de documentos

| # | Pendiente | Detalle |
|---|---|---|
| 1 | **Correo al adquiriente sin implementación** | `IEnviadorCorreo` existe, pero `Program.CrearSalidas` no registra ninguna: no sale ningún correo. Falta el adaptador del servicio **EmailAPI**; solo se conoce `IMiddleEmail.EnviarMail(parametros)` y `GetTracking`. Hace falta el contrato completo. |
| 2 | **Contenedor `AttachedDocument`** | El servicio no genera el contenedor; el correo (cuando exista) adjuntaría XML, respuesta de la DIAN y PDF por separado. `GeneradorContenedor` existe en `Documentos`. |
| 3 | **Plantillas de NC, ND y documento soporte** | Solo hay plantilla de factura (`01`). NC y ND quedan aprobadas con `PdfMensaje` "no hay plantilla". En el modelo de impresión de las notas la resolución sale vacía. |
| 4 | **Almacén de documentos en archivos** | `Datos:Carpeta`, un solo servidor y **un solo proceso escribe**. Pasarlo a MySQL (y soportar varias instancias) está pendiente. |
| 5 | **Validación de matrícula/sucursal** | La API anterior valida la licencia por matrícula mercantil; la nueva imprime con el perfil de la sucursal que coincida, o con el del cliente si no hay. Propuesta sin decidir: si el cliente tiene sucursales, rechazar un documento cuya matrícula no corresponda a ninguna. |
| 6 | **Envío en segundo plano con `Task.Run`** | Sin cola persistente: si el proceso se reinicia a mitad de camino, `ReintentosWorker` recupera los `Recibido` después de 2 minutos. Solo funciona con el almacén de la Admin (el índice `tenants.json` antiguo no es enumerable). |
| 7 | **PDF "unos instantes" después de aprobar** | Al consultar justo tras la aprobación puede verse `Aprobado` con `Pdf: false`: el PDF se genera después de guardar el estado. El webhook se envía ya con el PDF listo. |
| 8 | **Notas que referencian notas** | Solo se referencian facturas (algoritmo CUFE). |
| 9 | **Sin probar** | Persona natural, exportación, contingencia, líneas excluidas y retenciones con datos reales, impuestos distintos del IVA, crédito con plazo; notas crédito y débito y webhook real **en la VM**; documentos equivalentes distintos del POS y sus notas de ajuste; evento RADIAN de reclamo. |
| 10 | **Webhook contra una URL real** | La entrega HTTP se probó con entrega simulada; falta una prueba contra una URL real. |
| 11 | **Concepto de las notas** | `documento_ref.Concepto` es opcional; sin él la nota crédito sale como devolución parcial (1) y la débito como otros (4). **QuantyERP debe empezar a enviarlo.** |

## 2. Nómina y RADIAN

- **Habilitación de nómina en el portal de la DIAN** sin completar para MARLIT: sin ello, el envío síncrono responde con la regla 92 y, con el set ya aceptado, la consulta solo informa el estado del set (código `2`) y no el resultado de cada documento. Es una limitación de la habilitación, no del código.
- **No migrado desde la API anterior:** usuarios y login (`UsuariosController`), importación de nómina desde Excel (`ExcelController`), maestros DIAN (`MaestrosController`), PDF y correo de nómina, historial de eventos. La aceptación tácita (`034`) ya estaba "en desarrollo" en la API anterior.
- Los eventos RADIAN solo se aceptan sobre facturas a crédito y en orden (acuse, recibo, aceptación); un ERP debe esperar unos segundos entre un evento y el siguiente (la DIAN los registra de forma asíncrona).

## 3. Admin y portal

| # | Pendiente |
|---|---|
| 1 | **Verificación en dos pasos** y **correo de bienvenida/recuperación** de contraseña (depende del contrato del EmailAPI). |
| 2 | Alta de **administradores** y recuperación de contraseña solo por consola (`crear-admin`); falta hacerlo desde la web. |
| 3 | Los **rangos de numeración** por cliente no se guardan (hoy los envía el ERP en cada documento). |
| 4 | **Aviso automático de certificados por vencer** (hoy se ve en la lista de clientes y en la lista de producción). |
| 5 | **Subida del certificado por el cliente**: hoy solo la hace el operador (decisión por defecto; opcional). |
| 6 | Administradores y auditoría **en archivos**: con varias instancias de `Admin.Web` habría que pasarlos a MySQL. |
| 7 | La **página del cliente** (sucursales, webhook) compila y está probada por pruebas automáticas, pero no tuvo revisión visual exhaustiva. |
| 8 | `importar-cliente` deja un cliente **a medias** si falla a mitad (hay que borrarlo a mano); no hay rollback. |
| 9 | La validación de la URL del webhook contra destinos internos (SSRF): **no verificada**. |

## 4. Datos (MySQL)

- **Migración de columnas**: `EsquemaMySql.sql` solo usa `CREATE TABLE IF NOT EXISTS`; agregar una columna a una tabla existente no tiene mecanismo definido. (Las tablas nuevas, como `dian_sucursales` y `dian_webhooks`, se crean solas con `crear-esquema`.)
- Las pruebas de MySQL (`MySqlTenantStoreTests`) pasaron el 2026-11-07 contra un MySQL 8.4 de la VM de pruebas; el caso de **sucursales y logos de sucursal en MySQL** y la tabla `dian_webhooks` se añadieron después y las bitácoras indican que **no se han corrido contra una base MySQL real**.

## 5. Plataforma y dependencias

| # | Tema | Detalle |
|---|---|---|
| 1 | **DLL propia de WCF** (`libs/WCF`) | Compilada contra una versión vieja (5.0) de dotnet/wcf para agregar la suite `Basic256Sha256Rsa15`. Una actualización de .NET puede romperla. La reconstrucción desde el fork **no se ha intentado**. Salida a largo plazo: proponer la suite a dotnet/wcf o llamar a la DIAN como SOAP crudo con `HttpClient` y firma WS-Security propia. |
| 2 | **Licencia de DevExpress** | Que la suscripción cubra el despliegue en el servidor Linux (decisión 5.9 del plan) es un tema contractual **sin confirmar**. La prueba técnica no mostró marca de evaluación. |
| 3 | **Empaquetado de `pdf/`** | Se hace a mano; el instalador **no agrega** las variables `Pdf__*` a `dian.env`. |
| 4 | **`System.Drawing.Common`** | El generador de PDF necesita la 4.7.2 y la API fija la 10.0.14: conviven por `AssemblyLoadContext`. No unificar. |
| 5 | **Fuentes en Windows** | Con DirectWrite hay que registrar solo la variante Regular (con las cuatro, todo sale en negrita). |
| 6 | **Avisos del compilador** | Quedan avisos heredados (clases criptográficas obsoletas, `WebRequest`). Las vulnerabilidades NU1902/NU1903 de `System.Security.Cryptography.Xml` y `.Pkcs` se resolvieron subiéndolas a 10.0.14. |
| 7 | **`XadesValidator`** | No funciona en .NET Core (ni en la copia original: usa tipos internos de .NET Framework). Para validar firmas use `SignedXml.CheckSignature` en un proceso aparte del que firma (`FirmaXades.Pruebas`). La ruta OCSP de FirmaXades usa tipos solo de Windows; no se usa para firmar a la DIAN. |
| 8 | **`GeneradorFactura` no usa `Emisor.RazonSocial`** | El nombre sale de `DatosComerciales.NombreComercial`. |
| 9 | **Reglas de la DIAN que enseñaron los rechazos** | Ciudades y departamentos como en la lista de la DIAN (`Cali`, `Valle del Cauca`, no en mayúsculas, salvo en la factura); el código postal `N/A` no vale cuando el documento lo exige; las tarifas llevan dos decimales; los rangos de numeración se consultan a la DIAN, no se suponen; el modelo del ERP trae marcadores `48`/`49` que **no** son códigos DIAN y no deben ir en `TaxLevelCode`. |

## 6. Cambios pendientes en QuantyERP (otro repositorio)

- Enviar `documento_ref.Concepto` en notas crédito y débito.
- Cliente HTTP de `api/documento` (cambiar la ruta y la autenticación del `RealDianCloudClient` a `x-api-key`), consulta por `cambios`/`estado`.
- `DianWorker`: al detectar un documento autorizado, **descargar y guardar el PDF oficial** de la API.
- Impresión: si está autorizado, el PDF oficial; si no, borrador con la marca **"DOCUMENTO NO VALIDADO POR LA AUTORIDAD TRIBUTARIA"**. Caso conservador: si la DIAN aprobó pero el ERP aún no lo sabe, imprime borrador (nunca al revés).

## 7. Operación e infraestructura (resumen; detalle en [../despliegue/README.md](../despliegue/README.md))

- El servidor de pruebas (`quanty-lab`, VM Hyper-V) es de **pruebas**: falta definir el servidor definitivo antes de clientes reales.
- Hoy se publica por `https://services.sistemas360.com:47125`; la URL `https://dian.sistemas360.com` sin puerto exige reenviar el 443 en el router y cambiar el DNS.
- Respaldo **fuera del servidor** y llave privada del respaldo guardada aparte: sin automatizar.
- Los datos de MARLIT (cliente, certificado, XML) siguen en la VM de pruebas; la decisión de borrarlos está pendiente.
- Servicios antiguos que siguen vivos: FE (una instancia .NET Framework por cliente, hasta que se decida la fase 4), Licencias (`Radian.Gateway`) y Maestros DIAN: **siguen separados** de la API única (decisión 5.4).

## 8. Primer cliente en producción

Ningún cliente tiene autorizada la producción. El **primer documento real** exige: autorización expresa del responsable, un cliente piloto, certificado vigente (el de prueba de MARLIT vence el 2027-08-25), software de producción registrado ante la DIAN y la lista de verificación del Admin completa.

## Límites identificados en la revisión documental

- Factura y soporte no envían a un SET por TestSetId, aunque el panel permita guardarlo.
- La lista de producción depende de factura y evalúa hasta 1.000 registros recientes; puede bloquear clientes exclusivos de otro módulo.
- Retirar producción no cambia el ambiente de registros pendientes ni cancela envíos en curso.
- La importación y migración no ofrecen una transacción global entre todos los archivos y registros.

Detalle y fuentes: [revisión documental](revision-documental.md).

## No verificado

- Cada fila se tomó de las bitácoras `fase1-resumen-cierre.md`, `fase3-nomina-radian.md`, `fase4-fe-pdf-webhook.md`, `fase2-app-admin.md`, `plan-unificacion-api.md` y de lecturas puntuales del código (en particular la ausencia de un `IEnviadorCorreo` registrado en `Program.cs`). El estado puede haber cambiado desde su fecha; confirme contra el código antes de actuar.
- El punto 5.6 (reemplazo de `BouncyCastle.NetCore` por 2.x) estaba pendiente en la bitácora de la fase 1; el `FirmaXades.csproj` actual referencia `BouncyCastle.Cryptography 2.6.2`, así que probablemente ya se resolvió, pero no se confirmó y por eso no figura como pendiente.
