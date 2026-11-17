# Fase 1 – Bitácora de migración de bibliotecas

**Destino:** `Unificado\src\Shared`, .NET 10 LTS. Las copias originales no se modificaron.

---

## 1. FirmaXades y Microsoft.Xades (migradas el 2026-11-07)

| | |
|---|---|
| **Origen** | `PosDocumentoSoporte\eFacturacionColombia_NETCore\FirmaXadesNETCore\FirmaXades` y `...\Microsoft.Xades`. |
| **Por qué esa copia** | Las copias de `NominaRadian.Bibliotecas` y de POS son idénticas en los archivos `.cs` (comparación sin CRLF ni espacios finales). Solo cambian el `TargetFramework`, un `.csproj.user` y una carpeta `Properties`. |
| **Destino** | `Unificado\src\Shared\FirmaXades` y `Unificado\src\Shared\Microsoft.Xades`. |
| **Resultado** | Compila con 0 errores. Quedan 70 avisos (ver 1.3). |
| **Probado** | Compila y **firma** un documento con un certificado de prueba. Las firmas de la copia nueva y de la original tienen la misma estructura y las verifica un verificador independiente (sección 3). **No probado:** el flujo completo con un certificado real de la DIAN, `FirmaElectronica` (biblioteca Firma, aún sin migrar) ni la validación con `XadesValidator`, que falla también en la copia original. |

### 1.1 Cambios respecto al original

1. **Framework:** `net6.0` pasa a `net10.0`, definido una vez en `Unificado\Directory.Build.props` (también `Nullable` y `ImplicitUsings` desactivados, igual que el código original).
2. **`BaseOutputPath` eliminado:** el original escribía la salida en `..\..\BINARIOS`, fuera del proyecto.
3. **`System.Security.Cryptography.Xml`:** de 4.7.0 a 10.0.0.
4. **`CertUtil.SelectCertificate` eliminado.** Abría un diálogo de Windows para elegir un certificado (`X509Certificate2UI`), que no existe en .NET 10 ni tiene sentido en un servidor Linux. Ningún otro código de las bibliotecas lo llamaba, y `FirmaElectronica` carga el certificado por otra vía.
5. **`Signer.SetSigningKey`: código inalcanzable eliminado.** El método ya retornaba con `certificate.GetRSAPrivateKey()` antes de un bloque que usaba `RSACryptoServiceProvider` y `CspParameters` (APIs solo de Windows). Ese bloque nunca se ejecutaba. Se quitó para que no genere avisos de plataforma. La ruta que se usa realmente es multiplataforma.
6. **Carpeta `Properties\PublishProfiles` y archivos `.user` no copiados.**

### 1.2 Decisión: `net10.0` solamente, sin `netstandard2.0`

El plan proponía multi-target `net10.0;netstandard2.0` para que Nómina (net5) y POS (net6) siguieran compilando contra la misma biblioteca. Como los proyectos antiguos no se modifican y conservan su propia copia, no habría ningún consumidor del target `netstandard2.0`. Se elimina para no mantener dos compilaciones.

### 1.3 Avisos pendientes

| Aviso | Cantidad aprox. | Dónde | Qué significa |
|---|---|---|---|
| SYSLIB0021 | 14 | `SHA1Managed`, `SHA256Managed`, `SHA512Managed`, `SHA1CryptoServiceProvider` | Clases criptográficas derivadas obsoletas. Se reemplazan por `SHA1.Create()` y similares. |
| SYSLIB0057 | 8 | `new X509Certificate2(byte[])` en `XadesXLUpgrader` | Cargar certificados por el constructor está obsoleto; usar `X509CertificateLoader`. |
| SYSLIB0014 | 4 | `WebRequest.Create` en `OcspClient` y `TimeStampClient` | Reemplazar por `HttpClient`. |
| SYSLIB0047 | 2 | `XmlSecureResolver` | Usar `XmlResolver.ThrowingResolver`. |
| SYSLIB0028 | 2 | `OcspClient`: `PrivateKey` y cast a `RSACryptoServiceProvider` | **Solo Windows.** Se usa al generar solicitudes OCSP para actualizar firmas XAdES-XL. La firma de la DIAN (`FirmaElectronica`) no pasa por esa ruta, pero si algún día se usa, fallará en Linux. |
| CS0168, CS0618 | 6 | Variables sin usar y `AlgorithmIdentifier.ObjectID` | Limpieza menor. |

### 1.4 Dependencias que conviene revisar

- **`BouncyCastle.NetCore` 1.8.5:** paquete antiguo y sin mantenimiento. Compila en .NET 10, pero el sucesor es `BouncyCastle.Cryptography` 2.x, que cambia algunas APIs. Se deja para después de las pruebas de firma.

---

## 2. Siguiente en la lista

Orden recomendado en `fase0-inventario-bibliotecas.md`: **Util** (una diferencia menor), luego **Tipos**, **Firma**, **ServiciosWeb**, **Pdf** y **Documentos**.

La prueba de firma ya está hecha para FirmaXades (sección 3). Al migrar **Firma**, hay que repetirla pasando por `FirmaElectronica` y con cada tipo de documento.

---

## 3. Prueba de firma: copia original (.NET 6) contra copia nueva (.NET 10)

**Fecha:** 2026-11-07.

### 3.1 Qué se hizo

Un mismo programa de prueba, `Unificado\tests\FirmaXades.Pruebas\Program.cs`, se compiló contra las dos copias:

- **Nueva:** `Unificado\src\Shared\FirmaXades` en .NET 10.
- **Original:** copia de `PosDocumentoSoporte\...\FirmaXadesNETCore` en .NET 6, hecha en la carpeta temporal de la sesión (las carpetas originales no se tocaron).

Cada copia firmó la misma factura mínima (`muestra\factura-minima.xml`) con el mismo certificado autofirmado de prueba y la misma fecha de firma. Los parámetros replican `FirmaElectronica.FirmarDocumento`: RSA con SHA-256, firma envuelta, política de firma de la DIAN, rol `supplier` y destino en la segunda `UBLExtension`.

### 3.2 Resultados

| Comprobación | Resultado |
|---|---|
| Firmar con la copia nueva | Correcto |
| Firmar con la copia original | Correcto |
| Tamaño de los dos XML firmados | Igual (6,797 bytes) |
| Estructura (elementos, atributos y algoritmos, sin Ids ni valores de firma) | **Idéntica**, 73 líneas en ambas |
| Verificación independiente (`System.Security.Cryptography.Xml.SignedXml`), cuatro combinaciones de firma y verificador (.NET 10 y .NET 6) | **Las cuatro correctas**, con 3 referencias firmadas |
| Control negativo: cambiar un valor del documento firmado (1000.00 por 2000.00) | **Incorrecta**, como debe ser |

Los Ids y los valores de firma cambian en cada ejecución, así que no se compara byte a byte.

