# Secretos locales de DIAN y proyectos antiguos

Los valores originales se conservan en `C:\Users\Eduardo\AppData\Local\Dian\secretos.json`, fuera del repositorio. El archivo contiene un objeto JSON simple con claves y valores de texto; no se regeneraron las contraseñas ni las claves de cifrado.

## Claves del archivo

- `ModeloDian.*`: clave de desarrollo y parámetros de cifrado del modelo de Unificado.
- `Legado.<ruta del archivo>::<nombre>.<línea original>`: constantes extraídas de los servicios anteriores y tokens de integraciones copiados al módulo de nómina unificado. El sufijo identifica su origen; no debe cambiar al editar las líneas del código.
- `ArchivoConfiguracion.<ruta relativa>`: contenido completo de una configuración privada, conservado como un valor de texto para restaurarla sin perder campos o credenciales. Es una copia de recuperación, no una configuración superpuesta automáticamente.

El lector de los proyectos antiguos está en [Compartido/SecretosLocales.cs](../Compartido/SecretosLocales.cs). Se enlaza como fuente en los proyectos afectados, compatible con .NET Framework 4.7.2 y .NET Standard 2.0. El lector del modelo moderno permanece en su proyecto. Ambos utilizan el mismo archivo, sin valores secretos alternativos.

## Windows y Linux

En Windows se utiliza `%LOCALAPPDATA%\Dian\secretos.json` del usuario que ejecuta la aplicación. Para IIS, tareas programadas o servicios con otra cuenta, coloque el archivo en una ubicación privada accesible a esa cuenta y configure `DIAN_SECRETOS_ARCHIVO` con su ruta absoluta. La variable contiene solo la ruta.

En Linux, la ruta predeterminada es `/var/lib/dian/secretos.json`. Copie allí el archivo por un canal privado, asigne al usuario del servicio y limite los permisos a `600`. No se realizó esta copia en un servidor durante el cambio.

Se carga una vez por proceso, al primer uso de la clase lectora. Reinicie la aplicación después de cambiar el archivo. Una clave ausente o un archivo ilegible produce un error sin imprimir sus valores. Mantenga todos los valores originales para conservar compatibilidad de cifrado y autenticación.

## Restaurar las configuraciones en otro equipo

Las configuraciones privadas originales siguen en sus rutas locales y están excluidas de Git. Para reconstruirlas en un clon nuevo, copie primero `secretos.json` a la ruta privada y ejecute desde la raíz de DIAN:

```powershell
./tools/Restaurar-ConfiguracionLocal.ps1 -WhatIf
./tools/Restaurar-ConfiguracionLocal.ps1
```

El script valida que las rutas permanezcan dentro del repositorio, no muestra valores y **no sobrescribe archivos existentes**. La restauración incluye credenciales privadas: mantenga las exclusiones de `.gitignore`.

## Respaldo privado

En la carpeta local `Dian` también se conserva `respaldo-antes-migracion-legado.json` y `fuentes-originales/` de los archivos modificados del legado. Esas copias contienen secretos y no se publican. No sustituyen los respaldos de bases de datos ni certificados.

GitHub respalda código y documentación, **no el JSON privado ni certificados**. Conserve una copia adicional de esos archivos en almacenamiento privado. El script de respaldo Linux existente selecciona determinadas subcarpetas de `/var/lib/dian` y no incluye automáticamente `secretos.json`.

## Archivos compilados

Los ensamblados antiguos pueden contener los valores que estaban incrustados cuando se compilaron. Cuando se detecta una coincidencia se excluye el binario del repositorio, sin borrarlo localmente. Deben reconstruirse desde los fuentes adaptados antes de distribuirse. No basta con retirar los valores de los `.cs` si se publica una DLL anterior.

Las pruebas de compilación no validan que una credencial siga vigente en un servicio externo. No se enviaron documentos DIAN ni se probaron credenciales contra servicios reales durante esta migración.
