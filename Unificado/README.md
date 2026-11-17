# DIAN Unificado

Sistema vigente de SISTEMAS 360 que unifica los servicios DIAN en .NET 10: facturación electrónica, notas, documento soporte, POS, nómina y eventos RADIAN. Incluye una API multiempresa y un panel de administración con portal de clientes.

Última revisión documental: 2026-11-16. Esta revisión describe el código; no certifica el estado de servidores ni la habilitación de clientes ante la DIAN.

## Documentación por audiencia

| Categoría | Contenido |
|---|---|
| [Equipo de desarrollo](docs/desarrollo/README.md) | Arquitectura, entorno, compilación, pruebas, seguridad y extensión. |
| [Despliegues](docs/despliegue/README.md) | Publicación, Linux, proxies, TLS, configuración, actualización y recuperación. |
| [Mantenimiento de la plataforma](docs/mantenimiento/README.md) | Alta y configuración de clientes, ambientes, logo, certificado inicial y renovación, pruebas y producción. |
| [Integración con ERP y SDK](docs/integracion/README.md) | Contratos HTTP, autenticación, ejemplos, estados, webhook y clientes en cualquier lenguaje. |

El [índice general](docs/README.md) reúne las categorías. La [revisión documental](docs/desarrollo/revision-documental.md) registra alcance, fuentes y límites detectados.

## Estructura actual

| Ruta | Responsabilidad |
|---|---|
| `src/Pos/Pos.Api` | Host HTTP de documentos, POS, soporte, nómina y RADIAN. |
| `src/Pos/Pos.Servicios` | Servicios de facturación, notas, POS y soporte. |
| `src/Nomina` | Servicios y modelos de nómina y eventos RADIAN. |
| `src/Admin/Admin.Web` | Administración, portal, importación y migración del almacén. |
| `src/Shared` | Modelos, traducción, XML, firma, transporte DIAN, clientes y PDF. |
| `tests` | Pruebas automatizadas y herramientas de habilitación. |
| `tools/Reportes`, `reportes` | Herramientas, plantillas y fuentes de impresión. |
| `linux/despliegue` | Instalador, systemd, nginx, TLS, respaldo y firewall. |
| `docs` | Guías propias versionables y referencias externas DIAN excluidas de git. |

La facturación electrónica ya está implementada en `Pos.Api`, `Pos.Servicios/Documentos` y bibliotecas compartidas. No necesita un host separado `src/FacturacionElectronica`.

## Compilar y probar

Desde esta carpeta, con el SDK de [global.json](global.json):

```bash
dotnet build DIAN.Unificado.sln
dotnet test DIAN.Unificado.sln
```

Las pruebas externas requieren configuración explícita; consulte [pruebas](docs/desarrollo/pruebas.md). El generador PDF con DevExpress está fuera de la solución y tiene [requisitos adicionales](docs/desarrollo/plantillas-pdf.md).

## Alcance y legado

`Unificado` contiene el nuevo sistema dentro del repositorio único cuya raíz es `DIAN`. Las carpetas hermanas `FacturacionElectronica` (incluido .NET Framework), `NominaRadian.Api`, `NominaRadian.Bibliotecas`, `PosDocumentoSoporte` y `Radian.Gateway` contienen los servicios anteriores en distintas versiones de .NET. Son antecedentes de migración; no son componentes que se deban desplegar para ejecutar Unificado.

Las bitácoras de `DIAN/docs`, incluidas en el repositorio raíz, explican decisiones históricas. Las instrucciones operativas se encuentran en las cuatro categorías anteriores y están disponibles en un clon de DIAN.

## Límites relevantes

- `/api/documento` atiende `01`, `91` y `92`; las demás familias tienen contratos propios.
- Enviar a habilitación no equivale a asociar una factura a un `TestSetId`: consulte [operación por módulo](docs/mantenimiento/operacion-por-modulo.md).
- La autorización de producción del panel exige verificaciones de factura, incluso para clientes de otros módulos.
- El correo no está conectado en el arranque de la API; la plantilla PDF documentada cubre factura de venta.
- Producción, recuperación y conectividad requieren validación en el ambiente destino.

No versionar certificados, claves, datos de clientes ni paquetes de publicación. Actualizar la documentación junto con los cambios de comportamiento.
