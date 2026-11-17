# Documentos soportados y su contrato

Última revisión: 2026-11-16

| Documento | Código DIAN | Por dónde se envía | Modo |
|---|---|---|---|
| Factura electrónica de venta | `01` | `POST /api/documento/agregar` | Asíncrono, idempotente |
| Nota crédito | `91` | `POST /api/documento/agregar` | Asíncrono, idempotente |
| Nota débito | `92` | `POST /api/documento/agregar` | Asíncrono, idempotente |
| Documento soporte (no obligados a facturar) | `05` | `POST /api/DocumentoSoporte/EnviarDSNO` o `EnviarDE` | Síncrono |
| Tiquete POS electrónico (documento equivalente) | `20` | `POST /api/DocumentoElectronico/EnviarDE` | Síncrono |
| Nota de ajuste crédito del POS | `94` | `POST /api/DocumentoElectronico/EnviarDE` | Síncrono |
| Nómina individual | (nómina) | `POST /api/v1/Nomina/EnviarNE` | Síncrono |
| Eventos RADIAN | `030` a `034` | `POST /api/v1/Eventos/*` | Síncrono |

Los tipos `02` (exportación) y `03` (contingencia) **no están soportados** en `api/documento`: responden 400 (`El tipo de documento 02 no está soportado (solo 01, 91 y 92)`).

> El servicio **nunca asigna ni cambia** su numeración, sus precios ni sus impuestos. Traduce lo que usted envía. Los valores de impuestos, bases y totales los calcula **usted** (como lo hace QuantyERP) y el servicio los toma de las líneas.

---

## Factura, nota crédito y nota débito: el JSON `DocumentoDian`

Es el modelo que arma QuantyERP con `DianDocumentBuilder`. El servicio lo lee con la misma regla de la API anterior: **los nombres de campo no distinguen mayúsculas/minúsculas**, los campos que no conoce se ignoran, las enumeraciones aceptan el nombre (`"Contado"`) o el número (`1`), y las fechas son ISO 8601 (`2026-11-16T10:30:00`, **hora local de Colombia, sin zona**).

> Los nombres respetan las erratas del modelo original (por ejemplo `IdenficadorPago`). Escríbalos tal cual.

Ejemplos completos y válidos en el formato: [ejemplos/factura.json](ejemplos/factura.json), [nota-credito.json](ejemplos/nota-credito.json), [nota-debito.json](ejemplos/nota-debito.json).

### Raíz del documento

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `TipoDocDian` | texto | **Sí** | `"01"` factura, `"91"` nota crédito, `"92"` nota débito. Cualquier otro: 400. |
| `TipoDocErp` | texto | No | Su código interno (`FV`, `NC`, `ND`). Se guarda y se devuelve en el registro; no afecta a la DIAN. |
| `Numero` | texto | **Sí** | Número **completo con prefijo** (`SETP990000519`). Es la llave de idempotencia. |
| `FechaHoraDoc` | fecha-hora | **Sí** | Fecha y hora de emisión, hora de Colombia. |
| `FormaPago` | objeto | **Sí** | Ver abajo. |
| `Resolucion` | objeto | **Sí** | Resolución de numeración de la DIAN. Ver abajo. |
| `Emisor` | objeto `persona` | **Sí** | Su empresa. |
| `Receptor` | objeto `persona` | **Sí** | El adquiriente. |
| `Items` | lista de `item` | **Sí** | Al menos una línea. |
| `Notas` | lista de texto | No | Observaciones. La primera es la nota principal. Las vacías se ignoran. |
| `OrdenRef` | objeto `{Numero, Fecha, Descripcion}` | No | Orden de compra del cliente. **Solo facturas (`01`)**; en notas se ignora. |
| `DocumentoRef` | objeto | **Sí en `91` y `92`** | Documento que la nota corrige. Ver abajo. |

