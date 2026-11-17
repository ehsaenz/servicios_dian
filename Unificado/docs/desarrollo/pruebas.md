# Pruebas

Última revisión: 2026-11-16

## 1. Qué hay

| Proyecto | Tipo | Qué prueba |
|---|---|---|
| `tests/Shared.Pruebas` | xUnit | Utilidades, generadores de XML (`GeneradoresTests`), modelo (`ModeloDianTests`), traductores de factura y notas, lector de documento aprobado (`ImpresionTests`). Sin red ni secretos. |
| `tests/Pos.Pruebas` | xUnit + `WebApplicationFactory<Program>` | La API completa con la DIAN **simulada** (`FabricaFalsa`): `api/documento` (`DocumentoApiTests`, PDF y correo, webhook), documento soporte, POS, nómina, eventos RADIAN, tenants y servicio de administración. |
| `tests/Admin.Pruebas` | xUnit + `WebApplicationFactory` | La app Admin: login, roles, portal de clientes, documentos y PDF, lista de producción, entrada pública, importador y almacén de MySQL. |
| `tests/FirmaXades.Pruebas` | Consola | Revisa un certificado y verifica la firma de un XML de forma independiente. |
| `tests/Habilitacion.Pruebas` | Consola | Prueba de punta a punta contra la **habilitación** de la DIAN (genera, firma y envía). No es xUnit. |

Cantidades declaradas (`[Fact]` y `[Theory]`; las teorías se expanden al ejecutar): `Shared.Pruebas` 42, `Pos.Pruebas` 75, `Admin.Pruebas` 27. La última ejecución conocida de `Admin.Pruebas` fue 32 correctas y 11 omitidas (2026-11-09).

## 2. Cómo correrlas

```bash
dotnet test DIAN.Unificado.sln                         # todo
dotnet test tests/Pos.Pruebas                          # un proyecto
dotnet test tests/Pos.Pruebas --filter "FullyQualifiedName~DocumentoApiTests"
```

Un clon nuevo debe pasar sin configurar nada: lo que necesita secretos, la DIAN o MySQL **se omite solo** con `Skip`.

## 3. Pruebas manuales (se omiten sin su variable)

Cada una declara un atributo propio (`MySqlFact`, `AislamientoFact`, `RealFact`, `PilotoFact`…) que fija el `Skip` si falta la variable. La cabecera de cada archivo dice cuáles definir.

| Archivo | Para qué | Variables (nombres; los valores no se versionan) |
|---|---|---|
| `Admin.Pruebas/MySqlTenantStoreTests.cs` | Almacén de clientes en MySQL. **Vacía las tablas `dian_*`**: usar una base desechable. | `DIAN_MYSQL` |
| `Pos.Pruebas/HabilitacionRealTests.cs` y las `HabilitacionReal*Tests` | Envío real a la habilitación de la DIAN (factura, POS, nómina, RADIAN). | `DIAN_REAL=1`, `DIAN_REAL_CONSECUTIVO` y la variable de la clave del certificado (`MARLIT_P12`); según la prueba también `DIAN_REAL_FACTURA`, `DIAN_REAL_NOTA`, `DIAN_REAL_POS`, `DIAN_REAL_EVT`, `DIAN_REAL_NOMINA` |
| `Pos.Pruebas/AislamientoRealTests.cs` | Que dos clientes con certificados distintos no se mezclen. | `DIAN_AISLAMIENTO=1`, `MARLIT_P12`, `SIS360_P12` y el certificado de SISTEMAS en `config-local` |
| `Pos.Pruebas/PilotoRemotoTests.cs` y `PilotoRemotoNominaRadianTests.cs` | Contra una API ya desplegada (por ejemplo en la VM de pruebas). | `DIAN_PILOTO_URL`, `DIAN_PILOTO_CLAVE`, `DIAN_PILOTO_NIT`, `DIAN_PILOTO_RAZON`, consecutivos (`DIAN_PILOTO_DS`, `_POS`, `_NOTA`, …) y `DIAN_PILOTO_INSEGURO=1` si el certificado es autofirmado |
| `Shared.Pruebas/ImpresionTests.cs` | Con un par real (documento + respuesta) y para revisar el PDF a mano. | `DIAN_REAL_DOC` y `DIAN_REAL_AR` (rutas); `DIAN_IMPRESION_JSON`, `DIAN_IMPRESION_CONTENEDOR` (salida) |

Regla: **estas pruebas pueden consumir consecutivos reales de la habilitación**. Antes de correrlas, confirme el siguiente consecutivo libre (la DIAN rechaza repetidos).

## 4. La herramienta de habilitación (`Habilitacion.Pruebas`)

Siempre apunta a **habilitación**; si el endpoint no lo es, aborta. Nunca envía a producción. Los datos salen de `config-local` (fuera del repositorio) y la clave del certificado de una variable de entorno (`MARLIT_P12`, o `FIRMA_PFX_CLAVE`); nunca se imprime.