### 3.3 Hallazgo: `XadesValidator` no funciona en .NET Core, ni antes ni ahora

La validación con `XadesService.Validate` falla con las dos copias, incluso al validar una firma con la misma copia que la creó. La causa es una `NullReferenceException` en `XadesSignedXml.CheckDigestedReferences` (`Microsoft.Xades`, línea 1850): usa reflexión para cargar el ensamblado `System.Security, Version=4.0.0.0` y un tipo interno de .NET Framework (`CanonicalXmlNodeList`), que no existen en .NET Core ni en .NET 10. `XadesValidator` además descarta la excepción real y devuelve un mensaje genérico.

Consecuencias:

- La firma funciona; la validación incluida en la biblioteca no. El código de la DIAN solo firma, así que no afecta hoy.
- No hay que usar `XadesService.Validate` en el servicio. Para validar, usar `SignedXml.CheckSignature` como hace el programa de prueba.
- Es una limitación heredada de la copia original, no un defecto introducido por la migración.

### 3.4 Cómo repetir la prueba

```bash
dotnet build Unificado/tests/FirmaXades.Pruebas
dotnet Unificado/tests/FirmaXades.Pruebas/bin/Debug/net10.0/FirmaXades.Pruebas.dll certificado cert.pfx
dotnet Unificado/tests/FirmaXades.Pruebas/bin/Debug/net10.0/FirmaXades.Pruebas.dll firmar Unificado/tests/FirmaXades.Pruebas/muestra/factura-minima.xml cert.pfx firmado.xml
dotnet Unificado/tests/FirmaXades.Pruebas/bin/Debug/net10.0/FirmaXades.Pruebas.dll verificar firmado.xml
```

El certificado se crea al vuelo y es solo de prueba; no es de ningún cliente.

### 3.5 Lo que falta para dar la firma por equivalente

- Probar con un certificado real de un cliente en el ambiente de habilitación de la DIAN, que es la única validación que cuenta.
- Probar los demás tipos de documento (nota crédito, nota débito, documento soporte, nómina, evento) cuando se migre la biblioteca Firma.
- Probar en Linux. Esta prueba corrió en Windows.

---

## 4. Util, Metadatos, Tipos, Firma, Documentos y ServiciosWeb (2026-11-07)

Se migraron para poder generar, firmar y enviar una factura de punta a punta. Todas compilan en .NET 10 con 0 errores. Las copias originales no se modificaron.

| Biblioteca | Origen | Cambios respecto al original |
|---|---|---|
| **Util** | POS. `CryptographyHelper`, `XmlDocumentExtensions` y `ZipHelper` son idénticas en Nómina y POS. | `DateTimeHelper`: copia de POS más identificador de zona horaria `America/Bogota` con respaldo al de Windows y a UTC-5 fijo. `CryptographyHelper`: `SHA1.HashData` y similares en lugar de las clases obsoletas; mismo resultado, comprobado con vectores estándar. 10 pruebas xUnit en `tests\Shared.Pruebas`, que pasaron antes y después del cambio de hashes. |
| **Metadatos** (`FacturacionColombia_Metadatos`) | POS. | Pasa de netstandard2.0 a net10.0. Se quitó la referencia a `System.Text.Json` porque ya viene en el framework. Depende de `BasicLibraryExt` 1.0.10. |
| **Tipos** | `Standard.cs` de POS; `NominaElectronica.cs` de Nómina. | Se restauró la propiedad `SchemaLocation` de `ApplicationResponse`, que POS tenía comentada (los eventos RADIAN la usan). |
| **Firma** | POS (superconjunto). | Sin cambios de código. **Pendiente:** restaurar el parámetro opcional `numExtension` de Nómina. |
| **Documentos** | POS, tal cual. | Sin cambios de código. **Pendiente:** comparar con la copia de Nómina antes de usarla para Nómina y RADIAN (`GeneradorNominaIndividual`, `GeneradorEvento`, `GeneradorAjusteNominaIndividual` y otros difieren). |
| **ServiciosWeb** | POS. | La DLL suelta de WCF se reemplaza por una copia en `Unificado\libs\WCF` (ver 4.1). |

La biblioteca Pdf no se migró (decisión 5.9 del plan).

### 4.1 Hallazgo: la DIAN exige una versión modificada de WCF

`ClienteServicioDian` configura la seguridad del mensaje con `SecurityAlgorithmSuite.Basic256Sha256Rsa15`. El paquete oficial `System.ServiceModel` 8.1.2 no tiene esa suite: la compilación falla con `CS0117`. La copia original funciona porque usa `System.Private.ServiceModel.dll` (versión 5.0.24.27801, compilada en mayo de 2024) hecha desde una copia modificada de dotnet/wcf (`PosDocumentoSoporte\...\WCF\wcf`), donde sí se agregó esa suite.

- Esa DLL se copió a `Unificado\libs\WCF` y la biblioteca la referencia. Compila en .NET 10. **No se ha probado todavía contra la DIAN.**
- **Riesgos:** depende de un binario propio cuyo origen hay que poder reconstruir desde el código fuente; el transporte de clave RSA 1.5 es un algoritmo que Microsoft no incluye en el paquete oficial por considerarlo débil, pero la DIAN lo exige; y cada actualización de .NET puede obligar a recompilarla.
- **Pendiente:** documentar cómo se compila la DLL desde la copia modificada y dejarla reproducible.

### 4.2 Hallazgo: Microsoft.Xades cambia la configuración criptográfica del proceso

Después de firmar con `XadesService`, `SignedXml.CheckSignature` falla con `NullReferenceException` dentro del mismo proceso, porque Microsoft.Xades registra sus propios algoritmos en `CryptoConfig`. En un proceso aparte la misma firma verifica bien. Consecuencia: no verificar firmas con `SignedXml` en el proceso del servicio que firma. En las pruebas, la verificación se hace en un proceso separado.

### 4.3 Prueba local de punta a punta (sin enviar nada)

La herramienta `tests\Habilitacion.Pruebas` generó la factura `SETP990000501` con los datos de MARLIT, calculó el CUFE con la clave técnica del set, la firmó con el certificado real usando las bibliotecas nuevas (Util, Metadatos, Tipos, Documentos, Firma y FirmaXades) y la firma se verificó en un proceso aparte: correcta, con 3 referencias firmadas. Se usaron valores provisionales para régimen fiscal, responsabilidad tributaria, matrícula mercantil y teléfono, que son los datos del RUT que aún faltan, así que esa factura no sirve para enviar.

El envío se bloquea mientras haya datos provisionales y exige `--confirmar`.

### 4.4 Envío a habilitación (2026-11-07): la DIAN aceptó una factura

