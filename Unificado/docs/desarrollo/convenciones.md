# Convenciones del proyecto

Última revisión: 2026-11-16

## 1. Git

| Tema | Regla |
|---|---|
| Repositorio | La raíz Git es `DIAN`. Remoto: `https://github.com/ehsaenz/servicios_dian.git`; los remotos anteriores permanecen en el respaldo local. |
| Ramas | La rama inicial del repositorio consolidado es `dev`; las ramas antiguas se conservan en los historiales respaldados. |
| Quién sube | Quien hace los commits no sube: el responsable del repositorio revisa y hace el `push`. |
| Identidad | Autor de los commits: la del responsable (`Eduardo Saenz`, correo del repositorio). Los commits que redacta un asistente de IA llevan además la línea `Co-Authored-By: …` al final del mensaje. |
| Mensajes | En **español**: línea de título que dice el cambio (no el archivo) y un **cuerpo** que explica el porqué, el alcance y cómo se verificó. |
| Tamaño | Un commit por cambio coherente (una fase, un arreglo). No mezclar documentación, código y configuración de despliegue sin relación. |
| Proyectos antiguos | `NominaRadian.*`, `PosDocumentoSoporte\…`, `FacturacionElectronica\…` y `Radian.Gateway` permanecen solo en el equipo local, excluidos del repositorio publicado y no se modifican durante la migración, salvo correcciones de soporte que se portan aquí. |

Ejemplo de mensaje:

```
Admin: lista de verificación para pasar un cliente a producción

La página del cliente muestra qué falta antes de autorizar producción… (qué, por qué)
Lo bloqueante se revisa también en el servidor, no solo en el botón. (alcance)
Probado: 32 pruebas del Admin correctas. (verificación)
```

## 2. Qué nunca va al repositorio

El `.gitignore` ya lo excluye; **verifique con `git status` antes de cada commit**.

| Excluido | Por qué |
|---|---|
| `config-local/` | Certificados, software y PIN de habilitación, datos de prueba y almacenes locales. |
| `*.p12`, `*.pfx`, `*.pem`, `*.key` | Certificados y llaves privadas. |
| `MtxSoftware.xml` | **Apunta a producción.** Nunca lo use para enviar desde desarrollo. |
| `almacen/`, `llaves/`, `datos/`, `cache/` | Datos y secretos de clientes. |
| `bin/`, `obj/`, `.vs/`, `TestResults/`, `dist/` | Generados. |
| Referencias externas en `docs/` | Anexos técnicos y cajas de herramientas; las cuatro colecciones propias sí se versionan. |

Además:

- **Ningún dato personal real** en el código, en pruebas ni en ejemplos: nombres, documentos de identidad, correos, teléfonos. Use datos ficticios (y que se vean ficticios).
- **Ningún secreto** en el código ni en los mensajes de commit: contraseñas, PIN, claves de API, claves de certificados, cadenas de conexión con clave. Los secretos van en variables de entorno o en el almacén cifrado.
- Si un secreto llegó a un commit: **no lo borre con un commit nuevo**; avise al responsable para rotarlo y reescribir el historial.

La configuración actual usa `docs/*` y excepciones para `README.md`, `desarrollo/`, `despliegue/`, `mantenimiento/` e `integracion/`. Verifique los archivos con `git status --short --untracked-files=all`; no agregue anexos ni secretos al publicar las guías.

## 3. Código

- **Destino:** `net10.0`, una sola copia de cada biblioteca compartida (`src/Shared`), sin multi-target. Los nombres de ensamblado y de espacio de nombres heredados (`eFacturacionColombia_NETCore.*`, `FacturacionColombia_Metadatos`, `DianModelDoc`, `FE.Models.Doc`) **no se renombran**: mantienen el contrato con el ERP y las referencias.
- **Idioma:** nombres de dominio, mensajes de error y de registro, y comentarios en español (el código de las bibliotecas heredadas mezcla inglés por los tipos UBL de la DIAN).
- **Capas:** `DataModels → DataServices → Business → Api` del ecosistema QuantyERP se refleja aquí como Api (controladores delgados) → servicios (`Pos.Servicios`, `Nomina.Servicios`) → bibliotecas compartidas → `Tenants`. Un controlador no contiene lógica de negocio.
- **Sin estado global estático:** las bibliotecas reciben su configuración por parámetro. (El `Global.CfgApi` de las APIs anteriores no se porta.)
- **Hora:** siempre la hora de Colombia (`DateTimeHelper`); nunca `DateTime.Now` del servidor.
- **Errores de usuario:** excepciones propias (`ErrorAdministracion`, `DocumentoNoTraducibleException`, `DocumentoNoAprobadoException`, `SinPlantillaException`) con mensajes claros en español; el controlador las convierte en 400/409/503 según el caso.
- **Nunca registre ni imprima secretos:** ni la clave de API (solo el id del cliente), ni PIN, ni claves de certificados, ni secretos del webhook, ni contraseñas. Ver [seguridad.md](seguridad.md).
- **Compilación limpia:** la solución compila sin errores; no agregue avisos nuevos (quedan avisos heredados de clases criptográficas obsoletas).

## 4. Datos y SQL

- **El MySQL de la API de facturación electrónica es moderno (8.x).** La restricción de **MySQL 5.1.73** (sin `CHARSET`/`COLLATE` en `CREATE TABLE`, sin funciones nuevas) es del ERP QuantyERP; para este proyecto fue una decisión explícita (2026-11-07) **no** exigir esa compatibilidad. Aun así, el esquema actual es simple (InnoDB, sin claves foráneas, sin `CHARSET`) y la base debe crearse en `utf8mb4`: conserve ese estilo.
- **Nombres:** si la llave primaria se llama `IdEntidad`, la referencia se llama `EntidadId` (`dian_clientes.IdCliente` ↔ `dian_claves.ClienteId`).
- **Idempotencia:** el script de esquema debe poder ejecutarse varias veces (`CREATE TABLE IF NOT EXISTS`).
- **Terceros:** el identificador de un tercero (cédula/NIT) es alfanumérico, no numérico.

## 5. Documentación

- Toda funcionalidad nueva se documenta en la carpeta correspondiente de `docs/` (desarrollo, despliegue, mantenimiento, integración) en el mismo cambio.
- Los registros históricos de decisiones y bitácoras por fase están en `DIAN\docs`, dentro del repositorio raíz (`plan-unificacion-api.md`, `fase1-…` a `fase4-…`). Cuando algo de allí pase a ser norma, copie lo esencial a esta documentación.
- Escriba para quien viene después: qué hace, por qué, cómo se prueba y qué **no** se verificó.

## 6. Reglas de seguridad operativa durante el desarrollo

- **Nada va a producción sin autorización expresa del responsable.** Los clientes de prueba, los scripts de verificación y las herramientas apuntan a **habilitación**. `Habilitacion.Pruebas` aborta si el endpoint no es de habilitación.
- No toque datos de bases de otros sistemas (por ejemplo, la base del ERP en el servidor de pruebas) ni los sitios de otros servicios que comparten el servidor.
- Las descargas de software (SDK, paquetes, DLL) las confirma el responsable.
- Los cambios en servidores (instalar, abrir puertos, certificados) se anuncian y se confirman antes de ejecutarlos.

## No verificado

- El `git config user.name`/`user.email` local coincide con la identidad documentada (se vio en los commits recientes, no se revisó el archivo de configuración).
- Que existan hooks de git o integración continua: no se encontró ninguno, pero no se buscó en el servidor del remoto.
