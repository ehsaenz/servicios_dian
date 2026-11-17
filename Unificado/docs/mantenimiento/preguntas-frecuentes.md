# Preguntas frecuentes

Última revisión: 2026-11-16

## Acceso

**No puedo abrir el panel.**
Revise: (1) el túnel SSH está abierto en una terminal; (2) usa `https://localhost:8443`, **no** `172.31.50.10`; (3) está en la VPN si no está en la oficina. Ver [acceso-al-panel.md](acceso-al-panel.md).

**El navegador dice «No es seguro».**
Es normal: el servidor usa un certificado propio. Acepte continuar.

**Escribo bien mi clave y dice «Usuario o contraseña incorrectos».**
Tres causas posibles: (1) es un operador entrando por la **dirección pública**: entre por el túnel; (2) la contraseña es incorrecta; (3) el usuario está bloqueado (mensaje «Demasiados intentos…»): espere 15 minutos.

**Olvidé mi contraseña de operador.**
Cree de nuevo su usuario con el mismo comando de [acceso-al-panel.md](acceso-al-panel.md) (reemplaza la contraseña).

**Un cliente olvidó su contraseña.**
Operador → página del cliente → **Usuarios del portal** → **Restablecer contraseña**. Entréguela por un canal seguro ([usuarios-del-portal.md](usuarios-del-portal.md)).

## Clientes y configuración

**Me equivoqué en el id del cliente.**
El id no se puede cambiar. Cree otro y desactive el equivocado (**Desactivar cliente**).

**No me deja guardar el software.**
Revise: el identificador debe ser un GUID válido y, si es la primera vez, debe escribir el PIN.

**¿Cómo cambio el PIN de un software?**
Escríbalo en el campo **PIN** y guarde. Con el campo vacío se conserva el actual.

**¿Dónde configuro la resolución y el prefijo?**
No se configuran en el panel: los envía el ERP del cliente en cada documento.

**El certificado dice «Atención: el NIT … no aparece en el sujeto».**
Puede ser un certificado de otra empresa. Confirme con el cliente antes de enviar.

**Subí un certificado y sale «No se pudo abrir el certificado…».**
La clave es incorrecta o el archivo no es el `.p12` completo. Verifique con quien lo emitió.

**El logo no se sube.**
Debe ser PNG o JPG real, de máximo 1 MB. Un archivo renombrado no sirve.

**El botón «Autorizar producción» está deshabilitado (gris).**
Hay un ✖ pendiente en **Camino a producción**. Ver [paso-a-produccion.md](paso-a-produccion.md).

## Documentos

**El ERP del cliente recibe 401.**
La clave no existe, fue revocada o el cliente está **Desactivado**. Mire **Claves de API** y el **Estado** del cliente.

**Un documento está «Rechazado».**
La DIAN lo rechazó; el motivo sale debajo del estado. Se corrige en el ERP y se **reenvía con el mismo número**.

**Un documento está «Error».**
Es una falla de red o una respuesta inesperada; el servicio lo **reintenta solo** (hasta 10 veces con espera creciente). Si persiste horas, avise al equipo de desarrollo.

**Un documento aprobado no tiene PDF («Sin PDF»).**
El PDF se genera tras la aprobación; si falla, se reintenta cuando el ERP lo pide. Hoy solo existe plantilla para **factura de venta**: las notas crédito y débito aprobadas no tienen PDF.

**El certificado venció y hay documentos pendientes.**
Suba el certificado nuevo ([certificado.md](certificado.md)). Los documentos en **Error** se reintentan solos; los **rechazados** hay que reenviarlos desde el ERP.

**El cliente dice que no le llega el webhook.**
Mire **Documentos recientes**, columna **Avisos**: «webhook pendiente» = se está reintentando. Revise que la URL sea accesible desde internet y responda 2xx. Si el ERP no es accesible, quite el webhook y use consulta (polling) ([webhook.md](webhook.md)).

**¿El correo al adquiriente funciona?**
El servicio está preparado, pero la conexión con el servicio de correo (EmailAPI) **aún no está integrada**: no cuente con ello por ahora.

## Seguridad

**Se filtró una clave de API.**
Revóquela y genere otra de inmediato ([claves-api.md](claves-api.md)).

**¿Puedo recuperar una clave o un secreto que ya no veo?**
No. Se generan nuevos.

**¿Dónde veo quién cambió algo?**
En **Auditoría** (solo operadores) ([auditoria-y-respaldos.md](auditoria-y-respaldos.md)).

## ¿A quién aviso?

Si el problema no está aquí, avise al equipo de desarrollo con: el **id del cliente**, el **número del documento**, la **hora (UTC)** y una captura del mensaje.