Campos que el servicio **ignora** aunque su ERP los serialice: `ModoEnvio`, `ValorDoc`, `SubTotal`, `BaseGravada`, `BaseNoGravada`, `BaseTotal`, `ValorImp`, `IVA`, `ReteIVA`, `ReteFuente`, `ReteICA`, `Impuestos`, `Retenciones` (de cabecera), `ImpuestosList`, `Errores`, `Folio`, `TipoDocObj`, `ReadyDevTime`. Los importes se recalculan **desde las líneas** (`Items`).

### `FormaPago`

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `ID` | enum | Sí | `Contado` (1) o `Credito` (2). |
| `CodigoMedioPago` | texto | No | Código DIAN del medio de pago (`10` efectivo, `42` consignación, `48` tarjeta crédito, `49` tarjeta débito, `31` transferencia, ...). Si falta: `10`. |
| `IdenficadorPago` | texto | No | Identificador del pago. Si está vacío no se envía. |
| `Plazo` | entero | No | Días de plazo. El vencimiento es `FechaHoraDoc + Plazo`. |

Los códigos de medio de pago son los de la tabla de la DIAN (anexo técnico). El servicio no los valida: la DIAN sí.

### `Resolucion`

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `NumeroResolucion` | texto numérico | **Sí** | Número de la resolución de facturación (solo dígitos; se convierte a entero largo). |
| `Prefijo` | texto | Sí | Prefijo autorizado (`SETP` en habilitación). El `Numero` debe empezar por él. |
| `Desde`, `Hasta` | entero largo | Sí | Rango autorizado. |
| `FechaExpedicion`, `FechaVencimiento` | fecha | Sí | Vigencia de la resolución. |
| `ClaveTecnica` | texto | **Sí en `01`** | Clave técnica de la resolución. Con ella el servicio calcula el CUFE. No se pide en notas. |

### `persona` (`Emisor` y `Receptor`)

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `TipoPersona` | texto | **Sí** | `"JURIDICA"` o `"NATURAL"`, **en mayúsculas**. Otro valor se trata como natural. |
| `NIT` | texto | **Sí** | Identificación sin dígito de verificación. Es alfanumérica (cédula/NIT/pasaporte). |
| `DV` | texto | Emisor: sí | Dígito de verificación. |
| `TipoIdDian` | texto | Receptor | Tipo de documento DIAN (`31` NIT, `13` cédula, `22` cédula de extranjería, `41` pasaporte, `91` NUIP...). Si falta en el receptor: `31`. |
| `RazonSocial` | texto | Si `JURIDICA` | Razón social. |
| `PrimerNombre`, `SegundoNombre`, `PrimerApellido`, `SegundoApellido` | texto | Si `NATURAL` | Nombre de la persona natural. |
| `NombreComercial` | texto | No | El servicio usa la razón social/nombre completo. |
| `Codigo` | texto | Recomendado | Su código interno del tercero (`CodigoCliente`). |
| `MatriculaMercantil` | texto | Emisor: si usa sucursales | En el **emisor** decide el **logo** del PDF (por sucursal). En el receptor, si falta, se envía `00000`. |
| `EmailContacto` | texto | Receptor: recomendado | Correo del adquiriente; sale en el XML y es el destino del correo con el documento. |
| `TelefonoContacto` | texto | No | Teléfono. |
| `LocacionPrincipal` | objeto | **Sí** | Dirección fiscal. Ver abajo. |
| `LocacionComercial` | objeto | No | Dirección comercial; si falta se usa la principal. |
| `responsabilidades_fiscales` | lista | Recomendado | Ver abajo. |

`LocacionPrincipal` / `LocacionComercial`:

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `Direccion` | texto | Sí | |
| `CodigoMunicipio` | texto | **Sí** | Código DANE de 5 dígitos (`11001` Bogotá, `76001` Cali). Sin él: 400 `Falta la locación (dirección y código de municipio) de un tercero.` |
| `CodigoDepartamento` | texto | No | Si falta, son los 2 primeros dígitos del municipio. |
| `NombreMunicipio`, `NombreDepartamento` | texto | No | Si faltan, se toman de los catálogos DANE del servicio. |

