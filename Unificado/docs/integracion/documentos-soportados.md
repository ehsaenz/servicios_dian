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

## Campos que el código exige y esta guía no decía

Añadido el 2026-11-21 tras auditar el contrato bloque por bloque. Cada fila la verificó un revisor que intentó refutarla leyendo el código.
Lo que sigue **no es opcional**: si el ERP no lo envía, la API responde 400 o el documento sale mal.

### Tiquete POS electrónico (20)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `Items[].BaseGeneral` | Un ERP que envíe Cantidad y PrecioUnitario (lo natural) deja BaseGeneral en 0: no hay 400, el tiquete se firma y se transmite con LineExtensionAmount 0, PayableAmount 0 y cada línea... | Sí |
| `Items[] (Descripcion / Cantidad / PrecioUnitario / Impuestos[].Tipo)` | El ERP que reutiliza la única forma documentada manda Detalle/Cant/ValorUnit: se ignoran en silencio (línea con Descripcion null, Cantidad 0, PrecioUnitario 0) y, como el impuesto llega... | Sí |
| `Items[].Impuestos[].Porcentaje` | Si el ERP sigue la doc y manda 0.19, el XML sale con cbc:Percent 0.19 mientras el Importe corresponde al 19%: rechazo DIAN por inconsistencia tarifa/valor (no hay 400 previo). | Sí |
| `Totales (Totales.TotalPagable)` | Omitir Totales (razonable, porque el servicio recalcula) produce NullReferenceException antes de firmar: 400 'Ocurrió un error inesperado el documento no se envió: Object reference not... | Sí |
| `DetallePago (Forma, Metodo)` | Sin DetallePago (o sin DetallePago.Metodo) el envío falla con NullReferenceException: 400 'Ocurrió un error inesperado el documento no se envió: ...', sin decir qué campo falta. | Sí |
| `Emisor.DatosComerciales, Emisor.DireccionFiscal` | Un ERP que arme el Emisor según la sección del 20 recibe 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set...' en el primer envío. | Sí |
| `Adquiriente.DireccionComercial, Adquiriente.TipoIdentificacion` | El caso típico del POS (consumidor final con solo identificación y nombre) produce NullReferenceException: 400 'Ocurrió un error inesperado el documento no se envió: ...'. | Sí |
| `ResponseDian.statusCode (y StrQR)` | El ERP que evalúe statusCode=='00' da por fallido todo tiquete aceptado en habilitación ('2' no figura en la tabla). El propio servicio tiene el mismo sesgo:... | Sí |
| `EnviarDEJson.JsonDE` | El ERP que siga la guía manda el JSON en claro, Convert.FromBase64String lanza y el servicio responde 400 'Ocurrió un error desserializando el ojeto', sin pista de que faltaba codificar... | Sí |
| `Items[].Impuestos[].Tipo.Codigo` | Un código de tributo fuera del catálogo (Tipos.cs:420-444) no produce 400: se firma y se transmite, y la DIAN lo rechaza con una regla críptica. Además, como Tipo.Nombre también se copia... | No |
| `ResponseDian.TrackID` | ConsultarDE con ese TrackID entra por la rama de lote (TransporteDocumentoEquivalente.cs:114) y llama ConsultarDocumentos (GetStatusZip) con un CUDE; al no venir resultado se lanza... | No |
| `InformacionCaja (PlacaCaja, UbicacionCaja, NombreCajero, TipoCaja)` | El ERP no tiene cómo saber que debe enviarlos: el tiquete sale con <Value></Value> vacíos en PlacaCaja, Cajero y UbicaciónCaja de la extensión PuntoVenta (solo TipoCaja se rellena con... | No |

### Nota de ajuste POS (94)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `Totales (ValorBruto, BaseImponible, ValorBrutoConImpuestos, TotalPagable)` | HTTP 400 "Totales es obligatorio." para todo ERP que arme la nota con lo que dice la doc. Si lo envía, hay un segundo 400 ("Los totales de la nota no coinciden..."): BaseImponible debe... | Sí |
| `Items[].BaseGeneral` | HTTP 400 "BaseGeneral debe corresponder a cantidad por precio...". Un ERP que siga la doc del 94 ("al menos una línea, con descripción, unidad de medida, cantidad positiva e importes no... | Sí |
| `Totales.Descuentos, Totales.Cargos, Totales.Anticipo` | HTTP 400 en cualquier nota de ajuste que venga de una venta POS con descuento global (caso muy común: descuento por total de la compra, no por línea). El ERP tiene que redistribuir el... | Sí |
| `Emisor.RazonSocial vs Emisor.DatosComerciales.NombreComercial` | Un ERP que siga documentos-soportados.md:229 ("Emisor completo (identificación, razón social, ..., datos comerciales...)") y mande RazonSocial con un DatosComerciales sin NombreComercial... | Sí |
| `Adquiriente.RazonSocial / Adquiriente.NombreComercial` | HTTP 400 "Faltan datos del adquiriente o su dirección comercial." aunque documentos-soportados.md:230 dice que basta "razón social o nombre comercial". Es el caso normal de un ERP que... | Sí |
| `Adquiriente.NombreComercial` | Un adquiriente identificado solo con RazonSocial (permitido por documentos-soportados.md:230) pasa la validación y produce <cac:PartyName><cbc:Name/></cac:PartyName> vacío. La propia doc... | Sí |
| `DetallePago.Forma (y cualquier enum o número citado) en EnviarDEJson` | El mismo cuerpo que funciona en EnviarDE devuelve HTTP 400 "Ocurrió un error desserializando el ojeto" en EnviarDEJson si trae "Forma": "CONTADO" (o "Contado"), si cita números... | Sí |
| `JsonDE` | El ERP que siga la guía manda el JSON en claro, Convert.FromBase64String lanza FormatException y recibe HTTP 400 "Ocurrió un error desserializando el ojeto", un mensaje que apunta al... | Sí |
| `Items[].Impuestos[] con Tipo 05, 06 o 07` | HTTP 400 para cualquier nota cuyo tiquete original llevara retención. La lista de validaciones del 94 (documentos-soportados.md:226-233) no menciona la restricción, mientras que el... | Sí |
| `Código HTTP de EnviarDE con TipoDocumento 94` | referencia-api.md:259 y documentos-soportados.md:235 dicen que el 409 del 94 significa "ya existe una nota con ese número y otros datos, sin resolver". Un ERP que automatice ese 409... | No |

### Documento soporte (05)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `Items[].PeriodoFacturacionLine` | documentos-soportados.md:204 solo nombra 'Items' y nunca menciona un periodo por línea. Un ERP que mande líneas con Cantidad/PrecioUnitario/Descripcion/Impuestos provoca... | Sí |
| `Adquiriente.TipoIdentificacion` | documentos-soportados.md:206-210 enumera las 'Reglas verificadas en el servicio' del 05 y del adquiriente solo exige mismo NIT y misma razón social; referencia-api.md:229 repite lo... | Sí |
| `DetallePago.Metodo` | documentos-soportados.md:204 solo nombra 'DetallePago' entre los campos principales del 05, sin detallar sus miembros; el único sitio donde la doc exige el método es la nota de ajuste 94... | Sí |
| `ResponseDian.TrackID` | referencia-api.md:330 dice del campo TrackID: 'En habilitación, identificador del envío (ZipKey); en producción, el UUID. Úselo para consultar.', y referencia-api.md:324 declara esa... | Sí |
| `track_id de POST /api/DocumentoSoporte/ConsultarDSNO` | referencia-api.md:238 instruye lo contrario: 'En habilitación es el ZipKey del envío; en producción, el UUID (CUDS) del documento'. En habilitación el ERP no tiene ningún ZipKey que... | Sí |
| `JsonDE (cuerpo MetaDatosDE del 05 por EnviarDEJson)` | referencia-api.md:266 dice 'Igual que EnviarDE, pero el MetaDatosDE viaja en un parámetro JsonDE codificado en Base64' y guia-para-quantyerp.md:159 'EnviarDEJson recibe el mismo... | Sí |
| `Periodo.FormaGT` | documentos-soportados.md:204 incluye 'Periodo' entre los campos del cuerpo del 05 y nada dice de que su forma de generación se descarte. Un ERP que agrupe compras de la semana y mande... | No |

### Nota de ajuste de documento soporte (95)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `Proveedor.DireccionFisica (el ERP escribiría Proveedor.DireccionFiscal)` | Un ERP que mande `Proveedor: { "Identificacion": ..., "DireccionFiscal": {...} }` (el nombre que usan Emisor y Adquiriente) pierde la dirección en silencio, DireccionFisica queda null y... | Sí |
| `Adquiriente.TipoIdentificacion` | El ERP que arma el adquiriente con lo documentado (Identificacion + RazonSocial) recibe HTTP 400 `Ocurrió un error inesperado el documento no se envió: Object reference not set to an... | Sí |
| `DocumentoReferencia.AlgoritmoCufe.Codigo y DocumentoReferencia.UUID` | El ERP que copia el contrato del 94 (AlgoritmoCufe = CUDE-SHA384) o que omite AlgoritmoCufe —porque la doc nunca lo nombra para el 95— recibe HTTP 400 `La referencia requiere el CUDS del... | Sí |
| `DocumentoReferencia.TipoDocumento.Codigo` | Enviar la referencia sin TipoDocumento, o con el tipo de la propia nota (95), da HTTP 400 `DocumentoReferencia debe identificar el documento soporte original (05).` Quien reutilice... | Sí |
| `ConceptoAjuste.Codigo` | Un ERP que arme la nota con lo documentado (referencia al 05 + líneas) y sin ConceptoAjuste recibe HTTP 400 `ConceptoAjuste.Codigo debe estar entre 1 y 5 (concepto de la nota de ajuste... | Sí |
| `Adquiriente.RazonSocial` | El mismo adquiriente que el servicio rechaza con 400 en un 05 pasa sin aviso en un 95, y la nota viaja a la DIAN con un RegistrationName distinto al del documento soporte que corrige.... | No |
| `TipoDocumento.Codigo = "95" en POST /api/DocumentoSoporte/EnviarDSNO` | El equipo que integre leyendo los dos documentos de referencia no encuentra el contrato del 95: ni los campos obligatorios (ConceptoAjuste, DocumentoReferencia con tipo 05 y... | No |

### Nómina electrónica (102 individual y 103 ajuste)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `DocumentoReferencia (Numero, Cune, FechaGeneracion)` | Es exactamente la familia del bug de DocumentoRef en facturación. El integrador que lee el mensaje de error agrega un objeto `Referencia` (o, viniendo de notas 91/92, un `DocumentoRef`),... | Sí |
| `CuentaBanco` | Un ERP que paga en efectivo (MetodoPago distinto de "42") envía correctamente el 102 sin CuentaBanco y, al intentar el 103 que lo reemplaza o elimina, recibe HTTP 400 con statusCode "99"... | Sí |
| `InformacionDevengados.* y OtrosIngresos en el 103` | set-de-pruebas-produccion.md:14 y guia-para-quantyerp.md:136 anuncian el bloque de nómina como "(102, 103)" con "el mismo cuerpo JSON habitual", pero el 103 que reemplaza a un 102 con... | Sí |
| `Notas` | referencia-api.md:316 remite al integrador a MetaDatosNEI.cs como esquema, y documentos-soportados.md:43 documenta `Notas` como lista de texto en el otro bloque, así que el ERP lo... | No |
| `HoraExtraLab.Pago / Deducciones (Salud, FondoPension)` | El ERP que ya liquidó su nómina (libranzas, retefuente, fondo de solidaridad, salud o pensión distintas del 4%, jornada mensual distinta de 240 horas pese a existir... | No |

### Eventos RADIAN (030 a 033)

| Campo | Qué pasa si falta o va distinto | Bloquea |
|---|---|---|
| `Codigo = "034" / POST /api/v1/Eventos/AceptacionTacita` | El ERP que implemente la aceptación tácita según la documentación recibe siempre HTTP 400 con statusCode 109. Además 109 no figura en la tabla de códigos 400 de referencia-api.md:302... | Sí |
| `ResponseDian.UUID (y Uuid de set-radian.json) en 030-033` | El ERP que guarde response.UUID como CUDE del evento (lo que dice la documentación) guarda el CUFE de la factura, y además lo guarda repetido para el acuse, el recibo y la aceptación de... | No |
| `ResponseDian.TrackID en 030-033` | El ERP que reserve una columna TrackID para los eventos (y que programe una consulta posterior como en POS o nómina) recibe siempre cadena vacía; para eventos RADIAN tampoco existe una... | No |

El detalle completo, con archivo y línea de cada caso, está en [hallazgos-auditoria-contrato.md](hallazgos-auditoria-contrato.md).
