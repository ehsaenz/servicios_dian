# Logo del cliente y de sus sucursales

Última revisión: 2026-11-16 · Rol: **operador y cliente** (ambos pueden)

El logo sale en la **representación gráfica (PDF)** de las facturas. Hay un logo **general del cliente** y, opcionalmente, un logo **por sucursal**.

## Requisitos del archivo

- Formato **PNG** o **JPG** (`.png`, `.jpg`, `.jpeg`).
- Peso entre 1 byte y **1 MB**.
- El sistema comprueba el **contenido real** del archivo: renombrar un archivo de otro tipo a `.png` no sirve («El archivo no es una imagen PNG o JPG válida.»).

Recomendación de buena práctica (no es una exigencia del sistema): use una imagen horizontal, nítida, con fondo blanco o transparente (PNG), para que se vea bien en la esquina del encabezado de la factura.

## Antes de subir un logo: revisar que el PDF lo pueda usar

Un PNG puede traer metadatos que el generador de PDF (DevExpress) no lee. El caso real fue un logo con metadatos **C2PA** (bloque `caBX`, credenciales de contenido que agregan algunas herramientas): el PDF de **todas** las facturas del cliente falló con `503` («Data at the root level is invalid...»). El detalle está en [solucion-de-problemas.md](../despliegue/solucion-de-problemas.md#el-pdf-responde-503-con-data-at-the-root-level-is-invalid-line-1-position-1-la-factura-sí-está-aprobada).

**Al subir un logo, el sistema quita automáticamente los metadatos antes de guardarlo.** Los píxeles no cambian. La auditoría registra qué se quitó (por ejemplo `metadatos quitados: caBX` o `APP1`).

- **PNG:** se quitan los bloques que no son de imagen (`caBX`, `tEXt`, `eXIf` y similares).
- **JPG / JPEG:** se quitan los segmentos `APP1` (EXIF, incluida la orientación de la foto), `APP3` a `APP15` (XMP, IPTC, C2PA en `APP11`, etc.) y `COM`. Se conservan `APP0` (JFIF), `APP2` (perfil de color ICC) y `APP14` (Adobe), porque describen los colores de la imagen. Un JPG con EXIF de orientación puede verse girado tras la limpieza: si pasa, suba una imagen ya orientada.

Si el archivo está dañado o no tiene la estructura esperada, el sistema lo rechaza con «El PNG (o JPG) está dañado o no se puede leer. Expórtelo de nuevo como PNG (o JPG) y vuelva a subirlo.»

**Qué hacer después de subir un logo:**

1. Genere (o pida el PDF de) un documento aprobado y compruebe que `GET /api/documento/pdf/{numero}` responde `200` con `%PDF`. Solo entonces dé el cliente por listo.
2. Si el PDF sigue fallando, revise la traza con `journalctl` como indica [solucion-de-problemas.md](../despliegue/solucion-de-problemas.md).

**Logos que ya estaban guardados** (subidos antes de este cambio) no se modifican solos. Compruebe si tienen metadatos con `grep -a -c caBX ruta/al/logo.png` (en Windows: `Select-String -Path ruta\al\logo.png -Pattern caBX -SimpleMatch -Quiet`). Si el resultado es 1 o más, vuelva a subir el mismo archivo desde el panel: el sistema lo limpia al guardarlo.

### Quitar el bloque C2PA a mano (solo si hace falta fuera del panel)

Este script copia solo los bloques de imagen; los píxeles no cambian. Úselo sobre una **copia**, nunca sobre el original:

```bash
python3 - <<'EOF'
import struct
src='/tmp/logo-original.png'; dst='/tmp/logo-limpio.png'
d=open(src,'rb').read()
assert d[:8]==b'\x89PNG\r\n\x1a\n', 'no es PNG'
keep={b'IHDR',b'PLTE',b'IDAT',b'IEND',b'tRNS',b'gAMA',b'cHRM',b'sRGB',b'iCCP',b'pHYs',b'bKGD',b'sBIT'}
out=[d[:8]]; i=8; quitados=set()
while i<len(d):
    n,=struct.unpack('>I',d[i:i+4]); t=d[i+4:i+8]; bloque=d[i:i+12+n]
    if t in keep: out.append(bloque)
    else: quitados.add(t.decode('latin1'))
    i+=12+n
open(dst,'wb').write(b''.join(out))
print('bloques quitados:', sorted(quitados))
EOF
```

El script deja fuera **todos** los bloques que no estén en la lista (no solo `caBX`); por eso, después de usarlo, compruebe el PDF como en el primer paso de esta sección.

## Logo general del cliente

1. Abra la página del cliente → sección **Logo**.
2. Si ya hay uno, se ve su vista previa.
3. En «Imagen PNG o JPG (hasta 1 MB)» elija el archivo.
4. Pulse **Subir logo** (o **Reemplazar logo** si ya había uno). Verá «Logo guardado.»

Mensajes: «Seleccione una imagen PNG o JPG (hasta 1 MB).», «El logo debe ser una imagen PNG o JPG.», «El logo debe pesar entre 1 byte y 1 MB.»

## Sucursales

Una sucursal es un establecimiento del cliente con su propia **matrícula mercantil**. La matrícula es la que viaja en el XML del documento y es la llave para encontrar el nombre, el régimen y el logo con que se imprime.

### Crear o actualizar una sucursal

Sección **Sucursales**, formulario de abajo:

| Campo | Qué escribir | Reglas |
|---|---|---|
| **Matrícula mercantil** | La matrícula de esa sucursal, igual a la que envía el ERP | 1 a 30 caracteres: letras, números, punto, guion y guion bajo |
| **Nombre del establecimiento** | Nombre que se imprime | Obligatorio, hasta 200 caracteres |
| **Régimen fiscal (texto impreso)** | Texto del régimen tal como debe salir | Opcional, hasta 100 caracteres |

Pulse **Guardar sucursal** («Sucursal guardada.»). Si la matrícula ya existe, se actualizan el nombre y el régimen y **se conserva el logo**.

### Logo de una sucursal

En la tabla de sucursales, columna **Logo**: elija el archivo y pulse **Subir logo** (o **Reemplazar logo**). Verá «Logo de la sucursal guardado.»

- Si una sucursal **no tiene logo**, se usa el del cliente.
- Si no hay sucursal para la matrícula del documento, se usa el logo y la razón social del cliente.

### Eliminar una sucursal

Botón **Eliminar** al final de su fila («Sucursal eliminada.»). También borra el logo de esa sucursal.

## Qué puede hacer el cliente

El usuario del portal del cliente **puede** subir el logo general, crear y eliminar sucursales y subir su logo. **No puede** cambiar el emisor, el software ni el certificado.

## Cómo comprobar el resultado

Genere un documento aprobado y descargue su PDF desde **Documentos recientes** ([habilitacion-y-set-de-pruebas.md](habilitacion-y-set-de-pruebas.md)). El logo debe aparecer en el encabezado. Los PDF anteriores a subir el logo no cambian; se vuelve a generar al pedir el PDF de un documento nuevo.

En **Camino a producción**, el ítem «Logo» es un aviso (▲) si no hay logo: «Sin logo: el PDF sale sin él.» No impide pasar a producción.

## No verificado

- No se ha comprobado qué tamaño en píxeles se ve mejor en el PDF.
- No se probó si un PDF ya generado se actualiza al cambiar el logo; asuma que **no**.
