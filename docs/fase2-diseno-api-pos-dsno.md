# Fase 2: diseño de la API multiempresa para POS y Documento Soporte

**Fecha:** 2026-11-07 · **Estado:** aprobado el 2026-11-07 con la app Admin añadida (sección 3.8); en ejecución desde el paso 2.1
**Base:** `plan-unificacion-api.md` (decisiones 5.3, 5.8), `fase1-resumen-cierre.md`.

## 1. Cómo funciona hoy

La API actual (`PosDocumentoSoporte\...\FacturacionElectronicaAPI`) atiende a **un solo cliente por instancia**:

- La configuración sale de `CONFIG\ConfigAPI.json` y se guarda en una variable estática (`Global.CfgApi`). Contiene el emisor, el software por tipo de documento y el certificado (nombre de archivo y **clave en texto plano**).
- Cada servicio (`DocumentoSoporte`, `DocumentoEquivalente`, `NotaAjustePos`) lee esa variable estática y carga el certificado por su cuenta, en cada solicitud.
- Las rutas son `api/DocumentoElectronico/{EnviarDE, EnviarDEJson, ConsultarDE, GetQRCode*}`, `api/DocumentoSoporte/{EnviarDSNO, ConsultarDSNO}` y `api/Servicio/GetAmbientePruebas`.
- No hay autenticación: quien llega a la URL, envía.
- Quedó código de prueba dentro del flujo: el correo de `EnviarDSNO` va a una dirección fija de prueba con el perfil `SIS03`.
- El documento soporte solo se acepta si el adquiriente es el emisor configurado (regla que se conserva).

## 2. Objetivo

Una sola aplicación .NET 10 que atienda a varios clientes (tenants) sin que uno pueda tocar los datos o el certificado de otro, con las mismas rutas y los mismos cuerpos JSON que hoy, para que el ERP del cliente solo cambie la URL y agregue una clave.

## 3. Diseño

### 3.1 Proyectos

| Proyecto | Contenido |
|---|---|
| `src\Shared\Tenants` | `TenantConfig`, `ITenantConfigProvider`, `ICertificadoProvider`, resolución del tenant. Sin ASP.NET: lo puede usar Nómina después. |
| `src\Pos\Pos.Servicios` | Lógica de documento soporte, documento equivalente POS y nota de ajuste, migrada de los tres `Servicios\*.cs`. Recibe el tenant por parámetro. |
| `src\Pos\Pos.Api` | ASP.NET, middleware de autenticación y controladores delgados. |
| `src\Admin\Admin.Web` | App web de administración (sección 3.8). |
| `tests\Pos.Pruebas` | Pruebas de aislamiento entre tenants y de los endpoints. |

### 3.2 Identificar al cliente

- Cada petición lleva `x-api-key`. Un middleware la busca, **comparando su hash SHA-256** (la base nunca guarda la clave), y deja el `TenantConfig` en el contexto de la petición.
- Sin clave o con clave desconocida o inactiva: 401. Una ruta pública mínima de salud, sin datos.
- Cada registro del log lleva el id del tenant. Nunca se registran claves, PIN ni contraseñas.

### 3.3 Configuración del tenant

```
TenantConfig
  Id, Activo
  Emisor: Nit, DV, RazonSocial, MatriculaMercantil, Email, NumeroTel
  Ambiente: Habilitacion | Produccion          (por tipo de documento)
  Software[tipo]: Identificador, PIN, TestSetId (tipos: DocumentoSoporte, DocumentoEquivalente)
  Certificado: referencia al archivo + secreto
  Correo: perfil (opcional)
```

**Dos proveedores detrás de la misma interfaz `ITenantConfigProvider`:**

1. **`JsonTenantConfigProvider`** (primero): lee los `ConfigAPI*.json` actuales, para migrar sin riesgo y probar con MARLIT.
2. **`MySqlTenantConfigProvider`** (después), con `IDatabaseProvider`. Esquema compatible con MySQL 5.1, sin `CHARSET` ni `COLLATE`:

