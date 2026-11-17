# Plan de unificación de servicios DIAN en una sola API

**Estado:** borrador. Decisiones tomadas el 2026-11-07: 5.1 (.NET 10 LTS), 5.7 (host Linux) y 5.8 (multiempresa). Las demás decisiones de la sección 5 siguen abiertas.
**Avance:** las fases 0 y 1 están cerradas (2026-11-07); ver `fase1-resumen-cierre.md`. Las bibliotecas compartidas están migradas y probadas en habilitación, en Windows y en Linux. La siguiente es la fase 2 (API multiempresa para POS y DSNO).
**Fecha:** 2026-11-07
**Alcance:** los servicios de facturación electrónica de `C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN`.

---

## 1. Objetivo

Reemplazar los proyectos independientes por una sola API en .NET 10 LTS, sin romper el soporte a los usuarios actuales ni forzar la migración de facturación electrónica antes de tiempo.

Motivación: cada obligación DIAN se construyó en un proyecto nuevo, con el stack vigente en ese momento. Hoy hay cuatro versiones de .NET distintas (.NET Framework, 5, 6 y 8), bibliotecas duplicadas y tres formas de acceso a datos.

---

## 2. Estado actual

| Servicio | Proyecto raíz | Framework | Acceso a datos | Estado |
|---|---|---|---|---|
| Facturación electrónica (FE) | `FacturacionElectronica\04_FE_V2_WebService` (`controlfacturasws.csproj`) | .NET Framework 4.8 | MongoDB (`Services\MongoTools.cs`) | Producción |
| FE – bibliotecas de modelo y cliente | `01_FE_V2_Models`, `05_FE_V2_HttpClientLib` | netstandard2.0 (SDK-style) | — | Producción |
| FE – núcleo | `03_FE_V2_Library` (`FacturacionColombia_V2.sln`) | .NET Framework (referencia DLLs de `DEPENDENCIAS`) | — | Producción |
| Nómina electrónica + RADIAN | `NominaRadian.Api\API_NOMINA\API_NOMINA` (`API_NOMINA.csproj`) | net5.0 | MySQL con `MySqlLibrary` (ADO, clases `Ado*`) | Producción |
| POS + Documento Soporte (DSNO) | `PosDocumentoSoporte\eFacturacionColombia_NETCore\FacturacionElectronicaAPI` | net6.0 | Por verificar | Producción / pruebas |
| Licencias (gateway) | `Radian.Gateway` (`MatrixGateway.csproj`) | net8.0 | Por verificar | Por ubicar en el plan |
| Maestros DIAN | `NominaRadian.Api\MaestrosDianAPI\MaestrosDianAPI` (`MaestrosDianAPI.csproj`) | net6.0 | Por verificar | Por ubicar en el plan |

### 2.1 Soporte de .NET

| Versión | Fin de soporte | Servicios afectados |
|---|---|---|
| .NET Framework 4.8 | Soportado (ligado a Windows) | FE |
| .NET 5 | Terminado (mayo 2022) | Nómina/RADIAN |
| .NET 6 | Terminado (noviembre 2024) | POS/DSNO, Maestros DIAN |
| .NET 8 | 10 de noviembre de 2026 | Licencias |
| .NET 10 LTS | Noviembre de 2028 | Destino |

Nómina y POS ya no reciben parches de seguridad de Microsoft. Esto justifica empezar pronto, aunque la unificación sea posterior.

### 2.2 Endpoints actuales

- **Nómina/RADIAN** (`API_NOMINA`): `api/v1/[controller]/[action]`. Controladores `Nomina`, `Eventos`, `Usuarios`, `Excel`, y `Maestros` con `api/[controller]`.
- **POS/DSNO** (`FacturacionElectronicaAPI`): `api/[controller]/[action]`. Controladores `DocumentoElectronico`, `DocumentoSoporte`, `Servicio`.
- **Licencias** (`Radian.Gateway`): `api/Licencias/{NIT}/{APP}` y `api/Licencias/{NIT}`.
- **FE** (`04_FE_V2_WebService`): controladores como `DocumentoController`, con fases 0, 1 y 2 (verificar, firmar y enviar a la DIAN).

### 2.3 Duplicación de bibliotecas

Hay siete pares de bibliotecas con el mismo nombre en Nómina y en POS, cada una con su propia copia del código:

