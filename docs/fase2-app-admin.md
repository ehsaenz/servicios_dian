# App Admin: alta y mantenimiento de clientes

**Fecha:** 2026-11-07 · **Proyecto:** `Unificado\src\Admin\Admin.Web` (ASP.NET Razor Pages, .NET 10) · **Pruebas:** `tests\Admin.Pruebas` (15) y `tests\Pos.Pruebas\AdminServicioTests.cs` (13)

## Qué permite

Desde el navegador, sin tocar archivos a mano:

- Crear un cliente (id, NIT, DV, razón social) y completar sus datos de emisor y perfil de correo.
- Configurar el **software DIAN por tipo de documento** (documento soporte y documento equivalente POS): identificador, PIN, TestSetId y ambiente.
- **Subir el certificado `.p12` con su clave.** Se valida antes de guardar: que abra, que tenga llave privada y que no esté vencido; avisa si el NIT no aparece en el sujeto o si vence en menos de 30 días.
- **Subir el logo** (PNG o JPG, hasta 1 MB, se comprueba el contenido real del archivo).
- **Generar y revocar claves de API.** La clave completa se muestra una sola vez; el almacén guarda solo su hash SHA-256.
- Activar o desactivar un cliente, y **autorizar o retirar producción** (exige escribir `AUTORIZO PRODUCCION` y que el cliente tenga certificado y software).
- Ver la **auditoría** de cambios (quién, cuándo, qué), sin valores secretos.

## Cómo se pone en marcha

1. Compilar y crear el primer administrador. La contraseña (mínimo 12 caracteres) se pide por consola o viene en `ADMIN_CLAVE`; no se guarda en ningún archivo de configuración.

```bash
dotnet run --project Unificado/src/Admin/Admin.Web -- crear-admin <usuario> --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<carpeta de llaves>
```

2. Arrancar la app, por defecto solo accesible desde el mismo equipo:

```bash
dotnet run --project Unificado/src/Admin/Admin.Web -- --urls http://127.0.0.1:5280 --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<carpeta de llaves>
```

3. Arrancar la API pública (`Pos.Api`) con **las mismas** dos carpetas. Es lo que comunica las dos aplicaciones: la API lee los clientes que la app Admin crea y descifra lo que esta protege.

```bash
dotnet run --project Unificado/src/Pos/Pos.Api -- --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<carpeta de llaves> --Datos:Carpeta=<carpeta de trabajo>
```

## Dónde queda cada cosa

| Qué | Dónde |
|---|---|
| Datos de un cliente | `{Almacen}\clientes\{id}\cliente.json` |
| Certificado y logo | `{Almacen}\clientes\{id}\certificado.p12` y `logo.png/jpg` |
| Administradores | `{Almacen}\admin-usuarios.json` (PBKDF2-SHA256, sal propia) |
| Auditoría | `{Almacen}\auditoria.jsonl` |
| Llaves de cifrado (PIN y claves de certificado) | `{Secretos:Llaves}` (ASP.NET Data Protection) |
| XML generados por la API | `{Datos:Carpeta}\{id}\XML` |

## Seguridad

- Todas las páginas exigen sesión (cookie `HttpOnly`, `SameSite=Strict`, 30 minutos con renovación). Todos los formularios llevan token antifalsificación.
- Cinco contraseñas incorrectas bloquean al usuario 15 minutos, y quedan registradas.
- PIN y claves de certificado se guardan **cifrados**; nunca en claro en `cliente.json`.
- Cabeceras de seguridad (`X-Frame-Options`, `Content-Security-Policy`, `nosniff`, sin caché) y todo el contenido se escapa al mostrarlo.
- Nada de lo secreto va a la auditoría ni al registro.

## Lo que hay que tener en cuenta al desplegar

- **Poner la app detrás de HTTPS** (proxy inverso) y accesible solo por VPN o red interna. No escucha fuera de `127.0.0.1` salvo que se cambie `--urls`.
- **La carpeta de llaves y la del almacén deben tener permisos restringidos** al usuario que ejecuta las aplicaciones. Las llaves de Data Protection quedan en disco sin cifrar (en Linux no hay DPAPI); quien las lea puede descifrar los PIN y las claves de certificado. Mejora posible: cifrarlas con un certificado.
- **Respaldos:** hay que respaldar el almacén y las llaves juntos; sin las llaves, los secretos guardados no se pueden recuperar.
- El certificado y su clave quedan en la máquina que ejecuta la API. Para los 10 clientes con servidor propio, la instancia dedicada usa su propio almacén con un solo cliente.

## Almacén en MySQL (paso 2.5)

Además del almacén de archivos hay uno en MySQL. Se activa con la cadena de conexión (variable `MySql__Cadena` o `--MySql:Cadena=`), tanto en la API como en la app Admin; si no hay cadena, se usa el de archivos. El destino es MySQL moderno (8.x): la API de facturación electrónica **no necesita compatibilidad con el 5.1.73** del ERP (decisión del 2026-11-07). La base debe crearse en `utf8mb4`.