`responsabilidades_fiscales` es una lista de `{Codigo, CodigoFE, Descripcion}`:

- Los códigos que empiezan por `O-` o `R-` (por ejemplo `O-13` gran contribuyente, `O-15` autorretenedor, `O-23`, `R-99-PN`) van a la DIAN como **responsabilidades fiscales** (`TaxLevelCode`). Se toma `CodigoFE` y, si está vacío, `Codigo`. Si no queda ninguno: `R-99-PN`.
- Una entrada con `Codigo` **`"48"`** marca al tercero como **responsable de IVA** (tributo `01`); sin ella el tributo es `ZZ` (no aplica).

### `item` (cada línea)

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `ID` | entero | Recomendado | Número de línea (1, 2, 3...). |
| `Codigo` | texto | Sí* | Su código de producto. *Si falta se usa `Referencia`. Va como código "estándar de adopción del contribuyente". |
| `Referencia` | texto | No | Referencia del fabricante. |
| `Detalle` | texto | **Sí** | Descripción del producto o servicio. |
| `Cant` | decimal | **Sí** | Cantidad. |
| `ValorUnit` | decimal | **Sí** | Valor unitario **antes de IVA y de descuentos**. |
| `DtoDir` | decimal | No | Descuento comercial de la línea como **fracción** (`0.10` = 10 %). |
| `Excluido` | booleano | No | `true` si la línea es excluida de IVA: no se envían sus impuestos. |
| `TarifaIVA` | decimal | No | Tarifa de IVA como fracción (`0.19`). Informativa para el servicio. |
| `impuestos` | lista | Sí si no es excluido | Impuestos de la línea (IVA, consumo...). Ver abajo. |
| `retenciones` | lista | No | Retenciones de la línea. Misma forma que `impuestos`. |

`impuestos[]` / `retenciones[]` (cada uno):

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `ID` | texto | **Sí** | Código DIAN del tributo: `01` IVA, `04` INC consumo, `05` ReteIVA, `06` ReteFuente, `07` ReteICA, ... Un código inexistente rompe la traducción (400). |
| `Nombre` | texto | No | |
| `Porcentaje` | decimal | **Sí** | Tarifa como **fracción** (`0.19`); el servicio la convierte a porcentaje (`19.00`). |
| `ValorBase` | decimal | **Sí** | Base del impuesto: la **base de la línea** (cantidad × valor × (1 − descuento)). |
| `Operacion` | enum | No | `SUMA` (1) o `RESTA` (2). |
| `Calculo` | enum | No | `PORCENTUAL` (por defecto), `NOMINAL`, `MILESIMAL`. |

**Reglas de cálculo que su ERP debe cumplir** (el servicio las toma tal cual; la DIAN rechaza si no cuadran):

- Base de la línea: `BaseGral = redondeo(Cant × ValorUnit × (1 − DtoDir), 2)`.
- Para una línea gravada, `impuestos[].ValorBase = BaseGral`.
- Impuesto por línea: `redondeo(ValorBase × Porcentaje, 2)` (el servicio lo recalcula para tributos porcentuales).
- Descuento: si `Cant × ValorUnit > BaseGral`, el servicio informa la diferencia como descuento de la línea.
- Totales: valor bruto = Σ `BaseGral`; impuestos = Σ impuestos de líneas no excluidas; **total a pagar = bruto + impuestos**. Las **retenciones no restan** del total del documento electrónico: se informan aparte.
- Redondeo "bancario" a 2 decimales, el mismo de QuantyERP.

### `DocumentoRef` (notas `91` y `92`)

| Campo | Tipo | Oblig. | Notas |
|---|---|---|---|
| `Numero` | texto | **Sí** | Número de la **factura** que se corrige. |
| `CUFE` | texto | **Sí** | CUFE de esa factura (96 hexadecimales). Lo da la respuesta de `agregar` de la factura original. |
| `Fecha` | fecha | Recomendado | Fecha de emisión de la factura origen. |
| `Concepto` | texto | No | Código del concepto de la nota (tabla abajo). |
| `Descripcion` | texto | No | |

