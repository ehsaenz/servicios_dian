# Documentación de despliegues

Última revisión: 2026-11-16

Alcance: código y documentación local de Unificado. Las URLs y estados del piloto son referencias históricas; confirme el ambiente actual con el operador. No se inspeccionaron servidores en esta revisión.

Cómo se instala, se publica y se opera DIAN (la API de facturación `Pos.Api` y la app `Admin.Web`) en el servidor. Todo está basado en los archivos de [`linux/despliegue`](../../linux/despliegue/) del repositorio; lo que no se pudo comprobar está marcado como **No verificado**.

## Índice

| Documento | Para qué sirve |
|---|---|
| [arquitectura-de-red.md](arquitectura-de-red.md) | Qué máquinas hay, cómo llega el tráfico de internet hasta la API y el portal, y por dónde entra el operador. |
| [instalacion-linux.md](instalacion-linux.md) | Publicar, empaquetar, instalar y actualizar en Linux; qué crea el instalador; variables de `dian.env`; primer administrador. |
| [nginx-y-firewall.md](nginx-y-firewall.md) | La configuración de nginx bloque por bloque y el comando `dian-firewall`. |
| [tls-y-certificados.md](tls-y-certificados.md) | El certificado de la VM (`dian-tls`) y el certificado comodín del IIS. |
| [iis-proxy.md](iis-proxy.md) | El sitio `DIAN.Linux` del IIS del host, el router y la regla de Windows Firewall. |
| [respaldos-y-operacion.md](respaldos-y-operacion.md) | Respaldos cifrados, temporizadores, registros y qué revisar a diario. |
| [produccion.md](produccion.md) | Lista de pasos y pendientes para un servidor definitivo y la URL sin puerto. |
| [solucion-de-problemas.md](solucion-de-problemas.md) | Los errores reales que han aparecido y cómo se resolvieron. |

## Resumen en diez líneas

- **Servidor de pruebas hoy:** VM Ubuntu 24.04 `quanty-lab` (`172.31.50.10`), dentro de un host Windows con Hyper-V e IIS (`SERVIDOR-SISTEMAS360`). **No es producción.**
- **Servicios:** `dian-pos-api` (API, `127.0.0.1:5290`) y `dian-admin-web` (portal y panel, `127.0.0.1:5280`), con el usuario sin privilegios `dian`.
- **Proxy en la VM:** nginx (`/etc/nginx/conf.d/dian.conf`) con los puertos 443, 8090, 8443 y 80.
- **Entrada pública:** `https://services.sistemas360.com:47125` → IIS del host (sitio `DIAN.Linux`) → nginx `172.31.50.10:8090`.
- **Entrada del operador:** solo por el 8443 (VPN, red interna o túnel SSH). Nunca por internet.
- **Instalar o actualizar:** siempre con `instalar.sh`; es repetible y no toca datos, configuración ni certificado.
- **Datos y secretos:** `/var/lib/dian` (700, usuario `dian`). Configuración: `/etc/dian/dian.env` (600, root).
- **Respaldos:** `dian-respaldo`, cifrados con llave pública, diarios a las 02:30.
- **Ningún cliente tiene producción autorizada.** Todo corre contra la habilitación de la DIAN.
- **Sin subir:** los commits de la rama `dev` los sube el usuario.

## Otras categorías

- Equipo de desarrollo: [`../desarrollo/`](../desarrollo/)
- Mantenimiento de la plataforma (clientes, logo, certificado, paso a producción): [`../mantenimiento/`](../mantenimiento/)
- Integración con ERP y SDK: [`../integracion/`](../integracion/)

## Documentos relacionados fuera de esta carpeta

- [`linux/despliegue/LEEME.md`](../../linux/despliegue/LEEME.md): guía original del despliegue (esta carpeta la amplía y ordena).
- [`linux/LEEME-LINUX.md`](../../linux/LEEME-LINUX.md): paquete de pruebas de habilitación en Linux (no es el servicio instalado).
- `DIAN/docs/fase2-despliegue-linux.md` y `DIAN/docs/fase4-publicacion.md`: bitácoras de las pruebas (dentro del repositorio raíz DIAN).
- `QuantyERP/docs/infraestructura.md` (repositorio de QuantyERP): inventario de la infraestructura compartida.


## Guías complementarias

- [Configuración del servidor y migración del almacén](configuracion-y-migracion.md).
- [Actualización, reversión y recuperación](actualizacion-y-recuperacion.md).