| Biblioteca | Nómina (`NominaRadian.Bibliotecas`) | POS (`PosDocumentoSoporte\...\eFacturacionColombia_NETCore`) |
|---|---|---|
| Documentos | net5.0 | net6.0 |
| Firma | net5.0 | net6.0 |
| Pdf | net5.0 | net6.0 |
| Tipos | net5.0 | net6.0 |
| Util | net5.0 | net6.0 |
| FirmaXades | net5.0 | net6.0 |
| ServiciosWeb | net5.0 | net6.0 |

Cualquier corrección hecha en una copia hay que replicarla a mano en la otra.

---

## 3. Hallazgos que cambian el punto de partida

1. **FE está en .NET Framework, no en .NET Standard.** Solo `01_FE_V2_Models` y `05_FE_V2_HttpClientLib` son netstandard2.0. El web service (`controlfacturasws`, v4.8) y el núcleo (`03_FE_V2_Library`) son .NET Framework. FE es el bloque más grande y el que más riesgo tiene.
2. **RADIAN ya vive dentro de `API_NOMINA`** (`EventosController`, `AdoEventos`). Comparte la API, el framework (net5.0) y la base de datos MySQL con la nómina.
3. **Hay dos proyectos fuera del planteamiento inicial:** `Radian.Gateway` (net8.0, validación de licencias) y `MaestrosDianAPI` (net6.0). Hay que decidir si entran en la unificación.
4. **Hay tres formas de acceso a datos:** MySQL con `MySqlLibrary` (Nómina), MongoDB (FE) y, por verificar, el acceso del POS.
5. **Los binarios compartidos de FE** se publican en la carpeta `DEPENDENCIAS`. Si la API nueva consume FE, hay que decidir si la referencia sigue siendo por DLL o se pasa a `ProjectReference`.
6. **Las bibliotecas netstandard2.0 se pueden consumir desde .NET 10.** Eso permite migrar por servicios sin tocar todas las bibliotecas a la vez.

---

## 4. Opciones de arquitectura

**Opción A – API única en .NET 10 LTS (decidida, decisión 5.1):** una sola solución con un proyecto Web API por dominio (POS/DSNO, Nómina/RADIAN, y FE cuando se decida) y bibliotecas compartidas de una sola copia.

**Opción B – API única en .NET 8: descartada.** Su soporte termina el 10 de noviembre de 2026. Invertir en ella ahora dejaría un stack que hay que volver a migrar en menos de dos años.

**Opción C – Mantener cuatro APIs y compartir solo bibliotecas:** es el paso inicial de la opción A, no un destino final. Permite entregar valor sin unificar endpoints todavía.

> En la práctica la secuencia es C → A: primero bibliotecas compartidas en .NET 10, luego cada servicio migrado, y al final la unificación de endpoints.

---

## 5. Decisiones

