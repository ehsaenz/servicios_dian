# Fase 0 – Inventario de bibliotecas duplicadas (Nómina vs POS)

**Fecha:** 2026-11-07
**Método:** comparación de archivos `.cs` y `.csproj` de cada par, ignorando finales de línea CRLF/LF y espacios al final. Se excluyeron `bin`, `obj` y `packages`. Ningún archivo fue modificado.

---

## 1. Resumen

| Biblioteca | Nómina (`NominaRadian.Bibliotecas`) | POS (`PosDocumentoSoporte\...\eFacturacionColombia_NETCore`) | Archivos .cs con diferencia real | Líneas que cambian | Veredicto |
|---|---|---|---|---|---|
| FirmaXades | net5.0 | net6.0 | 0 de 31 | 0 | **Idénticas** salvo TargetFramework. Migración directa. |
| Firma | net5.0 | net6.0 | 1 de 2 | 162 | Cambios menores. Revisar `FirmaElectronica.cs`. |
| Tipos | net5.0 | net6.0 | 2 de 2 | 34 | Diferencias moderadas en `NominaElectronica.cs` y `Standard.cs`. |
| Util | net5.0 | net6.0 | 1 de 4 | 19 | Diferencia menor en `DateTimeHelper.cs`. |
| ServiciosWeb | net5.0 | net6.0 | 3 de 5 | 117 | Divergencia alta en `ClienteServicioDian.cs`. Además usa WCF con DLL distinta (ver sección 3). |
| Pdf | net5.0 | net6.0 | 5 de 33 | ~1,960 | Divergencia alta en `FacturaPdf.cs`. Usa `System.Drawing` y `ZXing.Net` solo en POS. |
| Documentos | net5.0 | net6.0 | 12 de 16 | ~5,570 | Divergencia **muy alta**. POS tiene 8 extensiones y generadores que Nómina no tiene. |

**Conclusión:** solo FirmaXades puede unificarse sin revisión. Las demás requieren decidir, archivo por archivo, qué versión es la correcta, porque ambas tienen cambios que no existen en la otra.

---

## 2. Hallazgos por biblioteca

### 2.1 Documentos (la más crítica)

- `Tipos.cs` existe en Nómina (2,969 líneas) y en POS tiene **0 líneas**. Probablemente el tipo se movió a la biblioteca `Tipos`. Hay que confirmar antes de borrar nada.
- POS tiene archivos que Nómina no tiene: `ExtensionBeneficiosComprador`, `ExtensionCajaVenta`, `ExtensionDocumentoEquivalente`, `ExtensionExportacion`, `ExtensionFabricanteSoftware`, `ExtensionJuegoLocalizado`, `ExtensionTicketEspectaculoPublico`, `ExtensionTicketTransporteTerrestre`, `CustomTagGeneralExportacion`, `GeneradorDocumentoEquivalente`, `GeneradorNotaAjusteDocumentoEquivalente`, `GeneradorNotaAjusteDocumentoSoporte`, `GeneradorRips`, `Heper`, `MetaDatosDE`. Son funcionalidad de **DSNO y documento equivalente**; deben conservarse.
- Nómina tiene archivos de su propio dominio (`GeneradorEvento`, `GeneradorNominaIndividual`, `GeneradorAjusteNominaIndividual`) que POS también tiene, con diferencias.
- POS referencia `FacturacionColombia_Metadatos` y `Newtonsoft.Json` 13.0.3; Nómina no.
- `GeneradorFactura.cs` difiere en 491 líneas. Es el generador de factura electrónica que FE también usa conceptualmente. Hay que revisar si una de las dos copias tiene correcciones de DIAN que la otra no.

### 2.2 Pdf

- POS agrega `DocumentoSoportePdf`, `NotaAjustesDocumentoSoportePdf`, `QRHelperProv` y una carpeta `Properties`.
- `FacturaPdf.cs`: Nómina 718 líneas, POS 1,887 líneas. POS tiene 1,449 líneas que Nómina no tiene.
- Dependencias de POS: `System.Drawing.Common` 6.0.0 y `ZXing.Net` 0.16.9. Nómina usa `System.Drawing.Common` 4.7.0 y no tiene ZXing.
- `QRHelper.cs`, `CustomQr.cs` y `DocumentoPdf.cs` usan `System.Drawing` en ambas copias. Ver sección 3.