Con los datos del RUT de MARLIT cargados (régimen `R-99-PN`; responsabilidad tributaria `ZA`, porque el RUT tiene las responsabilidades 48 IVA y 33 impuesto nacional al consumo; matrícula mercantil, teléfono, correo y dirección del RUT), se enviaron dos facturas al set de pruebas de habilitación, siempre con el certificado real y las bibliotecas nuevas:

| Documento | Resultado de la DIAN |
|---|---|
| `SETP-990000501` | Rechazada (código 99, errores en campos mandatorios): `FAJ43a`, `FAJ43` y `FAJ20` (nombre y razón social no informados), más las notificaciones `FAJ43b` y `FAJ41`. |
| `SETP-990000502` | **Aceptada** (código 00, "Procesado correctamente"): "La Factura electrónica SETP-990000502, ha sido autorizada." Con una notificación `FAJ41`. |

**Causa del primer rechazo:** `GeneradorFactura` no usa `Emisor.RazonSocial`. El nombre del emisor (`PartyName` y `RegistrationName`) sale de `Emisor.DatosComerciales.NombreComercial`, y la herramienta de prueba no lo asignaba, así que el XML llevaba el nombre vacío. Se corrigió en la herramienta, con la razón social tal como está en el RUT (`MARLIT GROUP S.A.S.`). Es un comportamiento de la biblioteca que cualquier consumidor debe conocer, y un candidato a corregir: usar `RazonSocial` como respaldo cuando `NombreComercial` esté vacío.

**Qué demuestra:** la cadena nueva funciona de punta a punta en Windows: generación del XML, cálculo del CUFE, firma XAdES con FirmaXades en .NET 10, WCF con la DLL propia (suite RSA 1.5) y validación por la DIAN en habilitación. El set de pruebas pide al menos una factura aceptada; con esta, ese requisito queda cumplido, si el set sigue vigente.

**Notificación `FAJ41` (no bloquea), causa identificada:** el anexo técnico de la DIAN define `FAJ41` sobre `/Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyTaxScheme/cac:TaxScheme/cbc:Name`: si el nombre del tributo del emisor no corresponde a la lista de valores válidos, es una notificación. Con la responsabilidad tributaria `ZA` el XML llevaba `ID` = `ZA` y `Name` = `IVA e INC`, y la DIAN lo notificó. Al cambiar solo ese dato a `01` (`Name` = `IVA`), la factura `SETP990000504` se autorizó **sin ninguna notificación**. La comparación es limpia: `SETP-990000502` (con `ZA`) trae `FAJ41` y `SETP-990000504` (con `01`) no, y lo demás es igual.

Qué queda abierto: no sé qué nombre espera la DIAN para el código `ZA`; el que trae la biblioteca (`IVA e INC`) es el que ella notifica. Hay que consultar la lista de valores del anexo técnico para ese código. Además, el RUT de MARLIT tiene IVA (48) e INC (33); usar `01` en lugar de `ZA` es una decisión fiscal que debe confirmar quien lleve la contabilidad.

**Estado del set de pruebas:** después de la primera factura aceptada, la DIAN responde a los envíos siguientes al set con "Set de prueba con identificador (...) se encuentra Aceptado" y no devuelve el detalle de cada documento. Por eso la herramienta tiene el modo `enviar-sync` (envío síncrono de un solo documento a habilitación), que sí devuelve el resultado completo. `SETP-990000503` se envió al set después de aceptado y no tiene resultado individual.

**Detalle operativo:** la DIAN tardó más de 30 segundos en validar el lote. La herramienta sondea ahora hasta 12 veces cada 10 segundos, y el modo `consultar --zipkey` hace lo mismo.

**Qué falta probar:** en Linux (zona horaria, carga del certificado y, sobre todo, la DLL propia de WCF), los demás tipos de documento (documento soporte, nómina, eventos RADIAN y POS), y un envío a producción, que no se ha hecho ni se hará sin autorización explícita.

### 4.5 Notas crédito y débito en habilitación (2026-11-07): aceptadas

Se enviaron por el modo `enviar-sync`, ambas referenciando la factura aceptada `SETP-990000504` (número, CUFE, tipo y fecha leídos de su XML firmado):

| Documento | Concepto | Resultado de la DIAN |
|---|---|---|
| `SETP-990000505` | Nota crédito, anulación total (mismas líneas y totales que la factura) | **Autorizada**, sin notificaciones |
| `SETP-990000506` | Nota débito por intereses (una línea de 10.000 con IVA 19 %) | **Autorizada**, sin notificaciones |

Las dos firmas verifican con el verificador independiente (3 referencias firmadas cada una), y el CUDE se calculó con `AsignarCUDE` y el PIN del software. Con esto, los tres documentos de venta (factura, nota crédito y nota débito) funcionan de punta a punta con las bibliotecas nuevas. En total se han enviado seis documentos a la habilitación de MARLIT: la `…501` rechazada y las `…502` a `…506` aceptadas; la `…503` sin resultado individual (ver 4.4).

La herramienta ahora admite `--tipo factura|nota-credito|nota-debito` y `--referencia <número>` para las notas.

### 4.6 Documento soporte (2026-11-07): listo en local, faltan los datos de habilitación

**Consulta de rangos (solo lectura, modo `rangos`):** para MARLIT con el software de facturación (QuantyERP), la DIAN responde `(302) No registra prefijos asociados al código de software`. Es decir, ese software no tiene prefijos autorizados en habilitación, y el set `SETP` de facturación no sirve para el documento soporte. El documento soporte necesita su **propio software** (identificador y PIN) y su **propio rango de numeración**, que se obtienen en el proceso de habilitación del documento soporte.

**Prueba local con datos provisionales:** la herramienta ahora admite `--tipo documento-soporte`. Con las bibliotecas Documentos y Firma nuevas genera el documento soporte `SEDS984000001`, calcula el CUDS, lo firma con `FirmarDocumentoSoporte` y la firma verifica con el verificador independiente. No se envió nada: los valores de software y rango eran de relleno.

**Hallazgo (error latente de la biblioteca):** `MetaDatosDE.AddEmisorPersonaNatural` y `AddEmisorPersonaJuridica` comparan `_tipoDocumento == TipoDocumentoElectronico.DOCUMENTO_SOPORTE` por referencia. Como `DOCUMENTO_SOPORTE` es una propiedad estática que crea un objeto nuevo en cada acceso, la comparación nunca es verdadera; el método cae en la rama de documentos no soporte, el proveedor queda vacío y `ConProveedor` lanza `NullReferenceException`. El método `AddEmisor` sí compara por `Codigo` y funciona. Corrección propuesta: comparar por `Codigo` en las dos líneas (`MetaDatosDE.cs`, líneas 239 y 276). Además, `Documentos\MetaDatosDE.cs` es una copia antigua que no se compila (`Compile Remove`) y conviene borrar.

