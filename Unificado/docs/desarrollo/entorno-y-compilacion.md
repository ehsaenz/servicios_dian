# Entorno y compilación

Última revisión: 2026-11-16

## 1. Requisitos

| Qué | Detalle |
|---|---|
| SDK | .NET 10 (`global.json` fija `10.0.401` con `rollForward: latestFeature`). |
| Sistema | Windows 11 o Linux para desarrollo; el servidor de ejecución es Linux (Ubuntu 24.04). |
| Para la solución | Nada más: `dotnet build` y `dotnet test` en un clon nuevo compilan y pasan. |
| Para PDF y plantillas | **DevExpress Reporting 24.1** instalado (por defecto en `%ProgramFiles%\DevExpress 24.1\Components\Bin\NetCore`). Solo lo necesitan `Impresion.DevExpress`, `GeneradorReportes` y `PruebaLinux`, que están **fuera** de la solución. Si DevExpress está en otra ruta, defina `DevExpressBinPath` o `DevExpress24_1Dir`. La licencia de DevExpress para el despliegue en servidor está sin confirmar (ver [problemas-conocidos.md](problemas-conocidos.md)). |
| Fuentes | **Liberation Sans** (licencia libre OFL) en `reportes/fuentes`: viaja con la aplicación y se registra en DevExpress. No hace falta instalar ninguna fuente en el servidor. |
| Linux, ejecución | `tzdata` (zona `America/Bogota`) y, normalmente, ICU; si falta ICU, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. Salida a `vpfe-hab.dian.gov.co` (habilitación) por el 443. Ver `linux/LEEME-LINUX.md`. |

## 2. Compilar y probar

Desde la carpeta `Unificado`:

```bash
dotnet build DIAN.Unificado.sln
dotnet test DIAN.Unificado.sln
```

- Las pruebas que necesitan secretos, la DIAN o MySQL se **omiten solas**. Detalle en [pruebas.md](pruebas.md).
- Para publicar para Linux (autocontenido), como en la guía de despliegue:

```bash
dotnet publish src/Pos/Pos.Api -c Release -r linux-x64 --self-contained true -o dist/servicios/Pos.Api
dotnet publish src/Admin/Admin.Web -c Release -r linux-x64 --self-contained true -o dist/servicios/Admin.Web
```

El empaquetado completo y la instalación están en [../despliegue/README.md](../despliegue/README.md) (y en `linux/despliegue/LEEME.md`).

- Compilar los proyectos con DevExpress (solo en una máquina que lo tenga):

```bash
dotnet build src/Shared/Impresion.DevExpress/Impresion.DevExpress.csproj
```

## 3. Estructura del repositorio

| Carpeta | Contenido |
|---|---|
| `src/Shared` | Bibliotecas compartidas, una sola copia. |
| `src/Pos`, `src/Nomina`, `src/Admin` | API, servicios y app web. La facturación electrónica está implementada en `Pos.Api`, `Pos.Servicios/Documentos` y bibliotecas compartidas. |
| `tests` | Proyectos de prueba y herramientas de habilitación. |
| `tools/Reportes` | Herramientas de plantillas PDF (fuera de la solución). |
| `reportes` | Plantillas `.repx` (origen y generada), muestras y `fuentes/`. |
| `libs/WCF` | `System.Private.ServiceModel.dll` propia (ver abajo). |
| `linux` | Paquete de pruebas para Linux y `despliegue/` (instalador, nginx, systemd, scripts). |
| `config-local` | **Secretos y datos de prueba. No se versiona** (ver [convenciones.md](convenciones.md)). |
| `dist` | Paquetes generados. No se versiona. |
| `docs` | Esta documentación (ver la nota del [README](README.md) sobre el `.gitignore`). |

### La DLL de WCF (`libs/WCF`)

La DIAN exige la suite de seguridad `Basic256Sha256Rsa15`, que el paquete oficial `System.ServiceModel` no trae. `ServiciosWeb.csproj` referencia por `HintPath` una DLL propia, compilada desde un fork de dotnet/wcf. Versión, SHA-256, archivos modificados y procedimiento de reconstrucción: `libs/WCF/LEEME.md`. Funciona en Windows y Linux; la reconstrucción desde el fork no se ha intentado. **Si una actualización de .NET rompe el envío a la DIAN, esta DLL es la primera sospechosa**: las pruebas de habilitación son la alarma.

## 4. Configuración de ejecución

Las aplicaciones leen configuración de argumentos (`--Clave:Sub=valor`), variables de entorno (`Clave__Sub=valor`) y `appsettings`. Claves usadas:

| Clave (variable de entorno) | La usa | Para qué |
|---|---|---|
| `Almacen:Carpeta` (`Almacen__Carpeta`) | API y Admin | Almacén de clientes en archivos (admin-usuarios.json, auditoria.jsonl, `clientes/`). |
| `MySql:Cadena` (`MySql__Cadena`) | API y Admin | Si existe, el almacén de clientes es MySQL en lugar de archivos. |
| `Cache:Carpeta` | API y Admin | Copia local de certificado y logos cuando se usa MySQL. |
| `Secretos:Llaves` (`Secretos__Llaves`) | API y Admin | Llaves de Data Protection. **Misma carpeta en ambas.** |
| `Datos:Carpeta` (`Datos__Carpeta`) | API (escribe), Admin (lee) | XML, PDF y estado de los documentos. |
| `Tenants:Carpeta` | API | Índice `tenants.json` antiguo; solo para migrar. |
| `Pdf:Generador`, `Pdf:Carpeta`, `Pdf:Plantillas`, `Pdf:Fuentes` | API | Carga del generador de PDF (ver [plantillas-pdf.md](plantillas-pdf.md)). |
| `Documentos:EnvioEnSegundoPlano` | API | `true` por defecto; las pruebas lo ponen en `false`. |
| `Documentos:ReintentoSegundos` | API | Periodo del trabajo de reintentos (60; 0 lo apaga). |
| `Dian:EsperaSetSegundos` | API | Espera al consultar el set de pruebas (5 por defecto). |
| `Portal:OperadorSoloInterno` | Admin | `true` por defecto: un operador no puede entrar por la entrada pública. |
| `ADMIN_CLAVE` (variable) | Admin, comando `crear-admin` | Contraseña del primer administrador, para no pedirla por consola. |

Si las dos aplicaciones no apuntan a **las mismas** carpetas de almacén y llaves (o a la misma base MySQL), no se ven entre sí.

## 5. Arranque local de las dos aplicaciones

```bash
# Primer administrador (contraseña de 12+ caracteres, por consola o en ADMIN_CLAVE)
dotnet run --project src/Admin/Admin.Web -- crear-admin <usuario> --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<llaves>

# App Admin (solo en este equipo)
dotnet run --project src/Admin/Admin.Web -- --urls http://127.0.0.1:5280 --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<llaves> --Cache:Carpeta=<cache>

# API (mismas carpetas)
dotnet run --project src/Pos/Pos.Api -- --Almacen:Carpeta=<carpeta> --Secretos:Llaves=<llaves> --Datos:Carpeta=<trabajo>
```

Use carpetas de `config-local` (ignoradas por git). Comandos de consola de `Admin.Web`:

| Comando | Qué hace |
|---|---|
| `crear-admin <usuario>` | Crea un operador. |
| `crear-esquema` | Crea las tablas `dian_*` en MySQL (requiere `MySql:Cadena`). Repetible. |
| `migrar-almacen` | Copia los clientes del almacén de archivos a MySQL (sin sobrescribir). |
| `importar-cliente <id> <config.json> [--clave-cert-env=VARIABLE] [--generar-clave=true]` | Alta de un cliente desde un JSON en el formato de la API anterior, con su certificado. La clave del certificado va por **variable de entorno**; con `--generar-clave=true` la última línea de la salida es la clave de API (se muestra una sola vez). Las opciones propias usan la forma `--opcion=valor`. |

## 6. `config-local` (no versionado)

Datos de prueba y secretos que usan las herramientas de habilitación y algunas pruebas manuales (ver [pruebas.md](pruebas.md)): certificado `.p12`, `emisor-marlit.json`, `habilitacion-sw-propio.json`, `emisor-rut.json`, `documento-soporte.json`, `nomina.json`, `pos.json`, y subcarpetas de almacenes de prueba. **`MtxSoftware.xml` apunta a producción: no lo use para enviar.** La clave del certificado va en una variable de entorno (`MARLIT_P12`, `SIS360_P12` o `FIRMA_PFX_CLAVE`), nunca en un archivo.

## No verificado

- Que `dotnet build` y `dotnet test` de un clon limpio terminen sin errores en esta revisión (se verificó en sesiones anteriores, no se repitió hoy).
- El procedimiento exacto para armar la carpeta `pdf/` del paquete de la API (DLL de DevExpress y Skia, `plantillas/`, `fuentes/`): se hizo a mano; no hay un script en el repositorio. Está descrito en [plantillas-pdf.md](plantillas-pdf.md) a partir del resultado en `dist/servicios/Pos.Api/pdf`.
- La ruta de instalación de DevExpress en otras máquinas.