### 2.3 Firma

- Diferencia de 145 líneas en `FirmaElectronica.cs` (POS tiene más). Revisar si son funciones de DSNO.

### 2.4 ServiciosWeb

- `ClienteServicioDian.cs`: 51 líneas solo en Nómina y 61 solo en POS. Es el cliente que llama a la DIAN; cualquier diferencia aquí afecta el envío de documentos. **Revisar con prioridad.**
- Referencias WCF distintas: Nómina usa `..\Libs\System.Private.ServiceModel.dll` (fecha abril 2022) y POS usa `..\DEPENDENCIAS\System.Private.ServiceModel.dll` (mayo 2024). Son binarios de versiones diferentes.
- `Connected Services` (`Reference.cs` y `ConnectedService.json`) difiere entre ambas copias.
- Nómina tiene una carpeta `Properties` que POS no tiene.

### 2.5 Tipos y Util

- `NominaElectronica.cs` (20 líneas) y `Standard.cs` (14 líneas) difieren en Tipos.
- `DateTimeHelper.cs` (19 líneas) difiere en Util. Confirmar cuál es la correcta antes de unificar, porque las fechas afectan la firma y los CUFE.
- POS tiene `BaseOutputPath` apuntando a `..\BINARIOS` en Util. Es una configuración de build que no debe llegar a la solución nueva.

---

## 3. Riesgos técnicos para .NET 10

1. **`System.Drawing` no funciona en Linux.** El host de producción es Linux (decidido 2026-11-07), y desde .NET 6 `System.Drawing.Common` lanza `PlatformNotSupportedException` fuera de Windows. Hay que reemplazarlo. Su uso está concentrado:
   - `eFacturacionColombia_NETCore.Pdf`: `CustomQr.cs` (genera un `Bitmap` del QR), `QRHelper.cs`, `QRHelperProv.cs` (redimensiona y guarda JPEG), `DocumentoPdf.cs` y `NominaIndividualPdf.cs` (usa `System.Drawing.Color`).
   - `FacturacionElectronicaAPI`: `Servicios\DocumentoEquivalente.cs` y `Helper\Tools.cs` tienen `using System.Drawing`, pero las líneas que usan `Bitmap` están comentadas.
   - Opciones de reemplazo: `QRCoder` o `ZXing.Net` (que ya está en POS) para generar el QR, y `SkiaSharp` o `SixLabors.ImageSharp` para redimensionar. Hay que verificar además si `iTextSharp.LGPLv2.Core` 1.6.3 depende de `System.Drawing.Common` en esta versión. **Nota (2026-11-07):** si los PDF pasan a un servicio de reportes con DevExpress (decisión 5.9 del plan), la biblioteca Pdf no se migra y este problema desaparece.