- **Tablas** (`src\Shared\Tenants\EsquemaMySql.sql`): `dian_clientes`, `dian_claves`, `dian_software` y `dian_archivos` (certificado y logo, en `LONGBLOB`). Sin claves foráneas ni `CHARSET` en las tablas.
- **Comandos de la app Admin:** `crear-esquema` crea las tablas (se puede repetir) y `migrar-almacen` copia los clientes del almacén de archivos a MySQL, con sus certificados y logos, sin sobrescribir los que ya existen.
- **Varias instancias:** cada una guarda la lista en memoria y la vuelve a leer cuando otra la cambió (se comprueba cada 2 segundos), así que una clave revocada o un certificado reemplazado llegan a todas en ese plazo.
- **Ediciones simultáneas:** cada cliente tiene un número de versión; si dos administradores editan a la vez, el segundo recibe "El cliente cambió mientras lo editaba" en lugar de pisar el cambio.
- **Archivos:** certificado y logo viven en la base y se copian a una carpeta local (`Cache:Carpeta`, permisos 700 en Linux), porque el resto del sistema los lee por ruta.
- **Qué sigue en archivos:** los administradores de la app y la auditoría. Para una sola instancia de la app Admin no hay problema; con varias habría que pasarlos también a MySQL.

```bash
export MySql__Cadena='Server=<servidor>;Database=<base>;Uid=<usuario>;Pwd=<clave>;CharSet=utf8mb4'
dotnet Admin.Web.dll crear-esquema
dotnet Admin.Web.dll migrar-almacen --Almacen:Carpeta=<carpeta del almacén de archivos> --Secretos:Llaves=<las mismas llaves>
```

La clave de la cadena de conexión no se guarda en ningún archivo del proyecto. Las llaves de cifrado (`Secretos:Llaves`) siguen siendo las mismas: si se migra, la API y la app deben seguir usando las llaves con las que se cifraron los PIN.

**Pruebas:** `tests\Admin.Pruebas\MySqlTenantStoreTests.cs` (10 pruebas: esquema repetible, alta y resolución por clave, secretos cifrados en la base, id repetido, ediciones simultáneas, cambios entre dos instancias, logo reemplazado, caracteres especiales, copia aislada y migración). Solo corren con `DIAN_MYSQL` apuntando a una base **desechable** (vacían las tablas `dian_*`). Pasaron el 2026-11-07 contra un MySQL 8.4 de la VM `quanty-lab`; la base y el usuario de prueba se borraron al terminar.

## Lo que falta

- Los **rangos de numeración** por cliente no se guardan (hoy los envía el ERP en cada documento).
- Recuperación de contraseña y gestión de administradores desde la web (por ahora, por consola).
- Aviso automático de certificados por vencer (hoy se ve en la lista de clientes).

## Portal de clientes (roles)

La misma app sirve a dos tipos de usuario (`Roles.cs`, `AdminUsuarios.cs`):

| | Operador | Usuario de cliente |
|---|---|---|
| Alcance | todos los clientes | solo su cliente (cualquier otro responde 404) |
| Puede | crear clientes, emisor, software, certificado, estado, autorizar producción, usuarios del portal, auditoría | logo, sucursales, claves de API, webhook, ver sus documentos y descargar los PDF aprobados, cambiar su contraseña |

- Los usuarios del portal los crea el operador desde la página del cliente («Usuarios del portal»). La contraseña es aleatoria y se muestra una sola vez; el usuario la cambia en «Mi cuenta». Mismas reglas que el operador: PBKDF2, bloqueo tras 5 intentos.
- Un usuario de cliente cae directo en su cliente al entrar. Las acciones reservadas al operador se rechazan en el servidor aunque se arme la petición a mano (`ClienteModel.OnPageHandlerExecuting`).
- **Documentos:** la página del cliente lista los 50 más recientes (estado, PDF, avisos). La app lee en solo lectura la carpeta de datos de la API (`Datos:Carpeta`, la misma variable en `/etc/dian/dian.env`); el PDF se sirve en `/pdf/{cliente}/{numero}` con el mismo control de alcance.
- **Camino a producción (operador):** la página del cliente muestra una lista de verificación (`ListaProduccion.cs`): emisor, certificado vigente, software de facturación, set de pruebas (facturas aprobadas por la DIAN de habilitación, y avisos si faltan notas crédito/débito), software de producción, clave de API y logo. Los puntos ✖ impiden autorizar producción; se revisa en el servidor además de deshabilitar el botón, y la autorización sigue pidiendo la frase de confirmación.
- Pendiente: verificación en dos pasos, correo de bienvenida/recuperación (falta el contrato del EmailAPI) y despliegue del portal por HTTPS.