**Para poder enviar** hay que completar `config-local\documento-soporte.json` (ya existe la plantilla con los campos vacíos): identificador y PIN del software del documento soporte, prefijo, número y fecha de la resolución, rango desde y hasta, y vigencia. El `testSetId` del documento soporte solo hace falta si se usa el set de pruebas; con `enviar-sync` no.

### 4.7 Documento soporte en habilitación (2026-11-07): autorizado, con cuatro notificaciones

El usuario entregó los datos del documento soporte: el software es el mismo de facturación (QuantyERP), el rango es `SEDS` 984000000 a 985000000 con la resolución de pruebas, y un TestSetID propio. Se enviaron dos documentos:

| Documento | Vía | Resultado de la DIAN |
|---|---|---|
| `SEDS-984000501` | Set de pruebas | El set ya figuraba **Aceptado**; no devolvió detalle del documento. |
| `SEDS-984000502` | Síncrono (`enviar-sync`) | **Autorizado** (código 00): "La Documento soporte con no obligados SEDS984000502, ha sido autorizada." Con cuatro notificaciones. |

La firma de ambos verifica con el verificador independiente. El proveedor es ficticio (persona natural de prueba, cédula `1000000001`).

**Notificaciones (no bloquean), pendientes de resolver:**

| Regla | Mensaje de la DIAN |
|---|---|
| `DSAK57` | Estructura código no válida |
| `DSAJ73` | Estructura código no válida |
| `DSAJ10a` | El nombre no corresponde un valor correcto de la lista |
| `DSAJ11` | El nombre no corresponde un valor correcto de la lista |

No se sabe a qué elementos corresponden: la respuesta solo da el código de la regla. Para identificarlos hace falta la fila de cada regla en el anexo técnico del documento soporte (el elemento y la lista de valores), como se hizo con `FAJ41`. En el XML generado hay candidatos que se ven sospechosos, sin confirmar:

- En el proveedor, `CompanyID` lleva `schemeName="31"` (NIT) aunque es una persona natural con cédula, y un dígito de verificación (`schemeID="9"`). El generador fija ese `31` "quemado" en el código.
- El esquema tributario del proveedor es `ZZ` con nombre `No aplica`; el nombre de lista del régimen es `No aplica – Otros *`.
- El adquiriente (MARLIT) lleva esquema tributario `01` y nombre `IVA`.

**Qué demuestra:** la cadena nueva funciona de punta a punta también para el documento soporte: generación, CUDS, firma con `FirmarDocumentoSoporte`, WCF y autorización de la DIAN en habilitación. Hasta aquí se han probado en habilitación la factura, la nota crédito, la nota débito y el documento soporte, siempre en Windows.

### 4.8 Documento soporte: `DSAK57` resuelta, quedan tres notificaciones

La sección "Reglas eliminadas" de la versión actual del anexo técnico del documento soporte dice que se quitaron, entre otros, estos grupos: en el proveedor, `PartyLegalEntity` y `Contact`; en el adquiriente, `PhysicalLocation` y `Contact`; en la factura, `Delivery`, `DeliveryTerms` y `PrepaidPayment`; y en las líneas, `PricingReference`. La regla `DSAK57` está en la lista de eliminadas.

`GeneradorDocumentoSoporte.ConAdquiriente` seguía generando `PhysicalLocation` para el adquiriente. Se quitó ese grupo (cambio en `Unificado\src\Shared\Documentos\GeneradorDocumentoSoporte.cs`). Resultado en habilitación: `SEDS-984000503` autorizado y **`DSAK57` desapareció**.

El proveedor del generador ya no emite `PartyLegalEntity` ni `Contact`, y las funciones de entrega, términos de entrega y anticipo no se usaron en estas pruebas. `GeneradorNotaAjusteDocumentoSoporte` no emite `PhysicalLocation` para el adquiriente; sus dos apariciones son la del proveedor y la de los detalles de entrega. (Una versión anterior de esta nota decía lo contrario; se corrigió al revisar el código.)

**Siguen tres notificaciones**, que no bloquean:

| Regla | Mensaje |
|---|---|
| `DSAJ10a` | El nombre no corresponde un valor correcto de la lista |
| `DSAJ11` | El nombre no corresponde un valor correcto de la lista |
| `DSAJ73` | Estructura código no válida |

**Experimento:** se envió `SEDS-984000504` con un proveedor persona jurídica con NIT en lugar de persona natural con cédula (variable `HAB_PROVEEDOR=juridico`). Salieron las mismas tres notificaciones, así que **no dependen del tipo de proveedor**. Las dos reglas de "nombre" son parecidas a `FAJ41`, que resultó ser el nombre del esquema tributario; los candidatos son el nombre del esquema tributario `ZZ` (`No aplica`) y el nombre de lista del régimen (`No aplica – Otros *`) del proveedor. Para confirmarlo hacen falta las filas de esas tres reglas en el anexo.

### 4.9 Documento soporte: autorizado sin ninguna notificación (2026-11-07)

El usuario pegó la fila de `DSAJ73`: `/Invoice/cac:AccountingSupplierParty/cac:Party/cac:PhysicalLocation/cac:Address/cbc:PostalZone` (código postal del proveedor), que debe ser un valor de la tabla 16.4.4 del anexo cuando `CustomizationID` es `10` (proveedor residente).

| Regla | Causa | Arreglo | Resultado |
|---|---|---|---|
| `DSAK57` | El anexo eliminó `PhysicalLocation` del adquiriente y el generador lo seguía emitiendo. | Se quitó el grupo de `GeneradorDocumentoSoporte.ConAdquiriente`. | Desapareció (`SEDS-984000503`). |
| `DSAJ73` | `DireccionFisica.CodigoPostal` vale `"N/A"` por defecto y salía en `PostalZone`. | Código postal real del proveedor (`760003`). | Desapareció (`SEDS-984000505`). |
| `DSAJ10a` y `DSAJ11` | Nombre de la ciudad y del departamento del proveedor, escritos en mayúsculas (`CALI`, `VALLE DEL CAUCA`) y la lista de la DIAN los trae en minúsculas con inicial en mayúscula. | `Cali` y `Valle del Cauca`. | Desaparecieron (`SEDS-984000506`). |

**Resultado final:** `SEDS-984000506` quedó **autorizado sin ninguna notificación**, y su firma verifica. La fila de `DSAJ10a` que pegó el usuario confirma que apunta a `/Invoice/cac:AccountingSupplierParty/cac:Party/cac:PhysicalLocation/cac:Address/cbc:CityName` y que debe ser un municipio de Colombia cuando `CustomizationID` es `10`. La fila de `DSAJ11` que pegó después confirma lo mismo para `/Invoice/cac:AccountingSupplierParty/cac:Party/cac:PhysicalLocation/cac:Address/cbc:CountrySubentity`: debe ser un departamento de Colombia cuando `CustomizationID` es `10` y `IdentificationCode` es `CO`, y corresponder a un valor de la lista. Con esto las cuatro reglas del documento soporte quedan explicadas por su fila del anexo, salvo `DSAK57`, que se explicó por la lista de reglas eliminadas.

