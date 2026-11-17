# Operación por módulo y configuración de ambientes

Última revisión: 2026-11-16 · Rol: operador, con apoyo del integrador.

## Qué configuración se carga y dónde

| Información | Dónde se registra | Comprobación |
|---|---|---|
| Empresa, NIT y DV | Panel → cliente → Emisor | Guardar y volver a abrir el cliente. |
| GUID, PIN, ambiente y TestSetId | Bloque Software del módulo | Revisar tipo y casilla de pruebas. El PIN no se vuelve a mostrar. |
| Configuración heredada POS/DS | Comando `importar-cliente` | Ver [importación](subir-configuracion.md); crea un cliente, no actualiza uno existente. |
| Certificado y contraseña | Certificado de firma | Revisar sujeto y fecha; [alta y renovación](certificado.md). |
| Logo y sucursales | Panel → Logo / Sucursales | Revisar imagen y matrícula; [guía](logo.md). |
| Resolución, rango, prefijo y clave técnica cuando aplique | ERP, en el documento enviado | No existe carga de resoluciones en el panel. |
| URL pública, MySQL, carpetas, PDF y reintentos | Servidor, `/etc/dian/dian.env` | [Configuración del despliegue](../despliegue/configuracion-y-migracion.md). |

No hay una pantalla para subir un archivo general de «ambiente». Se configura el software por módulo. Tampoco se guardan dos versiones simultáneas del mismo módulo para alternar entre pruebas y producción: guardar reemplaza sus valores.

## Elegir el módulo y comprobar el resultado

| Módulo del panel | Entrada del ERP | Uso de TestSetId en el código actual | Resultado que debe conservar el ERP |
|---|---|---|---|
| Facturación electrónica | `/api/documento/agregar` (`01`, `91`, `92`) | No se utiliza: `DocumentoServicio` llama a `EnviarDocumento` | Número, CUFE, `Habilitacion`, estado y errores; consulta por número. |
| Documento soporte | `/api/DocumentoSoporte/EnviarDSNO` o `EnviarDE` con `05` | No se utiliza en `DocumentoSoporteServicio` | `ResponseDian` e identificador; consulta `ConsultarDSNO`. |
| Documento equivalente | `/api/DocumentoElectronico/EnviarDE` (`20`, `94`) | Sí, en habilitación con TestSetId | Respuesta y `track_id`; consulta `ConsultarDE`. |
| Nómina electrónica | `/api/v1/Nomina/EnviarNE` | Sí, en habilitación con TestSetId | `ResponseDian`, `TrackID`; consulta `ConsultarNE/{TractID}`. |
| Eventos RADIAN | `/api/v1/Eventos/{acción}` | No corresponde al flujo de set de documentos | Resultado por evento; HTTP 200 no implica aceptación. |

Los cuerpos y códigos están en la [referencia API](../integracion/referencia-api.md). Las rutas de otras familias no heredan el cursor, webhook, PDF ni reintentos de `/api/documento`.

## Validación de facturas frente al SET

Para probar una factura, configure software de facturación en pruebas, certificado y clave de API, y envíe desde el ERP. Confirme `Aprobado` y `Habilitacion=true`.

Esto demuestra el envío a habilitación. **No demuestra envío a un SET concreto**: aunque se guarde TestSetId en el panel, esta ruta no llama a `EnviarSetPruebas`. Si el proceso del cliente necesita asociar facturas a un SET, escale al equipo de desarrollo antes de declarar terminado ese paso. No existe un botón alternativo en el panel que lo resuelva.

En POS y nómina, configure el TestSetId del módulo, envíe por su ruta y consulte el identificador devuelto. Registre el resultado confirmado por la DIAN. No cambie las cantidades exigidas por el SET basándose en los contadores del panel.

## Alcance de «Camino a producción»

La lista implementada revisa facturación electrónica: exige software de factura, al menos una factura aprobada en habilitación y software de factura marcado como producción. Las notas crédito y débito faltantes producen un aviso.

El cálculo examina los **1.000 documentos más recientes** de `/api/documento`; la tabla visible muestra 50. No consulta el estado oficial del SET ni evalúa por separado POS, soporte o nómina. Un cliente que usa exclusivamente esos módulos puede quedar bloqueado por requisitos de factura. Escale ese caso a desarrollo; no cree facturas ficticias para eludir el control ni modifique la autorización directamente en archivos o SQL.

La autorización es del cliente completo. Antes de concederla, revise **todos** sus bloques de software: cada módulo con `AmbientePruebas=false` podrá usar producción cuando `PermiteProduccion=true`.

## Cambio de ambiente con documentos pendientes

1. Coordine la pausa de nuevos envíos en el ERP y registre los documentos pendientes.
2. Resuelva o concilie los pendientes antes de reemplazar la configuración. En `/api/documento`, el ambiente queda guardado en cada registro.
3. Complete el [paso a producción](paso-a-produccion.md), con autorización del cliente y configuración real del ERP.
4. Reanude los envíos y verifique el ambiente del primer documento.

Retirar producción cambia la elección para documentos nuevos, pero **no transforma el XML ni el ambiente de los ya almacenados**. Un reintento usa `registro.Habilitacion`. Desactivar al cliente bloquea nuevas peticiones y lo excluye del trabajador periódico; no cancela tareas ya iniciadas. Para una contención inmediata, coordine con infraestructura la detención de la API y la revisión de pendientes antes de arrancarla.

## No verificado

Contrastado con controladores, `TenantConfig`, `DocumentoServicio`, servicios POS/nómina, `ListaProduccion` y `Cliente.cshtml.cs`. No se ejecutaron envíos, cambios de ambiente ni autorizaciones reales durante esta revisión.