```sql
CREATE TABLE tenants (
  IdTenant        INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Nit             VARCHAR(20) NOT NULL,
  DV              VARCHAR(2) NOT NULL,
  RazonSocial     VARCHAR(200) NOT NULL,
  MatriculaMercantil VARCHAR(30) NULL,
  Email           VARCHAR(150) NULL,
  NumeroTel       VARCHAR(30) NULL,
  Activo          TINYINT NOT NULL DEFAULT 1
);
CREATE TABLE tenant_claves (
  IdClave         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TenantId        INT NOT NULL,
  HashClave       CHAR(64) NOT NULL,
  Descripcion     VARCHAR(100) NULL,
  Activa          TINYINT NOT NULL DEFAULT 1,
  UNIQUE KEY uq_tenant_claves_hash (HashClave)
);
CREATE TABLE tenant_software (
  IdSoftware      INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TenantId        INT NOT NULL,
  TipoSoftware    INT NOT NULL,
  Identificador   VARCHAR(40) NOT NULL,
  PIN             VARCHAR(255) NOT NULL,
  TestSetId       VARCHAR(40) NULL,
  AmbientePruebas TINYINT NOT NULL DEFAULT 1
);
CREATE TABLE tenant_certificados (
  IdCertificado   INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  TenantId        INT NOT NULL,
  Archivo         VARCHAR(255) NOT NULL,
  ClaveCifrada    VARCHAR(500) NOT NULL,
  Vence           DATE NULL
);
```

### 3.4 Certificados

- Los `.p12` viven en una carpeta con permisos restringidos, fuera de la carpeta de la aplicación.
- La clave va **cifrada** (ASP.NET Data Protection) o se toma de una variable de entorno; nunca en texto plano en JSON ni en base.
- Se cargan una vez por tenant con `X509CertificateLoader` y se guardan en caché. Se recargan si el archivo cambia. Se avisa en el log cuando faltan 30 días para el vencimiento.
- Prueba de aislamiento: dos tenants con certificados distintos firman, y el sujeto del certificado en cada XML coincide con su tenant.

### 3.5 Sin estado global

- Se elimina `Global.CfgApi`. Los servicios son `Scoped` y reciben el `TenantConfig` en el constructor.
- Los archivos temporales (XML, ZIP) se escriben en `datos\{IdTenant}\...`, con nombre único por solicitud. Sin `Console.WriteLine`.
- `ServicePointManager.SecurityProtocol` se quita; en .NET 10 el sistema negocia TLS.

### 3.6 Rutas y compatibilidad

- Se **conservan** `api/DocumentoElectronico/{EnviarDE, EnviarDEJson, ConsultarDE}`, `api/DocumentoSoporte/{EnviarDSNO, ConsultarDSNO}` y `api/Servicio/GetAmbientePruebas`, con los mismos cuerpos JSON y las mismas respuestas (`ResponseDian`).
- El 94 (nota de ajuste del POS) y el 20 (tiquete POS) siguen en `EnviarDE`.
- **No entran en esta fase:** `GetQRCode*` (dependen del servicio de reportes, decisión 5.9) y el envío de correo (se decide aparte; hoy tiene datos de prueba escritos en el código).

### 3.7 Seguridad de producción

- Cada tenant tiene su ambiente. **Enviar a producción exige un indicador explícito en la configuración del tenant**, que solo se activa con la autorización del cliente. Por defecto, habilitación.
- Límite de tamaño del cuerpo y límite de solicitudes por clave.
- El documento soporte sigue exigiendo que el adquiriente sea el NIT y la razón social del tenant.

### 3.8 App web Admin

Para dar de alta clientes sin tocar archivos ni base de datos a mano.