- **5.1 Framework destino. Decidido 2026-11-07: .NET 10 LTS.** Se descarta .NET 8 por su fin de soporte y se abandonan .NET 5 y .NET 6 por haber terminado.
- **5.2 Migración de FE. Decidido por delegación 2026-11-08 (revisable): FE se queda como está (una instancia .NET Framework por cliente) hasta la fase 4.** QuantyERP envía hoy sus facturas de venta a esa API (`api/acceso/login` + `api/documento/agregar`), que no existe en la API unificada; por eso el ERP todavía no puede apuntar a la API nueva para FV. La fase 4 portará el núcleo de FE a .NET 10 reutilizando las bibliotecas ya migradas. Pregunta original: ¿FE pasa a .NET 10 en la unificación, o se queda en .NET Framework detrás de un adaptador mientras tanto? Antes de decidir conviene confirmar qué partes de FE pueden migrar sin reescribir el web service.
- **5.3 Acceso a datos. Decidido por delegación 2026-11-08: MySQL detrás de una interfaz (ya implementado para los tenants con `ITenantStore`); sin EF Core ni MongoDB en la API unificada.** Pregunta original: ¿MySQL con `MySqlLibrary` (como Nómina), EF Core o Dapper? ¿Y MongoDB para FE se mantiene? **Recomendación:** `IDatabaseProvider`, que ya usa `Radian.Gateway` y coincide con la directiva general de acceso a datos del ecosistema QuantyERP. El POS no necesita base de datos hoy.
- **5.4 Licencias y Maestros DIAN. Decidido por delegación 2026-11-08: siguen separados de la API única** (no bloquean a ningún cliente; se revisan en la fase 5). Pregunta original: ¿Entran en la API única o siguen separados?
- **5.5 Rutas públicas. Decidido por delegación 2026-11-08: se conservan las rutas actuales** (`api/v1/Nomina/...`, `api/DocumentoElectronico/...`, `api/v1/Eventos/...`), como ya está implementado. Pregunta original: ¿Se conservan las rutas actuales (`api/v1/Nomina/...`, `api/DocumentoElectronico/...`) para no romper clientes, o se define un esquema nuevo con redirecciones?
- **5.6 Orden de migración. Propuesto, pendiente de confirmación.** Bibliotecas compartidas primero, luego POS/DSNO, luego Nómina/RADIAN, y FE al final.
- **5.7 Host de producción. Decidido 2026-11-07: Linux.** Obliga a reemplazar `System.Drawing` en las bibliotecas de PDF, a revisar fuentes y certificados en Linux, y a que FE (hoy .NET Framework en IIS) no pueda compartir host con la API unificada hasta que se migre o se exponga detrás de un adaptador.
- **5.8 Modelo de despliegue. Decidido el 2026-11-07: código multiempresa; instancia compartida para los 22 clientes del servidor propio e instancia dedicada para los 10 con servidor propio.** Hoy hay 32 clientes. Diez tienen su propio servidor y 22 están en un servidor del propietario. Cada cliente funciona con una instancia por cada grupo de servicios (FE, Nómina/RADIAN y POS/DSNO), es decir, hasta 96 instancias, y administrarlas es muy difícil. Además, en 2027 se suman QuantyERP y una integración con Bitrix24, que traerán más clientes. Los archivos `ConfigAPI_*.json` de la carpeta `CONFIG` son configuraciones de depuración, no una lista de clientes.
  **Recomendación:** el código es multiempresa desde el inicio, y el despliegue se decide por cliente con el mismo programa. Dos revisiones anteriores de este plan fueron descartadas porque partían de datos incompletos.
  - **Los 22 clientes del servidor propio:** una instancia compartida multiempresa. Pasan de hasta 66 instancias a una por servicio unificado. El certificado de estos clientes ya está bajo tu custodia hoy, así que centralizarlo no agrega una responsabilidad nueva.
  - **Los 10 clientes con servidor propio:** una instancia dedicada con la misma aplicación y un solo tenant. Pasan de hasta 30 instancias a 10. Su certificado sigue en su servidor. Si algún día aceptan pasar a la instancia compartida, es un cambio de configuración, no de código.
  - **Un cliente es un tenant.** Una clave de API (`x-api-key`) identifica al tenant y apunta a su configuración: emisor, ambiente (habilitación o producción), certificado, perfil de correo, rangos de numeración y el software DIAN **por tipo de documento**, porque FE, Nómina, DSNO y POS pueden tener softwares registrados distintos ante la DIAN. Hay que verificarlo con la configuración real de un cliente.
  - **Configuración de tenants en MySQL** mediante `IDatabaseProvider`, no en archivos JSON. Con más de 32 clientes y altas por Bitrix24 y QuantyERP, el archivo por cliente no escala. Para migrar sin riesgo se empieza con un proveedor que lee los JSON actuales detrás de la misma interfaz (`ITenantConfigProvider`).
  - **Secretos protegidos.** La clave del certificado va cifrada o en un almacén de secretos, nunca en texto plano, y los `.pfx` en una carpeta con permisos restringidos. Los certificados se mantienen en caché por tenant para no cargarlos en cada solicitud.
  - **Sin estado global estático.** Las bibliotecas compartidas reciben la configuración por parámetro y no leen `Global.CfgApi`. Hay que verificarlo en cada biblioteca durante la fase 1.
  - **FE sigue como hasta ahora** mientras no se migre (fase 4): una instancia por cliente en .NET Framework. La unificación multiempresa empieza con POS/DSNO y Nómina/RADIAN.
  - **Salida gradual:** el ERP del cliente solo cambia la URL y la clave. Se migra un tenant a la vez, empezando por un cliente piloto, y se mantienen las rutas actuales.
  - **Riesgos:** una caída de la instancia compartida afecta a todos sus tenants, y un error de aislamiento podría firmar con el certificado equivocado. Se mitiga con pruebas automáticas por tenant, registros separados por tenant y con instancias dedicadas para los clientes críticos. Durante la migración habrá clientes en versiones distintas, por lo que hay que llevar una tabla de versión por cliente.