Sin `Numero` o sin `CUFE`: 400 `La nota debe referenciar el documento origen (número y CUFE).` El documento origen **debe ser una factura de venta**; referenciar otra nota no está soportado.

**Conceptos de la nota crédito (`91`)**

| `Concepto` | Significado |
|---|---|
| `1` (o vacío) | Devolución parcial de los bienes y/o no aceptación parcial del servicio |
| `2` | Anulación de la factura electrónica |
| `3` | Rebaja o descuento parcial o total |
| `4` | Ajuste de precio |
| `5` | Descuento comercial por pronto pago |
| `6` | Descuento comercial por volumen de ventas |

**Conceptos de la nota débito (`92`)**

| `Concepto` | Significado |
|---|---|
| `1` | Intereses |
| `2` | Gastos por cobrar |
| `3` | Cambio del valor |
| `4` (o vacío) | Otros |

Otro valor: 400 `Concepto de nota crédito desconocido: x.` (o `de nota débito`). **Envíe siempre el concepto**: si falta, la nota crédito se emite como devolución parcial y la débito como "otros".

### Reglas de numeración y consecutivos

- **El servicio no asigna números.** Su ERP numera dentro del rango autorizado en `Resolucion.Desde`/`Hasta` y con el `Prefijo`.
- El servicio **no comprueba** que el número esté en rango ni que lleve el prefijo; la DIAN sí y rechazará (estado `Rechazado`).
- Un número **aprobado no se reutiliza ni se modifica**: reenviarlo devuelve el existente sin cambios (aunque el contenido sea distinto).
- Un número **rechazado** o en **error** se puede **corregir y reenviar con el mismo número** (el servicio lo reprocesa).
- El servicio **no compara** el NIT del `Emisor` del documento con el NIT del cliente en `api/documento`; si no coinciden, la DIAN rechaza. Envíe siempre su propio NIT.
- Las notas llevan su propio número y se mandan a la misma resolución o a la que su empresa tenga para notas, según su configuración ante la DIAN. En habilitación las pruebas de MARLIT usaron el mismo prefijo y rango del set de pruebas (**No verificado** para producción).

### Una factura mínima, comentada

```jsonc
{
  "TipoDocDian": "01",
  "TipoDocErp": "FV",
  "Numero": "SETP990001001",
  "FechaHoraDoc": "2026-11-16T10:00:00",
  "FormaPago": { "ID": "Contado", "CodigoMedioPago": "10", "IdenficadorPago": "", "Plazo": 0 },
  "Resolucion": { "NumeroResolucion": "18760000001", "Prefijo": "SETP", "Desde": 990000000, "Hasta": 995000000,
                  "FechaExpedicion": "2019-01-19T00:00:00", "FechaVencimiento": "2030-01-19T00:00:00",
                  "ClaveTecnica": "<CLAVE_TECNICA_DE_LA_RESOLUCION>" },
  "Emisor":   { "...": "ver ejemplos/factura.json" },
  "Receptor": { "...": "ver ejemplos/factura.json" },
  "Items": [ { "...": "ver ejemplos/factura.json" } ]
}
```

---

## Documento soporte (`05`)

`POST /api/DocumentoSoporte/EnviarDSNO` (o `EnviarDE` con `TipoDocumento.Codigo = "05"`). Cuerpo `MetaDatosDE`, con los campos principales: `TipoDocumento`, `Numero`, `FechaEmision`, `FechaVencimiento`, `Periodo`, `ExtensionDian` (rango de numeración), `Adquiriente`, `Proveedor` (o `Emisor` del no obligado), `DetallePago`, `Totales`, `Items`.

Reglas verificadas en el servicio:

