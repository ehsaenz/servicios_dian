# Hallazgos de la auditoría de contrato (POS, documento soporte, nómina y RADIAN)

Fecha: 2026-11-21. Alcance: el contrato JSON que exige el código frente a lo que dice `docs/integracion`.

Método: seis auditores independientes, uno por bloque, y un verificador adversarial por hallazgo que intentaba refutarlo leyendo el código. De 60 hallazgos, 57 sobrevivieron a la refutación y 3 se descartaron.

**Qué NO cubre:** facturación electrónica (01, 91, 92), que ya se había auditado aparte. Ninguno de estos hallazgos bloquea la primera prueba de factura contra la DIAN.

**Cómo leerlo:** la mayoría son huecos de documentación, no fallos de la API. Rompen a un ERP que integre siguiendo la documentación, no al servicio en sí. Los que sí son defectos de código están marcados abajo en su bloque.

## Resumen

| Bloque | Hallazgos | Bloqueantes |
|---|---:|---:|
| Tiquete POS electrónico (20) | 13 | 9 |
| Nota de ajuste POS (94) | 12 | 9 |
| Documento soporte (05) | 8 | 7 |
| Nota de ajuste de documento soporte (95) | 9 | 6 |
| Nómina electrónica (102 individual y 103 ajuste) | 8 | 6 |
| Eventos RADIAN (030 a 033) | 7 | 2 |
| **Total** | **57** | **39** |

Por tipo: doc-contradice-codigo (24), obligatorio-no-documentado (16), otro (7), codigo-fuera-de-catalogo (6), nombre-json-distinto (4).

## Tiquete POS electrónico (20)

### **BLOQUEANTE** — El importe de cada línea sale de Items[].BaseGeneral, que el servicio nunca calcula y la documentación nunca menciona

- **Campo:** `Items[].BaseGeneral`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:204`
- **Consecuencia:** Un ERP que envíe Cantidad y PrecioUnitario (lo natural) deja BaseGeneral en 0: no hay 400, el tiquete se firma y se transmite con LineExtensionAmount 0, PayableAmount 0 y cada línea marcada FreeOfChargeIndicator=true (artículo gratis). Documento legalmente errado enviado a la DIAN sin aviso.
- **Evidencia:** var totalValorUnit = tpe.Items.Sum(item => item.BaseGeneral).RedondearDian();  // y GeneradorDocumentoEquivalente.cs:1717 'Value = linea.BaseGeneral.RedondearDian()' y :1719 'FreeOfChargeIndicator = ... (linea.BaseGeneral <= 0M)'. El cálculo Cantidad*PrecioUnitario solo existe en el helper en memoria MetaDatosDE.cs:450 ('lineaX.BaseGeneral = (cantidad * valor_unitario) + total_cargos - total_dtos;'), que NO corre en el camino JSON. La sección 'Tiquete POS electrónico (20)' de documentos-soportados.md:212-220 no nombra Items ni BaseGeneral.
- **Corrección propuesta:** Dos cambios, ambos necesarios:

### **BLOQUEANTE** — Las claves de las líneas del tiquete son las de MetaDatosDE.Linea, distintas de las únicas líneas documentadas (las de /api/documento), y las claves desconocidas se ignoran en silencio

- **Campo:** `Items[] (Descripcion / Cantidad / PrecioUnitario / Impuestos[].Tipo)`
- **Tipo:** nombre-json-distinto
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:2732`
- **Consecuencia:** El ERP que reutiliza la única forma documentada manda Detalle/Cant/ValorUnit: se ignoran en silencio (línea con Descripcion null, Cantidad 0, PrecioUnitario 0) y, como el impuesto llega con ID en vez de Tipo, revienta en DocumentoEquivalenteServicio.cs:188 ('imp.Tipo.Codigo') con 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set...'.
- **Evidencia:** Linea: 'public decimal Cantidad { get; set; }' (Tipos.cs:2732), 'public string Descripcion' (:2747), 'public decimal PrecioUnitario' (:2767), 'public List<Impuesto> Impuestos' (:2799) con 'public TipoImpuesto Tipo' (:2481). La única tabla de línea en docs/integracion es la de DocumentoDian: documentos-soportados.md:110 '| `Detalle` | texto | **Sí** | Descripción del producto o servicio. |', :111 '| `Cant` | decimal | **Sí** | Cantidad. |', :112 '| `ValorUnit` | decimal ...' y :123 '| `ID` | texto | **Sí** | Código DIAN del tributo'. La sección del 20 (documentos-soportados.md:212-220) no docum
- **Corrección propuesta:** Hay que cerrar la brecha en los dos frentes:

### **BLOQUEANTE** — En el tiquete, Impuestos[].Porcentaje debe venir ya en porcentaje (19.00); la doc dice que se envía como fracción y que el servicio lo convierte

- **Campo:** `Items[].Impuestos[].Porcentaje`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:180`
- **Consecuencia:** Si el ERP sigue la doc y manda 0.19, el XML sale con cbc:Percent 0.19 mientras el Importe corresponde al 19%: rechazo DIAN por inconsistencia tarifa/valor (no hay 400 previo).
- **Evidencia:** 'imp.Porcentaje = imp.Porcentaje.RedondearDian();' (solo redondea, no multiplica) y el generador lo copia tal cual: GeneradorDocumentoEquivalente.cs:1905 'Percent = (impuesto.PorUnidad > 0M) ? null : new PercentType1 { Value = impuesto.Porcentaje.RedondearDian() }'. La conversión x100 solo existe en el helper en memoria MetaDatosDE.cs:463 ('Porcentaje = (tarifa_iva * 100).RedondearDian()') y en el traductor de facturación. documentos-soportados.md:125: '| `Porcentaje` | decimal | **Sí** | Tarifa como **fracción** (`0.19`); el servicio la convierte a porcentaje (`19.00`). |'
- **Corrección propuesta:** Corregir la documentación (es la que está equivocada, el código es consistente en todas las rutas): en C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/documentos-soportados.md:125 cambiar '| `Porcentaje` | decimal | **Sí** | Tarifa como **fracción** (`0.19`); el servicio la convierte a porcentaje (`19.00`). |' por 'Tarifa ya en **porcentaje** (`19.00`), no en fracción; el servicio solo la redondea a 2 decimales.' Ajustar también la línea 134 ('Impuesto por línea: redond

### **BLOQUEANTE** — Totales es obligatorio aunque el servicio recalcula los totales por su cuenta, y la doc del 20 no lo exige

- **Campo:** `Totales (Totales.TotalPagable)`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:231`
- **Consecuencia:** Omitir Totales (razonable, porque el servicio recalcula) produce NullReferenceException antes de firmar: 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object.'
- **Evidencia:** 'PuntosAcumulados = Math.Round(tpe.Totales.TotalPagable / 1000, 0).ToString("N0")' y :243 'SubTotal = tpe.Totales.TotalPagable'. MetaDatosDE.cs:54 'public Totales Totales { get; set; }' no tiene inicializador (null si se omite), mientras que los totales que van al UBL se construyen internamente en :209-216. La sección del 20 (documentos-soportados.md:212-220) solo exige Emisor.Identificacion, ExtensionDian.RangoNumeracion y Adquiriente.CodigoCliente/NombreComercial.
- **Corrección propuesta:** Dos opciones, hay que hacer una: (a) en DocumentoEquivalenteServicio.cs, usar los totales ya calculados internamente en lugar del request — cambiar :231 a `Math.Round(totales.TotalPagable / 1000, 0).ToString("N0")` y :243 a `SubTotal = totales.TotalPagable`, moviendo el bloque extBeneficios/extCaja después de :216 (ya lo está). Así Totales deja de ser obligatorio de verdad y queda coherente con que el servicio recalcula. O (b) si se quiere exigirlo, añadir a ValidarDatos una comprobación explíci

### **BLOQUEANTE** — DetallePago es obligatorio para el tiquete y la doc del 20 no lo menciona

- **Campo:** `DetallePago (Forma, Metodo)`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:270`
- **Consecuencia:** Sin DetallePago (o sin DetallePago.Metodo) el envío falla con NullReferenceException: 400 'Ocurrió un error inesperado el documento no se envió: ...', sin decir qué campo falta.
- **Evidencia:** 'generador.ConDetallePago(tpe.DetallePago);' y en el generador GeneradorDocumentoEquivalente.cs:1005 'ID = new IDType { Value = ((int)pago.Forma).ToString() }' con ':1006 PaymentMeansCode = ... pago.Metodo.Codigo'. MetaDatosDE.cs:52 'public DetallePago DetallePago { get; set; }' sin inicializador. documentos-soportados.md lo exige explícitamente para la nota 94 (:234) pero no en la sección del 20 (:212-220).
- **Corrección propuesta:** Dos cambios:

### **BLOQUEANTE** — El tiquete exige Emisor.DatosComerciales y Emisor.DireccionFiscal, pero la doc del 20 solo exige Emisor.Identificacion

- **Campo:** `Emisor.DatosComerciales, Emisor.DireccionFiscal`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoEquivalente.cs:356`
- **Consecuencia:** Un ERP que arme el Emisor según la sección del 20 recibe 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set...' en el primer envío.
- **Evidencia:** 'Name = new NameType1 { Value = emisor.DatosComerciales.NombreComercial }' (también :394, :443 y :455 con MatriculaMercantil) y ':360-381' usa emisor.DireccionFiscal.Municipio/Departamento/Pais sin comprobar null. Tipos.cs:2001 'public DireccionFisica DireccionFiscal { get; set; }' y :2017 'public DatosComerciales DatosComerciales { get; set; }' no tienen inicializador. documentos-soportados.md:216 para el 20 solo dice '- `Emisor.Identificacion` debe ser el NIT de su cliente.' (la lista de 'Emisor completo' está solo en la sección de la nota 94, :235).
- **Corrección propuesta:** Dos cosas, cualquiera de ellas cierra el hallazgo, pero conviene hacer ambas: (1) Documentar en docs/documentos-soportados.md, en la sección "Tiquete POS electrónico (`20`)", que el `Emisor` debe venir completo igual que en el 94 — identificación, razón social, tipo de identificación, `DireccionFiscal` (con `Pais`, y Municipio/Departamento recomendados) y `DatosComerciales` (`NombreComercial` y `MatriculaMercantil`) — o bien enlazar explícitamente a la lista de la sección del 94. (2) En Document

### **BLOQUEANTE** — El tiquete exige Adquiriente.DireccionComercial y Adquiriente.TipoIdentificacion, no documentados para el 20