**Lecciones para la API unificada:**

- El nombre del municipio y del departamento no debe ser texto libre: hay que tomarlo de un catálogo por código (DIVIPOLA), con la escritura de la lista de la DIAN. En la factura de MARLIT los nombres en mayúsculas no generaron notificación, pero en el documento soporte sí; no se puede asumir que dos documentos validan igual.
- `DireccionFisica.CodigoPostal` con `"N/A"` por defecto es un valor inválido en cuanto el documento lo exija. El proveedor de un documento soporte necesita un código postal real.
- El generador debe seguir la versión vigente del anexo: hay grupos eliminados que la biblioteca original todavía emite. Conviene repasar los demás generadores contra la sección de reglas eliminadas.

Documentos soporte enviados a habilitación de MARLIT: `SEDS-984000501` (al set, sin detalle) y `…502` a `…506` (síncronos); el siguiente consecutivo libre es 984000507. La herramienta de prueba ya usa el código postal y los nombres corregidos.

### 4.10 Nota de ajuste del documento soporte (2026-11-07): autorizada, con notificaciones por revisar

Se envió una anulación total de `SEDS-984000506` con el modo `--tipo nota-ajuste-ds`. Se necesitaron tres intentos:

| Documento | Resultado |
|---|---|
| `SEDS-984000601` | **Rechazada** (código 99): `NSAJ25a`, el contenido del atributo no corresponde a `31`. |
| `SEDS-984000602` | Autorizada, con `NSAZ04`, `NSAZ05`, `NSAX04`, `NSAJ43b` y `NSAJ44b`. |
| `SEDS-984000603` | Autorizada, con `NSAS14`, `NSAX04`, `NSAJ43b` y `NSAJ44b`. |
| `SEDS-984000604` | **Autorizada**, con `NSAX04`, `NSAJ43b` y `NSAJ44b`. La firma verifica. |

**Correcciones hechas en `GeneradorNotaAjusteDocumentoSoporte`:**

- **`NSAJ25a` (rechazo):** el tipo de identificación del proveedor salía con el código real (`13` para una cédula) y la DIAN exige `31`. Se fijó `31` en `ConProveedor`, igual que ya hacía el generador del documento soporte.
- **`NSAZ04` y `NSAZ05`:** la línea de la nota no llevaba marca ni modelo del artículo. Se agregaron `BrandName` y `ModelName` en `AgregarLinea` (a partir de `Linea.Marca` y `Linea.Referencia`). Hay que darles valor real: los valores por defecto de `Linea` son `UNKNOW_BRAND` y `UNKNOW_MODEL`.

**Notificaciones que quedan (no bloquean):**

| Regla | Mensaje | Estado |
|---|---|---|
| `NSAX04` | No se encuentra el grupo TaxSubtotal | Pendiente. Un resumen de IVA al 0 % lo reemplaza por `NSAS14` (la tarifa 0 no está en la tabla 16.3.8), así que no sirve. Falta la fila de la regla en el anexo para saber a qué grupo apunta. |
| `NSAJ43b` y `NSAJ44b` | El nombre o el NIT no corresponde al RUT | Se deben al proveedor ficticio de la prueba; con un proveedor real no deberían salir. |

**Estado de los documentos en habilitación (siempre en Windows):** factura, nota crédito, nota débito, documento soporte y nota de ajuste del documento soporte, todos autorizados. Sin probar: Linux, nómina, eventos RADIAN y documentos del POS.

### 4.11 Nota de ajuste del documento soporte: `NSAX04` resuelta

El usuario pegó la fila de `NSAX04`: apunta a `/CreditNote/cac:CreditNoteLine/cac:TaxTotal/cbc:TaxSubtotal`, el grupo de impuestos **de cada línea** de la nota, no el del documento. `GeneradorNotaAjusteDocumentoSoporte.AgregarLinea` solo crea el `TaxTotal` de la línea si `Linea.Impuestos` tiene elementos; en la prueba el servicio no tiene impuestos, así que el grupo no se genera y la DIAN lo notifica.

Se intentó darle a la línea un impuesto en cero. Todos los intentos se hicieron en habilitación:

| Intento | Qué se cambió | Resultado |
|---|---|---|
| `SEDS-984000603` | Resumen de IVA al 0 % **en el documento** | Autorizada, pero cambia `NSAX04` por `NSAS14` (la tarifa llegó como `0` y no como `0.00`; la tabla 16.3.8 sí admite 0.00 para el IVA exento). |
| `SEDS-984000605` | Tributo `ZY` ("No causa") en la **línea**, base 100.000 | **Rechazada:** `NSAU04` (la base imponible del total no es la suma de las bases de las líneas) y `NSAX16` (el identificador no corresponde a la columna "Identificador"). |
| `SEDS-984000606` | `ZZ` ("Otros") en la línea, con la base del total corregida | **Rechazada:** `NSAX16`. `NSAU04` ya no sale. |
| `SEDS-984000607` | Sin impuesto en la línea (estado anterior) | Autorizada, con `NSAX04`, `NSAJ43b` y `NSAJ44b`. |

**Qué aclaró la tabla 16.3.8.** El usuario la pegó después: es la tabla de **tarifas** (`cbc:Percent`) por impuesto, no la lista de identificadores de tributo (esa es otra tabla; `NSAX16` se refiere a su columna "Identificador"). Para el IVA admite 0.00 (exento), 5.00 y 19.00, y dice que un bien o servicio **excluido** no se debe reportar en el grupo `TaxTotal`. Con eso: `ZY` y `ZZ` no son identificadores de tributo válidos en la línea, y el IVA exento al `0.00` sí. Mi lectura anterior de que 16.3.8 era la lista de identificadores estaba equivocada.

**Solución (`SEDS-984000608` y `…609`):** la línea sin impuestos se reporta como IVA (`01`) con tarifa `0.00`, importe `0.00` y base igual a la de la línea. La base imponible del total incluye esa base (si no, `NSAU04` rechaza) y el documento lleva el resumen de IVA exento. Resultado: autorizada y `NSAX04` desapareció. Quedan solo `NSAJ43b` y `NSAJ44b`, que son del proveedor ficticio.

**Dónde quedó el arreglo:** en `GeneradorNotaAjusteDocumentoSoporte`, no solo en la herramienta de prueba. `AgregarLinea` genera el IVA exento de las líneas sin impuestos, y `ConTotales` suma su base a la base imponible del total y agrega el resumen de IVA exento del documento. `ConTotales` debe llamarse después de `AgregarLinea` y antes de `AsignarCUDS`, y `Totales.BaseImponible` solo debe incluir las líneas que sí tienen impuestos. `SEDS-984000609` se generó así y su firma verifica.

