# Documentación para el equipo de desarrollo

Última revisión: 2026-11-16

`DIAN.Unificado` es la solución .NET 10 de SISTEMAS 360 que unifica la facturación electrónica DIAN: una **API multiempresa** (`Pos.Api`: factura, notas, documento soporte, documento equivalente POS, nómina y eventos RADIAN), una **app web** (`Admin.Web`: administración y portal de clientes) y bibliotecas compartidas de firma, XML, impresión y acceso a la DIAN.

## Por dónde empezar

1. [arquitectura.md](arquitectura.md): capas, proyectos y el recorrido de un documento de punta a punta.
2. [entorno-y-compilacion.md](entorno-y-compilacion.md): requisitos, cómo compilar, configuración y arranque local.
3. [pruebas.md](pruebas.md): qué pruebas hay, cuáles son manuales y cómo probar contra la habilitación de la DIAN.
4. [convenciones.md](convenciones.md): git, secretos, estilo y reglas de trabajo.

## Índice

| Documento | Para qué |
|---|---|
| [arquitectura.md](arquitectura.md) | Visión técnica: proyectos, flujo del documento, multiempresa, almacenes y secretos, PDF. |
| [entorno-y-compilacion.md](entorno-y-compilacion.md) | Requisitos (.NET 10, DevExpress, fuentes), compilar y publicar, variables de configuración, `config-local`, comandos de consola. |
| [pruebas.md](pruebas.md) | Proyectos de prueba, pruebas manuales (variables), herramienta de habilitación, datos ficticios. |
| [plantillas-pdf.md](plantillas-pdf.md) | Plantillas `.repx`, generador, fuentes, cómo regenerar y publicar el generador con la API. |
| [extender.md](extender.md) | Recetas: endpoint, tipo de documento, campo del cliente, página del Admin, plantilla, correo. |
| [convenciones.md](convenciones.md) | Ramas, mensajes de commit, qué nunca se versiona, convenciones de código y SQL. |
| [seguridad.md](seguridad.md) | Modelo de amenazas, controles de la API y el portal, lista de verificación, riesgos conocidos. |
| [problemas-conocidos.md](problemas-conocidos.md) | Pendientes y deuda técnica, con lo que no está verificado. |

## Las otras tres colecciones

| Colección | Para quién |
|---|---|
| [../despliegue/README.md](../despliegue/README.md) | Quien instala y opera los servidores (Linux, nginx, IIS, certificados, respaldos). |
| [../mantenimiento/README.md](../mantenimiento/README.md) | Quien administra la plataforma: crear clientes, logo, certificado, habilitación, paso a producción. |
| [../integracion/README.md](../integracion/README.md) | Quien conecta un ERP o POS: contrato de la API, ejemplos, errores. |

## Mapa rápido del repositorio

```
Unificado/
  src/Shared/      bibliotecas compartidas (Tenants, Traductor, Documentos, Firma, ServiciosWeb, Impresion…)
  src/Pos/         Pos.Api (HTTP) y Pos.Servicios (lógica)
  src/Nomina/      Nomina.Servicios y MetaDatosNE
  src/Admin/       Admin.Web (Razor Pages)
  tests/           pruebas xUnit y herramientas de habilitación
  tools/Reportes/  generador de plantillas PDF (necesita DevExpress)
  reportes/        plantillas .repx, muestras y fuentes
  libs/WCF/        DLL propia de WCF
  linux/           paquete de pruebas y despliegue (systemd, nginx, scripts)
  config-local/    secretos y datos de prueba (NO versionado)
```

## Documentación histórica del repositorio

El plan de unificación, las decisiones y las bitácoras por fase viven en la carpeta hermana `DIAN\docs` (dentro del repositorio raíz DIAN): `plan-unificacion-api.md`, `fase0-…` a `fase4-…`. Úselas para entender **por qué** se decidió algo; esta documentación describe **cómo está hoy**.

El `.gitignore` permite `docs/README.md` y las cuatro carpetas de guías; los anexos externos DIAN siguen excluidos. Conserve estas excepciones.

## Convenciones de estos documentos

- Cada archivo lleva su fecha de revisión y termina con una sección **"No verificado"**: lo que se afirma sin haberlo comprobado en esa revisión.
- No incluyen contraseñas, PIN, claves de API ni datos personales reales. Los valores de ejemplo son ficticios.
- Si cambia el código, actualice el documento en el mismo commit.

## Guía complementaria

- [Alcance y trazabilidad documental](revision-documental.md).

## No verificado

- La operación en servidores externos no se revalidó durante esta revisión. Ver [alcance y trazabilidad](revision-documental.md).
