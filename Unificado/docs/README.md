# Documentación de DIAN Unificado

Última revisión: 2026-11-16.

Plataforma de SISTEMAS 360 SAS que recibe documentos de cualquier ERP o POS (factura de venta, notas crédito y débito, documento soporte y otros), los firma, los envía a la DIAN, genera el PDF, avisa por webhook y ofrece un panel para administrar a los clientes.

La documentación se divide en cuatro colecciones según quién la lee:

| Si usted es… | Empiece por | Qué encontrará |
|---|---|---|
| **Programador del equipo** | [desarrollo/README.md](desarrollo/README.md) | Arquitectura, cómo compilar y probar, plantillas de PDF, cómo extender, convenciones, seguridad, deuda técnica |
| **Quien instala y opera los servidores** | [despliegue/README.md](despliegue/README.md) | Red, instalación en Linux, nginx, firewall, certificados, proxy IIS, respaldos, paso a un servidor definitivo, solución de problemas |
| **Soporte u operación de la plataforma** | [mantenimiento/README.md](mantenimiento/README.md) | Crear y configurar clientes, logo, certificado, claves de API, webhook, habilitación y set de pruebas, paso a producción |
| **Desarrollador de un ERP o SDK que se conecta** | [integracion/README.md](integracion/README.md) | Autenticación, referencia de la API, documentos, estados y errores, webhook, ambientes, ejemplos y lista de certificación |

## Alcance de la revisión

Esta documentación corresponde exclusivamente a **Unificado**, el nuevo sistema. Los servicios de las carpetas hermanas son legado, incluida facturación electrónica en .NET Framework. Consulte la [trazabilidad de la revisión](desarrollo/revision-documental.md).

Las direcciones, máquinas y estados siguientes proceden de la documentación del piloto; **no se consultaron en vivo** durante esta revisión. Confirme la instalación actual antes de operar.

## Datos que sirven para orientarse

| Qué | Dónde |
|---|---|
| API de facturación (la usan los ERP) | `https://services.sistemas360.com:47125/api/...` y `/salud` |
| Portal de clientes | `https://services.sistemas360.com:47125/Login` |
| Panel del operador | `https://localhost:8443` con túnel SSH a la VM (no sale a internet) |
| Código | `src/` (servicios y bibliotecas), `tests/`, `tools/`, `linux/despliegue/` (instalación) |
| Estado actual | Todo en **habilitación** (pruebas) en la VM `quanty-lab`; ningún cliente autorizado en producción |

## Qué está pendiente, en una vista

Cada colección tiene su propia sección de pendientes y de lo "No verificado". Lo más importante:

- El **correo** al adquiriente no está conectado (falta el contrato del EmailAPI).
- **Notas crédito y débito** no tienen plantilla de PDF.
- El portal público no tiene **verificación en dos pasos** ni correo de recuperación.
- `quanty-lab` es una **VM de pruebas**: falta definir el servidor definitivo y la URL sin puerto (`dian.sistemas360.com`).

Los documentos de las fases del proyecto (bitácora de la migración, diseño original) están en la carpeta `DIAN/docs` del repositorio de trabajo, dentro del repositorio raíz DIAN. Lo que sigue vigente de ellos está resumido aquí.

## Convenciones de estos documentos

- Español, Markdown, enlaces relativos.
- Nunca llevan contraseñas, PIN, claves de API ni datos personales reales; los ejemplos usan valores ficticios.
- Cada archivo indica su fecha de revisión y cierra con lo que **no se pudo verificar**. Si encuentra algo que ya no es cierto, corríjalo en el mismo cambio que lo altera.
