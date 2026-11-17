# Secretos locales del modelo heredado

El código ya no contiene la clave de desarrollo ni los parámetros de cifrado heredados de `ModeloDian`. Los valores originales se conservan en un JSON externo de pares clave/valor, sin generar valores nuevos.

## Windows

Ruta predeterminada: `%LOCALAPPDATA%\Dian\secretos.json`. En el equipo de Eduardo corresponde a `C:\Users\Eduardo\AppData\Local\Dian\secretos.json`.

Claves del archivo (los valores aquí son solo marcadores):

```json
{
  "ModeloDian.ClaveDesarrollo": "<valor original>",
  "ModeloDian.PasswordHash": "<valor original>",
  "ModeloDian.SaltKey": "<valor original>",
  "ModeloDian.VIKey": "<valor original>"
}
```

Conserve una copia privada del archivo. No reemplace estos valores por otros arbitrarios: cambiar los parámetros de cifrado impide descifrar valores generados con los originales. El archivo está en texto plano y debe quedar accesible solo a su usuario y administradores autorizados. No se publica en Git.

## Linux

Copie de forma privada el mismo archivo a `/var/lib/dian/secretos.json`, propiedad del usuario del servicio `dian`, con modo `600`. Esa es la ruta predeterminada en Linux y es compatible con las restricciones de los servicios systemd existentes.

Para usar otra ruta, configure en `/etc/dian/dian.env`:

```ini
DIAN_SECRETOS_ARCHIVO=/ruta/absoluta/secretos.json
```

La variable contiene únicamente la ruta; los secretos siguen siendo pares clave/valor en el JSON. Reinicie el proceso tras cambiar el archivo o su ubicación. No se copiaron secretos a servidores Linux durante esta modificación.

## Comportamiento

Se lee al utilizar las funciones heredadas de cifrado o la validación `ReadyDevTime`. Crear un `DocumentoDian` ordinario no requiere cargar el archivo. Se conserva el algoritmo existente y sus valores para mantener compatibilidad.

Si el archivo no existe, no es JSON válido o falta una clave, la operación falla con un mensaje sin valores secretos. No hay valores predeterminados incrustados ni claves generadas automáticamente. Los PIN y certificados de los clientes siguen administrándose por el almacén de la plataforma; este archivo no reemplaza ese almacén.

El respaldo actual de `/var/lib/dian` selecciona `almacen`, `llaves` y `trabajo`; **no incluye automáticamente `secretos.json`**. Respalde este archivo por un canal privado adicional antes de depender de él en producción.
