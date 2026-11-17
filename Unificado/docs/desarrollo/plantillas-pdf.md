# Plantillas PDF (representación gráfica)

Última revisión: 2026-11-16

La API imprime con **DevExpress Reporting 24.1** (motor Skia, funciona en Windows y Linux). Hoy solo existe plantilla de **factura de venta (tipo `01`)**; nota crédito y nota débito quedan aprobadas pero sin PDF (`PdfMensaje`: "no hay plantilla"). Arquitectura general: [arquitectura.md](arquitectura.md#6-representación-gráfica-pdf).

## 1. Piezas

| Pieza | Ruta | Rol |
|---|---|---|
| Plantilla de origen | `reportes/03FacturaVenta0.origen.repx` | Plantilla `03FacturaVenta0` de QuantyERP, punto de partida. |
| Plantilla generada | `reportes/FacturaVentaElectronica0.repx` | La que usa la API. **No se edita a mano**: se regenera. |
| Muestras | `reportes/FacturaVentaElectronica0.muestra.pdf/.png`, `.muestra-linux.png` | Resultado de referencia para revisar el diseño. |
| Fuentes | `reportes/fuentes` | Liberation Sans (Regular, Bold, Italic, BoldItalic) y su licencia. |
| Generador | `tools/Reportes/GeneradorReportes` | Construye la plantilla desde el origen y genera una muestra. |
| Ejemplo de datos | `tools/Reportes/GeneradorReportes/ejemplo-factura.json` | JSON con la forma del modelo de impresión (datos ficticios). |
| Renderizador | `src/Shared/Impresion.DevExpress` | `GeneradorPdfDevExpress` (por tipo de documento) y `RenderizadorPdf`. |
| Prueba de cadena | `tools/Reportes/PruebaLinux` | Contenedor XML aprobado → modelo → PDF. |

## 2. Cómo funciona la impresión

1. `GeneradorPdfDevExpress` elige la plantilla por `TipoDocDian` (diccionario interno: `"01"` → `FacturaVentaElectronica0.repx`). Sin entrada → `SinPlantillaException`. Archivo ausente en `Pdf:Plantillas` → también `SinPlantillaException`.
2. `RenderizadorPdf.Generar(modelo, plantilla, logo)`:
   - se niega (`DocumentoNoAprobadoException`) si el modelo no está marcado `Aprobado` (ver la guardia legal en la arquitectura);
   - carga la `.repx` desde memoria y **elimina los scripts** de la plantilla: las de QuantyERP los traen y no se ejecutan en la API;
   - llena el reporte con el JSON del modelo (`DocumentoImpresion.ToJson()`) como origen de datos;
   - pone el logo en el control `pictureBox1` si hay;
   - exporta PDF.
3. Las fuentes se registran **una vez por proceso** con `DXFontRepository.Instance.AddFont`.
4. DevExpress busca por nombre `DevExpress.Drawing.v24.1.Skia` y `DevExpress.Pdf.v24.1.SkiaRenderer`; no son dependencia de compilación. `Impresion.DevExpress.csproj` los copia a la salida como `None` y `RenderizadorPdf` registra un resolvedor que los carga desde la carpeta de la aplicación. Sin ellos caería a GDI+ y en Linux pediría `libgdiplus`.

## 3. Campos que espera la plantilla

Los de `03FacturaVenta0` de QuantyERP más los del bloque DIAN (los llena `LectorDocumentoAprobado`):

| Campo | Contenido |
|---|---|
| `CUFE` | CUFE (96 hexadecimales). |
| `QR` | URL del QR; si falta, la plantilla la arma con el CUFE y el `Ambiente`. |
| `Ambiente` | `"1"` producción, `"2"` habilitación. |
| `FechaValidacionDian` | Fecha y hora de validación, ya con formato y zona. |
| `ResolucionTexto` | Texto de la resolución de numeración. |

La plantilla oculta el bloque DIAN si no hay CUFE (así el ERP puede imprimir borrador con la misma plantilla). El modelo completo está en `src/Shared/Impresion/ModeloImpresion.cs`.

## 4. Regenerar la plantilla

Requiere DevExpress 24.1 instalado. Desde `tools/Reportes/GeneradorReportes`:

```bash
dotnet run -- ../../../reportes/03FacturaVenta0.origen.repx ../../../reportes/FacturaVentaElectronica0.repx ejemplo-factura.json ../../../reportes/FacturaVentaElectronica0.muestra.pdf ../../../reportes/FacturaVentaElectronica0.muestra.png
```

Argumentos: `<origen.repx> <destino.repx> [<ejemplo.json> <salida.pdf> [<vista.png>]]`. Los tres últimos son opcionales (la muestra y la vista previa PNG de la primera página a 110 dpi). Variable `DIAN_FUENTES` para cambiar la carpeta de fuentes (por defecto `reportes/fuentes`).

Qué le hace el generador a la plantilla de origen (hay que editar `Program.cs`, no el `.repx`, para cambiarlo de forma duradera):

- cambia **todas las fuentes a Liberation Sans** (la de QuantyERP usa Segoe UI, que no existe en Linux);
- ajusta el título (`xrLabel17`): recuadro fijo de 380 de ancho, ajuste de línea y 15,5 pt, para que no se salga de la hoja;
- agrega al pie el bloque DIAN: QR (`qrDian`), CUFE, fecha de validación, resolución y leyenda; visibles solo si hay CUFE;
- quita scripts.

Después de regenerar: revise el PNG, vuelva a generar el PDF en Linux con `PruebaLinux` y copie la plantilla al paquete (sección 6).

### Fuentes y Windows

Con DirectWrite en Windows, registrar las cuatro variantes de la fuente hace que **todo el texto salga en negrita**. En Windows se registra solo `LiberationSans-Regular.ttf` (la negrita se sintetiza); en Linux (Skia) se registran las cuatro. El generador de plantillas ya lo hace así. Aplica también al borrador que imprima el ERP en Windows.

## 5. Probar la cadena completa

`tools/Reportes/PruebaLinux` toma un contenedor XML aprobado y produce el PDF (con y sin logo). Funciona igual en Windows y Linux. Se vio en la VM de pruebas (Ubuntu 24.04, .NET 10 autocontenido): el primer PDF tarda alrededor de 1,4 s y los siguientes unos 100 ms. Para obtener un contenedor de prueba, `ImpresionTests` lo escribe con `DIAN_IMPRESION_CONTENEDOR=<archivo>`. `DIAN_DIAG=1` imprime diagnóstico del entorno.

## 6. Publicar el generador con la API

El generador **no** forma parte de `Pos.Api`: vive en una carpeta aparte y la API lo carga con `Pdf:Carpeta`. Estructura del paquete (observada en `dist/servicios/Pos.Api/pdf`):

```
Pos.Api/
  pdf/
    Impresion.DevExpress.dll + DLL de DevExpress 24.1 + Skia/HarfBuzz (y nativos)
    plantillas/FacturaVentaElectronica0.repx
    fuentes/LiberationSans-*.ttf
```

Configuración en `/etc/dian/dian.env` (nombres; ver [../despliegue/README.md](../despliegue/README.md)):

```
Pdf__Generador=DIAN.Impresion.DevExpress.GeneradorPdfDevExpress
Pdf__Carpeta=/opt/dian/pos-api/pdf
Pdf__Plantillas=/opt/dian/pos-api/pdf/plantillas
Pdf__Fuentes=/opt/dian/pos-api/pdf/fuentes
```

Notas:

- El instalador **no agrega** las variables `Pdf__*` a `dian.env`: hay que añadirlas a mano. Un error de comillas en esas líneas dejó la API sin arrancar en una prueba; un generador mal configurado tumba el servicio a propósito.
- Para compilar y publicar se necesita DevExpress 24.1 en la máquina de compilación, o llevar sus DLL a una carpeta y apuntar `DevExpressBinPath`.
- El generador necesita `System.Drawing.Common` 4.7.2 mientras la API fija la 10.0.14: por eso se carga en su propio `AssemblyLoadContext` (`CargadorGeneradorPdf`). No intente "unificar" esas versiones.
- DLL necesarias, por la prueba del 2026-11-08: `DevExpress.Drawing.v24.1.Skia`, `DevExpress.Pdf.v24.1.SkiaRenderer`, SkiaSharp 2.88.9 y SkiaSharp.HarfBuzz, los nativos `SkiaSharp.NativeAssets.Linux.NoDependencies` y `HarfBuzzSharp.NativeAssets.Linux`, y `System.Drawing.Common` 4.7.2.

## 7. Agregar una plantilla nueva

Ver [extender.md](extender.md#agregar-una-plantilla-pdf).

## No verificado

- No se regeneró la plantilla ni se ejecutó el generador en esta revisión (requiere DevExpress instalado). Los argumentos salen del código de `Program.cs`.
- La lista completa de DLL de `pdf/` se tomó de la documentación de la fase 4 y de una vista parcial de `dist/servicios/Pos.Api/pdf`; no hay script de empaquetado en el repositorio.
- Licencia de DevExpress para el servidor Linux: pendiente de confirmar con la suscripción (ver [problemas-conocidos.md](problemas-conocidos.md)).
- No hay plantilla de nota crédito, nota débito, documento soporte ni nómina; en el modelo de impresión de las notas la resolución sale vacía (el XML de la nota no la imprime como la factura).
