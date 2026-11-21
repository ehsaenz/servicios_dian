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

Limpieza de logos (commit de este cambio en `dev`):

- `src/Shared/Tenants/LimpiadorLogo.cs` (nuevo): quita de los PNG los bloques que no son de imagen (`caBX` de C2PA, `tEXt`, `eXIf`...) y de los JPG/JPEG los segmentos APP1 (EXIF), APP3–APP15 (incluido C2PA en APP11) y COM. Conserva APP0, APP2 (perfil ICC) y APP14. Los píxeles no cambian.
- `src/Shared/Tenants/ClienteAdminServicio.cs`: `SubirLogoAsync` y `SubirLogoSucursalAsync` limpian el logo antes de guardarlo y dejan en la auditoría lo que quitaron (`metadatos quitados: ...`).
- `tests/Admin.Pruebas/LimpiadorLogoTests.cs` (nuevo): 10 pruebas (5 de PNG, 5 de JPEG). Verificado: `Admin.Pruebas` da 42 correctas y 11 omitidas (las de MySQL, que requieren base real).
- Verificado además con un JPG real (logo de prueba) al que se inyectaron EXIF y C2PA: la limpieza dejó el archivo idéntico al original. Prueba temporal, ya borrada.
- **No verificado:** que DevExpress genere el PDF con un JPG limpio. Eso se confirma solo en el servidor, después de publicar.
- `tools/` sigue en `.gitignore` (no publicar: contiene `Restaurar-ConfiguracionLocal.ps1`).

Cambios del 2026-11-20 (sin commitear, para revisión):

- Candado por documento en `DocumentoServicio` (`Agregar` y `Procesar`, por cliente y número). Evita el doble envío a la DIAN entre el envío inicial, el reintento manual y el mantenimiento. Es por proceso: con varias instancias haría falta un candado distribuido.
- Índice en memoria en `AlmacenDocumentos`: los JSON se leen una vez y cada `Guardar` actualiza el índice y el archivo. `CambiosDesde` y `Todos` ya no leen el disco en cada llamada. Un solo proceso escribe, como antes.
- Lista de producción por bloque (facturación, POS, nómina, RADIAN, documento soporte): cada bloque se habilita por separado y tiene su propio set de pruebas. Un bloque en habilitación no bloquea. Un bloque en producción queda «Pendiente» mientras su set no esté verificado: la lista todavía no lee la aprobación de los sets de POS, nómina, RADIAN ni documento soporte. El ítem de facturación se exige solo si el cliente tiene software de facturación.
- Pruebas: `Pos.Pruebas` 80 correctas (incluida la nueva del índice) y 8 omitidas; `Admin.Pruebas` 42 correctas y 11 omitidas (MySQL). Se corrigieron fixtures de logo que eran un PNG y un JPG falsos y que ahora se rechazan por estructura.
- Correcciones tras la revisión de Antigravity: el almacén de documentos se comparte por carpeta de cliente (`AlmacenDocumentos.Para`), así el índice y el candado son los mismos en todas las peticiones; el webhook del mantenimiento y el PDF se ejecutan dentro del candado del documento; los sets de otros bloques dejan de bloquear de forma permanente (ver la lista por bloque, más abajo).
- Pendiente: implementar la verificación del set de pruebas de POS, nómina, RADIAN y documento soporte en la lista. Requiere leer la aprobación de cada bloque desde sus registros y confirmar las reglas de la DIAN por bloque. Hasta entonces, un bloque en producción no puede pasar la lista.

## Servidor `quanty-lab`

- Alias SSH `quanty-lab` (usuario `sis360`, IP `172.31.50.10`, salto por `servidor-sis360`). Verificado en solo lectura.
- Servicios: `dian-pos-api` y `dian-admin-web` activos; `nginx.service` habilitado.
- Entrada pública por IIS (`services.sistemas360.com:47125`) hacia nginx en el puerto 8090.
- Los cambios en servidores se anuncian y se confirman antes de ejecutarlos.
- Habilitación MARLIT (no es producción): la factura de prueba **SETP990001006 está Aprobada** por la DIAN (CUFE `9d2118627e…`). Su PDF se generó con `200` y `%PDF`.
- Causa del fallo del PDF: el logo de MARLIT traía metadatos C2PA (`caBX`) que DevExpress no puede leer. Se quitó en el servidor con una copia limpia; el original queda en `/var/lib/dian/almacen/clientes/MARLIT/logo.original.bak`. Detalle en [solucion-de-problemas.md](../Unificado/docs/despliegue/solucion-de-problemas.md).
- El servidor sigue con el binario anterior: la limpieza automática de logos aún no está publicada.
- **No verificado:** que `~/factura-ok.json` (contiene la clave técnica del set) se haya borrado en `quanty-lab`.

## Pendientes para decidir al despertar (2026-11-22)