- **Campo:** `Adquiriente.DireccionComercial, Adquiriente.TipoIdentificacion`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoEquivalente.cs:515`
- **Consecuencia:** El caso típico del POS (consumidor final con solo identificación y nombre) produce NullReferenceException: 400 'Ocurrió un error inesperado el documento no se envió: ...'.
- **Evidencia:** 'ID = (adquiriente.DireccionComercial.Municipio != null) ?' (desreferencia DireccionComercial sin null-check; igual en :517, :519, :525, :529) y :547 'schemeName = adquiriente.TipoIdentificacion.Codigo' (también :498 y :572). Tipos.cs:2110 'public TipoIdentificacion TipoIdentificacion { get; set; }' y :2116 'public DireccionFisica DireccionComercial { get; set; }' sin inicializador. documentos-soportados.md:218 para el 20 solo exige '`Adquiriente.CodigoCliente` y `Adquiriente.NombreComercial` ... en producción'.
- **Corrección propuesta:** Dos cambios, y conviene hacer ambos: (1) Documentación: en Unificado/docs/integracion/documentos-soportados.md, sección "Tiquete POS electrónico (20)", añadir que Adquiriente.TipoIdentificacion y Adquiriente.DireccionComercial (incluido DireccionComercial.Pais, que se desreferencia siempre) son obligatorios, igual que ya se documenta para el 94. (2) Código: en Pos.Servicios/DocumentoEquivalenteServicio.cs, antes de .ConAdquiriente(tpe.Adquiriente), validar esos campos y devolver 400 con un mensa

### **BLOQUEANTE** — En habilitación con TestSetId el tiquete devuelve el código crudo de la DIAN ('2'), no 00/99/SPxx como dice la tabla de ResponseDian

- **Campo:** `ResponseDian.statusCode (y StrQR)`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/TransporteDocumentoEquivalente.cs:88`
- **Consecuencia:** El ERP que evalúe statusCode=='00' da por fallido todo tiquete aceptado en habilitación ('2' no figura en la tabla). El propio servicio tiene el mismo sesgo: DocumentoEquivalenteServicio.cs:107 'if (respuesta.statusCode == "00") respuesta.StrQR = textoQr;', así que en habilitación nunca devuelve el texto del QR aunque la doc lo documente sin salvedades.
- **Evidencia:** 'resp.statusCode = dian.StatusCode;' en la rama de set de pruebas (:75 'if (_habilitacion && !string.IsNullOrEmpty(_software.TestSetId))'). Que ese código es '2' lo confirma el propio archivo en :156: 'if (dian.StatusCode == "2" && mensaje.Contains("se encuentra Aceptado", ...))' y DocumentoServicio.cs:36 ('codigo?.Trim() == "2" ... ? "aceptado"'). 'SPxx' solo lo genera nómina (NominaServicio.cs:328). referencia-api.md:328: '| `statusCode` | texto | `00` procesado correctamente (autorizado); `99` validación con errores (rechazo); `SPxx` envío al set de pruebas; ...' y documentos-soportados.md:
- **Corrección propuesta:** Dos opciones, hay que elegir una y dejarla coherente: (a) Normalizar en el transporte: en TransporteDocumentoEquivalente.Enviar (:88) y Consultar (:118), mapear el código del set de pruebas al contrato documentado antes de asignarlo (p. ej. "2" + mensaje "se encuentra Aceptado" -> "00", o un "SPxx" como hace nómina en NominaServicio.cs:328), conservando el crudo en statusMessage/Errors; y ajustar DocumentoEquivalenteServicio.cs:107 para que StrQR se asigne con ese código normalizado. (b) Si se p

### **BLOQUEANTE** — La guía de QuantyERP dice que EnviarDEJson recibe el JSON como texto; el controlador exige Base64

- **Campo:** `EnviarDEJson.JsonDE`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs:102`
- **Consecuencia:** El ERP que siga la guía manda el JSON en claro, Convert.FromBase64String lanza y el servicio responde 400 'Ocurrió un error desserializando el ojeto', sin pista de que faltaba codificar en Base64.
- **Evidencia:** 'var original = Encoding.UTF8.GetString(Convert.FromBase64String(JsonDE));'. docs/integracion/guia-para-quantyerp.md:159: '- `EnviarDEJson` recibe el mismo contenido como texto JSON.' (referencia-api.md:266 sí dice Base64, así que las dos guías se contradicen entre sí).
- **Corrección propuesta:** En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/guia-para-quantyerp.md línea 159, reemplazar "- `EnviarDEJson` recibe el mismo contenido como texto JSON." por algo como: "- `EnviarDEJson` recibe el mismo `MetaDatosDE` en el parámetro `JsonDE`, serializado a JSON y luego codificado en Base64 de UTF-8 (`Convert.ToBase64String(Encoding.UTF8.GetBytes(json))`). Si no viene en Base64 la API responde 400 `Ocurrió un error desserializando el ojeto`." Opcionalmente, mejorar t

### Aviso — El tiquete no valida el código de tributo contra el catálogo, aunque la doc promete 400 para un código desconocido

- **Campo:** `Items[].Impuestos[].Tipo.Codigo`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoEquivalente.cs:1908`
- **Consecuencia:** Un código de tributo fuera del catálogo (Tipos.cs:420-444) no produce 400: se firma y se transmite, y la DIAN lo rechaza con una regla críptica. Además, como Tipo.Nombre también se copia tal cual, si el ERP manda solo {Codigo} el XML sale con cbc:Name vacío.
- **Evidencia:** 'ID = new IDType { Value = impuesto.Tipo.Codigo }' y ':1909 Name = new NameType1 { Value = impuesto.Tipo.Nombre }': el valor del ERP va al XML sin pasar por TipoImpuesto.GetFromCodigo (Tipos.cs:447). Un grep de TipoImpuesto.GetFromCodigo en src/ solo lo encuentra en Shared/Traductor/TraductorFactura.cs:278, 302 y 311 (bloque de facturación), nunca en el camino POS. documentos-soportados.md:248: '... el servicio solo valida los de tributo (código desconocido = 400).'
- **Corrección propuesta:** En el camino POS (src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs, en el prólogo de `Firmar`, antes de construir `resumenImpuestos`, y de forma análoga en NotaAjustePos.cs junto a las validaciones de la línea 97), normalizar cada impuesto contra el catálogo: `imp.Tipo = FacturacionColombia_Metadatos.TipoImpuesto.GetFromCodigo(imp.Tipo?.Codigo)` dentro de un try/catch que traduzca la ArgumentException a un 400 con mensaje explícito (p. ej. "Código de tributo desconocido: {codigo}"). Esto re

### Aviso — El TrackID que devuelve el tiquete en habilitación es el CUDE, no el ZipKey que la doc manda usar para consultar

- **Campo:** `ResponseDian.TrackID`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/TransporteDocumentoEquivalente.cs:86`
- **Consecuencia:** ConsultarDE con ese TrackID entra por la rama de lote (TransporteDocumentoEquivalente.cs:114) y llama ConsultarDocumentos (GetStatusZip) con un CUDE; al no venir resultado se lanza InvalidOperationException ':117 La DIAN no devolvió información para ese identificador.' y el controlador responde 400.
- **Evidencia:** 'resp.TrackID = dian.XmlDocumentKey ?? subida.ZipKey;' seguido de ':87 resp.UUID = dian.XmlDocumentKey ?? string.Empty;' (cuando la DIAN devuelve XmlDocumentKey, que es el caso normal, TrackID y UUID son el mismo CUDE). referencia-api.md:330: '| `TrackID` | texto | En habilitación, identificador del envío (ZipKey); en producción, el UUID. Úselo para consultar. |'
- **Corrección propuesta:** Opción preferida (coherente con la doc): en la rama del set pruebas, devolver siempre el ZipKey como identificador de consulta y dejar el CUDE solo en UUID, es decir cambiar TransporteDocumentoEquivalente.cs:86 a `resp.TrackID = subida.ZipKey;` manteniendo :87 `resp.UUID = dian.XmlDocumentKey ?? string.Empty;`. Alternativa si se quiere conservar el CUDE en TrackID: hacer Consultar tolerante — en la rama de lote (línea 114-117), si ConsultarDocumentos no devuelve resultado, reintentar con `_envia

### Aviso — InformacionCaja alimenta la extensión DIAN del punto de venta y no está documentada en ninguna parte