```bash
dotnet run --project tests/Habilitacion.Pruebas -- generar --consecutivo N [--provisional]   # genera y firma, no envía
dotnet run --project tests/Habilitacion.Pruebas -- enviar-sync --consecutivo N --confirmar   # envía y espera el resultado
dotnet run --project tests/Habilitacion.Pruebas -- enviar --consecutivo N --confirmar        # envía al set de pruebas
dotnet run --project tests/Habilitacion.Pruebas -- consultar --zipkey <ZipKey>
dotnet run --project tests/Habilitacion.Pruebas -- rangos                                    # rangos de numeración (solo lectura)
```

- `--tipo`: `factura` (por defecto), `factura-erp`, `nota-credito`, `nota-debito`, `nota-credito-erp`, `nota-debito-erp`, `documento-soporte`, `nota-ajuste-ds`, `nomina`, `nomina-ajuste`, `evento`, `documento-equivalente`. Las notas, `nota-ajuste-ds`, `evento` y `nomina-ajuste` exigen `--referencia <número del documento ya generado>`.
- Los modos `*-erp` arman el documento **como lo entrega QuantyERP** (`DocumentoDian`), lo pasan por JSON y por el traductor, igual que la API.
- Enviar exige `--confirmar` explícito y no admite `--provisional` (datos de relleno solo valen para generar).
- Con `DIAN_API_URL` y `DIAN_API_KEY`, el documento **no se firma localmente**: se entrega a una API unificada ya desplegada (por ejemplo la de la VM por un túnel SSH) y se consulta su resultado. Es la forma de probar el servicio instalado de punta a punta.
- Otras variables: `HAB_SOFTWARE`, `HAB_ADQUIRIENTE`, `HAB_PAGO`, `HAB_EVENTO`, `HAB_AJUSTE`, `HAB_PROVEEDOR` (variaciones del documento de prueba; ver la cabecera de `Program.cs`).
- Archivos esperados en `config-local` (no versionados): ver [entorno-y-compilacion.md](entorno-y-compilacion.md#6-config-local-no-versionado).

### En Linux

El paquete `dist/DIAN.Unificado.Pruebas.linux-x64.tar.gz` y `linux/probar-linux.sh` repiten la prueba en un servidor Linux (modos `diagnostico`, `local` y `habilitacion`). Instrucciones paso a paso en `linux/LEEME-LINUX.md`.

## 5. Datos de prueba

- **Todo dato de prueba que se versiona es ficticio.** No se commitean nombres, documentos, correos ni teléfonos de personas reales. El cuerpo de ejemplo de la API de nómina anterior trae datos de una persona real y **no se versiona**.
- Los NIT, razones sociales y direcciones de las pruebas automáticas son inventados (por ejemplo el cliente `DEMO`).
- El software DIAN de habilitación (identificador, PIN, TestSetId), los rangos y el certificado viven solo en `config-local`.
- Los consecutivos de habilitación se gastan: el siguiente libre depende del último uso; consúltelo con el modo `rangos` y el registro de la bitácora (`DIAN\docs`).

## 6. Patrones para escribir pruebas

- **API:** `WebApplicationFactory<Program>` con `AddInMemoryCollection` para `Tenants:Carpeta`/`Datos:Carpeta`, `Documentos:EnvioEnSegundoPlano=false` y `IFabricaEnviadorDian` reemplazado por `FabricaFalsa` (ver `DocumentoApiTests`). Las carpetas se crean en `Path.GetTempPath()` y se borran en `Dispose`.
- **PDF y correo:** se sustituyen `IGeneradorPdf` e `IEnviadorCorreo` por falsos (`DocumentoPdfApiTests`): así se prueba la regla "una falla del generador no pierde la aprobación".
- **Admin:** páginas por `WebApplicationFactory<Program>` de `Admin.Web`. Cuidado: Razor codifica en HTML el texto con tildes (por ejemplo "Contraseña"); afirme sobre palabras sin tilde ("cambiada", "restablecida").
- **Documentos aprobados de prueba:** los generadores de la propia solución arman FV/NC/ND (`Shared.Pruebas/DatosPos.cs`, `TraductorFacturaTests`).

## No verificado

- El resultado de `dotnet test` en esta revisión (las cifras salen de lo anotado el 2026-11-09 y del conteo de atributos).
- Que todas las `HabilitacionReal*` y `PilotoRemoto*` usen exactamente las variables listadas: se confirmaron las de `RealFact`, `AislamientoFact`, `PilotoFact` y `MySqlFact`; el resto se infiere por los nombres encontrados en los archivos.
- El contenido de `FirmaXades.Pruebas` más allá de su descripción en `linux/LEEME-LINUX.md`.
