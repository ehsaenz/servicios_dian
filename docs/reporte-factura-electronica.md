# Reporte de impresión de la factura electrónica de venta (DevExpress)

**Fecha:** 2026-11-08 · **Estado:** plantilla diseñada y probada con datos de ejemplo. Falta conectarla en QuantyERP (sección 4).
**Archivos** (en `Unificado\`): `reportes\FacturaVentaElectronica0.repx` (la plantilla), `reportes\FacturaVentaElectronica0.muestra.png` y `.pdf` (muestra con datos ficticios), `reportes\03FacturaVenta0.origen.repx` (copia de la plantilla de FV de QuantyERP de la que se parte) y `tools\Reportes\GeneradorReportes` (la herramienta que la genera).

## 1. Qué es

Es la plantilla `03FacturaVenta0` de QuantyERP (mismo diseño, mismos campos del documento y mismo motor `QuantyERP.Reportes.DevExpress`) con un bloque nuevo de **factura electrónica DIAN** en el pie, a la izquierda de los totales:

- **Código QR** con la URL de consulta del documento en el catálogo de la DIAN (`catalogo-vpfe.dian.gov.co` en producción; `catalogo-vpfe-hab.dian.gov.co` en habilitación).
- **CUFE**.
- **Fecha y hora de validación** de la DIAN.
- **Resolución de numeración** (número, fecha, prefijo, rango y vigencia).
- **Leyenda** de representación gráfica validada por la DIAN.

El bloque solo se imprime si el documento trae `CUFE`; sin CUFE (por ejemplo una cotización o una FV aún no validada) la hoja sale igual que la de siempre.

## 2. Campos nuevos del JSON

Además de los campos de `03FacturaVenta0` (`Emisor`, `Receptor`, `Items`, `Numero`, `FechaHoraDoc`, `FormaPago`, `SubTotal`, `ValorIVA`, `ValorTotal`, retenciones y `Logo`):

| Campo | Contenido |
|---|---|
| `CUFE` | CUFE de la factura. Si falta, no se imprime el bloque. |
| `QR` | Opcional: la URL completa del QR. Si falta, el reporte la arma con el `CUFE` y el `Ambiente`. |
| `Ambiente` | `"1"` producción, `"2"` habilitación (solo para armar el QR). |
| `FechaValidacionDian` | Fecha y hora de validación, ya con formato. |
| `ResolucionTexto` | Texto de la resolución de numeración. |

El título del documento sale de `TipoDocObj.Nombre` (por ejemplo "FACTURA ELECTRÓNICA DE VENTA"). Un JSON de ejemplo completo está en `tools\Reportes\GeneradorReportes\ejemplo-factura.json`.

## 3. Cómo regenerar o ajustar la plantilla

La plantilla se genera con código para que el cambio sea repetible; también se puede abrir `FacturaVentaElectronica0.repx` en el diseñador de DevExpress y ajustar a mano.

```bash
cd Unificado/tools/Reportes/GeneradorReportes
dotnet run -c Release -- ../../../reportes/03FacturaVenta0.origen.repx ../../../reportes/FacturaVentaElectronica0.repx ejemplo-factura.json salida.pdf vista.png
```

Requiere DevExpress 24.1 en `C:\Program Files\DevExpress 24.1` (o `DevExpressBinPath`). La herramienta no forma parte de `DIAN.Unificado.sln` por esa dependencia.

## 4. Lo que falta para usarla

- **En QuantyERP:** copiar `FacturaVentaElectronica0.repx` a `QuantyERP.Api\Reportes`, registrarla como formato de la FV electrónica y hacer que `ReportesService.BuildReportDataAsync` agregue los cinco campos nuevos desde el envío DIAN (`dian_envios`: CUFE, respuesta y fecha de validación; la resolución sale de la numeración). No lo hice: el repositorio de QuantyERP tiene cambios tuyos sin guardar, y lo correcto es decidir contigo en qué formato/familia de documento se registra.
- **En la API unificada (decisión 5.9):** si la API va a generar el PDF por sí misma, hay que (a) verificar DevExpress en Linux (existen `DevExpress.Drawing.v24.1.Skia` y `Pdf.v24.1.SkiaRenderer`, sin probar aquí) y (b) confirmar que la licencia cubre ese despliegue. Plantillas por cliente y desactivar los scripts de reporte siguen pendientes.
- **Otras plantillas:** nota crédito y débito, documento soporte, nómina y nota de ajuste siguen el mismo patrón, partiendo de su plantilla de QuantyERP o a partir de esta.

## 5. Por qué QuantyERP todavía no usa la API nueva para las FV

QuantyERP (`QuantyERP.DianWorker`) envía sus facturas de venta a la API anterior de facturación electrónica con `api/acceso/login` y `api/documento/agregar`. Esa ruta no existe en la API unificada, que hoy cubre POS, documento soporte, nómina y eventos RADIAN, y el plan deja FE para la fase 4 (decisión 5.2). Las otras operaciones que el ERP no usa hoy (documento soporte, nómina, RADIAN) sí están listas.

El camino para que el ERP apunte a la API unificada es la fase 4: portar el núcleo de FE a .NET 10 y exponer `api/documento/agregar` con la clave de API y el mismo JSON. Mientras tanto, MARLIT sigue en pruebas con la API de FE existente.

Las claves de API de cada cliente ya se crean desde la App Admin (botón de generar clave en la pantalla del cliente; se muestra una sola vez y se pueden revocar).
