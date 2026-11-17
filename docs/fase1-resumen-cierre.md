# Fase 1: resumen de cierre

**Fecha:** 2026-11-07
**Alcance:** bibliotecas compartidas en .NET 10 LTS y su validación contra la habilitación de la DIAN, en Windows y en Linux.
**Documentos relacionados:** `plan-unificacion-api.md` (plan y decisiones), `fase0-inventario-bibliotecas.md` (inventario), `fase1-bitacora-migracion.md` (detalle técnico de cada paso), `fase1-prueba-certificado-real.md` (prueba con el certificado de MARLIT).

---

## 1. Qué se logró

- Una solución nueva, `Unificado\DIAN.Unificado.sln`, en .NET 10 LTS, con **8 bibliotecas migradas**: Util, Metadatos, Tipos, Firma, FirmaXades, Microsoft.Xades, Documentos y ServiciosWeb. Compila con 0 errores.
- Una sola copia de cada biblioteca, en lugar de las dos que había en Nómina y en POS. La biblioteca Pdf no se migró: se reemplazará por un servicio de reportes (decisión 5.9).
- **Tres proyectos de prueba:** pruebas unitarias de Util (10, todas pasan), una herramienta de firma y una herramienta de punta a punta contra la habilitación (`Habilitacion.Pruebas`).
- **Todos los tipos de documento probados contra la habilitación de la DIAN**, con el certificado real de MARLIT y las bibliotecas nuevas.
- **Prueba en Linux** (Ubuntu 24.04) con cuatro de esos documentos: 14 pasos, 0 fallas.

## 2. Qué quedó probado

| Documento | Windows | Linux |
|---|---|---|
| Factura (contado y crédito) | Autorizada | Autorizada |
| Nota crédito | Autorizada | Sin probar |
| Nota débito | Autorizada | Sin probar |
| Documento soporte | Autorizado | Autorizado |
| Nota de ajuste del documento soporte | Autorizada | Sin probar |
| Nómina individual | Autorizada | Autorizada |
| Ajuste de nómina (reemplazar y eliminar) | Autorizado (por el set) | Sin probar |
| Eventos RADIAN (acuse, recibo, aceptación) | Autorizados | Sin probar |
| Documento equivalente POS | Autorizado | Autorizado |

Todos salieron sin notificaciones, salvo las dos de la nota de ajuste del documento soporte que se deben al proveedor ficticio de la prueba. En todos los casos la firma se verificó con un verificador independiente.

**No probado:** el evento de rechazo (reclamo), los demás tipos de documento equivalente y sus notas de ajuste, casos con varios impuestos, descuentos o exportación, y cualquier envío a producción (no se ha hecho ni se hará sin tu autorización explícita).

## 3. Decisiones tomadas

- **.NET 10 LTS** como destino, sin multi-target a `netstandard2.0`. .NET 8 se descartó (soporte hasta el 10 de noviembre de 2026).
- **Host de producción en Linux.**
- **Multiempresa en el código:** instancia compartida para los 22 clientes del servidor propio e instancia dedicada para los 10 con servidor propio.
- **Los proyectos antiguos no se tocan**; los errores de soporte se corrigen allí y se portan a la solución nueva.

## 4. Hallazgos que conviene recordar

**De la biblioteca (ya corregidos en `Unificado`, o por corregir):**

- **WCF:** la DIAN exige la suite `Basic256Sha256Rsa15`, que el paquete oficial no trae. Se usa una DLL propia, hecha desde tu copia modificada de dotnet/wcf. Funciona en Windows y en Linux. Falta dejar reproducible cómo se compila.
- **`XadesValidator` no funciona en .NET Core**, ni en la copia original: usa tipos internos de .NET Framework. Para validar firmas hay que usar `SignedXml.CheckSignature`, y en un proceso aparte del que firma.
- **`GeneradorFactura` no usa `Emisor.RazonSocial`:** el nombre sale de `DatosComerciales.NombreComercial`.
- **`MetaDatosDE.AddEmisorPersonaNatural` y `AddEmisorPersonaJuridica`** comparan por referencia y nunca se comportan como documento soporte. Se evitó usándolas con `AddEmisor`; falta corregirlas.
- **Corregidos en la copia nueva:** el grupo `PhysicalLocation` del adquiriente del documento soporte (el anexo lo eliminó), el tipo de identificación `31` del proveedor en la nota de ajuste, marca y modelo en las líneas de esa nota, el IVA exento en sus líneas sin impuestos, y el código postal del emisor en el documento equivalente.

**De la DIAN (reglas que enseñaron los rechazos y notificaciones):**

