# Configuración del servidor y migración del almacén

Última revisión: 2026-11-16 · Ejecutar en una ventana de mantenimiento.

## Separar servidor y cliente

`/etc/dian/dian.env` configura la instalación. El panel configura cada cliente y sus módulos. Cambiar una variable del servidor no sustituye registrar software, certificado o autorización de producción del cliente.

Ejemplo sin secretos para una instalación con almacén de archivos:

```ini
Almacen__Carpeta=/var/lib/dian/almacen
Secretos__Llaves=/var/lib/dian/llaves
Datos__Carpeta=/var/lib/dian/trabajo
Cache__Carpeta=/var/lib/dian/cache
Portal__OperadorSoloInterno=true
Documentos__EnvioEnSegundoPlano=true
Documentos__ReintentoSegundos=60
Dian__EsperaSetSegundos=5
```

El instalador crea las primeras cuatro rutas y conserva el archivo si ya existe. Las demás variables son opcionales; aquí se explicitan sus valores predeterminados. Use una asignación por línea, sin `export` ni comentarios al final de la asignación: el wrapper `dian-admin` lee el archivo literalmente.

| Variable adicional | Uso |
|---|---|
| `MySql__Cadena` | Selecciona almacén MySQL. Introducir credenciales en el servidor, no en este documento. |
| `Pdf__Generador` | Tipo de implementación `IGeneradorPdf`. |
| `Pdf__Carpeta` | Carpeta del generador publicado y sus dependencias. |
| `Pdf__Plantillas`, `Pdf__Fuentes` | Plantillas y fuentes del generador. |

Consulte [publicación del PDF](../desarrollo/plantillas-pdf.md) para empaquetar su implementación. El generador no se incorpora por publicar solamente `Pos.Api`.

## Aplicar un cambio

1. Registre qué variables cambiarán y conserve la versión anterior bajo los mismos permisos privados del original.
2. Coordine la pausa de los ERP; revise documentos pendientes y registre el punto de recuperación.
3. Detenga ambas aplicaciones antes de cambiar el almacén o las llaves. Para un cambio ordinario, edite con `sudoedit /etc/dian/dian.env` y reinicie ambos servicios.
4. Mantenga `dian.env` propiedad de root y modo 600. API y Admin deben compartir almacén/base, llaves y carpeta de documentos.
5. Compruebe servicios, `/salud`, acceso al panel y `Quien` con una clave de prueba. Una salud correcta no verifica base de datos, firma ni conexión DIAN.
6. Revise los registros sin copiar credenciales a incidencias. Reanude el ERP cuando se confirme la identidad y configuración correctas.

No arranque la API restaurada con documentos pendientes y salida DIAN habilitada hasta revisar su ambiente: el trabajador puede reanudarlos automáticamente.

## Migrar de archivos a MySQL

El esquema está en [EsquemaMySql.sql](../../src/Shared/Tenants/EsquemaMySql.sql). El comando crea las tablas `dian_clientes`, `dian_claves`, `dian_software` y `dian_archivos`; la base y sus credenciales deben existir. El destino indicado en el código es MySQL 8.x; la revisión documental no cambió SQL ni comprobó otros motores/versiones.

1. Haga respaldo verificable y conserve las llaves de Data Protection. Registre clientes y conteos de claves, módulos, certificados y logos.
2. Detenga ambos servicios para evitar cambios mientras copia:

   ```bash
   sudo systemctl stop dian-pos-api dian-admin-web
   ```

3. Configure `MySql__Cadena` de forma privada. Conserve `Almacen__Carpeta` apuntando al origen y **las mismas** `Secretos__Llaves`; el migrador copia los secretos cifrados, no los vuelve a cifrar.
4. Con los servicios detenidos, ejecute:

   ```bash
   sudo dian-admin crear-esquema
   sudo dian-admin migrar-almacen
   ```

5. Compare inventario de origen y destino antes de abrir tráfico. El migrador **omite clientes que ya existan** en destino: repetirlo no actualiza ni repara automáticamente un cliente parcial.
6. Arranque ambas aplicaciones, compruebe acceso, clientes, certificado/logo y `Quien`:

   ```bash
   sudo systemctl start dian-pos-api dian-admin-web
   ```

7. Conserve el origen hasta completar la aceptación y un respaldo nuevo que incluya MySQL.

Los documentos de trabajo, administradores y auditoría siguen usando archivos. Migrar a MySQL no elimina la necesidad de respaldar `/var/lib/dian`. La caché materializa certificados y logos y no sustituye el almacén.

Si falla la copia, mantenga detenido el servicio mientras se inspecciona el destino. No borre ni sobrescriba clientes como reparación automática. Volver al almacén de archivos retirando `MySql__Cadena` solo es seguro si no hubo escrituras posteriores que deban conciliarse.

## No verificado

Procedimiento contrastado con `Program.cs` de ambas aplicaciones, `MigradorMySql`, `AlmacenClientes` y el wrapper generado por `instalar.sh`. No se ejecutó una migración ni se editaron servidores en esta revisión.
