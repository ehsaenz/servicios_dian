# Prueba de firma con un certificado real

**Estado:** partes A y B ejecutadas el 2026-11-07 con el certificado de MARLIT GROUP SAS (resultados en la sección 5). La parte C necesita bibliotecas que aún no se han migrado y datos que faltan.

---

## 1. Seguridad

- **No pegues la clave del certificado en el chat ni en un comando.** La herramienta la lee de la variable de entorno `FIRMA_PFX_CLAVE`, que la defines tú en tu propia ventana de terminal y borras al terminar. Nunca se imprime.
- **No copies el `.pfx` real al repositorio.** Déjalo donde está y usa su ruta.
- Los datos del set de pruebas de la DIAN (identificador del software, clave técnica y PIN) están en `Unificado\config-local\habilitacion-sw-propio.json`. Esa carpeta está en el `.gitignore` de `Unificado`. Hoy `Unificado` todavía no es un repositorio git, así que ese archivo está en texto plano en el disco; cuando se cree el repositorio, el `.gitignore` evita que se suba. Este documento no repite esos valores.
- Si compartes la salida de los comandos conmigo, ten en cuenta que incluye el nombre del titular del certificado.

---

## 2. Parte A: revisar el certificado (local, no envía nada)

Comprueba si el certificado sirve para firmar con la biblioteca nueva: que tenga clave privada, que sea RSA, que la clave se pueda usar para firmar con SHA-256 y que la cadena de confianza se pueda construir.

En PowerShell:

```powershell
cd C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN\Unificado
dotnet build tests\FirmaXades.Pruebas
$env:FIRMA_PFX_CLAVE = '<la clave del certificado>'
dotnet tests\FirmaXades.Pruebas\bin\Debug\net10.0\FirmaXades.Pruebas.dll certificado-info "C:\ruta\certificado.pfx"
```

Resultado esperado: clave privada `sí`, RSA de 2048 bits o más, `Firma SHA-256: correcta`, y vigencia con días restantes. La cadena puede salir `con observaciones` si falta el certificado raíz de la entidad certificadora en tu equipo; no impide firmar.

Si sale `Firma SHA-256: FALLA`, el certificado está en un dispositivo (token o tarjeta) o requiere un PIN, y habrá que tratarlo distinto.

---

## 3. Parte B: firmar la muestra y verificarla (local, no envía nada)

```powershell
dotnet tests\FirmaXades.Pruebas\bin\Debug\net10.0\FirmaXades.Pruebas.dll firmar tests\FirmaXades.Pruebas\muestra\factura-minima.xml "C:\ruta\certificado.pfx" "$env:TEMP\firmado-real.xml"
dotnet tests\FirmaXades.Pruebas\bin\Debug\net10.0\FirmaXades.Pruebas.dll verificar "$env:TEMP\firmado-real.xml"
Remove-Item Env:FIRMA_PFX_CLAVE
Remove-Item "$env:TEMP\firmado-real.xml"
```

Resultado esperado: `xmldsig estándar: correcta (referencias firmadas: 3)`.

La muestra es una factura mínima de prueba, no un documento válido para la DIAN. Esta parte solo demuestra que la clave real firma bien con la biblioteca nueva.

---

## 4. Parte C: envío al ambiente de habilitación de la DIAN (pendiente)

Todavía no se puede hacer con las bibliotecas nuevas. Para enviar un documento válido hacen falta, como mínimo, estas bibliotecas migradas y probadas: **Util**, **Tipos**, **Firma**, **Documentos** (genera el XML con el CUFE) y **ServiciosWeb** (cliente SOAP de la DIAN, con el problema pendiente de WCF).

### 4.1 Datos que ya están disponibles

En `config-local\habilitacion-sw-propio.json`: modo de operación (software propio), rango de numeración de pruebas (prefijo `SETP`, resolución de pruebas, rango del 990000000 al 995000000), software `QuantyERP` con su identificador, clave técnica y PIN, y los totales requeridos: 50 documentos (30 facturas, 10 notas débito y 10 notas crédito), con al menos 1 factura aceptada.

### 4.2 Datos que faltan

- **TestSetID** de la DIAN para el contribuyente que se va a habilitar. No viene en el archivo.
- **Confirmar el NIT y su dígito de verificación.** Se indicó `9010333076`, que coincide con el número de serie del certificado. Se lee como NIT 901033307 con dígito de verificación 6; hay que confirmarlo.
- **Datos del emisor para el XML:** dirección, código de municipio, responsabilidades del RUT, régimen, correo, teléfono y matrícula mercantil de MARLIT GROUP SAS. Se pueden tomar de la configuración de QuantyERP para MARLIT.
- **Confirmar que MARLIT GROUP SAS está habilitada ante la DIAN con ese software** (QuantyERP), y que el set de pruebas sigue vigente (ver 4.3).
- **Consecutivos libres.** La aplicación actual ya envió documentos del rango `SETP` desde QuantyERP. Hay que saber el último consecutivo usado para no repetir uno, porque la DIAN rechaza un número ya recibido.

### 4.3 Una duda sobre las fechas

