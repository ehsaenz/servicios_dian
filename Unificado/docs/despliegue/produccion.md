# Hacia un servidor definitivo

Última revisión: 2026-11-16 · Volver al [índice](README.md)

Hoy todo corre en `quanty-lab`, una VM **de pruebas**, y **ningún cliente tiene producción autorizada** (todo va a la habilitación de la DIAN). Este documento lista lo que falta antes de dar usuarios reales. No es un plan cerrado: cada punto es una decisión o una tarea pendiente.

## 1. Lista de verificación

| # | Pendiente | Estado |
|---|---|---|
| 1 | **Definir el servidor definitivo.** `quanty-lab` aloja también QuantyERP y es de pruebas. | Pendiente |
| 2 | **URL sin puerto** `https://dian.sistemas360.com` (sección 2). | Pendiente (requiere router y DNS) |
| 3 | **Verificación en dos pasos** del portal. Hoy: usuario y contraseña generada, que el cliente cambia en «Mi cuenta». | Pendiente |
| 4 | **Correo de bienvenida y recuperación** de contraseña. Depende del contrato del EmailAPI (solo se conoce `IMiddleEmail.EnviarMail`/`GetTracking`). | Pendiente |
| 5 | **Respaldo configurado** (`dian-respaldo configurar`) y **copia fuera del servidor**, con la llave privada guardada aparte. | Pendiente |
| 6 | **Licencia de DevExpress para servidor.** El PDF usa DevExpress 24.1; la comprobación técnica (sin marca de evaluación) no equivale a que la licencia cubra el despliegue. Lo confirma el responsable del contrato. | Pendiente |
| 7 | Variables `Pdf__*` en `/etc/dian/dian.env` del servidor definitivo (el instalador no las agrega). | Pendiente ([instalacion-linux.md](instalacion-linux.md)) |
| 8 | Firewall con los orígenes reales (`dian-firewall aplicar ...` con todas las opciones). | Por hacer en el servidor nuevo |
| 9 | Certificado definitivo (ver [tls-y-certificados.md](tls-y-certificados.md)). | Por hacer |
| 10 | Política de actualizaciones del sistema operativo y de monitoreo. | Sin definir |
| 11 | Cada cliente en producción: decisión aparte, con autorización expresa, empezando por un piloto. La app ya exige una lista de verificación y una frase de confirmación (ver [`../mantenimiento/`](../mantenimiento/)). | Sin autorizar |

## 2. Pasar a `https://dian.sistemas360.com` sin puerto

Todo esto depende de acceder al router (hoy no hay acceso). La VM no cambia, salvo una línea de nginx.

1. **Router:** reenviar el puerto público **443** hacia el IIS del host. (El 443 hoy está cerrado; el 80 lo usa otro sitio: no moverlo.)
2. **DNS** en latinoamericahosting: cambiar el registro A de `dian` de `5.9.84.181` (IP del hosting) a **`190.242.61.214`**.
3. **IIS:** agregar al sitio `DIAN.Linux` un enlace HTTPS en el puerto 443 con el nombre de host `dian.sistemas360.com`, usando el comodín (que ya cubre ese nombre). Cuidado con los demás sitios del host que puedan usar el 443 (**No verificado**: hoy el 443 está cerrado en el router, no se comprobó qué sitios tienen ese enlace en el IIS).
4. **nginx de la VM:** en el bloque `listen 8090`, cambiar la línea

   ```
   proxy_set_header X-Forwarded-Host services.sistemas360.com:47125;
   ```

   por

   ```
   proxy_set_header X-Forwarded-Host dian.sistemas360.com;
   ```

   y volver a instalar (`instalar.sh`) o, en la VM, `sudo nginx -t && sudo systemctl reload nginx`. Sin este cambio, el portal redirigiría a la dirección vieja.
5. **Prueba:** `https://dian.sistemas360.com/Login` (200), `/salud` (200), `/api/documento` (401 sin clave) y `curl -si https://dian.sistemas360.com/ | grep -i ^location` debe apuntar a `https://dian.sistemas360.com/Login`.
6. Avisar a los ERP y clientes de la URL nueva (la anterior puede mantenerse mientras tanto).

## 3. Servidor definitivo en una máquina distinta

1. Preparar Linux (Ubuntu 24.04 probado) con `nginx`, `openssl`, `curl`, `ufw`.
2. Publicar y empaquetar ([instalacion-linux.md](instalacion-linux.md)) y correr `instalar.sh`.
3. Completar `/etc/dian/dian.env` (incluidas las `Pdf__*`) y, si se usa MySQL, `MySql__Cadena`, `crear-esquema` y `migrar-almacen`.
4. Primer administrador; certificado; firewall; respaldo.
5. Si el servidor tuviera IP pública propia con los puertos 80 y 443 libres, la opción más simple es Let's Encrypt (`dian-tls letsencrypt`) y el bloque `dian.sistemas360.com` de nginx, sin IIS. Ver [tls-y-certificados.md](tls-y-certificados.md) (**sin probar**).
6. Migrar los clientes: copiar `almacen`, `llaves` y `trabajo` desde un respaldo (`dian-respaldo restaurar`) o dar de alta de nuevo desde el panel.

## 4. Antes de abrir el portal a clientes reales

- Usuarios con contraseña generada y larga; mientras no exista la verificación en dos pasos, tratar el portal como de alto riesgo.
- Confirmar con `pruebas/probar-entrada-publica.sh` que un operador no entra por la vía pública (solo corre si no hay administradores; ver [instalacion-linux.md](instalacion-linux.md)).
- Confirmar que ningún cliente quedó en producción sin autorización.

## No verificado

- Los pasos 1 a 3 de la sección 2 (router, DNS, enlace del IIS): no se han podido hacer ni probar.
- La instalación en un servidor distinto de `quanty-lab`.
