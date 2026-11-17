# Logo del cliente y de sus sucursales

Última revisión: 2026-11-16 · Rol: **operador y cliente** (ambos pueden)

El logo sale en la **representación gráfica (PDF)** de las facturas. Hay un logo **general del cliente** y, opcionalmente, un logo **por sucursal**.

## Requisitos del archivo

- Formato **PNG** o **JPG** (`.png`, `.jpg`, `.jpeg`).
- Peso entre 1 byte y **1 MB**.
- El sistema comprueba el **contenido real** del archivo: renombrar un archivo de otro tipo a `.png` no sirve («El archivo no es una imagen PNG o JPG válida.»).

Recomendación de buena práctica (no es una exigencia del sistema): use una imagen horizontal, nítida, con fondo blanco o transparente (PNG), para que se vea bien en la esquina del encabezado de la factura.

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