2. **Fuentes en Linux.** Los PDF de iTextSharp usan fuentes del sistema. En el servidor Linux hay que instalar las fuentes que usan los formatos (por ejemplo Arial o Helvetica) o embeberlas. Revisar en la fase 1.
2. **WCF y binarios antiguos.** `ServiciosWeb` referencia `System.Private.ServiceModel.dll` como DLL suelta, no como paquete NuGet. Para .NET 10 hay que cambiar a paquetes NuGet de `System.ServiceModel.Http` y `System.ServiceModel.Primitives`, y regenerar el cliente con el mismo WSDL. Hay que verificar que el WSDL de la DIAN no cambió.
3. **Dos versiones de la DLL de WCF.** Nómina (2022) y POS (2024) usan binarios distintos. Hay que escoger uno y probar.
4. **Referencias a `Libs\` y `DEPENDENCIAS\`.** Son rutas relativas que no existen en la solución nueva. Hay que reemplazarlas por paquetes NuGet o por una carpeta `Libs` compartida dentro de `DIAN.Unificado`.
5. **`BaseOutputPath` apuntando a `..\BINARIOS`.** No debe pasar a la solución nueva.

---

## 4. Lo que falta en la fase 0

- [x] Acceso a datos del POS: no tiene base de datos; es una API sin estado con configuración en archivos JSON (sección 6).
- [x] `Radian.Gateway` y `MaestrosDianAPI` revisados (sección 6).
- [x] Buscar APIs solo de Windows y dependencias de WCF (sección 3).
- [x] Host de producción: **Linux** (decidido 2026-11-07). `System.Drawing` hay que reemplazarlo (sección 3).
- [ ] Confirmar si `iTextSharp.LGPLv2.Core` 1.6.3 funciona en Linux, y qué fuentes usan los formatos de PDF.
- [x] Diferencias de alto impacto revisadas: `ClienteServicioDian.cs`, `FirmaElectronica.cs`, `DateTimeHelper.cs`, `AmbienteServicio.cs`, `RespuestaDian.cs`, `Standard.cs` y `NominaElectronica.cs` (sección 7).
- [ ] Revisar `GeneradorFactura.cs` y `GeneradorDocumentoSoporte.cs`: son grandes (362 y 567 líneas solo en POS) y requieren comparación detallada con pruebas en la fase 1. `FacturaPdf.cs` ya no se compara porque la biblioteca Pdf no se migra.
- [ ] Verificar el identificador de zona horaria en Linux (sección 7, `DateTimeHelper`).
- [ ] Confirmar con la configuración real de un cliente si FE, Nómina, DSNO y POS usan softwares DIAN distintos.

---

## 5. Orden recomendado para la fase 1

1. **FirmaXades** (idéntica): migrar primero, es la de menor riesgo.
2. **Util**: diferencia pequeña. Confirmar `DateTimeHelper.cs`.
3. **Tipos**: confirmar qué archivos son la fuente de verdad.
4. **Firma**: revisar `FirmaElectronica.cs`.
5. **ServiciosWeb**: después de decidir WCF y revisar `ClienteServicioDian.cs`.
6. **Pdf**: no se migra. Se reemplaza por un servicio de reportes con DevExpress (decisión 5.9 del plan).
7. **Documentos**: la última, porque es la más grande y la que tiene más divergencia.

---

## 6. Acceso a datos y modelo de despliegue de los servicios

| Servicio | Base de datos | Configuración | Autenticación |
|---|---|---|---|
| POS/DSNO (`FacturacionElectronicaAPI`) | **Ninguna.** No usa MySQL, MongoDB ni EF. | Un archivo `CONFIG\ConfigAPI.JSON` cargado una vez al iniciar (`Global.CargarCfgApi()`). Contiene emisor (NIT, razón social), software DIAN (identificador, PIN, `TestSetID`) y certificado (archivo y clave). | Servicio externo (`URL_AUTH` y `APIKEY_AUTH` en `appsettings.json`). Correo por servicio externo (`URL_EMAIL_SERVICE`). |
| `Radian.Gateway` | MySQL, cadena `ConnectionStrings:MatrixSql`, con `MtxMySqlLib` e `IDatabaseProvider` (`MetaDB.Abstractions`). | `appsettings.json`, CORS configurable. | Sin autenticación propia. Solo `UseAuthorization()`. |
| `MaestrosDianAPI` | **Ninguna.** Los catálogos (municipios, departamentos, países) están como JSON embebido en recursos (`Maestros.resx`). | `x-api-key` configurable. | Middleware `AuthMiddleware` con cabecera `x-api-key`. |
| `API_NOMINA` | MySQL con `MySqlLibrary` (clases `Ado*`). | `Startup.cs`. | Por revisar. |

### 6.1 Hallazgos

1. **El POS es de un solo emisor por instancia.** `Global.CfgApi` es un objeto estático con un único emisor. Los archivos `ConfigAPI_*.json` de la carpeta `CONFIG` son configuraciones de depuración (confirmado por el usuario), no una lista de clientes. Hay 32 clientes: 10 con servidor propio y 22 en un servidor del propietario. Cada uno funciona con una instancia por grupo de servicios (hasta 96 instancias). **La API unificada debe poder resolver la configuración por cliente en una misma instancia** (ver decisión 5.8 del plan), porque mantener una instancia por cliente y por servicio no es administrable.
2. **La clave del certificado está en texto plano** en los JSON de `CONFIG`. Aunque sean de depuración, el mismo formato se usa en producción. En la unificación hay que moverla a variables de entorno o a un almacén de secretos. Este documento no reproduce ningún valor.
3. **`Radian.Gateway` ya sigue el patrón actual** del resto del ecosistema: inyección de dependencias, `IDatabaseProvider` y capas `Business`/`DataServices`. Es el modelo natural para la capa de datos de la API unificada (decisión 5.3).
4. **`MaestrosDianAPI` ya se publica para `linux-x64`** y no tiene base de datos. Es el servicio más fácil de absorber o de dejar aparte.
5. **El POS depende de dos servicios externos** (autenticación y correo). Hay que decidir si la API unificada los mantiene o los reemplaza.
6. **Archivo con codificación antigua:** `MaestrosDianAPI\Program.cs` no está en UTF-8 (los acentos del texto de contacto se ven rotos). Al migrar hay que convertirlo.

---

## 7. Diferencias de alto impacto: qué conservar al unificar

En todos los casos, "Nómina" es `NominaRadian.Bibliotecas` y "POS" es `PosDocumentoSoporte\...\eFacturacionColombia_NETCore`.

| Archivo | Qué difiere | Propuesta para la copia unificada |
|---|---|---|
| `ClienteServicioDian.cs` (ServiciosWeb) | POS agrega `LimiteRespuestaMb` (100 por defecto; Nómina lo fija en 25 MB) y los métodos `ConsultarEvento`, `ConsultarEventoFactura` y `ConsultarNotaFactura`. El resto son cambios de orden y de comentarios. | Tomar POS. Es un superconjunto funcional. |
| `FirmaElectronica.cs` (Firma) | POS agrega las firmas de Nota de Ajuste de Documento Soporte, Documento Equivalente y Nota de Ajuste de Documento Equivalente. Nómina tiene el parámetro opcional `numExtension = 2` en `FirmarFactura`, `FirmarNotaCredito` y `FirmarDocumentoSoporte`; POS lo fija en 2. | Tomar POS y restaurar el parámetro `numExtension`. La API de Nómina no lo pasa de forma explícita. |
| `DateTimeHelper.cs` (Util) | Nómina tiene dos sobrecargas (`GetColombianDate()` y `GetColombianDate(DateTime)`); POS tiene una con valor por defecto. | Tomar POS. Los dos llamadores de Nómina (`EventosRadianService`) usan `GetColombianDate()` sin argumentos y siguen funcionando. **Riesgo en Linux:** ambas copias usan el identificador de Windows `"SA Pacific Standard Time"`. En Linux depende de ICU y falla con globalización invariante. Usar `America/Bogota` con respaldo al identificador de Windows. |
| `AmbienteServicio.cs` (ServiciosWeb) | POS fija `PRODUCCION = 1` y `PRUEBAS = 2`; Nómina deja los valores implícitos (`PRODUCCION = 0`, `PRUEBAS = 1`). | **Cuidado.** Si algún dato guardado o algún cliente usa el número y no el nombre, unificar cambiaría el significado. Confirmar que nadie persiste el número antes de elegir. |
| `RespuestaDian.cs` (ServiciosWeb) | POS agrega la constante `XML_CON_ERRORES_PRIMARIOS = "206"`. | Tomar POS. |
| `Standard.cs` (Tipos) | POS agrega `DeserializeDE` y deja comentada la propiedad `SchemaLocation` de `ApplicationResponse`. Nómina conserva `SchemaLocation`. | Conservar `SchemaLocation` de Nómina para `ApplicationResponse`, porque los eventos RADIAN usan ese tipo. Validar con un evento real en habilitación antes de cambiarlo. Agregar `DeserializeDE` de POS. |
| `NominaElectronica.cs` (Tipos) | Nómina reemplaza la coma por punto en ocho propiedades numéricas; POS no lo hace. | Tomar Nómina. Mejor aún, formatear con `CultureInfo.InvariantCulture`, para que no dependa de la cultura del servidor. |

### 7.1 Pendientes de la comparación

`GeneradorFactura.cs` (129 líneas solo en Nómina, 362 solo en POS), y `GeneradorDocumentoSoporte.cs` (380 y 567) divergen demasiado para decidirlo leyendo diferencias. Se resuelven en la fase 1 con pruebas que generen el mismo documento con cada copia y comparen el XML y el PDF resultantes.