- **5.9 Generación de PDF. Propuesta del usuario (2026-11-07), pendiente de definir el diseño.** Hoy los tres proyectos arman el PDF a código con iTextSharp, lo que impide ofrecer formatos de documento personalizados por cliente. La propuesta es un servicio de reportes con DevExpress, como el que ya se usa en QuantyERP. Consecuencias para el plan:
  - **La biblioteca `Pdf` no se migra.** Se reemplaza por el servicio de reportes. Desaparecen las 1,960 líneas de divergencia entre las dos copias, el uso de `System.Drawing` y `ZXing.Net`, y la generación de QR a mano (DevExpress trae un control de código QR).
  - **Plantillas por cliente y tipo de documento**, guardadas en la base de datos junto con la configuración del tenant. Cada tipo de documento necesita una plantilla base por defecto equivalente a la salida actual de iTextSharp (factura, notas crédito y débito, nómina, documento soporte, nota de ajuste), para no perder lo que la DIAN exige en la representación gráfica (CUFE o CUNE, QR y datos del emisor).
  - **Seguridad de las plantillas.** Si los clientes pueden cargar plantillas propias, los scripts de reporte deben estar desactivados, porque pueden ejecutar código.
  - **Linux.** Hay que verificar que el motor de reportes funcione en Linux con las fuentes necesarias, y que la licencia de DevExpress cubra ese despliegue.
  - **Por decidir:** si el servicio de reportes se reutiliza como biblioteca dentro de la API (como en QuantyERP) o corre como servicio aparte.

---

## 6. Fases propuestas

Cada fase termina con la solución compilando, las pruebas de humo pasando y los servicios en producción sin cambios de comportamiento.

**Fase 0 – Inventario y verificación (sin cambios de código).**
- Confirmar el acceso a datos del POS, de `Radian.Gateway` y de `MaestrosDianAPI`.
- Comparar las siete copias duplicadas de bibliotecas y listar sus diferencias.
- Buscar en las bibliotecas APIs solo de Windows (por ejemplo `System.Drawing`) y dependencias de WCF, que cambian al pasar a .NET 10.
- Listar los endpoints en uso por cada cliente.
- Definir las pruebas de humo de cada flujo: emisión, firma, envío a la DIAN, RADIAN y DSNO.

**Fase 1 – Bibliotecas compartidas en .NET 10.**
- Una sola copia por biblioteca, solo `net10.0`. Se descartó el multi-target con `netstandard2.0` porque los proyectos antiguos conservan su propia copia y no habría quién lo consumiera (ver `fase1-bitacora-migracion.md`).
- Estado: FirmaXades y Microsoft.Xades migradas el 2026-11-07 (compilan; sin pruebas de firma todavía).
- Mantener los nombres de ensamblado actuales para no romper referencias.
- Validar cada cambio con las pruebas de humo de la fase 0.

**Fase 2 – POS y DSNO a .NET 10 (primer servicio).**
- Es el código más reciente y el que ya está en net6.0, así que la migración es la más corta.
- Sirve para validar las bibliotecas de la fase 1 en .NET 10 antes de tocar Nómina.
- Desplegar en paralelo con la versión anterior y cortar cuando las pruebas de humo pasen.
- Implementar aquí la resolución de tenant (decisión 5.8). La primera instancia compartida atiende primero a un cliente piloto del grupo de 22 y luego al resto.

**Fase 3 – Nómina y RADIAN a .NET 10.** *(Primera entrega hecha el 2026-11-07: ver `fase3-nomina-radian.md`.)*
- Migrar `API_NOMINA` de net5.0 a net10.0.
- Revisar `MySqlLibrary` y `MySql.Data` 8.0.27 para compatibilidad con .NET 10.
- RADIAN sale con Nómina porque comparten proyecto.

**Fase 4 – Facturación electrónica (según 5.2).**
- Los modelos (`01_FE_V2_Models`) y el cliente HTTP (`05_FE_V2_HttpClientLib`) ya son netstandard2.0 y se pueden consumir desde .NET 10 sin cambios.
- El núcleo (`03_FE_V2_Library`) y el web service (`04_FE_V2_WebService`) son .NET Framework y requieren un port o un adaptador. Este es el bloque de mayor riesgo y no debe bloquear las fases 1 a 3.

