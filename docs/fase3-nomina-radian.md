# Fase 3: Nómina electrónica y eventos RADIAN en la API multiempresa

**Fecha:** 2026-11-07 · **Estado:** primera entrega hecha y probada en habilitación con la DIAN (nómina individual, ajuste de nómina que reemplaza y que elimina, y eventos RADIAN). Falta lo que se indica en la sección 6.
**Código:** `Unificado\src\Nomina\MetaDatosNE`, `Unificado\src\Nomina\Nomina.Servicios` y los controladores `NominaController` y `EventosController` de `Pos.Api` (una sola API para POS, documento soporte, nómina y eventos).

## 1. Qué se migró

La API anterior (`API_NOMINA`, .NET 5) tenía 7.400 líneas con usuarios con sesión (JWT), MySQL, PDF, correo e importación de Excel. Se migró lo esencial, con **las mismas rutas y el mismo cuerpo JSON** para que el ERP solo cambie la URL y envíe la clave de API:

| Ruta | Qué hace |
|---|---|
| `POST api/v1/Nomina/EnviarNE` | Genera, firma y envía una nómina individual o un ajuste (reemplazar/eliminar). |
| `POST api/v1/Nomina/CrearXML` | Genera y firma, pero no envía (responde `100`). |
| `GET api/v1/Nomina/ConsultarNE/{TractID}` | Consulta el estado por TrackID. |
| `POST api/v1/Eventos/AcuseFactura` (030), `ReclamoFactura` (031), `ReciboBienesServicios` (032), `AceptacionExpresa` (033), `AceptacionTacita` (034) | Eventos RADIAN sobre una factura recibida. |

- **`MetaDatosNE`**: el modelo del cuerpo (`MetaDatosNEI` y sus enumeraciones), portado sin cambiar los espacios de nombres.
- **`ConstructorNomina`**: el mapeo del cuerpo del ERP hacia los generadores, convertido en función pura (sin base de datos, sin estado global, con la fecha por parámetro).
- **`NominaServicio`**: firma, envío, consulta y ajuste. **`EventosRadianServicio`**: los eventos.

## 2. Cómo se configura un cliente

En la app Admin, cada cliente ahora tiene dos secciones más de software: **Nómina electrónica** y **Eventos RADIAN** (identificador, PIN, TestSetId y ambiente, como los demás). Sin ellas, la API responde `108` con un mensaje claro ("los parámetros del módulo de NÓMINA ELECTRÓNICA no están configurados…").

## 3. Cómo funciona

**Envío de nómina.** En habilitación, si el software tiene `TestSetId`, el documento va al **set de pruebas** y la respuesta es `SP00` con el ZipKey como `TrackID`; luego el ERP consulta con ese TrackID. Sin `TestSetId`, o en producción, el envío es **síncrono** y la respuesta trae el resultado (`00`). Un documento ya enviado no se puede reenviar hasta consultar su estado (`108`), ni si está aprobado; sí si fue rechazado. Un ajuste aprobado marca el documento original como reemplazado o eliminado. El número del ajuste lleva la letra `R` o `E` después del prefijo, como antes.