**Decisión fiscal pendiente:** la tabla dice que un servicio *excluido* no se reporta en `TaxTotal`, pero `NSAX04` pide el grupo en toda línea de la nota. Reportar la línea como IVA exento hace que la DIAN autorice sin notificaciones, pero "exento" no es lo mismo que "excluido". Conviene que lo confirme quien lleve la contabilidad.

Consecutivos de nota de ajuste usados: `984000601` a `984000609`; el siguiente libre es `984000610`.

### 4.12 Nómina electrónica (2026-11-07): lista en local, faltan el software y el set de nómina

**Comparación de los generadores de nómina entre las copias.** Se compararon `GeneradorNominaIndividual`, `GeneradorAjusteNominaIndividual` y `GeneradorContenedor` de `NominaRadian.Bibliotecas` contra los de POS. Las diferencias son de adaptación de tipos, no de lógica: la copia de Nómina usa sus propias clases (`ambiente.Codigo`, `TipoDocumento.NOMINA_INDIVIDUAL`, `pago.Forma.Codigo`, `emisor.Nombre`, `Referencia.Cufe`), y la de POS usa los tipos de la biblioteca Metadatos (enumeraciones como `AmbienteDestino` y `FormaPago`, `TipoDocumentoElectronico`, `emisor.RazonSocial`, `Referencia.UUID`). La copia de POS agrega además `GenerarTextoQR` en la nómina individual. La copia de POS, que es la que está en `Unificado`, cubre entonces lo mismo que la de Nómina. Pero `API_NOMINA` está escrita contra los tipos de Nómina, así que su código tendrá que adaptarse a los de Metadatos cuando se migre.

**Prueba local.** La herramienta ahora admite `--tipo nomina`. Con datos provisionales de software generó la nómina `NOM1001` (trabajador ficticio con sueldo de 8.000.000, salud y pensión al 4 %), calculó el CUNE, la firmó con `FirmarNominaIndividual` y la firma verifica. No se envió nada.

**Para poder enviar** hay que completar `config-local\nomina.json` (ya existe la plantilla): identificador y PIN del software de nómina, y el identificador del set de pruebas de nómina. La nómina no usa resolución de numeración, solo prefijo (`NOM` por defecto) y consecutivo. El software de nómina es distinto del de facturación.

### 4.13 Nómina electrónica en habilitación (2026-11-07): autorizada, sin notificaciones

El usuario entregó los datos de nómina: software `QuantyERP` (identificador distinto del de facturación), su PIN y el identificador del set de pruebas de nómina, que se guardaron en `config-local\nomina.json`. Se envió `NOM-1001` al set de pruebas de nómina:

| Documento | Vía | Resultado de la DIAN |
|---|---|---|
| `NOM-1001` | Set de pruebas de nómina | **Autorizado** (código 00): "La Nomina Individual NOM-1001, ha sido autorizada." Sin notificaciones. |

La firma verifica con el verificador independiente. El trabajador es ficticio (dependiente, contrato a término indefinido, sueldo de 8.000.000, salud y pensión al 4 %), y el empleador es MARLIT con el municipio y el departamento escritos como en la lista de la DIAN (`Cali`, `Valle del Cauca`).

**Qué demuestra:** el generador de nómina de la biblioteca Documentos (copia de POS, adaptada a los tipos de Metadatos), `FirmarNominaIndividual`, el WCF propio y la validación de la DIAN funcionan de punta a punta para la nómina individual, en habilitación y en Windows. No se probó el ajuste de nómina individual ni ningún caso con horas extra, incapacidades u otros devengados.

**Estado en habilitación, siempre en Windows:** factura, nota crédito, nota débito, documento soporte, nota de ajuste del documento soporte y nómina individual, todos autorizados. Sin probar: Linux, ajuste de nómina, eventos RADIAN y documentos del POS (documento equivalente).

### 4.14 Eventos RADIAN en habilitación (2026-11-07): acuse, recibo y aceptación autorizados

Los eventos RADIAN los registra el **adquiriente** de la factura. Para poder probarlos con un solo certificado, se emitió una factura donde MARLIT es a la vez emisor y adquiriente (la DIAN la autorizó sin notificaciones) y sobre ella se registraron los eventos, firmados con el certificado de MARLIT y con el software de facturación (no hubo que registrar un software aparte en habilitación).

| Documento | Resultado de la DIAN |
|---|---|
| `SETP-990000507` (factura **de contado**, MARLIT emisor y adquiriente) | Autorizada. Pero los eventos sobre ella se rechazaron (ver abajo). |
| `EVT1`, `EVT2` y `EVT3` (acuse, recibo y aceptación sobre la `…507`) | **Rechazados:** `LGC62` ("La factura referenciada no es de tipo crédito, PaymentMeansID=2"). Además, `LGC09` (no se puede registrar el recibo del bien sin el acuse), `LGC12` y `LGC13` (la aceptación expresa exige antes el recibo del bien y el acuse). |
| `SETP-990000508` (factura **a crédito**, MARLIT emisor y adquiriente) | Autorizada, sin notificaciones. |
| `EVT4` (acuse de recibo, código 030) | **Autorizado.** |
| `EVT5` (recibo del bien o prestación del servicio, código 032) | **Autorizado.** |
| `EVT6` (aceptación expresa, código 033) | **Autorizado.** |

Las tres firmas de los eventos verifican con el verificador independiente.

**Lo que enseñaron los rechazos:**

- **Solo se aceptan eventos sobre facturas a crédito** (`PaymentMeansID` = 2). Con la factura de contado, los tres eventos se rechazan aunque estén bien armados.
- **El orden es obligatorio:** acuse de recibo (030), luego recibo del bien o servicio (032), luego aceptación expresa (033).
- Una factura con el mismo NIT de emisor y adquiriente es válida en habilitación, lo que permite probar RADIAN sin depender de otra empresa.

La herramienta admite ahora `--tipo evento --referencia <factura>`, con la variable `HAB_EVENTO` (`acuse`, `recibo`, `aceptacion` o `rechazo`), y las variables `HAB_ADQUIRIENTE=marlit` y `HAB_PAGO=credito` para emitir la factura de prueba. El evento de rechazo (reclamo) se puede enviar igual pero no se probó.

**Estado en habilitación, siempre en Windows:** factura (contado y crédito), nota crédito, nota débito, documento soporte, nota de ajuste del documento soporte, nómina individual y los tres eventos RADIAN principales. Sin probar: Linux, ajuste de nómina, el evento de rechazo y los documentos del POS (documento equivalente).

### 4.15 Documento equivalente POS (2026-11-07): listo en local, faltan los datos de habilitación del POS

Se agregó a la herramienta de prueba el modo `--tipo documento-equivalente`, que arma el documento con `GeneradorDocumentoEquivalente` (tipo POS, operación general, consumidor final) y las tres extensiones del POS: fabricante del software, beneficios del comprador y caja de venta. Lo firma con `FirmarDocumentoEquivalente`.

