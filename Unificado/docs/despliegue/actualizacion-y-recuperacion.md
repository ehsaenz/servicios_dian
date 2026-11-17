# Actualización, reversión y recuperación

Última revisión: 2026-11-16.

## Preparar la entrega

1. Identifique commit, SDK, pruebas ejecutadas y cambios de configuración/esquema.
2. Publique API y Admin según [instalación Linux](instalacion-linux.md). Si se requiere PDF, incluya generador, dependencias, plantillas y fuentes según [plantillas PDF](../desarrollo/plantillas-pdf.md).
3. Conserve el paquete de la versión anterior y una suma SHA-256 del nuevo. Revise el listado del archivo: debe contener `servicios/Pos.Api`, `servicios/Admin.Web` y `despliegue`; sin secretos ni datos de clientes.
4. Registre ventana, responsable y comprobaciones de aceptación. Coordine pausa de los ERP y estado de documentos pendientes.

## Instalar y aceptar

- Realice respaldo antes de la actualización. La copia de archivos del script de respaldo no es una instantánea transaccional del servicio; para un punto coherente coordine la detención de escrituras.
- Ejecute `instalar.sh` como indica la guía. Detiene aplicaciones, copia binarios, instala servicios y reemplaza configuración nginx. Mantiene `dian.env` y datos; no conserva versiones de binarios para rollback.
- Si personalizó nginx, revise previamente las diferencias con `dian-nginx.conf`: una configuración válida puede reemplazar personalizaciones aunque `nginx -t` pase.
- Verifique `systemctl is-active dian-pos-api dian-admin-web nginx`, `/salud`, `/Login` interno, identidad por `Quien`, documentos existentes y descarga de un PDF disponible.
- Para demostrar emisión, use un cliente de habilitación autorizado para pruebas. No use un documento real como prueba técnica automática del despliegue.
- Registre resultado y versión instalada antes de reanudar el ERP.

## Revertir una versión

Con los ERP pausados, reinstale el paquete anterior mediante el instalador. Revise también compatibilidad de configuración, datos y esquema: restaurar ejecutables no revierte una migración ni deshace documentos enviados. Si hubo cambios incompatibles, mantenga el servicio detenido y aplique el plan de recuperación preparado para esa versión.

Evite restaurar una copia antigua de datos sobre el servicio activo. Un documento que ya existe en la DIAN podría reaparecer localmente como pendiente y volver a intentarse.

## Ensayar una restauración

Hágalo en un destino aislado, sin ERP conectado, sin acceso de salida a la DIAN ni a receptores webhook. Detenga API y Admin antes de colocar datos. La opción `Documentos__ReintentoSegundos=0` deshabilita el trabajador de esa familia, pero no impide envíos por peticiones HTTP: no sustituye el aislamiento.

```bash
sudo dian-respaldo restaurar /ruta/respaldo.tar.gz.enc /ruta/recuperacion-vacia /ruta/llave-privada.pem
```

El comando verifica el `.sha256` si está junto al archivo, descifra y extrae en una carpeta vacía. **No pone el respaldo en servicio ni importa MySQL automáticamente.**

1. Compruebe presencia de `almacen`, `llaves`, `trabajo` y, si se usa MySQL, `mysql-dian.sql`.
2. Recupere por separado los binarios y la configuración del host. El script no respalda `/etc/dian`, nginx, TLS, reglas del firewall ni `/opt/dian`.
3. Copie las carpetas restauradas a las rutas del destino detenido, conservando permisos privados y propietario `dian`. Conserve las llaves originales de cifrado.
4. Si aplica, importe el SQL en una base de recuperación aislada y configure su conexión; mantenga API y Admin en la misma base.
5. Arranque solo dentro del entorno aislado y compruebe clientes, acceso, secretos descifrables, logos y documentos. Compare con el inventario del respaldo.
6. Registre fecha del respaldo, tiempo de recuperación, resultados y diferencias. Cierre el ensayo sin conectar sus pendientes a la DIAN.

Para volver a servicio real, concilie documentos posteriores al respaldo con los ERP y la DIAN antes de habilitar envíos. El objetivo de recuperación y la pérdida máxima de datos tolerable deben acordarse con el responsable de operación; el temporizador diario no los garantiza por sí mismo.

## No verificado

Receta derivada de `instalar.sh`, `dian-respaldo.sh` y `ReintentosWorker`. No se ensayó un rollback ni una restauración en servidor durante esta revisión. El procedimiento debe validarse en infraestructura aislada antes de depender de él en una incidencia.
