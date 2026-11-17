# Repositorio único DIAN

Preparado el 2026-11-16. La raíz es `DIAN`; rama de trabajo `dev`. Remoto: `https://github.com/ehsaenz/servicios_dian.git`. La publicación autorizada incluye únicamente `Unificado`, documentación y configuración Git; el legado permanece local y excluido.

## Qué se hizo

- Se retiraron los seis `.git` anidados de las carpetas de trabajo, conservándolos completos en `.git-backups/consolidacion-2026-11-16/`.
- El respaldo conserva objetos, ramas, remotos, índice y reflogs originales. `mapa.json` relaciona cada origen con su destino y los archivos `*-estado.txt` registran el estado local previo.
- Los archivos del proyecto y sus cambios locales permanecen en sus ubicaciones. El nuevo repositorio comienza un historial independiente; no mezcla los commits anteriores.
- `.gitignore` excluye bin/obj a cualquier profundidad, `.obj`, ZIP, paquetes, cachés, temporales, certificados y configuraciones particulares. Los `.gitignore` internos conservan exclusiones específicas de cada sistema.
- No se excluyeron globalmente DLL, EXE ni RESX: varios son dependencias o recursos fuente necesarios. Dos RESX de formularios del legado pesan aproximadamente 21 MiB cada uno.
- `.gitattributes` fija finales de línea apropiados para scripts Linux/PowerShell y trata imágenes, PDF y dependencias como binarios.
- `core.longpaths=true` se configura localmente para las rutas largas del código WCF. En otros clones Windows debe configurarse también.

## Revisar antes del primer commit

Desde `DIAN`:

```powershell
git status --short
git ls-files --others --exclude-standard
git check-ignore -v -- ruta/al/archivo
```

El código legado contiene coincidencias que requieren revisión de credenciales incrustadas. El inventario local `.git-backups/consolidacion-2026-11-16/revision-credenciales.txt` incluye solo rutas y líneas, no valores. Es un detector heurístico: contiene falsos positivos y no constituye una auditoría completa. Las exclusiones de configuración no eliminan secretos escritos dentro de archivos `.cs` u otros archivos fuente. No publicar sin revisar esos casos.

Los archivos de configuración excluidos siguen disponibles en esta máquina. Para nuevos clones deben suministrarse por un canal privado o crearse plantillas sin secretos. No se modificó código de ejecución para retirar credenciales durante la consolidación.

Cuando se haya revisado el contenido que se desea conservar en el historial:

```powershell
git add .
git diff --cached --stat
git diff --cached --check
git commit -m "Inicializar repositorio unificado DIAN"
```

El primer commit publica únicamente el alcance autorizado. El remoto `origin` es `https://github.com/ehsaenz/servicios_dian.git`. No usar force push para reemplazar historiales anteriores.

## Consultar los historiales anteriores

Ejemplo de consulta local:

```powershell
git --git-dir=.git-backups/consolidacion-2026-11-16/Unificado.git log --oneline -10
```

El respaldo está **excluido de Git** y no viajará con clones ni push. Copiarlo también a almacenamiento privado si se necesita conservarlo fuera de este equipo. Puede contener configuraciones de acceso de los remotos anteriores; no publicarlo.

## Revertir la consolidación

Cerrar procesos Git/IDE que estén usando el repositorio. Conservar primero cualquier commit o trabajo nuevo del repositorio raíz. Mover su `.git` a otra ubicación privada y después devolver cada directorio `*.git` del respaldo a la ruta `Origen` indicada en `mapa.json`, con nombre `.git`. No sobrescribir un `.git` existente. Los archivos de código no deben borrarse ni restaurarse automáticamente: sus modificaciones locales permanecen en disco.

Esta operación devuelve las asociaciones Git originales; los cambios documentales y de preparación permanecen como cambios de archivos. Los respaldos de metadatos no sustituyen una copia completa de los archivos de trabajo sin commit.

## Tamaño del directorio

Consulte el [inventario de archivos grandes](archivos-grandes.md) para distinguir espacio local, archivos excluidos y recursos necesarios que sí se versionan.