**Prueba local con datos provisionales:** generó `EPOS1` con un producto de 100.000 más IVA 19 %, calculó el CUDE y la firma verifica con el verificador independiente. No se envió nada: el software, el rango y el set eran de relleno.

**Para poder enviar** hay que completar `config-local\pos.json` (ya existe la plantilla): identificador y PIN del software del POS, identificador del set de pruebas del POS, y el rango de numeración. Las pruebas originales usaban, para la habilitación del POS, el prefijo `EPOS`, el rango 1 a 1000 y la resolución `18760000001` vigente del 2019-01-19 al 2030-01-19; son los valores provisionales de la herramienta y deben confirmarse con los que muestre la DIAN.

**Pendiente de decidir para el POS:** el adquiriente se fija como consumidor final (`222222222222`). El ejemplo original llamaba además a `ConAdquiriente` con una persona; aquí no se hizo, porque `ParaConsumidorFinal` ya lo define.

### 4.16 Documento equivalente POS en habilitación (2026-11-07): autorizado sin notificaciones

El usuario entregó el software del POS (identificador y PIN) y el set de pruebas del POS, y después el rango. Se enviaron tres documentos:

| Documento | Vía | Resultado de la DIAN |
|---|---|---|
| `EPOS-1` | Set de pruebas | **Rechazado** (código 99): `DEAB12b`, el valor final del rango informado no corresponde a un rango vigente del emisor. Además, las notificaciones `DEAJ10`, `DEAJ11`, `DEAJ30`, `DEAJ31` y `DEAJ73`. |
| `EPOS-2` | Set de pruebas | **Autorizado**, con la notificación `DEAJ73`. |
| `EPOS-3` | Síncrono | **Autorizado, sin notificaciones.** La firma verifica. |

**Causa del rechazo `DEAB12b`:** la herramienta usaba como rango provisional 1 a 1.000, el de las pruebas originales. Se consultó en solo lectura (modo `rangos`, con la variable `HAB_SOFTWARE` para elegir el software) qué rangos tiene la DIAN registrados para el NIT y el software del POS, y devolvió `EPOS`, de **1 a 1.000.000**, resolución `18760000001`, vigencia del 2019-01-19 al 2030-01-19. El usuario entregó el mismo rango. Al corregirlo, el rechazo desapareció. La DIAN informa la fecha de la resolución como 2026-01-01, distinta de la 2019-01-19 que se usa; no causó ninguna regla.

**Notificaciones de nombre y código (`DEAJ10`, `DEAJ11`, `DEAJ30`, `DEAJ31`):** desaparecieron al escribir la ciudad y el departamento del emisor como en la lista de la DIAN (`Cali`, `Valle del Cauca`) y al poner un código postal real, igual que con el documento soporte. No se aisló cuál de los dos cambios resolvió `DEAJ30` y `DEAJ31`.

**`DEAJ73` (código postal del emisor):** el XML no llevaba `PostalZone`, porque `GeneradorDocumentoEquivalente.ConEmisor` no lo generaba (el generador del documento soporte sí). Se agregó en la dirección física del emisor, tomado de `DireccionFiscal.CodigoPostal`. No se emite el valor por defecto `N/A` de `DireccionFisica`, que no es un código válido. Con ese cambio, `EPOS-3` salió sin notificaciones.

**Estado en habilitación, siempre en Windows:** factura (contado y crédito), nota crédito, nota débito, documento soporte, nota de ajuste del documento soporte, nómina individual, los eventos RADIAN principales y el documento equivalente POS, todos autorizados. **Sin probar:** Linux, el ajuste de nómina, el evento de rechazo, las notas de ajuste del documento equivalente y los demás tipos de documento equivalente (peajes, transporte, etc.).

### 4.17 Ajuste de nómina individual en habilitación (2026-11-07): reemplazar y eliminar autorizados

Se probaron las dos variantes del ajuste de nómina con `--tipo nomina-ajuste`, que elige entre `reemplazar` y `eliminar` con la variable `HAB_AJUSTE`. En `reemplazar` el ajuste lleva los devengados, deducciones y totales nuevos; en `eliminar` solo referencia la nómina. La referencia (número, CUNE y fecha) se lee del XML firmado de la nómina original.

| Documento | Vía | Resultado de la DIAN |
|---|---|---|
| `ANE-1` (reemplazar `NOM-1001`) | Síncrona | **Rechazado:** regla `92` ("El Emisor del Documento no se encuentra Habilitado en la Plataforma"), `NIAE017` y `NIAE033` (NIT sin guiones ni DV). |
| `ANE-2` (reemplazar `NOM-1001`) | Set de pruebas de nómina | **Autorizado:** "La Nomima Individual De Ajustes ANE-2, ha sido autorizada." |
| `NOM-1002` (nómina nueva) | Set de pruebas de nómina | **Autorizada.** |
| `ANE-3` (eliminar `NOM-1002`) | Set de pruebas de nómina | **Autorizado.** |

Las firmas de `NOM-1002`, `ANE-2` y `ANE-3` verifican con el verificador independiente.

**Sobre el rechazo de `ANE-1`:** la vía síncrona de nómina (`SendNominaSync`, método `ClienteServicioDian.EnviarNomina`) exige que el emisor esté **habilitado en la plataforma**, es decir, que la habilitación de nómina esté completada, no solo que se envíen documentos de prueba. Mientras no lo esté, los documentos de nómina solo entran por el set de pruebas. `NIAE017` y `NIAE033` acompañan a la regla `92`; el NIT enviado (`901033307`, sin guiones ni DV) es el correcto, y las mismas bibliotecas generaron nóminas autorizadas por el set. El modo `enviar-sync` de la herramienta ahora usa `EnviarNomina` para la nómina y sus ajustes, y `EnviarEvento` para los eventos.

**Pendiente en el portal de la DIAN:** completar la habilitación de nómina del software para poder usar la vía síncrona. Es un paso del portal, no del código.

**Estado en habilitación, siempre en Windows:** factura (contado y crédito), nota crédito, nota débito, documento soporte, nota de ajuste del documento soporte, nómina individual y su ajuste (reemplazar y eliminar), eventos RADIAN y documento equivalente POS, todos autorizados. **Sin probar:** Linux, el evento de rechazo, y los demás documentos equivalentes y sus notas de ajuste.

## 5. Paquete de prueba para Linux (2026-11-07)

Como en este equipo no hay WSL ni Docker, no se pudo probar en Linux. En su lugar se preparó un paquete autocontenido para `linux-x64`, que se corre en el servidor.