- **Prueba real contra la DIAN:** requiere aprobación de Eduardo (consume consecutivos y cuenta para el set).
- **Resolución de documento soporte (DSNO, prefijo S3):** venció el 2026-09-13. Hay que renovarla o cargar una vigente antes de emitir documento soporte.
- **Facturas de venta:** 27 facturas FV en la base de desarrollo, ninguna con CUFE y todas con `Resolucion` vacía. La resolución de pruebas (SETP, 990000000 a 995000000) está activa y el consecutivo va en 990000027.
- **Clave técnica:** la tabla `resoluciones` de la base de desarrollo la guarda en texto plano. Revisar cómo se protege.
- **Conexión a la base de QuantyERP:** se hizo solo con consultas de lectura, sin exportar datos. Falta autorizar la exportación anonimizada de productos y facturas.
- **Brechas del ERP (QuantyERP.DianWorker):** login legado en lugar de `x-api-key`; respuesta 202 no reconocida; notas 91 y 92 sin referencia ni resolución en el JSON; tipos 02 y 03 no soportados por la API; impuesto `14` (INC) sin mapear. Antigravity revisa en la siguiente ronda si la API las acepta.
- **Nota 95 en `EnviarDE`:** decisión pendiente.

## Pendientes

Revisión nocturna (2026-11-21), sin cambios de código:

- Documento soporte (05): autorizado en habilitación (`SEDS-984000502`, bitácora 4.7). Su nota de ajuste (95) también quedó autorizada en habilitación (`SEDS-984000604`, bitácora 4.10).
- **Brecha:** la API no expone la nota de ajuste de documento soporte (95). `docs/integracion/referencia-api.md` solo lista 05, 20 y 94 en `MetaDatosDE`. Hay que decidir si 95 entra en el alcance.
- Contenedor `AttachedDocument`: **no requerido.** Decisión de Eduardo (2026-11-21): el ERP no necesita ni el contenedor ni el XML. La biblioteca tiene `GeneradorContenedor`, pero no se implementa en el servicio.

Después del 28 de noviembre de 2026:

- Renombrar `Pos.Api` a `Dian.Api`, porque la API atiende a todos los bloques (FE, POS, nómina, RADIAN, documento soporte), no solo al POS. Toca el servicio `dian-pos-api` en systemd, la configuración de nginx, los scripts de despliegue, la documentación y los proyectos de prueba. Va como cambio aparte y con confirmación previa; no se hace antes de cerrar la lista por bloque.

Seguridad:

1. **Rotar la clave de API del módulo de nómina** (la que estaba en `MetaDatosNEI.cs`). Ya salió a GitHub en el commit `9c7efd7`. Aún no se sabe en qué proveedor se genera; el responsable debe identificarla.
2. Rotar los cuatro valores de `ModeloDian`. Siguen en GitLab (`dian_colombia/dian360`, rama `dev`, commit `952255e`) y en el historial local de `Unificado.git`. GitHub ya no los tiene.
3. La copia de GitLab (`dian_colombia/dian360`) **no se limpia** por decisión del responsable. Queda con los valores de `ModeloDian` en su historial.
4. `%LOCALAPPDATA%\Dian\fuentes-originales` (38 archivos con secretos en texto plano del código viejo) **se conserva** por decisión del responsable, fuera del repo.
5. Revisión de HEAD: hecha. Las coincidencias línea por línea son solo plantilla XML de los `web.config` legados, no secretos.
6. `tools/Restaurar-ConfiguracionLocal.ps1` queda en el disco, fuera del repo (`/tools/` en el `.gitignore`). El cambio al `.gitignore` está sin commitear.

Despliegue:

4. Variables `Pdf__*` en `dian.env` de la VM: el instalador no las agrega; falta verificar si ya están.
5. `secretos.json` en `/var/lib/dian/` de la VM: falta confirmar que existe y que el despliegue no lo sobrescribe.
6. Licencia de DevExpress para servidor: **decidido por el responsable**. Se mantiene en el servidor; los PDF funcionaron en el despliegue. No se consultó al proveedor. Riesgo aceptado: si fallan, se revisa la licencia.
7. Rollback del instalador: no hay procedimiento ensayado.
8. Logo de MARLIT en la VM: limpio y PDF generado (2026-11-17). Pendiente: borrar `logo.original.bak` cuando se confirme el PDF, y revisar `/tmp` del servidor.
8b. **Publicar la versión con `LimpiadorLogo`** en `quanty-lab` (plan con comandos antes de ejecutar; requiere confirmación). Después, volver a subir los logos existentes de los clientes para que queden limpios.
8c. Logos JPG de otros clientes de Facturación Electrónica: se pueden subir en cuanto esté publicada la versión; hoy se aceptan `.png`, `.jpg` y `.jpeg`.
9. URL sin puerto (`dian.sistemas360.com`): requiere reenviar el 443 en el router y cambiar el DNS.
10. Respaldo fuera del servidor, con la llave privada guardada aparte.
11. Verificación en dos pasos del portal y correo de recuperación (dependen del EmailAPI).
12. Definir el servidor definitivo; `quanty-lab` es de pruebas y también aloja QuantyERP.

## Próximos pasos acordados

- Probar un `git push` pequeño a GitHub.
- Preparar el despliegue en `quanty-lab` siguiendo la documentación: mostrar el plan con los comandos antes de ejecutar nada en el servidor.
