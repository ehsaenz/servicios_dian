# DIAN — SISTEMAS 360

Repositorio del sistema DIAN Unificado y su documentación. La raíz de Git es esta carpeta `DIAN`.

## Sistema vigente

[Unificado](Unificado/README.md) reúne los servicios en .NET 10. Su documentación está organizada en:

- [Desarrollo](Unificado/docs/desarrollo/README.md).
- [Despliegues](Unificado/docs/despliegue/README.md).
- [Mantenimiento de la plataforma](Unificado/docs/mantenimiento/README.md).
- [Integración ERP y SDK](Unificado/docs/integracion/README.md).

## Servicios anteriores

`FacturacionElectronica` (incluido .NET Framework), `NominaRadian.Api`, `NominaRadian.Bibliotecas`, `PosDocumentoSoporte` y `Radian.Gateway` se conservan únicamente en el equipo local, excluidos de Git. No se necesitan para desplegar Unificado. Las bitácoras generales están en `docs/`.

## Git

Consulte [preparación y recuperación de historiales](docs/repositorio-git.md). Se publica `Unificado` y la documentación; las carpetas del legado permanecen locales, sin repositorios Git anidados ni submódulos.

La configuración excluye `bin`, `obj`, archivos `.obj`, ZIP, paquetes y temporales, además de certificados y configuración local. Las exclusiones no borran archivos del disco. Se conservan dependencias binarias necesarias: no existe una exclusión global de DLL.

Los archivos de configuración excluidos deben suministrarse localmente. Su exclusión no asegura que el código legado esté libre de credenciales incrustadas: revisar antes de publicar.
