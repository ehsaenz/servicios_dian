# Integración de cualquier ERP y construcción de SDK

Última revisión: 2026-11-16.

## Contrato independiente del lenguaje

La integración usa HTTPS, JSON y cabecera `x-api-key`. Puede implementarse desde .NET Framework, .NET, Java, PHP, Python, JavaScript u otro entorno con HTTPS. El ERP no necesita cargar bibliotecas de firma, certificados ni WCF del servidor.

No existe en este repositorio un SDK publicado y versionado para todos esos lenguajes. Hay [ejemplos](ejemplos/README.md) y modelos .NET. Un SDK propio debe encapsular la [referencia HTTP](referencia-api.md), conservando las diferencias entre familias.

| Necesidad | Contrato |
|---|---|
| Factura, NC y ND | `DocumentoDian` → `/api/documento/*`; recepción 202 y consulta por número. |
| Soporte y POS | `MetaDatosDE` → `DocumentoSoporte` / `DocumentoElectronico`; `ResponseDian`. |
| Nómina | `MetaDatosNEI` → `/api/v1/Nomina/*`. |
| RADIAN | `EventoRadian` → acción de `/api/v1/Eventos/*`. |

## Adaptar el modelo del ERP

1. Defina un adaptador entre ventas, terceros, impuestos y resoluciones y los [campos del contrato](documentos-soportados.md).
2. Mantenga identificadores y NIT como texto; conserve ceros iniciales, prefijos y DV separado cuando corresponda.
3. Use números JSON para importes, sin separadores de miles ni formato monetario. Calcule bases, descuentos, impuestos y totales con precisión decimal y compruebe su coherencia.
4. Conserve los nombres del contrato. No cambie `Numero`, `TipoDocDian`, `CUFE` o `Estado` por convenciones de su serializador.
5. Compruebe el ambiente con el operador y la respuesta del documento. `GetAmbientePruebas` selecciona soporte, o POS si no existe soporte: no diagnostica todos los módulos.
6. Mantenga la numeración bajo control del ERP. La plataforma no asigna el siguiente consecutivo ni administra su resolución.

## Persistencia y recuperación

Para `/api/documento`, guarde por empresa: número completo, tipo DIAN, identificador interno ERP, JSON enviado, CUFE, ambiente, estado, errores y última secuencia. La idempotencia usa **cliente + número**, sin separar tipos ni ambientes: no reutilice un número para otra factura, nota o ambiente del mismo cliente.

```text
Guardar operación pendiente en ERP
→ POST agregar
→ conservar respuesta y número
→ consultar estado o recibir webhook
→ confirmar estado por GET
→ actualizar ERP y descargar archivos disponibles
```

Un 202 no significa aprobación. Un timeout no prueba que el servidor haya descartado la petición: consulte primero el mismo número. Si existe, concilie su estado; si no existe y la recepción sigue siendo incierta, reintente de forma controlada con el mismo número, nunca con uno nuevo para «probar».

`POST reintentar/{numero}` procesa el XML almacenado. Para corregir datos de un documento rechazado envíe el JSON corregido mediante `agregar` con el mismo número. Los aprobados no se reemplazan con un JSON nuevo.

No generalice esa idempotencia a nómina, RADIAN o rutas heredadas: consulte sus reglas particulares. Un HTTP 200 de esas rutas puede contener un rechazo DIAN.

## Cursor y webhook

- Mantenga un cursor independiente por cliente para `cambios?desde=N&limite=100`.
- Aplique los registros antes de persistir `Cursor`, idealmente en una transacción del ERP. Si falla a mitad de página, vuelva a aplicarla de forma idempotente.
- El cursor informa registros modificados; no es un historial inmutable de todos los estados intermedios.
- Verifique HMAC sobre el cuerpo crudo según [webhook](webhook.md). Asocie cada receptor al cliente y su secreto; el aviso no contiene identificador de empresa.
- Responda 2xx después de guardar el aviso duraderamente; procese fuera de la petición. Los ejemplos en memoria no sustituyen una cola persistente.
- Conserve polling para recuperar avisos perdidos. Un fallo de webhook o PDF no revierte una aprobación DIAN.

## Reutilización de modelos .NET

`src/Shared/ModeloDian/ModeloDian.csproj` genera `DianModelDoc`, incluye `FE.Models.Doc` y `WS_V2_HttpClientLib.Builders`, y hereda `net10.0` de `Directory.Build.props`. Puede referenciarse desde un consumidor compatible con .NET 10.

Un ERP en .NET Framework o .NET 8 no debe referenciar directamente ese proyecto `net10.0`. Puede implementar DTO equivalentes y consumir HTTP. Distribuir contratos con otro destino requiere compatibilidad, pruebas y versionado adicionales; no copie bibliotecas internas del servidor dentro del ERP.

## Criterios para entregar un SDK

| Área | Verificación |
|---|---|
| Configuración | URL y clave externas; sin secretos en logs. |
| Transporte | HTTPS verificado, timeout configurable, cancelación y liberación de respuestas. |
| Errores | Separar HTTP, traducción, estado DIAN, PDF y webhook. Admitir errores en texto. |
| Recuperación | Timeout posterior al POST, caída del ERP, webhook repetido y recuperación del cursor. |
| Empresas | Clave, configuración, número y cursor separados por cliente. |
| Contrato | Factura, NC, ND y cada módulo consumido por el ERP. |
| Entrega | Versión del SDK ligada al contrato probado, ejemplo ejecutable y registro de cambios. |

Complete la [lista de verificación](lista-de-verificacion.md) antes de activar producción.

## No verificado

No se certificaron ERP externos ni se publicaron SDK durante esta revisión. Los ejemplos requieren configuración y datos del cliente; no deben enviarse tal cual a la DIAN.
