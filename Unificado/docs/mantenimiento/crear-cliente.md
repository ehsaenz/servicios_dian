# Crear un cliente

Última revisión: 2026-11-16 · Rol: **operador**

Un «cliente» es una empresa que factura a través de la plataforma. Crearlo es el primer paso; después se completa su configuración.

## Pasos

1. Entre al panel ([acceso-al-panel.md](acceso-al-panel.md)). La pantalla **Clientes** muestra la lista (si no hay ninguno: «Todavía no hay clientes. Cree el primero abajo.»).
2. Baje a la sección **Nuevo cliente** y llene:

| Campo en pantalla | Qué escribir | Reglas |
|---|---|---|
| **Id (letras, números, guion)** | Un nombre corto y sin espacios para identificar al cliente, por ejemplo `EJEMPLO-SAS` | De 1 a 40 caracteres: letras, números, guion y guion bajo. **No se puede cambiar después** y no se puede repetir |
| **NIT (sin dígito de verificación)** | El NIT solo con números, por ejemplo `900123456` | Solo dígitos |
| **Dígito de verificación** | El DV, por ejemplo `7` | Un carácter. Aunque la pantalla lo deja vacío, **la lista de producción lo exige** |
| **Razón social** | El nombre legal de la empresa | Obligatoria |

3. Pulse **Crear y configurar**.

Si todo está bien, el panel abre la página del cliente. Si algo falla, aparece un cuadro rojo con el motivo:

| Mensaje | Qué hacer |
|---|---|
| «El id solo admite letras, números, guion y guion bajo (1 a 40 caracteres).» | Quite espacios, tildes y símbolos |
| «Ya existe un cliente con el id …» | Elija otro id |
| «El NIT solo debe llevar números, sin dígito de verificación.» | Quite puntos, guiones y el DV |
| «La razón social es obligatoria.» | Escríbala |

## Cómo queda un cliente recién creado

- Estado: **Habilitación (los envíos van a la DIAN de pruebas)**. Es el estado con el que nace; nada va a producción sin autorización ([paso-a-produccion.md](paso-a-produccion.md)).
- Está **activo**.
- Todavía **no tiene** certificado, software, claves de API ni usuarios. En la lista de clientes el certificado aparece como **Falta** y las claves como **0 activa(s)**.

## Qué sigue

Complete el cliente en este orden:

1. [Configurar el cliente](configurar-cliente.md) (emisor y software DIAN).
2. [Certificado](certificado.md).
3. [Clave de API](claves-api.md).

Se registra en la [auditoría](auditoria-y-respaldos.md) la acción `crear` con el NIT.

## Para un cliente que ya tiene archivo de configuración

Si el cliente ya existía en la API anterior (archivo `ConfigAPI*.json` con su certificado), no lo cree a mano: use la [importación](subir-configuracion.md).

## No verificado

- No hay en la pantalla una opción para **borrar** un cliente. Para dejar de usarlo, use **Desactivar cliente** ([configurar-cliente.md](configurar-cliente.md)). Si necesita borrarlo de verdad, avise al equipo de desarrollo.