- Los nombres de ciudad y departamento van como en la lista de la DIAN (`Cali`, `Valle del Cauca`), no en mayúsculas. En la factura las mayúsculas pasaron; en el documento soporte, el POS y la nómina, no.
- El código postal `N/A` por defecto no es válido en cuanto el documento lo exija.
- Las tarifas llevan dos decimales (`0.00`, no `0`).
- Los rangos de numeración se consultan a la DIAN (modo `rangos`), no se suponen.
- Los eventos RADIAN solo se aceptan sobre facturas a crédito y en orden: acuse, recibo, aceptación.
- Una factura con el mismo emisor y adquiriente es válida en habilitación.
- La vía síncrona de nómina exige que el emisor esté habilitado en el portal; mientras no, la nómina entra solo por el set.

## 5. Pendientes y orden recomendado

### A. Correcciones pequeñas de código (riesgo bajo)

1. ~~Restaurar el parámetro opcional `numExtension` en la biblioteca Firma.~~ **Hecho (2026-11-07).**
2. ~~Corregir la comparación por referencia en `MetaDatosDE.AddEmisorPersona*` y borrar el `Documentos\MetaDatosDE.cs` viejo.~~ **Hecho (2026-11-07).**
3. ~~Dejar reproducible la compilación de la DLL de WCF desde el fork, y anotar su versión.~~ **Documentado** en `Unificado\libs\WCF\LEEME.md` (versión, SHA-256, archivos modificados, pasos); la reconstrucción en sí no se intentó.
4. ~~Escribir pruebas automáticas de los generadores.~~ **Hecho:** `tests\Shared.Pruebas\GeneradoresTests.cs`; 23 pruebas en total, todas pasan.
5. Reducir los 130 avisos (pendiente). Las vulnerabilidades NU1902/NU1903 de `System.Security.Cryptography.Xml` y `.Pkcs` se resolvieron subiéndolos a 10.0.14 (factura SETP990000511 autorizada tras el cambio); quedan 21 avisos (clases criptográficas obsoletas, `WebRequest`) y reemplazar `BouncyCastle.NetCore` 1.8.5 por la versión 2.x. La ruta OCSP de FirmaXades usa tipos solo de Windows; no se usa para firmar a la DIAN.

### B. Decisiones tuyas

6. **Exento frente a excluido** en la nota de ajuste del documento soporte: lo debe confirmar quien lleve la contabilidad.
7. **Completar la habilitación de nómina en el portal** de la DIAN, para poder usar la vía síncrona.
8. **Decisiones abiertas del plan (5.2 a 5.5):** qué pasa con FE, el acceso a datos (recomendado `IDatabaseProvider`), si Licencias y Maestros entran en la API única, y las rutas públicas.
9. **Servicio de reportes con DevExpress (5.9):** biblioteca dentro de la API o servicio aparte, licencia en Linux, y plantillas por cliente con scripts desactivados.

### C. Siguiente fase

10. **API multiempresa para POS y DSNO** sobre estas bibliotecas, con la configuración de clientes en MySQL (resolución del cliente por clave de API), certificados protegidos y sin estado global.
11. **Nómina y RADIAN:** migrar `API_NOMINA` a los tipos de Metadatos.
12. **Facturación electrónica**, al final, según la decisión 5.2.

### D. Pruebas que faltan

13. Notas, nota de ajuste del documento soporte, ajuste de nómina y eventos RADIAN en Linux (la VM `quanty-lab` está lista).
14. Evento de rechazo, otros documentos equivalentes, y casos con varios impuestos, descuentos y exportación.
15. Primer documento real en producción, solo con tu autorización y empezando por un cliente piloto.

## 6. Operación

- **El certificado de MARLIT vence el 2027-08-25.** Conviene anotar la renovación.
- **Los datos de prueba** (software, PIN, sets, rangos, certificado) están en `Unificado\config-local`, fuera de git. `Unificado` es un repositorio git desde el 2026-11-07; `config-local`, los certificados, `dist` y `bin`/`obj` están en el `.gitignore`.
- **Consecutivos libres en la habilitación de MARLIT:** `SETP` 990000515, `SEDS` 984000512 (documento soporte), `EPOS` 11, `NOM` 1010, `ANE` 4, `EVT` 13.

## 7. Cómo repetir las pruebas

- **Windows:** `tests\Habilitacion.Pruebas` (modos `generar`, `enviar`, `enviar-sync`, `consultar`, `rangos`, y los tipos de documento). Las instrucciones y variables están en la cabecera de su `Program.cs` y en la bitácora.
- **Linux:** el paquete `dist\DIAN.Unificado.Pruebas.linux-x64.tar.gz` y `linux\LEEME-LINUX.md`. La VM `quanty-lab` tiene el paquete sin secretos; para usarla hay que copiar `config-local` y borrarla al terminar.
- **Firma y verificación:** `tests\FirmaXades.Pruebas`.
- **Pruebas unitarias:** `dotnet test tests\Shared.Pruebas`.

- **Fase 2 (API multiempresa POS/DSNO):** diseño propuesto, pendiente de tu revisión, en `fase2-diseno-api-pos-dsno.md`.