**Estado de los documentos.** Ya no hay tablas MySQL: cada documento es un archivo JSON en `datos\{cliente}\nomina\`, junto con sus XML y la respuesta de la DIAN.

**Eventos RADIAN.** La API pide la factura recibida a la DIAN (y la guarda en la carpeta del cliente), comprueba que el cliente sea el **receptor**, arma el evento, lo firma y lo envía. El número del evento es el campo opcional `Numero` del cuerpo, o un consecutivo del cliente por tipo de evento (`EVT1`, `EVT2`…). Si el cliente ya usó números en la DIAN, el ERP debe enviar `Numero` o hay que sembrar el consecutivo.

**Códigos de respuesta.** Se midió el comportamiento de la API anterior: `00` a `99` y `SP00` a `SP99` responden HTTP 200 (el `99` es la validación con errores de la DIAN, con su detalle en el cuerpo); `101`, `108`, etc. responden 400; una consulta de un TrackID desconocido responde 404. La única diferencia es el `100` de `CrearXML` ("XML creado, no enviado"), que antes respondía 400 por una comparación de rango y ahora responde 200.

## 4. Diferencias con la API anterior

- **Autenticación:** clave de API del cliente (`x-api-key`) en lugar de usuario y contraseña con permisos por evento. La persona que registra el evento (nombre, cargo, área) ya no se toma de una sesión.
- **Hora:** la fecha de generación y de firma es la **hora de Colombia**, no la del servidor (en Linux con UTC la anterior habría quedado corrida).
- **Respuesta definitiva en consultas:** solo se guarda como definitiva la respuesta `00` o `99`; las intermedias ("Batch en proceso") se vuelven a consultar. La API anterior guardaba la primera respuesta aunque fuera intermedia.
- **Forma de pago:** la nómina siempre es de contado (`1`); el `2` se mapea a crédito.
- **Forma del error:** `Errors[]` usa `Source` (antes `Ambito`); `Codigo` y `Descripcion` son iguales.
- **Eventos:** los archivos XML se guardan con el código del evento en el nombre (`030_EVT1.xml`), porque el consecutivo es independiente por tipo.

## 5. Pruebas

- **Automáticas** (enviador falso): 12 de nómina (set de pruebas, consulta, reenvío bloqueado, ajuste que reemplaza y que elimina, envío síncrono a producción, aislamiento entre clientes, NIT ajeno, cliente sin software) y 8 de eventos (certificado del cliente, consecutivos, receptor ajeno, factura no encontrada, causa del reclamo, `034`, cliente sin software, sin clave). En total pasan 101 pruebas de la solución.
- **En Linux (VM `quanty-lab`, servicio instalado con HTTPS, 2026-11-07):** la API desplegada envió los tres eventos RADIAN sobre la factura a crédito `SETP990000514` y la DIAN los autorizó (`EVT10`, `EVT11`, `EVT12`, con las esperas de la DIAN entre cada uno); la API pidió la factura a la DIAN por su cuenta. La nómina `NOM-1007`, `NOM-1008` y `NOM-1009` salió firmada desde Linux y la DIAN la recibió en el set de pruebas (`SP00`), pero sin resultado del documento (ver abajo). Los datos del cliente se borraron de la VM al terminar.
- **En Linux con un set de pruebas de nómina nuevo (2026-11-08):** con el software y el set nuevos de MARLIT, la API desplegada envió `NOM-1012` (autorizada tras cuatro consultas "Batch en proceso") y su ajuste `NOMR-1012` (autorizada). Un primer intento (`NOM-1011`) fue rechazado por `NIE020`/`NIE024` porque el PIN estaba mal digitado; con el PIN correcto pasó. Esto confirma que con un set sin aceptar la DIAN sí informa el resultado de cada documento. Los datos del cliente se borraron de la VM.
- **Reales en la habilitación de la DIAN, por la API nueva, con MARLIT (en Windows):**
  - Nómina individual `NOM-1004`: set de pruebas (`SP00`), dos consultas con "Batch en proceso" y autorizada.
  - **Ajuste de nómina:** `NOM-1005` autorizada y reemplazada por `NOMR-1005` (autorizada); `NOM-1006` autorizada y eliminada con `NOME-1006` (autorizada).
  - Eventos sobre la factura a crédito `SETP990000513`: acuse `EVT7`, recibo `EVT8` y aceptación expresa `EVT9`, autorizados. La DIAN registra cada evento de forma asíncrona (`LGC09`, `LGC12` y "Intente más tarde"), de modo que un ERP debe esperar unos segundos entre un evento y el siguiente.
  - La API obtuvo sola el XML de la factura recibida desde la DIAN y rechazó (`101`) el evento sobre una factura de la que MARLIT no era receptor.

## 6. Lo que falta

- **Limitación de la habilitación de MARLIT para nómina.** Cuando el set de pruebas ya está aceptado, la DIAN responde a la consulta por ZipKey solo con el estado del set (código `2`, "se encuentra Aceptado") y deja de informar el resultado de cada documento nuevo (tampoco lo da por CUNE: `66`, "TrackId no existe"). Las nóminas `NOM-1004` a `NOM-1006` y sus ajustes sí se vieron autorizadas porque se enviaron mientras el set aún se estaba cumpliendo. La API devuelve ese estado del set tal cual (HTTP 200, código `2`). Para volver a ver el resultado de cada nómina hay que **completar la habilitación de nómina en el portal** (así el envío síncrono deja de dar la regla 92) o, ya en producción, usar el envío síncrono. Lo mismo le pasará al documento equivalente POS enviado al set de un cliente que ya lo aceptó.

- **Envío síncrono de nómina** (`SendNominaSync`): la DIAN lo rechaza con la regla 92 mientras la habilitación de nómina no esté completada en el portal; sigue pendiente de ti (ver la limitación de arriba).
- **No migrado** (decisión pendiente o fuera de alcance): usuarios y login (`UsuariosController`), importación de nómina desde Excel (`ExcelController`), maestros DIAN (`MaestrosController`, decisión 5.4), PDF y correo (decisión 5.9 y fase aparte), consulta del historial de eventos y la aceptación tácita (`034`, que ya estaba "en desarrollo").
- **Datos de ejemplo**: el cuerpo de ejemplo de la API anterior (`Json_Prueba_API.txt`) trae datos de una persona real; las pruebas usan datos ficticios y ese archivo no se versiona.

## 7. Consecutivos libres en la habilitación de MARLIT

`SETP` 990000515, `NOM` 1013, `EVT` 13 (`NOM` 1010 se envió al software anterior; 1011 rechazada por PIN; 1012 autorizada). (Los ajustes llevan el prefijo con `R` o `E`: `NOMR1005` y `NOME1006` ya están usados.) (`SETP` 990000512 quedó con un adquiriente de ejemplo y 990000513 es la factura a crédito de MARLIT usada para los eventos.)