- **Tecnología:** Blazor Server en .NET 10, en la misma solución. Sin paso de compilación de JavaScript y con el mismo stack que el resto.
- **Qué permite, por cliente:** datos del emisor, ambiente, software y PIN por tipo de documento, TestSetId, rangos de numeración, **subir el certificado `.p12` y su clave**, **subir el logo**, perfil de correo, activar o desactivar el cliente, **generar y revocar claves de API** (la clave completa se muestra una sola vez; la base guarda solo el hash).
- **Validaciones al guardar:** el certificado abre con la clave dada, muestra su sujeto y vencimiento, y el NIT del sujeto coincide con el del emisor (o avisa si no). Opción de "probar contra habilitación" (consulta de rangos a la DIAN) antes de activar.
- **Acceso:** usuarios administradores propios (tabla `admin_usuarios`, contraseña con hash PBKDF2), bloqueo tras intentos fallidos, sesión corta. Se despliega separada de la API pública y solo accesible por VPN o red interna. Más adelante se puede pasar a inicio de sesión único de QuantyERP.
- **Almacenamiento de secretos:** el `.p12` y el logo se guardan en la carpeta restringida `secretos\{IdTenant}\`; la clave del certificado se cifra con ASP.NET Data Protection. La app Admin y la API comparten esa carpeta y las llaves de cifrado (en la instancia dedicada, solo hay un cliente).
- **Auditoría:** tabla `admin_auditoria` (quién, cuándo, qué cambió; nunca valores secretos).
- Las tablas de 3.3 ganan: `tenants.Logo`, `admin_usuarios`, `admin_auditoria`, y `tenant_rangos` (prefijo, desde, hasta, resolución, tipo).

## 4. Pasos

| Paso | Qué se hace | Cómo se sabe que terminó |
|---|---|---|
| 2.1 | `Tenants`, proveedor JSON, middleware de clave | **Hecho 2026-11-07:** `src\Shared\Tenants`, `src\Pos\Pos.Api` (solo middleware, `/salud` y `/api/Servicio/Quien` temporal), `tests\Pos.Pruebas` (12 pruebas: clave válida, inválida, inactiva, aislamiento de certificados, producción bloqueada por defecto) |
| 2.2 | Documento soporte (`EnviarDSNO`, `ConsultarDSNO`) | **Hecho 2026-11-07:** `src\Pos\Pos.Servicios` y controladores en `Pos.Api` (`EnviarDSNO`, `ConsultarDSNO`, `EnviarDE` para el tipo 05, `GetAmbientePruebas`, `Quien`). SEDS984000509 de MARLIT autorizado en habilitación a través de la API nueva. 18 pruebas en `Pos.Pruebas` (con enviador falso) y una prueba manual real (`DIAN_REAL=1`). |
| 2.3 | Documento equivalente POS y nota de ajuste | **Hecho 2026-11-07:** `DocumentoEquivalenteServicio` (tiquete 20) y `NotaAjustePosServicio` (94) en `Pos.Servicios`; `EnviarDE` y `ConsultarDE` los atienden. EPOS7 y NATP3 de MARLIT autorizados en habilitación a través de la API nueva. 25 pruebas automáticas en `Pos.Pruebas` y una prueba real manual (`HabilitacionRealPosTests`). |
| 2.4 | Segundo cliente (SISTEMAS 360, certificado real) | **Hecho 2026-11-07:** `tests\Pos.Pruebas\AislamientoRealTests.cs` envía 4 tiquetes intercalados de MARLIT y SISTEMAS 360 (mismo número) con un enviador falso: cada XML firmado lleva la huella del certificado de su cliente, un cliente no puede enviar a nombre del otro y las carpetas no se mezclan. Pasó con los dos certificados reales. No envió nada a la DIAN. Se corre con `DIAN_AISLAMIENTO=1`, `MARLIT_P12`, `SIS360_P12` y `config-local\CertificadoSISTEMAS.p12`. |
| 2.5 | Almacén MySQL | **Hecho 2026-11-07:** `MySqlTenantStore` (`dian_clientes`, `dian_claves`, `dian_software`, `dian_archivos`), comandos `crear-esquema` y `migrar-almacen`, 10 pruebas contra un MySQL 8.4 real; ver `fase2-app-admin.md`. Destino MySQL 8.x: no se exige compatibilidad con 5.1.73. |
| 2.5b | App Admin (alta de cliente, certificado, logo, claves, auditoría) | **Hecho 2026-11-07** sobre un almacén de archivos (`FileTenantStore`): ver `fase2-app-admin.md`. Razor Pages en lugar de Blazor. Un cliente dado de alta desde el admin envía un tiquete por la API con su clave (prueba automática con enviador falso). Falta cambiar el almacén a MySQL (2.5). |
| 2.6 | Despliegue en Linux (VM `quanty-lab`) y piloto, **en habilitación** | **Hecho 2026-11-07:** documento soporte SEDS984000510, tiquete EPOS9 y nota NATP5 de MARLIT autorizados por la API corriendo en Linux; ver `fase2-despliegue-linux.md`. |

## 5. Decisiones confirmadas (2026-11-07)

1. Las tablas de tenants van en una base propia de la API (`dian_api`).
2. Las claves de API y toda la configuración se gestionan desde la app web Admin (3.8).
3. Correo y QR quedan fuera de esta fase.
4. Primer cliente: **MARLIT GROUP** (habilitación). Segundo cliente para la prueba de aislamiento: **SISTEMAS 360**, con su certificado real (solo para firmar y comparar el sujeto del certificado en local; no se envía nada a la DIAN sin que se configure su software de habilitación).

## 6. Notas del paso 2.2

- Las respuestas conservan la forma de la API anterior (`ResponseDian`), con dos diferencias: los mensajes de la DIAN se clasifican siempre en `Errors` (rechazos) y `Warnings` (notificaciones), también en habilitación, donde antes salían en un solo error `DIAN001`; y `UUID` ahora trae el CUDS del documento.
- La comparación de la razón social del adquiriente ignora mayúsculas y espacios en los extremos; el NIT sigue exacto.
- `EnviarDE` responde 501 para el tiquete POS (20) y la nota de ajuste (94) hasta el paso 2.3.
- `/api/Servicio/Quien` queda como diagnóstico permanente: dice con qué cliente identificó la clave.
- Los archivos generados van en `datos\{IdCliente}\XML`. Se quitó el envío de correo (fuera de esta fase).
- Prueba real manual: `DIAN_REAL=1`, `DIAN_REAL_CONSECUTIVO=<n>` y `MARLIT_P12`, con `dotnet test tests\Pos.Pruebas --filter HabilitacionRealTests`.
- Consecutivos libres: SEDS 984000510.

## 7. Notas del paso 2.3

- **Idempotencia:** reenviar un tiquete ya enviado consulta a la DIAN y devuelve el resultado en vez de duplicarlo; solo un rechazo explícito permite volver a transmitir. Lo mismo para la nota de ajuste.
- **Habilitación:** si el software del cliente tiene `TestSetId`, el tiquete y la nota se envían al set de pruebas y se consulta su resultado (como la API anterior); sin `TestSetId` el envío es síncrono. Producción siempre es síncrono. La espera entre enviar y consultar el set se configura con `Dian:EsperaSetSegundos` (5 por defecto).
- **Datos que la DIAN exige y el ERP debe enviar.** Hallazgos de la prueba con MARLIT. **En producción son obligatorios** (la API responde 400 si faltan); solo en habilitación se completan solos, para facilitar las pruebas:

  | Dato | Regla DIAN | Habilitación (si falta) | Producción (si falta) |
  |---|---|---|---|
  | `Adquiriente.CodigoCliente` y `Adquiriente.NombreComercial` (tiquete) | DEPD16, DEPD18 | identificación y razón social del adquiriente | 400 |
  | `Emisor.PrefijoRangoNumeracion` (nota de ajuste) | NAAJ49 | letras iniciales del número (`NATP3` da `NATP`) | 400 |
  | `DetallePago.Identificador` (nota de ajuste) | estabilidad del reenvío | `NP` + número de la nota | 400 |

  El identificador de pago por defecto de la biblioteca es "NP" más la hora actual; se trata como "no enviado" porque haría distinto el XML de cada intento y bloquearía el reenvío de la misma nota (409).
- **Fuera de esta fase:** PDF, imagen del QR, correo y los endpoints `GetQRCode*`. El texto del QR sí se devuelve en `StrQR`.
- **Prueba real manual:** `DIAN_REAL=1`, `DIAN_REAL_POS=<consecutivo>`, `DIAN_REAL_NOTA=<consecutivo>` y `MARLIT_P12`, con `dotnet test tests\Pos.Pruebas --filter HabilitacionRealPosTests`.
- **Consecutivos libres en la habilitación de MARLIT:** EPOS 8, NATP 4 (NATP1 y NATP2 y EPOS5 se rechazaron por los datos de prueba y no se reutilizan).