| | |
|---|---|
| **Paquete** | `Unificado\dist\DIAN.Unificado.Pruebas.linux-x64.tar.gz` (unos 39 MB) y su suma de verificación `.sha256`. La carpeta `dist` está en `.gitignore`. |
| **Contenido** | `Habilitacion.Pruebas` y `FirmaXades.Pruebas` publicados como autocontenidos (no hace falta instalar .NET), la DLL propia de WCF, `probar-linux.sh` y `LEEME-LINUX.md`. Las fuentes del script y de las instrucciones están en `Unificado\linux`. |
| **Qué no incluye** | Certificado, clave y datos de habilitación (software, PIN, set de pruebas): se copian aparte desde `config-local`. Se comprobó que el paquete no contiene ningún `.p12`, `.pfx` ni carpeta `config-local`. |
| **Estado** | Compila y se publica. **No se ha ejecutado en Linux.** |

**Cómo se usa** (detalle en `LEEME-LINUX.md`): descomprimir, copiar `config-local` por un canal seguro, definir `MARLIT_P12` sin dejarla en el historial y correr `./probar-linux.sh diagnostico`, luego `local` y, si se quiere, `habilitacion`. `local` genera y firma cuatro documentos (factura, documento soporte, documento equivalente POS y nómina) y verifica sus firmas, sin enviar nada. `habilitacion` además envía esos cuatro a la habilitación de la DIAN, previa confirmación escrita. El resultado queda en `resultado-linux.txt`, sin claves.

**Ajuste hecho para esto:** la ruta del certificado en `emisor-marlit.json` es de Windows. La herramienta ahora busca el mismo nombre de archivo dentro de `config-local` si esa ruta no existe en la máquina.

**Qué se espera aprender, por orden de probabilidad de falla:** (1) el WCF propio al enviar a la DIAN, que solo se ejercita en el envío; (2) la carga del certificado `.p12` según la versión de OpenSSL; (3) la zona horaria, que depende de `tzdata`; (4) la ausencia de ICU.

**Consecutivos que usa el script:** factura `SETP990000509`, documento soporte `SEDS984000507`, documento equivalente `EPOS4` y nómina `NOM1003`.

### 5.1 Resultado en Linux (2026-11-07): todo funcionó

La prueba se hizo en la máquina virtual de Hyper-V `quanty-lab` de SERVIDOR-SISTEMAS360 (acceso por SSH a través de la VPN, con salto por el servidor). Es un Ubuntu 24.04.5 LTS, kernel 6.8, con zona horaria `America/Bogota`, `tzdata`, ICU y OpenSSL 3.0.13. El paquete se copió y se descomprimió allí; el certificado y los `.json` se copiaron por `scp` y la clave viajó por la entrada estándar de SSH, sin quedar en argumentos ni en el historial.

| Etapa | Resultado |
|---|---|
| Diagnóstico del entorno | Correcto: `tzdata`, ICU y OpenSSL presentes. |
| Revisión del certificado | Correcta. La clave se carga como `RSAOpenSsl` (en Windows era `RSACng`) y firma con SHA-256. La cadena sale con la observación de raíz no confiable, igual que en Windows. |
| Generar y firmar factura, documento soporte, documento equivalente POS y nómina | Los cuatro, correctos; las cuatro firmas verifican con el verificador independiente. |
| Enviar a habilitación `SETP-990000509` (factura) | **Autorizada**, sin notificaciones. |
| Enviar a habilitación `SEDS-984000507` (documento soporte) | **Autorizado**, sin notificaciones. |
| Enviar a habilitación `EPOS-4` (documento equivalente POS) | **Autorizado**, sin notificaciones. |
| Enviar al set de nómina `NOM-1003` | **Autorizada**, sin notificaciones. |

**Resumen: 14 pasos, 0 con fallas.** Los cuatro puntos de riesgo que se habían identificado resultaron sin problema: el WCF propio (`System.Private.ServiceModel.dll`) envía bien a la DIAN desde Linux, el certificado `.p12` carga, la zona horaria es correcta y no hizo falta la globalización invariante.

**Observación:** los XML firmados en Linux pesan unos 200 bytes menos que los de Windows (por ejemplo, la factura, 18.535 bytes frente a 18.747). La DIAN los aceptó igual. La diferencia probablemente viene de cómo cada sistema arma la cadena de certificados que se incluye en la firma; no se investigó.

**Limpieza:** al terminar se borró de la VM la carpeta `config-local` (certificado y configuración) y el registro del resultado. Se comprobó que no quedan archivos `.p12` ni `.pfx`, que `MARLIT_P12` no está en el entorno remoto y que no aparece en el historial. El paquete sin secretos quedó en `/home/sis360/dian-pruebas`. El resultado completo se guardó en `Unificado\dist\resultado-linux-quanty-lab.txt` (la carpeta `dist` está fuera del repositorio).

**Consecutivos consumidos en esta prueba:** `SETP990000509`, `SEDS984000507`, `EPOS4` y `NOM1003`. Los siguientes libres son `SETP990000510`, `SEDS984000508`, `EPOS5` y `NOM1004`.

**Estado en habilitación, en Windows y en Linux:** factura, nota crédito, nota débito, documento soporte, nota de ajuste, nómina y su ajuste, eventos RADIAN y documento equivalente POS, todos autorizados en Windows. En Linux se probaron factura, documento soporte, documento equivalente POS y nómina. **Sin probar en Linux:** notas crédito y débito, nota de ajuste del documento soporte, ajuste de nómina y eventos RADIAN, aunque usan las mismas bibliotecas de firma y de envío.

## 6. Correcciones del grupo A (2026-11-07, después del cierre)

- `FirmaElectronica.FirmarFactura/NotaCredito/DocumentoSoporte`: se restauró `numExtension = 2` como parámetro opcional.
- `MetaDatosDE.AddEmisorPersonaNatural/Juridica`: la comparación con `DOCUMENTO_SOPORTE` ahora es por código; se borró el `Documentos\MetaDatosDE.cs` viejo.
- Pruebas de regresión de los generadores (`tests\Shared.Pruebas\GeneradoresTests.cs`): 23 pruebas pasan.
- DLL de WCF documentada en `libs\WCF\LEEME.md` (SHA-256 79d09962...6dbb6).
- Verificación en habilitación con el código corregido: factura SETP990000510 y documento soporte SEDS984000508, ambos autorizados.
- Consecutivos libres: SETP 990000511, SEDS 984000509, EPOS 5, NOM 1004, ANE 4, EVT 7.
- Aviso nuevo: NuGet reporta vulnerabilidades conocidas en `System.Security.Cryptography.Xml` 10.0.0 y `.Pkcs` 10.0.0 (NU1902/NU1903).

- Vulnerabilidades NU1902/NU1903 resueltas: `System.Security.Cryptography.Xml` y `.Pkcs` subidos a 10.0.14 en Microsoft.Xades.csproj; 23 pruebas pasan y SETP990000511 autorizada. Libre: SETP 990000512.