- **Campo:** `InformacionCaja (PlacaCaja, UbicacionCaja, NombreCajero, TipoCaja)`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:238`
- **Consecuencia:** El ERP no tiene cómo saber que debe enviarlos: el tiquete sale con <Value></Value> vacíos en PlacaCaja, Cajero y UbicaciónCaja de la extensión PuntoVenta (solo TipoCaja se rellena con 'Fija' por defecto, :234).
- **Evidencia:** 'PlacaCaja = tpe.InformacionCaja.PlacaCaja,' (:239-242 NombreCajero, UbicacionCaja, TipoCaja) que se vuelcan en Extensiones/ExtensionCajaVenta.cs:33-41 ('<Name>PlacaCaja</Name> <Value>{informacion.PlacaCaja}</Value> ...'). Un grep de InformacionCaja/PlacaCaja/NombreCajero sobre todo docs/*.md no devuelve ninguna coincidencia (tampoco en docs/fase2-diseno-api-pos-dsno.md, citado como esquema en referencia-api.md:354).
- **Corrección propuesta:** Documentar el objeto InformacionCaja como parte del contrato del tiquete POS electrónico en Unificado/docs/integracion/referencia-api.md (y en el esquema de docs/fase2-diseno-api-pos-dsno.md que esa referencia cita): indicar que el payload del documento equivalente POS acepta InformacionCaja con los cuatro campos string PlacaCaja, UbicacionCaja, NombreCajero y TipoCaja; que alimentan la extensión DIAN PuntoVenta/InformacionCajaVenta (nombres XML: PlacaCaja, UbicaciónCaja, Cajero, TipoCaja); que 

### Aviso — Los impuestos de una línea marcada Excluido entran en los totales pero no en el resumen de impuestos del documento

- **Campo:** `Items[].Excluido / Items[].Impuestos[]`
- **Tipo:** otro
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoEquivalenteServicio.cs:187`
- **Consecuencia:** Caso corriente del POS (bolsa plástica: línea excluida de IVA con INC Bolsas código 22, como arma el propio helper MetaDatosDE.cs:469-485): el TaxInclusiveAmount y el PayableAmount incluyen ese impuesto pero el TaxTotal del documento no, y la DIAN rechaza por totales inconsistentes.
- **Evidencia:** ':187 var impuestos = tpe.Items.Where(x => !x.Excluido).SelectMany(x => x.Impuestos).ToList();' (de aquí sale resumenImpuestos y por tanto el TaxTotal del documento) frente a ':206 var totalImpuestos = tpe.Items.SelectMany(item => item.Impuestos).Sum(impuesto => impuesto.Importe).RedondearDian();' (sin filtrar), que alimenta ':214 ValorBrutoConImpuestos' y ':215 TotalPagable'. A nivel de línea el impuesto sí se emite (GeneradorDocumentoEquivalente.cs:1905-1909 para cualquier línea con Impuestos).
- **Corrección propuesta:** Unificar el criterio en DocumentoEquivalenteServicio.Firmar. O bien (a) los tres cálculos usan la misma colección: reutilizar la lista `impuestos` ya filtrada en :187 también en :205 y :206 (`totalBaseImponible = impuestos.Sum(i => i.BaseImponible)`, `totalImpuestos = impuestos.Sum(i => i.Importe)`), de modo que TaxTotal, TaxInclusiveAmount y PayableAmount cuadren; o bien (b) si el ICBP de una línea excluida de IVA debe ir en el TaxTotal (que es lo correcto ante la DIAN: la línea está excluida d

## Nota de ajuste POS (94)

### **BLOQUEANTE** — Totales es obligatorio y debe cuadrar con las líneas, pero la lista de validaciones del 94 no lo menciona

- **Campo:** `Totales (ValorBruto, BaseImponible, ValorBrutoConImpuestos, TotalPagable)`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjustePos.cs:105`
- **Consecuencia:** HTTP 400 "Totales es obligatorio." para todo ERP que arme la nota con lo que dice la doc. Si lo envía, hay un segundo 400 ("Los totales de la nota no coinciden..."): BaseImponible debe ser la suma de BaseGeneral de las líneas CON impuestos (la base de una línea se cuenta una vez aunque tenga IVA+INC, ver el comentario de :111-112), no la suma de Impuestos[].BaseImponible, que es la lectura natural y la que usa la propia muestra del tiquete 20 en tests/Shared.Pruebas/DatosPos.cs:58. Tolerancia 0.02.
- **Evidencia:** Requerir(de.Totales != null, "Totales es obligatorio."); y en :113-117 Requerir(totales.TotalPagable > 0 && Coincide(totales.ValorBruto, bruto) && Coincide(totales.BaseImponible, baseImponible) && Coincide(totales.ValorBrutoConImpuestos, bruto + impuestos) && Coincide(totales.TotalPagable, bruto + impuestos), "Los totales de la nota no coinciden con sus líneas e impuestos."). documentos-soportados.md:224-233 presenta la lista completa de validaciones del 94 y no nombra Totales ni una sola vez (solo lo nombra para el 05, en :204).
- **Corrección propuesta:** Documentar `Totales` en la sección del 94 de `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/documentos-soportados.md`, insertando entre la línea 232 ("Al menos una línea...") y la 233 ("**En producción**...") estos dos ítems:

### **BLOQUEANTE** — Cada línea debe traer BaseGeneral calculada por el ERP; el campo no aparece en ninguna doc de integración

- **Campo:** `Items[].BaseGeneral`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjustePos.cs:101`
- **Consecuencia:** HTTP 400 "BaseGeneral debe corresponder a cantidad por precio...". Un ERP que siga la doc del 94 ("al menos una línea, con descripción, unidad de medida, cantidad positiva e importes no negativos") manda Cantidad y PrecioUnitario, BaseGeneral queda en 0 por defecto y la nota se rechaza siempre. Además alimenta LineExtensionAmount y el FreeOfChargeIndicator del UBL (GeneradorNotaAjusteDocumentoEquivalente.cs:1193-1195).
- **Evidencia:** Requerir(Coincide(item.BaseGeneral, item.Cantidad * item.PrecioUnitario + item.Cargos.Sum(x => x.Monto) - item.Descuentos.Sum(x => x.Monto)), "BaseGeneral debe corresponder a cantidad por precio, más cargos y menos descuentos de la línea."). grep BaseGeneral en docs/ solo devuelve documentos-soportados.md:136, que habla de BaseGral (otro modelo, DocumentoDian, donde el servicio la calcula).
- **Corrección propuesta:** Documentar el campo en la sección "Nota de ajuste crédito del POS (94)" de C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/documentos-soportados.md (sustituir o acompañar la viñeta de la línea 232, que hoy solo dice "Al menos una línea, con descripción, unidad de medida, cantidad positiva e importes no negativos"):

### **BLOQUEANTE** — Descuentos, cargos globales y anticipos están prohibidos (400) y la doc no lo dice en ninguna parte

- **Campo:** `Totales.Descuentos, Totales.Cargos, Totales.Anticipo`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjustePos.cs:107`
- **Consecuencia:** HTTP 400 en cualquier nota de ajuste que venga de una venta POS con descuento global (caso muy común: descuento por total de la compra, no por línea). El ERP tiene que redistribuir el descuento a las líneas y recalcular BaseGeneral, y nada en documentos-soportados.md ni en referencia-api.md lo advierte.
- **Evidencia:** Requerir(totales.Descuentos == 0 && totales.Cargos == 0 && totales.Anticipo == 0, "Esta integración admite descuentos y cargos por línea; no admite descuentos/cargos globales ni anticipos."). El generador sí los soporta: GeneradorNotaAjusteDocumentoEquivalente.cs:1149-1163 emite AllowanceTotalAmount, ChargeTotalAmount y PrepaidAmount a partir de esos mismos campos.
- **Corrección propuesta:** Dos cambios, uno de documentación (obligatorio) y uno de código (recomendado).

### **BLOQUEANTE** — La razón social que exige la doc (Emisor.RazonSocial) no viaja en el XML: el nombre del emisor sale de Emisor.DatosComerciales.NombreComercial, que nadie valida

- **Campo:** `Emisor.RazonSocial vs Emisor.DatosComerciales.NombreComercial`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Shared/Documentos/GeneradorNotaAjusteDocumentoEquivalente.cs:332`
- **Consecuencia:** Un ERP que siga documentos-soportados.md:229 ("Emisor completo (identificación, razón social, ..., datos comerciales...)") y mande RazonSocial con un DatosComerciales sin NombreComercial pasa la validación (200 de la API) y se firma y transmite un CreditNote con <cbc:Name/>, <cbc:RegistrationName/> del emisor vacíos: rechazo de la DIAN con el número ya consumido, no un 400 que se pueda corregir antes de emitir.
- **Evidencia:** RegistrationName = new RegistrationNameType { Value = emisor.DatosComerciales.NombreComercial } (igual en :298 PartyName/Name y en :381 PartyLegalEntity/RegistrationName). Emisor.RazonSocial no se lee ni una vez en todo el generador (grep RazonSocial en ese archivo solo da líneas del adquiriente y del transportador). El validador exige RazonSocial en NotaAjustePos.cs:68-72 pero de DatosComerciales solo comprueba que no sea null, no que NombreComercial tenga valor; DatosComerciales.NombreComercial tiene default string.Empty (Tipos.cs:3734).
- **Corrección propuesta:** Dos cambios, ambos necesarios (el del generador arregla el XML; el del validador convierte el rechazo DIAN en un 400 corregible antes de consumir el número).

### **BLOQUEANTE** — Adquiriente: RazonSocial vacía ("") no cae al NombreComercial por el uso de ?? y la nota se rechaza con 400

- **Campo:** `Adquiriente.RazonSocial / Adquiriente.NombreComercial`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjustePos.cs:78`
- **Consecuencia:** HTTP 400 "Faltan datos del adquiriente o su dirección comercial." aunque documentos-soportados.md:230 dice que basta "razón social o nombre comercial". Es el caso normal de un ERP que serializa cadenas vacías en vez de omitir el campo (QuantyERP serializa el tercero completo). El mismo patrón ?? se repite en el XML (GeneradorNotaAjusteDocumentoEquivalente.cs:476 y :501).
- **Evidencia:** !string.IsNullOrWhiteSpace(de.Adquiriente.RazonSocial ?? de.Adquiriente.NombreComercial) — el operador ?? solo cae al segundo valor si RazonSocial es null; con RazonSocial = "" (cadena vacía) la expresión vale "" y IsNullOrWhiteSpace da true, aunque NombreComercial venga lleno.
- **Corrección propuesta:** 1) C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Pos/Pos.Servicios/NotaAjustePos.cs:78 — reemplazar el `??` por una comprobación que trate la cadena vacía/blanca como ausente: `(!string.IsNullOrWhiteSpace(de.Adquiriente.RazonSocial) || !string.IsNullOrWhiteSpace(de.Adquiriente.NombreComercial))`. 2) C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Documentos/GeneradorNotaAjusteDocumentoEquivalente.cs:476 y :501 — sustituir `adquiriente.RazonSocial ?? adquiriente

### **BLOQUEANTE** — El nombre del adquiriente en PartyName sale solo de NombreComercial, sin respaldo en RazonSocial

- **Campo:** `Adquiriente.NombreComercial`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Shared/Documentos/GeneradorNotaAjusteDocumentoEquivalente.cs:442`
- **Consecuencia:** Un adquiriente identificado solo con RazonSocial (permitido por documentos-soportados.md:230) pasa la validación y produce <cac:PartyName><cbc:Name/></cac:PartyName> vacío. La propia doc recuerda en :218 que la DIAN exige nombre del comprador en el bloque POS (DEPD18): rechazo de la DIAN con el consecutivo ya gastado.
- **Evidencia:** Name = new NameType1 { Value = adquiriente.NombreComercial } — sin el "?? RazonSocial" que sí usan las líneas :476 y :501 del mismo método ConAdquiriente.
- **Corrección propuesta:** En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Documentos/GeneradorNotaAjusteDocumentoEquivalente.cs línea 442, cambiar `Name = new NameType1 { Value = adquiriente.NombreComercial }` por `Name = new NameType1 { Value = adquiriente.NombreComercial ?? adquiriente.RazonSocial }`, para que PartyName use el mismo respaldo que ya usan :476 y :501 (allí el orden es `RazonSocial ?? NombreComercial`; en PartyName corresponde priorizar el nombre comercial y caer a la razón social).

### **BLOQUEANTE** — EnviarDEJson deserializa con opciones JSON distintas a EnviarDE: no acepta enumeraciones como texto ni números entre comillas

- **Campo:** `DetallePago.Forma (y cualquier enum o número citado) en EnviarDEJson`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs:103`
- **Consecuencia:** El mismo cuerpo que funciona en EnviarDE devuelve HTTP 400 "Ocurrió un error desserializando el ojeto" en EnviarDEJson si trae "Forma": "CONTADO" (o "Contado"), si cita números ("Cantidad": "1.00") o si cambia la caja de un nombre de campo. referencia-api.md:266 dice que EnviarDEJson es "Igual que EnviarDE" y :8 que la API usa "las enumeraciones como texto"; set-de-pruebas-produccion.md:12 ofrece EnviarDEJson para el set de pruebas del bloque POS.
- **Evidencia:** de = System.Text.Json.JsonSerializer.Deserialize<MetaDatosDE>(original); — sin JsonSerializerOptions, o sea JsonSerializerOptions.Default. La ruta EnviarDE sí usa las opciones del host, configuradas en src/Pos/Pos.Api/Program.cs:22-28 con JsonStringEnumConverter y sobre JsonSerializerDefaults.Web (case-insensitive y AllowReadingFromString). FormaPago es un enum (Tipos.cs:2207 public enum FormaPago { CONTADO = 1, CREDITO = 2 }).
- **Corrección propuesta:** En `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs`, `EnviarDEJson` debe deserializar con las mismas opciones que usa el pipeline de MVC, no con `JsonSerializerOptions.Default`.

### **BLOQUEANTE** — La guía de integración dice que EnviarDEJson recibe texto JSON; el código exige Base64

- **Campo:** `JsonDE`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs:102`
- **Consecuencia:** El ERP que siga la guía manda el JSON en claro, Convert.FromBase64String lanza FormatException y recibe HTTP 400 "Ocurrió un error desserializando el ojeto", un mensaje que apunta al contenido del documento y no a la codificación del parámetro.
- **Evidencia:** var original = Encoding.UTF8.GetString(Convert.FromBase64String(JsonDE)); frente a docs/integracion/guia-para-quantyerp.md:159 "`EnviarDEJson` recibe el mismo contenido como texto JSON." (referencia-api.md:266 sí dice Base64 de UTF-8, así que las dos docs se contradicen entre sí).
- **Corrección propuesta:** Dos cambios, el primero obligatorio (es el hallazgo), el segundo recomendado:

### **BLOQUEANTE** — Las retenciones por línea se rechazan con 400 y la doc del 94 no lo advierte

- **Campo:** `Items[].Impuestos[] con Tipo 05, 06 o 07`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjustePos.cs:97`
- **Consecuencia:** HTTP 400 para cualquier nota cuyo tiquete original llevara retención. La lista de validaciones del 94 (documentos-soportados.md:226-233) no menciona la restricción, mientras que el modelo de factura sí documenta retenciones por línea (documentos-soportados.md:117) — un ERP que reutilice su mapeo de factura queda bloqueado sin pista.
- **Evidencia:** Requerir(item.Impuestos.All(x => x != null && x.Tipo != null && !x.Retenido && x.Importe >= 0 && ...), "Impuestos inválidos: esta nota no admite retenciones ni importes negativos."). Impuesto.Retenido es calculado y da true para los códigos 05 ReteIVA, 06 ReteRenta y 07 ReteICA (Tipos.cs:2517-2524 contra el catálogo de Tipos.cs:425-427).
- **Corrección propuesta:** En `docs/integracion/documentos-soportados.md`, sección "## Nota de ajuste crédito del POS (`94`)" (lista de validaciones, líneas 226-233), agregar un bullet explícito junto a los demás, por ejemplo después del de "Al menos una línea...":

### Aviso — Un cliente sin el módulo POS configurado recibe 409, el código que la doc reserva para una nota duplicada

- **Campo:** `Código HTTP de EnviarDE con TipoDocumento 94`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs:67`
- **Consecuencia:** referencia-api.md:259 y documentos-soportados.md:235 dicen que el 409 del 94 significa "ya existe una nota con ese número y otros datos, sin resolver". Un ERP que automatice ese 409 cambiando el consecutivo reintentará en bucle contra un problema de configuración del cliente que nunca se va a resolver solo. Para el mismo error, la ruta de set de pruebas devuelve 400 (EnvioSetHelper.cs:24-27), así que el mismo fallo da dos códigos distintos según la cabecera X-Envio-Set.
- **Evidencia:** catch (InvalidOperationException ex) { return Conflict(ex.Message); } y NotaAjustePos.cs:169-170 lanza esa misma excepción con "Los parámetros del módulo de DOCUMENTO_EQUIVALENTE no están bien configurados" cuando ObtenerSoftware(TipoSoftware.DocumentoEquivalente) devuelve null.
- **Corrección propuesta:** Hay que separar el error de configuración del conflicto de duplicado, de modo que 409 quede reservado para lo que dice la doc.

### Aviso — El catálogo MetodoPago tiene la entrada "87" con Codigo "97": la clave y el código no coinciden

- **Campo:** `DetallePago.Metodo.Codigo (catálogo MetodoPago)`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:2234`
- **Consecuencia:** MetodoPago.GetFromCodigo("97") lanza ArgumentException "Código de MetodoPago no válido [97]" y hay que pedir el inexistente "87" para obtener el 97. El impacto en el 94 es indirecto porque el validador no consulta el catálogo (NotaAjustePos.cs:80-82 solo pide que Metodo.Codigo no esté vacío) y el valor pasa tal cual al XML (GeneradorNotaAjusteDocumentoEquivalente.cs:894), pero cualquier helper, ejemplo o prueba que use GetFromCodigo con el código real falla, y el catálogo deja de ser fuente de verdad.
- **Evidencia:** { "87", new MetodoPago { Codigo = "97", Descripcion = "ACUERDO_MUTUO" } }, — es la única entrada del diccionario cuya clave difiere del Codigo (todas las demás, :2224-2235, repiten la clave: "10"/"10", "48"/"48", "ZZZ"/"ZZZ").
- **Corrección propuesta:** En src/Shared/Metadatos/Tipos.cs, reemplazar la línea 2234 por una entrada coherente con el catálogo oficial MediosPago-2.1.gc, donde la clave y el Codigo coincidan y la descripción corresponda al código: `{ "97", new MetodoPago { Codigo = "97", Descripcion = "CLEARING_ENTRE_PARTNERS" } },`. Si lo que realmente se quería exponer era "Acuerdo mutuo", ese código es "ZZZ", que ya existe en la línea 2235; en ese caso corregir su descripción a "ACUERDO_MUTUO" (hoy dice "OTRO") y eliminar la entrada "

### Aviso — El catálogo RegimenFiscal tiene la entrada "O-99" con Codigo "No aplica", que no es un código de TaxLevelCode

- **Campo:** `Emisor.RegimenFiscal.Codigo / Adquiriente.RegimenFiscal.Codigo`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:515`
- **Consecuencia:** Quien construya el emisor con los helpers del SDK pasando "O-99" (MetaDatosDE.cs:302 RegimenFiscal.GetFromCodigo(regimen_fiscal), DatosFiscales en Tipos.cs:3652) emite <cbc:TaxLevelCode>No aplica</cbc:TaxLevelCode>: la DIAN rechaza el valor. Por JSON directo el ERP manda el código que quiera y el servicio no lo valida, así que el catálogo tampoco atrapa un código inválido.
- **Evidencia:** { "O-99", new RegimenFiscal { Codigo = "No aplica", Descripcion = "NO_APLICA", Obsoleto = true} }, — Codigo y Descripcion están intercambiados; compárese con ResponsabilidadFiscal en Tipos.cs:1951, donde O-99 sí es el Codigo. El generador copia ese valor literal al XML: GeneradorNotaAjusteDocumentoEquivalente.cs:367 TaxLevelCode { Value = emisor.RegimenFiscal?.Codigo ?? "R-99-PN" } (y :488 para el adquiriente).
- **Corrección propuesta:** En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Metadatos/Tipos.cs:515 intercambiar los dos valores para que respete el mismo contrato que el resto del diccionario:

## Documento soporte (05)

### **BLOQUEANTE** — Cada línea de Items debe traer PeriodoFacturacionLine o el envío revienta; la documentación no lo menciona

- **Campo:** `Items[].PeriodoFacturacionLine`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoSoporte.cs:1356`
- **Consecuencia:** documentos-soportados.md:204 solo nombra 'Items' y nunca menciona un periodo por línea. Un ERP que mande líneas con Cantidad/PrecioUnitario/Descripcion/Impuestos provoca NullReferenceException dentro de AgregarLineas; DocumentoSoporteController.cs:66-68 la atrapa y devuelve 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object.'. Nunca se llega a la DIAN. Las pruebas no lo detectan porque MetaDatosDE.AddItem (MetaDatosDE.cs:414) rellena el campo en proceso.
- **Evidencia:** StartDate = new StartDateType{ Value = linea.PeriodoFacturacionLine.FechaInicio },  // AgregarLineas, sin comprobación de nulo; Tipos.cs:2821 declara 'public PeriodoFacturacion PeriodoFacturacionLine { get; set; }' sin valor por defecto
- **Corrección propuesta:** Hay que hacer las tres cosas (la 1 es la que evita el 400):

### **BLOQUEANTE** — Adquiriente.TipoIdentificacion es obligatorio aunque la doc diga que del adquiriente solo importan NIT y razón social

- **Campo:** `Adquiriente.TipoIdentificacion`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoSoporte.cs:312`
- **Consecuencia:** documentos-soportados.md:206-210 enumera las 'Reglas verificadas en el servicio' del 05 y del adquiriente solo exige mismo NIT y misma razón social; referencia-api.md:229 repite lo mismo. Un ERP que mande Adquiriente con Identificacion y RazonSocial (exactamente lo documentado) pasa la validación de DocumentoSoporteServicio.cs:85-93 y luego revienta con NullReferenceException en el generador: HTTP 400 'Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object.'
- **Evidencia:** schemeName = adquiriente.TipoIdentificacion.Codigo,  // ConAdquiriente, sin comprobación; Tipos.cs:2110 'public TipoIdentificacion TipoIdentificacion { get; set; }' sin valor por defecto (a diferencia de RegimenFiscal y ResponsabilidadTributaria, que sí lo tienen en Tipos.cs:2120 y 2122)
- **Corrección propuesta:** Dos cambios, el de código es el importante porque el servicio ya tiene toda la información para no necesitar el campo.

### **BLOQUEANTE** — DetallePago.Metodo es obligatorio y no está documentado para el 05

- **Campo:** `DetallePago.Metodo`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoSoporte.cs:528`
- **Consecuencia:** documentos-soportados.md:204 solo nombra 'DetallePago' entre los campos principales del 05, sin detallar sus miembros; el único sitio donde la doc exige el método es la nota de ajuste 94 (documentos-soportados.md:231). Un DetallePago con Forma y Fecha pero sin Metodo produce NullReferenceException → HTTP 400 'Ocurrió un error inesperado el documento no se envió: ...'.
- **Evidencia:** PaymentMeansCode = new PaymentMeansCodeType { Value = pago.Metodo.Codigo },  // ConDetallePago, sin comprobación; Tipos.cs:2269 'public MetodoPago Metodo { get; set; }' sin valor por defecto (sí lo tiene Identificador, Tipos.cs:2275)
- **Corrección propuesta:** Hay que cerrar el hueco por los dos lados. Archivos con ruta absoluta:

### **BLOQUEANTE** — La dirección del proveedor se llama DireccionFisica, pero en Emisor y Adquiriente del mismo cuerpo se llama DireccionFiscal

- **Campo:** `Proveedor.DireccionFisica (vs Emisor.DireccionFiscal / Adquiriente.DireccionFiscal)`
- **Tipo:** otro
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:2032`
- **Consecuencia:** Ni documentos-soportados.md ni referencia-api.md nombran la clave de la dirección del proveedor para el 05. Un ERP que copie la clave de los objetos hermanos y mande 'Proveedor': { 'DireccionFiscal': {...} } no recibe ningún aviso: System.Text.Json descarta la propiedad desconocida, Proveedor.DireccionFisica queda en null y el generador revienta en GeneradorDocumentoSoporte.cs:227 ('ID = (proveedor.DireccionFisica.Municipio != null) ?') → HTTP 400 con 'Object reference not set to an instance of an object.'
- **Evidencia:** Tipos.cs:2032 (Proveedor) 'public DireccionFisica DireccionFisica { get; set; }' frente a Tipos.cs:2001 (Emisor) 'public DireccionFisica DireccionFiscal { get; set; }' y Tipos.cs:2117 (Adquiriente) 'public DireccionFisica DireccionFiscal { get; set; }'
- **Corrección propuesta:** Hay que cerrar las tres grietas (contrato, aviso y documentación). Mínimo imprescindible: 2 y 3; lo ideal es hacer 1 también.

### **BLOQUEANTE** — TrackID siempre vuelve vacío en documento soporte, aunque la doc diga que es lo que se usa para consultar

- **Campo:** `ResponseDian.TrackID`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoSoporteServicio.cs:157`
- **Consecuencia:** referencia-api.md:330 dice del campo TrackID: 'En habilitación, identificador del envío (ZipKey); en producción, el UUID. Úselo para consultar.', y referencia-api.md:324 declara esa tabla como la respuesta común de las rutas de documento soporte. El ERP que guarde TrackID para consultar después se queda con cadena vacía y la consulta posterior no identifica ningún documento. El único identificador real que devuelve EnviarDSNO es UUID (el CUDS).
- **Evidencia:** var resultado = new ResponseDian { statusCode = respuesta.StatusCode, statusMessage = respuesta.StatusMessage };  // Convertir() nunca asigna TrackID; Enviar() solo añade resultado.UUID (línea 62). ResponseDian.cs:18 lo inicializa en string.Empty.
- **Corrección propuesta:** Hay que alinear código y documentación; recomiendo hacer ambas cosas.

### **BLOQUEANTE** — ConsultarDSNO consulta siempre por CUDS con GetStatus; nunca acepta el ZipKey que la doc pide en habilitación

- **Campo:** `track_id de POST /api/DocumentoSoporte/ConsultarDSNO`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/DocumentoSoporteServicio.cs:145`
- **Consecuencia:** referencia-api.md:238 instruye lo contrario: 'En habilitación es el ZipKey del envío; en producción, el UUID (CUDS) del documento'. En habilitación el ERP no tiene ningún ZipKey que mandar (EnviarDSNO no devuelve uno; el ZipKey solo aparece en el ResultadoSet de X-Envio-Set, que se consulta con GetStatusZip en EnviadorSetPruebas). Si lo intentara, se llamaría GetStatus con un valor que la DIAN no reconoce y la consulta falla: 400 'Ocurrió un error inesperado en la consulta: La DIAN no devolvió información para ese identificador.' o un ResponseDian de error.
- **Evidencia:** var respuesta = enviador.ConsultarDocumento(trackId);  // precedido del comentario de la línea 143-144: 'El envío normal (SendBillSync) es el mismo en habilitación y producción: se consulta por el UUID (CUDS) con GetStatus. El ZipKey solo existe en el envío al set'. ConsultarDocumento llega a ClienteServicioDian.cs:146 'return client.GetStatus(uuid);'
- **Corrección propuesta:** 1) En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/referencia-api.md:238, reemplazar "En habilitación es el `ZipKey` del envío; en producción, el UUID (CUDS) del documento." por algo como: "`track_id` es siempre el UUID (CUDS) del documento, tanto en habilitación como en producción (la consulta usa `GetStatus`). El `ZipKey` solo existe en el envío al set de pruebas (`X-Envio-Set: true`), que devuelve su resultado en el `ResultadoSet` y se consulta internamente con `G

### **BLOQUEANTE** — EnviarDEJson deserializa con las opciones por defecto, así que el mismo cuerpo que funciona en EnviarDE es rechazado

- **Campo:** `JsonDE (cuerpo MetaDatosDE del 05 por EnviarDEJson)`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoElectronicoController.cs:103`
- **Consecuencia:** referencia-api.md:266 dice 'Igual que EnviarDE, pero el MetaDatosDE viaja en un parámetro JsonDE codificado en Base64' y guia-para-quantyerp.md:159 'EnviarDEJson recibe el mismo contenido'. No es el mismo contrato: sin JsonStringEnumConverter los enums en texto ('TipoPersona':'JURIDICA', DetallePago 'Forma':'CONTADO', 'TipoProcedencia':'RESIDENTE') lanzan JsonException, y sin PropertyNameCaseInsensitive las claves dejan de ser insensibles a mayúsculas. El ERP recibe 400 'Ocurrió un error desserializando el ojeto' con el mismo JSON que EnviarDE acepta.
- **Evidencia:** de = System.Text.Json.JsonSerializer.Deserialize<MetaDatosDE>(original);  // sin JsonSerializerOptions, frente a Program.cs:25-26 que para las demás rutas configura 'o.JsonSerializerOptions.PropertyNamingPolicy = null;' y 'o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());'
- **Corrección propuesta:** Dos opciones, ambas en DocumentoElectronicoController.cs.

### Aviso — Periodo.FormaGT se ignora: el periodo del documento siempre sale como 'Por operación'

- **Campo:** `Periodo.FormaGT`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Shared/Documentos/GeneradorDocumentoSoporte.cs:1580`
- **Consecuencia:** documentos-soportados.md:204 incluye 'Periodo' entre los campos del cuerpo del 05 y nada dice de que su forma de generación se descarte. Un ERP que agrupe compras de la semana y mande Periodo.FormaGT con código '2' (acumulado semanal) recibe 200, pero el XML firmado y enviado a la DIAN declara '1' / 'Por operación'. El dato llega mal informado y el ERP no tiene forma de enterarse. Nótese que en las líneas sí se respeta (GeneradorDocumentoSoporte.cs:1360 usa linea.PeriodoFacturacionLine.FormaGT.Codigo), con lo que cabecera y líneas pueden contradecirse.
- **Evidencia:** DescriptionCode =  new DescriptionCodeType[] { new DescriptionCodeType{ Value = "1"} },  y en 1581 Description = ... Value = "Por operación"  // valores fijos, aunque Tipos.cs:44 define 'public FormaGeneracionTransmision FormaGT { get; set; }' dentro de PeriodoFacturacion y el catálogo (Tipos.cs:56-57) contempla también ACUMULADO_SEMANAL con código '2'
- **Corrección propuesta:** En `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Documentos/GeneradorDocumentoSoporte.cs`, método `ConPeriodoFacturacion` (1570-1587), sustituir los literales de las líneas 1580-1581 por el valor que llega en el parámetro:

## Nota de ajuste de documento soporte (95)

### **BLOQUEANTE** — El proveedor se deserializa con la clave DireccionFisica, no DireccionFiscal como en Emisor y Adquiriente, y si falta revienta con NullReference

- **Campo:** `Proveedor.DireccionFisica (el ERP escribiría Proveedor.DireccionFiscal)`
- **Tipo:** nombre-json-distinto
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:2032`
- **Consecuencia:** Un ERP que mande `Proveedor: { "Identificacion": ..., "DireccionFiscal": {...} }` (el nombre que usan Emisor y Adquiriente) pierde la dirección en silencio, DireccionFisica queda null y el envío muere con NullReferenceException: HTTP 400 con el texto `Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object.` (DocumentoSoporteController.cs:52), sin decir qué campo falta. El mismo payload sin DireccionFisica en el Emisor (ruta de respaldo, NotaAjusteDocumentoSoporteServicio.cs:159) falla igual.
- **Evidencia:** Tipos.cs:2032 (Proveedor) `public DireccionFisica DireccionFisica { get; set; }` frente a Tipos.cs:2001 (Emisor) `public DireccionFisica DireccionFiscal { get; set; }` y Tipos.cs:2117 (Adquiriente) `public DireccionFisica DireccionFiscal { get; set; }`. No hay [JsonPropertyName] en todo Tipos.cs (solo dos [JsonIgnore] en 1984 y 1986), y System.Text.Json descarta en silencio las claves que no conoce (Pos.Api/Program.cs:22-28 no activa UnmappedMemberHandling). El generador la usa sin comprobar nulos: GeneradorNotaAjusteDocumentoSoporte.cs:302 `IdentificationCode = new IdentificationCodeType { Va
- **Corrección propuesta:** Dos cambios, ambos necesarios:

### **BLOQUEANTE** — Adquiriente.TipoIdentificacion es obligatorio de hecho (NullReference), pero la documentación solo exige NIT y razón social

- **Campo:** `Adquiriente.TipoIdentificacion`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Shared/Documentos/GeneradorNotaAjusteDocumentoSoporte.cs:368`
- **Consecuencia:** El ERP que arma el adquiriente con lo documentado (Identificacion + RazonSocial) recibe HTTP 400 `Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object.`, sin pista del campo. En el 94 la doc sí lista el tipo de identificación (documentos-soportados.md:230); en el bloque 05/95 no aparece en ninguna parte.
- **Evidencia:** GeneradorNotaAjusteDocumentoSoporte.cs:368 `schemeName = adquiriente.TipoIdentificacion.Codigo,` y la clase no trae valor por defecto: Tipos.cs:2110 `public TipoIdentificacion TipoIdentificacion { get; set; }` (compárese con Tipos.cs:2120 y :2122, que sí lo traen). Nada lo rellena en el camino del 95: en Pos solo se asigna TipoIdentificacion al Proveedor (NotaAjusteDocumentoSoporteServicio.cs:166 `de.Proveedor.TipoIdentificacion = TipoIdentificacion.GetFromCodigo("31");`). La validación previa solo pide la identificación: NotaAjusteDocumentoSoporteServicio.cs:70 `Requerir(de.Adquiriente != nul
- **Corrección propuesta:** Hay que hacer que el adquiriente del documento soporte no dependa de un campo no documentado. La vía más fiel a la regla DIAN (el adquiriente del 05/95 es siempre la propia empresa, NIT) es rellenarlo igual que ya se hace con el proveedor, y además documentarlo:

### **BLOQUEANTE** — Si el ERP no manda TipoPersona, el XML sale con AdditionalAccountID = 0, código que no existe en el catálogo TipoOrganizacion de la DIAN

- **Campo:** `Proveedor.TipoPersona y Adquiriente.TipoPersona (UBL cbc:AdditionalAccountID)`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Shared/Documentos/GeneradorNotaAjusteDocumentoSoporte.cs:354`
- **Consecuencia:** La nota se firma y se transmite con un código fuera de catálogo: la DIAN la rechaza, y como el rechazo viaja dentro de ResponseDian la API responde HTTP 200 (referencia-api.md:233), así que el ERP no ve ningún 400 que lo avise. TipoPersona no está documentado como obligatorio en ninguna parte del bloque 05/95.
- **Evidencia:** GeneradorNotaAjusteDocumentoSoporte.cs:354 `Value = ((int)adquiriente.TipoPersona).ToString()` y :277 `Value = ((int)proveedor.TipoPersona).ToString()`. El enum no tiene miembro 0: Tipos.cs:474 `public enum TipoPersona { JURIDICA = 1, NATURAL = 2 }`, así que al omitir el campo el valor por defecto de la propiedad (Tipos.cs:2119 y :2033, sin inicializador) es 0 y se serializa literal. El catálogo oficial que viene en el repo solo admite 1 y 2: docs/Caja_de_herramientas_Documento_Soporte_Validacion_Previa/Version 1.1/Listas de valores/TipoOrganizacion-2.1.gc (filas `1` Persona Jurídica y `2` Per
- **Corrección propuesta:** Hay que cerrar el hueco en validación (preferible a un default silencioso, que enviaría a la DIAN un tipo de persona inventado) y documentarlo.

### **BLOQUEANTE** — La referencia exige AlgoritmoCufe = CUDS-SHA384 y UUID de 96 hexadecimales, y el único ejemplo documentado (nota 94) dice CUDE-SHA384

- **Campo:** `DocumentoReferencia.AlgoritmoCufe.Codigo y DocumentoReferencia.UUID`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjusteDocumentoSoporteServicio.cs:62`
- **Consecuencia:** El ERP que copia el contrato del 94 (AlgoritmoCufe = CUDE-SHA384) o que omite AlgoritmoCufe —porque la doc nunca lo nombra para el 95— recibe HTTP 400 `La referencia requiere el CUDS del documento soporte original (96 caracteres hexadecimales) y CUDS-SHA384.` Además la clave se sigue llamando `AlgoritmoCufe` aunque lo que se exige es un CUDS.
- **Evidencia:** NotaAjusteDocumentoSoporteServicio.cs:62-64 `Requerir(referencia.AlgoritmoCufe?.Codigo == AlgoritmoSeguridadUUID.CUDS_SHA384.Codigo && Regex.IsMatch(referencia.UUID ?? "", @"\A[0-9a-fA-F]{96}\z"), "La referencia requiere el CUDS del documento soporte original (96 caracteres hexadecimales) y CUDS-SHA384.");` con Tipos.cs:135 `public static AlgoritmoSeguridadUUID CUDS_SHA384 => new AlgoritmoSeguridadUUID { Codigo = "CUDS-SHA384" };`. La doc del 95 solo dice (guia-para-quantyerp.md:149) «Debe referenciar un documento soporte `05` por su **CUDS**», sin nombrar el campo ni el valor; el único contra
- **Corrección propuesta:** Es un fallo de documentación, no de código: la regla es correcta según el anexo DIAN (un documento soporte 05 se identifica por CUDS), lo que falta es escribirla. Tres cambios:

### **BLOQUEANTE** — DocumentoReferencia.TipoDocumento debe ser 05, y el helper del propio modelo lo deja en 95

- **Campo:** `DocumentoReferencia.TipoDocumento.Codigo`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjusteDocumentoSoporteServicio.cs:58`
- **Consecuencia:** Enviar la referencia sin TipoDocumento, o con el tipo de la propia nota (95), da HTTP 400 `DocumentoReferencia debe identificar el documento soporte original (05).` Quien reutilice AddDocumentoReferencia del modelo compartido cae en los dos errores a la vez (tipo 95 y AlgoritmoCufe nulo).
- **Evidencia:** NotaAjusteDocumentoSoporteServicio.cs:58 `Requerir(referencia?.TipoDocumento?.Codigo == "05", "DocumentoReferencia debe identificar el documento soporte original (05).");`. El helper del propio modelo compartido deja el tipo de la nota (95) en la referencia salvo para el 94: MetaDatosDE.cs:80 `TipoDocumento = TipoDocumento.Codigo == "94" ? TipoDocumentoElectronico.DOCUMENTO_EQUIVALENTE_POS : TipoDocumento,` y su switch (MetaDatosDE.cs:86-99) solo asigna AlgoritmoCufe para "20", "01", "02", "03" y "04": para el 95 queda null. Ni referencia-api.md ni documentos-soportados.md describen el objeto 
- **Corrección propuesta:** Dos cambios, uno de contrato y uno de documentación.

### **BLOQUEANTE** — ConceptoAjuste es obligatorio y solo admite 1 a 5, pero la documentación del 95 nunca lo menciona

- **Campo:** `ConceptoAjuste.Codigo`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjusteDocumentoSoporteServicio.cs:54`
- **Consecuencia:** Un ERP que arme la nota con lo documentado (referencia al 05 + líneas) y sin ConceptoAjuste recibe HTTP 400 `ConceptoAjuste.Codigo debe estar entre 1 y 5 (concepto de la nota de ajuste del documento soporte).` Y si copia los conceptos del 94 puede mandar un código con un significado distinto al que la DIAN registra para el documento soporte.
- **Evidencia:** NotaAjusteDocumentoSoporteServicio.cs:54-55 `Requerir(de.ConceptoAjuste != null && new[] { "1", "2", "3", "4", "5" }.Contains(de.ConceptoAjuste.Codigo), "ConceptoAjuste.Codigo debe estar entre 1 y 5 (concepto de la nota de ajuste del documento soporte).");`. La única descripción del 95 en la documentación es guia-para-quantyerp.md:149, que solo habla de la referencia por CUDS; documentos-soportados.md documenta ConceptoAjuste únicamente para la nota POS 94 (línea 227) y su tabla de documentos soportados (líneas 5-14) no tiene fila para el 95. Además los significados no coinciden entre bloques:
- **Corrección propuesta:** En `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/docs/integracion/documentos-soportados.md`:

### Aviso — El concepto 5 se envía con la descripción OTRO, que no es el nombre del catálogo de la DIAN (Otros)

- **Campo:** `ConceptoAjuste código 5 (cbc:Description de DiscrepancyResponse)`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Shared/Metadatos/Tipos.cs:3594`
- **Consecuencia:** El XML firmado lleva `<cbc:Description>OTRO</cbc:Description>` en lugar del nombre oficial «Otros». El ejemplo de la DIAN (Ejemplificaciones/NotaDeAjuste.xml) usa texto libre en ese elemento, así que no hay rechazo demostrable; lo que sí ocurre es que el concepto impreso/retornado no coincide con el catálogo ni con la descripción que el ERP envió.
- **Evidencia:** Tipos.cs:3594 `{ "OTRO", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "5", Descripcion = "OTRO" } }`, que es el diccionario que usa el servicio: NotaAjusteDocumentoSoporteServicio.cs:77 `var concepto = ConceptoNotaAjusteDocumentoSoporte._tiposMap.Values.First(c => c.Codigo == de.ConceptoAjuste.Codigo);` y de ahí al XML en GeneradorNotaAjusteDocumentoSoporte.cs:47 `new DescriptionType { Value = concepto.Descripcion }`. El catálogo oficial del repo dice «Otros» para el 5 (docs/Caja_de_herramientas_Documento_Soporte_Validacion_Previa/Version 1.1/Listas de valores/ConceptoNotaAjuste-2.1.gc), 
- **Corrección propuesta:** En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Metadatos/Tipos.cs:3594 cambiar la descripción del concepto 5 para que coincida con el catálogo oficial: `{ "OTRO", new ConceptoNotaAjusteDocumentoSoporte { Codigo = "5", Descripcion = "Otros" } }` (la clave "OTRO" del diccionario es interna y puede quedarse, aunque renombrarla a "OTROS" la alinearía con la propiedad estática OTROS de Tipos.cs:3630). Con eso el `<cbc:Description>` del DiscrepancyResponse queda en «Otros», igu

### Aviso — La razón social del adquiriente se documenta como validada en EnviarDSNO, pero la nota 95 solo compara el NIT

- **Campo:** `Adquiriente.RazonSocial`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Servicios/NotaAjusteDocumentoSoporteServicio.cs:144`
- **Consecuencia:** El mismo adquiriente que el servicio rechaza con 400 en un 05 pasa sin aviso en un 95, y la nota viaja a la DIAN con un RegistrationName distinto al del documento soporte que corrige. Además el texto del 400 del 95 no es el documentado en referencia-api.md:234 (el controlador devuelve el mensaje crudo, DocumentoSoporteController.cs:46, sin el prefijo `Ocurrió un error inesperado el documento no se envió:`), así que un ERP que detecte el error por ese prefijo no lo reconoce.
- **Evidencia:** NotaAjusteDocumentoSoporteServicio.cs:144-145 `if (de.Adquiriente == null || de.Adquiriente.Identificacion != _tenant.Emisor.Nit) throw new ArgumentException("El adquiriente de la nota debe ser el NIT configurado para este cliente.");` — no hay comparación de razón social, a diferencia del documento soporte 05, que sí la hace: DocumentoSoporteServicio.cs:90-93 `if (!string.Equals(ds.Adquiriente.RazonSocial?.Trim(), _tenant.Emisor.RazonSocial.Trim(), StringComparison.OrdinalIgnoreCase))`. La doc atribuye la regla a toda la ruta: referencia-api.md:229 «el **adquiriente** debe ser **su propia emp
- **Corrección propuesta:** Dos cambios, uno de código y uno de documentación.

### Aviso — El tipo 95 no existe en la referencia de la API ni en la tabla de documentos soportados, aunque el controlador lo enruta a otro servicio con otras validaciones

- **Campo:** `TipoDocumento.Codigo = "95" en POST /api/DocumentoSoporte/EnviarDSNO`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Pos/Pos.Api/Controllers/DocumentoSoporteController.cs:34`
- **Consecuencia:** El equipo que integre leyendo los dos documentos de referencia no encuentra el contrato del 95: ni los campos obligatorios (ConceptoAjuste, DocumentoReferencia con tipo 05 y CUDS-SHA384), ni que el cuerpo es `TipoDocumento: { "Codigo": "95" }` como objeto —la guía lo escribe como «`TipoDocumento` = `95`», y un valor escalar ni siquiera deserializa en TipoDocumentoElectronico (Tipos.cs:11-13), lo que produce un 400 de enlace de modelo con un ProblemDetails que tampoco está documentado.
- **Evidencia:** DocumentoSoporteController.cs:34 `if (de.TipoDocumento.Codigo == "95")` desvía a `_servicios.NotaAjusteDocumentoSoporte(tenant)`, con un validador propio (NotaAjusteDocumentoSoporteServicio.Validar, líneas 43-71) y respuestas distintas (400 con el mensaje crudo en :46-47). En cambio referencia-api.md:226-234 documenta EnviarDSNO solo con «Si `TipoDocumento` falta, se asume documento soporte (`05`)» y no menciona el 95 en ninguna línea, y la tabla de documentos soportados (documentos-soportados.md:5-14) no tiene fila para el 95: su única fila de esa ruta es la 10, «Documento soporte (no obligad
- **Corrección propuesta:** Hay que documentar el tipo 95 en los dos documentos de referencia (hoy no aparece en ninguno de los dos):

## Nómina electrónica (102 individual y 103 ajuste)

### **BLOQUEANTE** — Un cuerpo con TipoDocumentoElectronico errado u omitido responde HTTP 200 con statusCode "88" sin haber enviado nada a la DIAN

- **Campo:** `InformacionEnvio.TipoDocumentoElectronico / statusCode "88"`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Nomina/Nomina.Servicios/NominaServicio.cs:153`
- **Consecuencia:** El ERP recibe HTTP 200 (referencia-api.md:317 documenta 200 = statusCode entre 00 y 99 = éxito) y marca la nómina como enviada, pero no se firmó, no se guardó en disco ni se envió a la DIAN. La misma inconsistencia a la inversa sí devuelve 108/400 (NominaServicio.cs:141), y la ruta de set de pruebas lanza ArgumentException (NominaServicio.cs:107-108), así que el comportamiento no es intencional. Nóminas perdidas en silencio y sin posibilidad de detectarlas por el código HTTP.
- **Evidencia:** return Rechazo("88", "El documento no se ha enviado. El campo [InformacionEnvio.TipoDocumentoElectronico] tiene un valor errado");  — y el filtro de éxito del controlador, NominaController.cs:43: `return int.TryParse(codigo, out var numero) && numero >= 0 && numero <= 99;` seguido de NominaController.cs:79 `return EsExito(respuesta.statusCode) ? Ok(respuesta) : BadRequest(respuesta);`. El código "88" no aparece en ningún catálogo: el único otro "88" del repo es el departamento de San Andrés (src/Shared/Metadatos/Tipos.cs:749), y referencia-api.md:328 solo documenta `00`, `99`, `SPxx`, `101`, `
- **Corrección propuesta:** Cambiar el código de estado de esa rama de "88" a "108", que es el código ya usado por las otras tres salidas "El documento no se ha enviado" del mismo switch y el único documentado para errores de negocio.

### **BLOQUEANTE** — El 400 del ajuste pide los campos [Referencia.Numero] y [Referencia.CUNE], pero las claves JSON reales son DocumentoReferencia.Numero y DocumentoReferencia.Cune

- **Campo:** `DocumentoReferencia (Numero, Cune, FechaGeneracion)`
- **Tipo:** nombre-json-distinto
- **Dónde:** `src/Nomina/Nomina.Servicios/NominaServicio.cs:134`
- **Consecuencia:** Es exactamente la familia del bug de DocumentoRef en facturación. El integrador que lee el mensaje de error agrega un objeto `Referencia` (o, viniendo de notas 91/92, un `DocumentoRef`), System.Text.Json lo descarta como propiedad desconocida y sigue recibiendo 400 con statusCode 108 indefinidamente. Ni referencia-api.md ni documentos-soportados.md nombran `DocumentoReferencia` en la sección de nómina, así que no hay dónde verificarlo.
- **Evidencia:** return Rechazo("108", "El documento no se ha enviado. Cuando se trata Nómina de Ajuste los campos[Referencia.Numero] y [Referencia.CUNE] son obligatorios para nómina de ajuste.");  — mismo texto en NominaServicio.cs:98 y NominaServicio.cs:110 ("Solo los documentos de Nómina de Ajuste necesitan los campos [Referencia.Numero] y [Referencia.CUNE]"). La propiedad que realmente deserializa es MetaDatosNEI.cs:64 `public documento_referencia DocumentoReferencia { get; set; }`, con MetaDatosNEI.cs:509 `public string Numero { get; set; }` y MetaDatosNEI.cs:514 `public string Cune { get; set; }`. No exi
- **Corrección propuesta:** A) Corregir los CUATRO mensajes en `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Nomina/Nomina.Servicios/NominaServicio.cs` para que nombren la clave JSON real `DocumentoReferencia` (el hallazgo solo listaba tres; L150 es la cuarta):

### **BLOQUEANTE** — CuentaBanco es obligatorio en el ajuste 103 aunque sea opcional en la nómina 102, y su ausencia produce NullReferenceException

- **Campo:** `CuentaBanco`
- **Tipo:** obligatorio-no-documentado
- **Dónde:** `src/Nomina/Nomina.Servicios/ConstructorNomina.cs:530`
- **Consecuencia:** Un ERP que paga en efectivo (MetodoPago distinto de "42") envía correctamente el 102 sin CuentaBanco y, al intentar el 103 que lo reemplaza o elimina, recibe HTTP 400 con statusCode "99" y el texto "Ocurrió un error inesperado el documento no se envió: Object reference not set to an instance of an object" (NominaController.cs:75); el 99 está documentado como rechazo de la DIAN (referencia-api.md:328), así que el mensaje apunta al lugar equivocado. Con el encabezado X-Envio-Set: true la NullReferenceException ni siquiera es ArgumentException y sale como HTTP 502 (EnvioSetHelper.cs:28-31), que set-de-pruebas-produccion.md:28 documenta como "error de comunicación inesperado al contactar la DIAN": el ERP reintentará en bucle un cuerpo que debe corregir. Nada de esto está documentado como obligatorio.
- **Evidencia:** CuentaBancaria cuentaBancaria = new CuentaBancaria\n{\n    NombreBanco = nom.CuentaBanco.NombreBanco,   // línea 532, sin comprobación de null\n    TipoCuenta = nom.CuentaBanco.TipoCuenta,\n    NumeroCuenta = nom.CuentaBanco.NumeroCuenta\n};  — mientras que el camino de la nómina individual lo protege, ConstructorNomina.cs:408-414: `CuentaBancaria CtaBanco = null;` / `if (nom.InformacionPago.MetodoPago == "42")` / `NombreBanco = nom.CuentaBanco.NombreBanco,`.
- **Corrección propuesta:** Dos cambios, uno de código y uno de documentación.

### **BLOQUEANTE** — El ajuste 103 de tipo Reemplazar pierde primas, vacaciones, cesantías, bonos, auxilios, bonificaciones, comisiones y 5 de las 7 clases de horas extra

- **Campo:** `InformacionDevengados.* y OtrosIngresos en el 103`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Nomina/Nomina.Servicios/ConstructorNomina.cs:567`
- **Consecuencia:** set-de-pruebas-produccion.md:14 y guia-para-quantyerp.md:136 anuncian el bloque de nómina como "(102, 103)" con "el mismo cuerpo JSON habitual", pero el 103 que reemplaza a un 102 con prima, cesantías o bonos genera ante la DIAN un documento de reemplazo que declara mucho menos de lo pagado. El reemplazo queda autorizado (no hay error visible) y el empleador queda reportando mal la nómina del periodo.
- **Evidencia:** En ConstruirAjuste los únicos devengados que se trasladan son el resumen, el auxilio de transporte y dos colecciones de horas extra: ConstructorNomina.cs:552 `var devengados = new Devengados { Resumen = ... }`, :561 `devengados.Transporte = new DevengadoTransporte { AuxilioTransporte = auxilioTransporte };`, :553 `foreach (HoraExtraLab hel in nom.HorasExtraDiurnas)` y :567 `foreach (HoraExtraLab hel in nom.HorasExtraNocturnas)`. No hay un solo `nom.InformacionDevengados.Primas`, `.VacacionesComunes`, `.VacacionesCompensadas`, `.Cesantias` ni `foreach(var otroIng in nom.OtrosIngresos)` después 
- **Corrección propuesta:** Hay que hacer que el 103 de tipo Reemplazar construya exactamente los mismos devengados que el 102, porque un "reemplazo" por definición sustituye al documento completo.

### **BLOQUEANTE** — TotalDevengados omite 5 de las 7 clases de horas extra y suma dos veces el bono salarial en lugar de la bonificación

- **Campo:** `TotalesNominaIndividual.TotalDevengados / ValorTotal`
- **Tipo:** otro
- **Dónde:** `src/Nomina/Nomina.Servicios/ConstructorNomina.cs:331`
- **Consecuencia:** El XML declara horas extra nocturnas y dominicales con su Pago en el detalle, pero el TotalDevengados no las incluye: la suma de los devengados no cuadra con el total y la DIAN rechaza el documento por reglas de totales (o lo autoriza con el ValorTotal mal, ya que ValorTotal = totalDevengados - totalDeducciones, línea 423). Le ocurre a cualquier nómina válida con trabajo nocturno o dominical, sin dato inválido alguno.
- **Evidencia:** ConstructorNomina.cs:329-331: `decimal ValorBSE = devengados.Bonificaciones?.Sum(bs => bs.BonificacionSalarial) ?? 0;` / `totalDevCS += ValorBSE; // Bonificacion Salarial Efectivo` / `totalDevengados += ValorBS;`  (suma ValorBS, el bono salarial de la línea 321, en vez de ValorBSE). Y en todo el bloque de totales (líneas 285-367) solo existen `ValorHED` (:293) y `ValorHEDDF` (:297): no se acumulan HorasExtraNocturnas, HorasRecargoNocturno, HorasRecargoDiurnoDominicalesFestivos, HorasExtraNocturnasDominicalesFestivos ni HorasRecargoNocturnoDominicalesFestivos, aunque sus pagos sí se escriben en
- **Corrección propuesta:** En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Nomina/Nomina.Servicios/ConstructorNomina.cs:

### **BLOQUEANTE** — HorasRecargoNocturno se acepta en el cuerpo pero nunca se traslada al XML

- **Campo:** `HorasRecargoNocturno`
- **Tipo:** otro
- **Dónde:** `src/Nomina/MetaDatosNE/MetaDatosNEI.cs:80`
- **Consecuencia:** El ERP envía el recargo nocturno (el concepto más común de la nómina con turnos), la API lo acepta con 200 y el XML que llega a la DIAN no lo contiene ni en el detalle ni en los totales. Pérdida silenciosa: no hay error, ni advertencia, ni diferencia visible en la respuesta.
- **Evidencia:** public Collection<HoraExtraLab> HorasRecargoNocturno { get; set; } = new Collection<HoraExtraLab>();  — es la única de las siete colecciones de horas que ConstructorNomina nunca recorre: están mapeadas HorasExtraDiurnas (:191), HorasExtraNocturnas (:215), HorasExtraDiurnasDominicalesFestivos (:229), HorasRecargoDiurnoDominicalesFestivos (:241), HorasExtraNocturnasDominicalesFestivos (:253) y HorasRecargoNocturnoDominicalesFestivos (:267), pero no hay ningún `nom.HorasRecargoNocturno` en el repo. El destino existe (GeneradorNominaIndividual.cs:227 `foreach (var hrn in devengados.HorasRecargoNoc
- **Corrección propuesta:** Hay que mapear la colección en ConstructorNomina.cs (C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN\Unificado\src\Nomina\Nomina.Servicios\ConstructorNomina.cs), en los DOS constructores, y además arreglar los totales.

### Aviso — Notas está declarado como campo público, no como propiedad, y System.Text.Json lo descarta siempre

- **Campo:** `Notas`
- **Tipo:** nombre-json-distinto
- **Dónde:** `src/Nomina/MetaDatosNE/MetaDatosNEI.cs:62`
- **Consecuencia:** referencia-api.md:316 remite al integrador a MetaDatosNEI.cs como esquema, y documentos-soportados.md:43 documenta `Notas` como lista de texto en el otro bloque, así que el ERP lo enviará. Nunca llega: el XML sale siempre con la nota por defecto de MatrixSQL (en habilitación, con el texto "por tanto no es válido"). Las observaciones de nómina del ERP se pierden sin aviso.
- **Evidencia:** public List<string> Notas;  — sin `{ get; set; }`, a diferencia de todas las demás propiedades del modelo. La API usa System.Text.Json (Program.cs:22-27: `builder.Services.AddControllers().AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = null; ... })`) y no hay un solo `IncludeFields` en src/, por lo que los campos públicos no se deserializan. El consumidor es ConstructorNomina.cs:436: `List<string> notas = nom.Notas?.ToList() ?? new List<string> { "Este documento fué generado por MatrixSQL-Nómina" + (habilitacion ? " en AMBIENTE DE PRUEBAS, por tanto no es válido" : " en a
- **Corrección propuesta:** Tres cambios, el primero es el que arregla el contrato:

### Aviso — La documentación afirma que el servicio nunca calcula valores, pero en nómina calcula el pago de las horas extra y fija las deducciones en 4% y 4%

- **Campo:** `HoraExtraLab.Pago / Deducciones (Salud, FondoPension)`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Nomina/Nomina.Servicios/ConstructorNomina.cs:376`
- **Consecuencia:** El ERP que ya liquidó su nómina (libranzas, retefuente, fondo de solidaridad, salud o pensión distintas del 4%, jornada mensual distinta de 240 horas pese a existir InformacionLaboral.HorasMensuales, que nunca se usa) no tiene forma de enviar sus propias cifras: la DIAN recibe deducciones y ValorTotal inventados por el servicio, que no coinciden con el desprendible de pago ni con la planilla PILA.
- **Evidencia:** documentos-soportados.md:18 dice: "El servicio **nunca asigna ni cambia** su numeración, sus precios ni sus impuestos. Traduce lo que usted envía. Los valores de impuestos, bases y totales los calcula **usted** (como lo hace QuantyERP)". El código hace lo contrario: ConstructorNomina.cs:375-383 `Salud = new DeduccionSalud { Porcentaje = 4, Deduccion = decimal.Round((totalDevCS / 100) * 4, 2) }, FondoPension = new FondoPension { Porcentaje = 4, Deduccion = decimal.Round((totalDevCS / 100) * 4, 2) }` (y :584-589 en el ajuste, allí sobre SueldoDevengado, con base distinta). El pago de cada hora e
- **Corrección propuesta:** Hay que cambiar las dos cosas: el modelo (para que el ERP pueda mandar sus cifras) y la documentación (para que la promesa deje de ser falsa mientras tanto).

## Eventos RADIAN (030 a 033)

### **BLOQUEANTE** — La documentación publica la ruta AceptacionTacita (034) como soportada, pero el servicio siempre la rechaza con 109 «Opcion en desarrollo»

- **Campo:** `Codigo = "034" / POST /api/v1/Eventos/AceptacionTacita`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Nomina/Nomina.Servicios/EventosRadianServicio.cs:157`
- **Consecuencia:** El ERP que implemente la aceptación tácita según la documentación recibe siempre HTTP 400 con statusCode 109. Además 109 no figura en la tabla de códigos 400 de referencia-api.md:302 (solo 101, 108 y 99), así que el ERP ni siquiera puede mapear el error. El dato correcto sí está en un documento interno (docs/fase3-nomina-radian.md:60: «la aceptación tácita (034, que ya estaba en desarrollo)»), pero no en docs/integracion.
- **Evidencia:** EventosRadianServicio.cs:157-159: `if (evento.Codigo == "034") { return (null, "", "", "109", "Opcion en desarrollo"); }` — contra referencia-api.md:288 `| `AceptacionTacita` | `034` |` y documentos-soportados.md:14 `| Eventos RADIAN | `030` a `034` | `POST /api/v1/Eventos/*` | Síncrono |`. La prueba lo confirma: tests/Pos.Pruebas/EventosRadianApiTests.cs:166-169 `Assert.Equal(HttpStatusCode.BadRequest, codigo); Assert.Equal("109", r.statusCode);`
- **Corrección propuesta:** Hay que alinear la documentación de `docs/integracion` con el comportamiento real (o implementar el 034). Lo mínimo, en los tres puntos:

### **BLOQUEANTE** — El código de causa del reclamo (031) no se valida contra el catálogo y la documentación no dice cuáles son los 4 valores válidos

- **Campo:** `Causa.Codigo (evento 031)`
- **Tipo:** codigo-fuera-de-catalogo
- **Dónde:** `src/Nomina/Nomina.Servicios/EventosRadianServicio.cs:196`
- **Consecuencia:** Un ERP que adivine el valor (p. ej. "1", "RECHAZO" o el texto de la causa) genera un XML con listID inválido y la DIAN rechaza el reclamo. Y el rechazo llega como HTTP 200: EventosRadianServicio.cs:113 pone statusCode "99" y EventosController.cs:64 responde Ok() para todo código entre 00 y 99, así que un ERP que solo mire el status HTTP creerá que el reclamo se registró.
- **Evidencia:** EventosRadianServicio.cs:196: `case "031": generador = generador.ParaRechazoDocumento(new ConceptoEventoRechazo { Codigo = evento.Causa?.Codigo }); break;` — el texto del ERP pasa tal cual al atributo listID del XML (GeneradorEvento.cs:276: `listID = concepto.Codigo`). El catálogo tiene exactamente cuatro valores: Tipos.cs:2852 `INCONSISTENCIAS_DOCUMENTO => new ConceptoEventoRechazo { Codigo = "01" }`, :2857 "02", :2862 "03", :2867 "04". La documentación solo dice referencia-api.md:296 `| `Causa` | objeto `{Codigo, Significado}` | Solo en `031` | Ambos textos son obligatorios en el reclamo. |`
- **Corrección propuesta:** Tres cambios. (1) Añadir un método de búsqueda al catálogo en C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Shared/Metadatos/Tipos.cs (clase ConceptoEventoRechazo, líneas 2845-2868), al estilo del `GetFromCodigo` que ya usa ResponsabilidadTributaria en ese mismo archivo (línea 2842): un `public static ConceptoEventoRechazo GetFromCodigo(string codigo)` que devuelva el concepto para "01".."04" y null para cualquier otro valor. (2) Validar en el controlador C:/Users/Eduardo/source/r

### Aviso — En la respuesta de los eventos, UUID devuelve el CUFE de la factura referenciada, no el CUDE del evento que se generó

- **Campo:** `ResponseDian.UUID (y Uuid de set-radian.json) en 030-033`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Nomina/Nomina.Servicios/EventosRadianServicio.cs:109`
- **Consecuencia:** El ERP que guarde response.UUID como CUDE del evento (lo que dice la documentación) guarda el CUFE de la factura, y además lo guarda repetido para el acuse, el recibo y la aceptación de la misma factura. El CUDE del evento no se devuelve por ninguna vía, así que el ERP no puede identificar ni rastrear el evento ante la DIAN ni ante RADIAN.
- **Evidencia:** EventosRadianServicio.cs:109: `respuesta.UUID = prep.UuidReferencia;` donde PrepararYFirmar devuelve el UUID de la factura leída, no el del evento (EventosRadianServicio.cs:208: `return (firmado, numero, referencia.UUID, "", "");`). El CUDE calculado en :203 (`generador.AsignarCUDE(software.PIN)`) se descarta. La documentación dice referencia-api.md:331 `| `UUID` | texto | CUFE, CUDE, CUDS o CUNE del documento. |` en la tabla declarada común a eventos (referencia-api.md:324), y ResponseDian.cs:21-23 lo documenta igual («Es el código: CUFE, CUDE, CUNE, CUDS ETC.»). Los demás bloques sí mandan e
- **Corrección propuesta:** 1) En C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Nomina/Nomina.Servicios/EventosRadianServicio.cs, hacer que PrepararYFirmar devuelva también el CUDE del evento: cambiar la firma de :152 a `(byte[] Firmado, string Numero, string Cude, string UuidReferencia, string ErrorCodigo, string ErrorMensaje)`, capturar en :203 `var applicationResponse = generador.AsignarCUDE(software.PIN).Obtener();` y retornar en :208 `return (firmado, numero, applicationResponse.UUID.Value, referencia.U

### Aviso — TrackID siempre vuelve vacío en los eventos aunque la documentación dice que sirve para consultar

- **Campo:** `ResponseDian.TrackID en 030-033`
- **Tipo:** doc-contradice-codigo
- **Dónde:** `src/Nomina/Nomina.Servicios/EventosRadianServicio.cs:107`
- **Consecuencia:** El ERP que reserve una columna TrackID para los eventos (y que programe una consulta posterior como en POS o nómina) recibe siempre cadena vacía; para eventos RADIAN tampoco existe una ruta de consulta. Es una promesa de la documentación que el bloque no cumple.
- **Evidencia:** GenerarEvento solo asigna tres campos: EventosRadianServicio.cs:107-109 `respuesta.statusCode = dian.StatusCode;` / `respuesta.statusMessage = dian.StatusMessage;` / `respuesta.UUID = prep.UuidReferencia;`. TrackID nunca se asigna y su valor por defecto es vacío (ResponseDian.cs:18: `public string TrackID { get; set; } = string.Empty;`). La documentación dice referencia-api.md:330 `| `TrackID` | texto | En habilitación, identificador del envío (ZipKey); en producción, el UUID. Úselo para consultar. |`, en la tabla declarada común a eventos (referencia-api.md:324).
- **Corrección propuesta:** Hay que hacer una de las dos cosas (yo recomiendo las dos):

### Aviso — En los eventos 030 y 032 la persona que acusa el recibo se llena con la identificación y el nombre del EMISOR de la factura

- **Campo:** `cac:IssuerParty/cac:Person (eventos 030 acuse y 032 recibo de bienes)`
- **Tipo:** otro
- **Dónde:** `src/Shared/Documentos/GeneradorEvento.cs:227`
- **Consecuencia:** El XML firmado declara que quien recibió la mercancía o la factura es el proveedor que la emitió (su NIT, su razón social como nombre y apellido, cargo y área «N/A»). La DIAN autorizó eventos así en habilitación (docs/fase3-nomina-radian.md:47 y :52), de modo que hoy no bloquea el envío, pero el evento registrado en RADIAN es falso y el ERP no tiene ningún campo en EventoRadian para corregirlo: la documentación no menciona la persona receptora.
- **Evidencia:** El servicio pone al emisor de la factura como ReceiverParty del evento (EventosRadianServicio.cs:190-191: `.ConRegistrante(adquiriente)` / `.ConReceptor(emisor);`) y el generador construye el ReceptorBienes por defecto leyendo justamente esa parte: GeneradorEvento.cs:227-229 `Identificacion = ApplicationResponse.ReceiverParty.PartyTaxScheme[0].CompanyID.Value,` / `Nombres = ...RegistrationName.Value,` / `Apellidos = ...RegistrationName.Value,` (con `Cargo = "N/A"`, `Departamento = "N/A"` en :230-231), y ese objeto se escribe en `ApplicationResponse.DocumentResponse[0].IssuerParty` (GeneradorEv
- **Corrección propuesta:** Hay que hacer que la persona de cac:IssuerParty/cac:Person salga del REGISTRANTE (el adquiriente que emite el evento), no de ReceiverParty, y permitir que el ERP la envíe.

### Aviso — Causa.Significado es obligatorio para el ERP pero el servicio lo descarta: nunca llega al XML ni a la DIAN

- **Campo:** `Causa.Significado (evento 031)`
- **Tipo:** otro
- **Dónde:** `src/Pos/Pos.Api/Controllers/EventosController.cs:39`
- **Consecuencia:** El integrador lee referencia-api.md:296 («Ambos textos son obligatorios en el reclamo») y supone que el motivo del reclamo viaja a la DIAN. Si lo omite recibe HTTP 400/101; si lo envía, el texto se pierde y el evento solo lleva el listID del código. Cualquier reclamo con un motivo redactado por el usuario del ERP queda sin ese motivo en RADIAN.
- **Evidencia:** EventosController.cs:39-40 lo exige: `if (string.IsNullOrEmpty(evento.Causa.Significado)) return BadRequest(new ResponseDian { statusCode = "101", statusMessage = "El Evento 031 requiere que se especifique el significado de la causa" });`. Pero el servicio solo usa el código (EventosRadianServicio.cs:196: `new ConceptoEventoRechazo { Codigo = evento.Causa?.Codigo }`) y el generador escribe una descripción fija (GeneradorEvento.cs:280: `new DescriptionType { Value = "Reclamo de la Factura Electrónica de Venta" }`). `Significado` no aparece en ninguna otra línea del flujo.
- **Corrección propuesta:** Hay que cerrar la brecha entre el contrato y lo que realmente viaja. Dos salidas válidas, en orden de preferencia:

### Aviso — El evento declara responsabilidad tributaria 01/IVA y persona jurídica para las dos partes, aunque la factura leída diga otra cosa

- **Campo:** `SenderParty/ReceiverParty: cac:TaxScheme/cbc:ID y CompanyID/@schemeVersionID`
- **Tipo:** otro
- **Dónde:** `src/Nomina/Nomina.Servicios/EventosRadianServicio.cs:258`
- **Consecuencia:** Para un cliente persona natural (caso frecuente en el grupo) o con responsabilidad 04, ZA o ZZ, el evento firmado afirma persona jurídica e IVA/01 para ambas partes, en contradicción con la factura que referencia. No rompe el envío hoy, pero el ERP no tiene forma de corregirlo y la inconsistencia queda en un documento firmado ante la DIAN.
- **Evidencia:** LeerFactura arma las partes solo con tres datos: EventosRadianServicio.cs:258-263 `return new ParteEvento { TipoIdentificacion = new TipoIdentificacion { Codigo = (string)empresa.Attribute("schemeName") }, Identificacion = empresa.Value, Nombre = impuestos.Element(cbc + "RegistrationName").Value };` — leyendo el mismo elemento cac:PartyTaxScheme del que ignora el hijo cac:TaxScheme. Quedan los valores por defecto de Tipos.cs:2842 `ResponsabilidadTributaria ... = ResponsabilidadTributaria.GetFromCodigo("01")` y Tipos.cs:2838 `TipoPersona ... = TipoPersona.JURIDICA`, que el generador escribe en 
- **Corrección propuesta:** En `C:/Users/Eduardo/source/repos/SISTEMAS360/DIAN/Unificado/src/Nomina/Nomina.Servicios/EventosRadianServicio.cs`, dentro de `LeerFactura`, la función local `Parte(string rol)` (líneas 254-264) debe leer los dos campos que hoy quedan en su valor por defecto. Cambiar:

