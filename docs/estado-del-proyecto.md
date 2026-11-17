# Estado del proyecto y pendientes

Documento de traspaso entre sesiones de trabajo. Describe lo que está hecho, dónde está y qué falta. No contiene secretos: los valores viven en `%LOCALAPPDATA%\Dian\secretos.json` y en `/var/lib/dian/secretos.json` del servidor.

Última actualización: 2026-11-17.

## Repositorio

- Carpeta raíz de git: `DIAN` (`C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN`).
- Rama `dev`: `d891bcf` (documentación y pruebas) sobre `49db1ef` ("Inicializar DIAN Unificado y su documentación"). `49db1ef` reemplazó al commit `9c7efd7` mediante force push para quitar la clave de API literal de `MetaDatosNEI.cs`.
- Remoto `origin`: `git@github.com:ehsaenz/servicios_dian.git`. `dev` en GitHub apunta a `d891bcf`. El commit anterior sigue accesible en GitHub por su hash hasta que GitHub lo recolecte; no está en el repo local.
- `core.sshCommand` del repositorio apunta a `C:/Windows/System32/OpenSSH/ssh.exe`. Sin ese ajuste, `git push` no ve el agente SSH de Windows.
- Respaldos de historiales anteriores en `DIAN\.git-backups\` (ignorados por git, no publicar). `Unificado.git` contiene 34 commits de `dev` y 4 de `master`, con secretos en 17 commits.

## Acceso SSH

- Llaves en `%USERPROFILE%\.ssh`: `github-sis360` (GitHub) y `gitlab-sis360` (GitLab). Las dos llevan frase de seguridad y están cargadas en el agente `ssh-agent` de Windows.
- Procedimiento completo en [ssh-llaves-y-frase-de-seguridad.md](ssh-llaves-y-frase-de-seguridad.md).
- Verificado: `ssh -T git@github.com` y `ssh -T git@gitlab-sis360` autentican.

## Código y documentación

- Commit limpio: los cuatro valores de `ModeloDian` (`ClaveDesarrollo`, `PasswordHash`, `SaltKey`, `VIKey`) y la clave de API de `MetaDatosNEI.cs` se leen de `secretos.json` mediante `SecretosLocales`. Verificado: 0 coincidencias de esas claves en HEAD.
- `MetaDatosNE` compila (0 errores, 0 avisos). Enlaza `src/Shared/ModeloDian/Servicios/SecretosLocales.cs` desde el `.csproj`.
- Pruebas `SecretosLocales`: 7 de 7 correctas.
- Documentación de cuatro colecciones en `Unificado/docs/` (desarrollo, despliegue, mantenimiento, integración).
- Despliegue Linux: `Unificado/docs/despliegue/instalacion-linux.md`, `Unificado/linux/despliegue/LEEME.md` y la bitácora `docs/fase4-publicacion.md`.

Sin commitear (en el árbol de trabajo):

- `Unificado/docs/desarrollo/convenciones.md` (corrección de identidad)
- `.gitignore` (raíz)
- `Unificado/src/Shared/ModeloDian/Servicios/AccesoPruebas.cs`
- `Unificado/tests/Shared.Pruebas/ConfiguracionSecretosPruebas.cs` y `SecretosLocalesTests.cs`
- `tools/` (no publicar: contiene `Restaurar-ConfiguracionLocal.ps1`)

## Servidor `quanty-lab`

- Alias SSH `quanty-lab` (usuario `sis360`, IP `172.31.50.10`, salto por `servidor-sis360`). Verificado en solo lectura.
- Servicios: `dian-pos-api` y `dian-admin-web` activos; `nginx.service` habilitado.
- Entrada pública por IIS (`services.sistemas360.com:47125`) hacia nginx en el puerto 8090.
- Los cambios en servidores se anuncian y se confirman antes de ejecutarlos.

## Pendientes

Seguridad:

1. **Rotar la clave de API del módulo de nómina** (la que estaba en `MetaDatosNEI.cs`). Ya salió a GitHub en el commit `9c7efd7`. Aún no se sabe en qué proveedor se genera; el responsable debe identificarla.
2. Rotar los cuatro valores de `ModeloDian`. Siguen en GitLab (`dian_colombia/dian360`, rama `dev`, commit `952255e`) y en el historial local de `Unificado.git`. GitHub ya no los tiene.
3. Decidir si la copia de GitLab (`dian_colombia/dian360`) se limpia; reescribirla afecta a quien ya la clonó.
4. Decidir sobre `%LOCALAPPDATA%\Dian\fuentes-originales` (38 archivos con secretos en texto plano del código viejo).
5. Revisar el resto de HEAD: la comprobación línea por línea de los valores de `secretos.json` no se completó en la última sesión.

Despliegue:

4. Variables `Pdf__*` en `dian.env` de la VM: el instalador no las agrega; falta verificar si ya están.
5. `secretos.json` en `/var/lib/dian/` de la VM: falta confirmar que existe y que el despliegue no lo sobrescribe.
6. Licencia de DevExpress para servidor: decisión pendiente del responsable.
7. Rollback del instalador: no hay procedimiento ensayado.
8. Logo de MARLIT en la VM y regeneración de PDF.
9. URL sin puerto (`dian.sistemas360.com`): requiere reenviar el 443 en el router y cambiar el DNS.
10. Respaldo fuera del servidor, con la llave privada guardada aparte.
11. Verificación en dos pasos del portal y correo de recuperación (dependen del EmailAPI).
12. Definir el servidor definitivo; `quanty-lab` es de pruebas y también aloja QuantyERP.

## Próximos pasos acordados

- Probar un `git push` pequeño a GitHub.
- Preparar el despliegue en `quanty-lab` siguiendo la documentación: mostrar el plan con los comandos antes de ejecutar nada en el servidor.