**Fase 5 – Licencias, Maestros DIAN y retiro de las APIs antiguas.**
- Decidir según 5.4 si `Radian.Gateway` y `MaestrosDianAPI` entran en la API única.
- Retirar las APIs antiguas solo cuando los clientes apunten a la nueva y haya un periodo de convivencia sin incidentes.

### 6.1 Soporte a usuarios durante la migración

- Mientras dure la migración, los errores se corrigen en el proyecto antiguo y, si aplica, se portan a la versión nueva.
- Congelar funcionalidades nuevas en los proyectos antiguos; solo se hacen correcciones.
- Los parches de seguridad de net5 y net6 son urgentes aunque la migración no haya empezado: hay que definir si se aplican o si se adelanta la fase 2.

---

## 7. Riesgos

- **Producción.** FE, Nómina y RADIAN están en uso. Cualquier cambio de ruta o de comportamiento puede rechazar documentos ante la DIAN.
- **Soporte dual.** Mientras dure la migración, un error puede necesitar corrección en dos proyectos. Mitigación: congelar funcionalidades y registrar qué se portó.
- **Bibliotecas duplicadas con divergencia.** Las copias de Nómina y POS pueden diferir sin que nadie lo haya notado. La fase 0 debe comparar las copias antes de unificarlas.
- **APIs de Windows.** Algunas bibliotecas pueden depender de APIs que en .NET 10 solo funcionan en Windows, o de WCF. Revisar en la fase 0.
- **Dependencias de FE.** La carpeta `DEPENDENCIAS` y las DLLs prearmadas acoplan FE a un orden de compilación manual.
- **Certificados y firma.** La firma XAdES y el certificado (`00_CERTIFICADO`) deben seguir funcionando en el nuevo host.
- **Tres formas de acceso a datos.** Un cambio de capa de datos puede afectar transacciones que hoy dependen de MySQL o MongoDB por separado.

---

## 8. Criterios de éxito

- Una sola solución compila y despliega todas las APIs en .NET 10.
- Cada biblioteca compartida existe en una sola copia.
- Los flujos de emisión, firma, envío y RADIAN producen los mismos resultados que antes en las pruebas de humo.
- Ningún cliente cambia de ruta sin un periodo de compatibilidad.
- Las decisiones de la sección 5 están resueltas y confirmadas por el usuario.

---

## 9. Expansión 2027 (preliminar)

Se planea sumar QuantyERP como cliente de la API y una integración de Bitrix24 con facturación electrónica. Esto ya se incluye en el diseño porque cambia el requisito: los clientes nuevos no tienen el mismo ERP ni a ti como proveedor de servidor.

- **Alta de clientes sin intervención manual.** Hará falta un servicio de administración de tenants: crear el emisor, cargar el certificado, definir rangos de numeración y pasar de habilitación a producción. Hoy es un archivo JSON copiado a mano.
- **QuantyERP** consume la API por HTTP, igual que los ERP actuales. Debe usar la misma clave por tenant.
- **Bitrix24** se integra como un conector aparte que llama a la API unificada, no dentro del núcleo. Así los cambios de Bitrix24 no afectan la emisión de documentos.
- **Estado de los documentos:** los clientes de Bitrix24 no tienen un ERP propio que consulte, así que conviene exponer webhooks que informen cuando la DIAN acepta o rechaza un documento.
- **Preguntas abiertas:** alcance exacto de la integración con Bitrix24 (¿emite facturas desde negocios, o solo consulta?) y quién administra el certificado de esos clientes.

---

## Anexo: fuentes revisadas

- `FacturacionElectronica\AGENTS.md` (estructura y orden de compilación de FE).
- `FacturacionElectronica\04_FE_V2_WebService\controlfacturasws.csproj` (.NET Framework 4.8).
- `FacturacionElectronica\01_FE_V2_Models\DianModels.csproj` (netstandard2.0).
- `NominaRadian.Api\API_NOMINA\API_NOMINA\Controllers\*.cs` y `Startup.cs`.
- `PosDocumentoSoporte\eFacturacionColombia_NETCore\FacturacionElectronicaAPI\Controllers\*.cs`.
- `Radian.Gateway\Controllers\V1\LicenciasController.cs` y `Program.cs`.
- Búsqueda de `TargetFramework` en todos los `.csproj` de la carpeta DIAN.