El archivo indica una ventana del set de pruebas del 2019-03-14 al 2019-06-14. Eso ya pasó. Hay que confirmar si la DIAN sigue aceptando envíos con ese set o si el contribuyente necesita un set de habilitación nuevo.

### 4.4 Cómo se haría

1. Migrar Util, Tipos, Firma, Documentos y ServiciosWeb a `Unificado\src\Shared`.
2. Generar con `Documentos` una factura `SETP-990000001` con los datos del emisor.
3. Firmar con `Firma` y la copia nueva de FirmaXades.
4. Enviar con `ClienteServicioDian.EnviarSetPruebas` al servicio de habilitación y leer la respuesta.
5. Comparar con el resultado del mismo documento generado y enviado por la aplicación actual.

Esta parte la autorizas tú explícitamente antes de ejecutarla, porque es un envío a un servicio externo con el certificado de un contribuyente.

---

## 5. Resultados de las partes A y B (2026-11-07)

Certificado `Unificado\config-local\certificado.p12`, clave tomada de la variable de entorno de usuario `MARLIT_P12` y pasada solo al proceso de la prueba. Todo fue local; no se envió nada.

### 5.1 Parte A: revisión del certificado

| Dato | Resultado |
|---|---|
| Titular | MARLIT GROUP SAS, "Emisor Factura Electrónica - Persona Jurídica" |
| Número de serie del titular | `9010333076`, coincide con el NIT indicado |
| Emisor del certificado | Andes SCD (CA ANDES SCD S.A. Clase II v3) |
| Vigencia | 2025-08-25 a 2027-08-25 (291 días restantes al 2026-11-07) |
| Clave | RSA de 2048 bits (`RSACng`), con clave privada |
| Firma SHA-256 de prueba | Correcta |
| Uso de clave | `KeyEncipherment`, `NonRepudiation`, `DigitalSignature` |
| Cadena | Con observación `UntrustedRoot`: la raíz de Andes SCD no está en el almacén de confianza de este equipo. No impide firmar. |

### 5.2 Parte B: firma de la muestra con el certificado real

| Comprobación | Resultado |
|---|---|
| Firmar con la copia nueva (.NET 10) y con la original (.NET 6) | Correcto en ambas |
| Tamaño de los dos XML firmados | Igual (8,533 bytes) |
| Estructura | Idéntica, 73 líneas |
| Verificación independiente (`SignedXml`), cuatro combinaciones de firma y verificador (.NET 10 y .NET 6) | Las cuatro correctas, con 3 referencias firmadas |

Los archivos firmados se borraron al terminar.

### 5.3 Qué demuestra y qué no

Demuestra que la clave real del certificado de MARLIT firma correctamente con la biblioteca nueva, igual que con la original. No demuestra que la DIAN acepte el documento: eso solo se sabe en la parte C.

El certificado vence el 2027-08-25. Conviene anotar la renovación.

---

## 6. Parte C: herramienta lista, envío pendiente de datos (2026-11-07)

Con las bibliotecas migradas (ver `fase1-bitacora-migracion.md`, sección 4) existe `Unificado\tests\Habilitacion.Pruebas`. Genera una factura `SETP`, la firma con el certificado de MARLIT y la envía al set de pruebas de habilitación.

- **Generar y firmar en local, sin enviar** (ya se ejecutó con valores provisionales): `generar --consecutivo N --provisional`.
- **Enviar:** `enviar --consecutivo N --confirmar`. No acepta datos provisionales, apunta siempre a habilitación (aborta si el endpoint no lo es) y exige `--confirmar`.
- **Datos:** salen de `Unificado\config-local`. La clave del certificado se toma de la variable `MARLIT_P12`.

Para poder enviar falta completar en `config-local\emisor-rut.json`: `regimenFiscal`, `responsabilidadTributaria`, `matriculaMercantil` y `telefono`, y confirmar departamento, municipio, dirección y correo (tomados del certificado). También hay que elegir un consecutivo `SETP` que no se haya usado.

Códigos válidos para los dos primeros:

- **`regimenFiscal`:** `R-99-PN` (no aplica; es el que usan las pruebas originales), `O-13` (gran contribuyente), `O-15` (autorretenedor), `O-23` (agente de retención de IVA) u `O-47` (régimen simple).
- **`responsabilidadTributaria`:** `01` (IVA), `04` (INC), `ZA` (IVA e INC) o `ZZ` (no aplica).

---

## 7. Resultado del envío a habilitación (2026-11-07)

Los datos del RUT salieron del PDF del RUT de MARLIT GROUP S.A.S. generado el 2025-08-16 y quedaron en `config-local\emisor-rut.json`. Se enviaron dos facturas al set de pruebas; la segunda, `SETP-990000502`, fue **aceptada por la DIAN** (código 00). La primera se rechazó porque el XML llevaba el nombre del emisor vacío. El detalle y la causa están en `fase1-bitacora-migracion.md`, sección 4.4.

La notificación `FAJ41` de esa factura venía del nombre del esquema tributario del emisor con el código `ZA`; con `01` desaparece (`SETP-990000504`, autorizada sin notificaciones). `config-local\emisor-rut.json` quedó con `01`. El set de pruebas ya figura como aceptado en la DIAN.

No se envió nada a producción.
