# Alcance y trazabilidad de la documentación

Última revisión: 2026-11-16.

## Consolidación Git posterior

El repositorio único ahora tiene raíz en `DIAN` e publica Unificado y bitácoras. El legado permanece local y excluido de Git. Los historiales anteriores están respaldados localmente; véase [preparación del repositorio](../../../docs/repositorio-git.md). El alcance funcional de este manual sigue siendo Unificado.

## Sistema documentado

El alcance es `Unificado`, sistema actual que reúne los antiguos servicios separados. Las carpetas hermanas, incluida facturación en .NET Framework, son legado. No se modificaron ni se requieren para seguir los manuales principales.

Se completó la organización existente en desarrollo, despliegues, mantenimiento e integración. Se conservan las guías detalladas y ejemplos iniciados previamente, con correcciones de consistencia y nuevas guías para las operaciones que faltaban.

## Fuentes de comportamiento

| Tema | Fuente en este repositorio |
|---|---|
| Rutas y respuestas HTTP | [Controladores](../../src/Pos/Pos.Api/Controllers/) y [arranque API](../../src/Pos/Pos.Api/Program.cs). |
| Autenticación y selección de empresa | [TenantMiddleware](../../src/Pos/Pos.Api/TenantMiddleware.cs). |
| Configuración por módulo y ambientes | [TenantConfig](../../src/Shared/Tenants/TenantConfig.cs). |
| Recepción, reintento, ambiente almacenado y webhook | [DocumentoServicio](../../src/Pos/Pos.Servicios/Documentos/DocumentoServicio.cs). |
| Reintentos en segundo plano | [ReintentosWorker](../../src/Pos/Pos.Api/ReintentosWorker.cs). |
| Panel y autorización | [Cliente.cshtml.cs](../../src/Admin/Admin.Web/Pages/Cliente.cshtml.cs), [ListaProduccion](../../src/Admin/Admin.Web/ListaProduccion.cs). |
| Importación y migración | [ImportadorCliente](../../src/Admin/Admin.Web/ImportadorCliente.cs), [MigradorMySql](../../src/Admin/Admin.Web/MigradorMySql.cs). |
| Renovación de certificado | [CertificadoCache](../../src/Shared/Tenants/CertificadoCache.cs) y [ClienteAdminServicio](../../src/Shared/Tenants/ClienteAdminServicio.cs). |
| Instalación y recuperación | [Scripts de despliegue](../../linux/despliegue/). |
| Contratos .NET | [ModeloDian.csproj](../../src/Shared/ModeloDian/ModeloDian.csproj), [Directory.Build.props](../../Directory.Build.props). |

## Diferencias relevantes aclaradas

1. Facturación ya forma parte del host unificado; el README anterior todavía la presentaba como fase futura.
2. Habilitación de factura/soporte no usa el TestSetId. POS y nómina sí tienen una rama de envío al SET. Aprobar documentos no confirma automáticamente un SET oficial.
3. La lista de producción es de facturación y considera hasta 1.000 registros recientes; no valida independientemente cada módulo.
4. Retirar producción no altera el ambiente guardado de un documento pendiente; desactivar tampoco cancela tareas ya iniciadas.
5. Importar valida antes del alta, pero no ofrece una transacción global con reversión de todas las escrituras.
6. El proyecto de modelos es `net10.0`; la integración HTTP permite consumir desde otros runtimes sin referenciarlo.
7. El `.gitignore` ya permite las cuatro colecciones propias. Sus excepciones deben conservarse al versionar la documentación.
8. Migrar a MySQL requiere copiar y verificar con aplicaciones detenidas antes de abrir tráfico; administradores, auditoría y documentos siguen en archivos.

## Qué significa «verificado»

- **Contrastado con código:** comportamiento inspeccionado en esta revisión; no implica ejecución en servidor.
- **Prueba histórica:** resultado descrito por las bitácoras originales. No se reejecutó por estar citado aquí.
- **No verificado:** necesita prueba en el ambiente destino, decisión operativa o integración externa.

Las URLs, máquinas y estados de clientes que aparecen en las guías originales son referencias del piloto, no inventario en vivo. Antes de operar, confirme con infraestructura la URL y con el panel la configuración actual. Esta revisión no accedió a servidores ni emitió documentos.

## Mantener la documentación

Ante cambios de rutas, modelos o serialización, actualice integración y ejemplos. Ante cambios de roles, validaciones o pantallas, actualice mantenimiento. Ante variables, servicios, almacenamiento o scripts, actualice despliegue y desarrollo. Mantenga enlaces relativos para que un clon del repositorio resulte autosuficiente.

## Comprobaciones locales realizadas

- Enlaces relativos de Markdown comprobados contra archivos y directorios locales.
- Los tres JSON de ejemplo se pudieron analizar.
- Sintaxis de los dos ejemplos Python y los dos módulos Node comprobada sin envíos.
- Proyectos de cliente C# y receptor ASP.NET Core compilados con .NET 10: cero errores y cero advertencias.

No se ejecutó la batería completa de la aplicación, porque no se modificó código de producción. No se ejecutaron los clientes contra la DIAN.

## No verificado

No se certificó el procedimiento externo del portal DIAN, el estado vivo de infraestructura, ni una primera emisión en producción. Las guías describen operaciones del sistema y señalan explícitamente sus límites; no sustituyen esas verificaciones.