- El **adquiriente** debe ser **su propia empresa**: mismo NIT (`Adquiriente.Identificacion`) y misma razón social (sin distinguir mayúsculas ni espacios en los extremos). Si no: 400 `Este servicio no puede enviar Documentos Soporte para el adquiriente ...`.
- Si `Proveedor.Identificacion` viene vacío, el servicio construye el proveedor a partir del `Emisor` (tipo de identificación `31`).
- El servicio completa los datos del software DIAN, calcula el **CUDS**, firma y envía. Respuesta `ResponseDian` con el CUDS en `UUID`.

## Tiquete POS electrónico (`20`)

`POST /api/DocumentoElectronico/EnviarDE`, `TipoDocumento.Codigo = "20"`.

- `Emisor.Identificacion` debe ser el NIT de su cliente.
- `ExtensionDian.RangoNumeracion` es obligatorio (la resolución de numeración).
- `Adquiriente.CodigoCliente` y `Adquiriente.NombreComercial` son obligatorios **en producción** (reglas DEPD16 y DEPD18 de la DIAN): faltando, 400. En **habilitación** el servicio los completa con la identificación y la razón social.
- Idempotente: reenviar uno ya autorizado devuelve su resultado sin duplicar.
- En habilitación, si el software del cliente tiene `TestSetId`, el envío va al **set de pruebas** y el servicio consulta su resultado tras unos segundos (5 s por defecto).

## Nota de ajuste crédito del POS (`94`)

`POST /api/DocumentoElectronico/EnviarDE`, `TipoDocumento.Codigo = "94"`. Se valida antes de enviar (400 con el motivo):

- `Numero`: obligatorio, solo letras, números, `-` y `_`, máximo 80 caracteres. `FechaEmision` obligatoria.
- `ConceptoAjuste.Codigo` de `1` a `5` y `ConceptoAjuste.Descripcion` obligatoria.
- `DocumentoReferencia`: tipo `20` (el tiquete original), `Numero`, `Fecha` (no posterior a la nota), `AlgoritmoCufe = CUDE-SHA384` y `UUID` = el **CUDE del tiquete original** (96 hexadecimales).
- `Emisor` completo (identificación, razón social, tipo de identificación, dirección fiscal, datos comerciales, responsabilidad tributaria); su NIT debe ser el de su cliente.
- `Adquiriente`: identificación obligatoria; si no es el consumidor final (`222222222222`), también tipo de identificación, dirección comercial, responsabilidad tributaria y razón social o nombre comercial.
- `DetallePago` (forma contado o crédito y método); si es crédito, vencimiento no anterior a la emisión.
- Al menos una línea, con descripción, unidad de medida, cantidad positiva e importes no negativos.
- **En producción** son obligatorios `Emisor.PrefijoRangoNumeracion` y `DetallePago.Identificador` (en habilitación se completan solos).

Respuestas especiales: 409 si ya existe una nota con ese número y otros datos sin resolver; 502 si no se pudo confirmar el envío (consulte el estado antes de reintentar).

## Nómina electrónica y eventos RADIAN

Ver [referencia-api.md](referencia-api.md#eventos-radian) y [nómina](referencia-api.md#nómina-electrónica). El esquema completo de `MetaDatosNEI` está en `src/Nomina/MetaDatosNE/MetaDatosNEI.cs` y en `docs/fase3-nomina-radian.md`.

---

## No verificado

- Los ejemplos JSON siguen el contrato del código y de las pruebas, pero **no se enviaron a la DIAN** en esta revisión. Los NIT, números y claves son ficticios.
- Tributos no porcentuales (consumo por unidad, impuesto a las bolsas): el servicio los acepta por su código, pero su cálculo no se probó con integradores externos.
- Retenciones por línea en el JSON: el servicio las envía como impuestos de retención de la línea y en el resumen; su aceptación depende de las reglas vigentes de la DIAN.
- La lista exacta de códigos de tributo, de medio de pago y de tipo de documento es la del anexo técnico de la DIAN vigente; el servicio solo valida los de tributo (código desconocido = 400).
