# Diseño del extractor anonimizado de QuantyERP y brechas con la API unificada

> **Estado:** documento de diseño (solo lectura). **No se conecta** a la base de datos de QuantyERP ni contiene credenciales, cadenas de conexión, secretos ni datos reales de clientes.

---

## 1. Objetivo y principios del extractor (`SimuladorErp` → ejemplos)

El simulador ([tools/SimuladorErp](../../tools/SimuladorErp/LEEME.md)) envía JSON ya construidos contra `Pos.Api`. Para probar casos realistas sin exponer clientes reales, un extractor de solo lectura sobre MySQL de QuantyERP debe:

1. **Leer únicamente lo fiscal y estructural:** productos, precios, impuestos, cabeceras/líneas de facturas o notas, resoluciones, parámetros de retención y catálogos geográficos/fiscales.
2. **Operar en solo lectura y fuera de línea respecto a DIAN:** usar un usuario MySQL `SELECT` (credenciales por variable de entorno, nunca en código o archivos de ejemplo) y emitir archivos JSON locales con la forma de `DocumentoDian` (ver [documentos-soportados.md](documentos-soportados.md) y [ejemplos/factura.json](ejemplos/factura.json)).
3. **Preservar invariantes matemáticas y tributarias:** mantener intactas cantidades, precios unitarios, descuentos porcentuales (`DtoDir`), tarifas e importes de impuestos/retenciones, flags `Excluido`, códigos DANE de municipio (`CodMunicipio`) y códigos de responsabilidad fiscal (`O-13`, `O-15`, `O-23`, `O-47`, `R-99-PN`, `48`/`49`).
4. **Anonimizar determinísticamente toda PII y secretos** antes de escribir en disco.

---

## 2. Tablas y columnas a leer en QuantyERP

Las consultas base ya existen en `QuantyERP.DataServices`: `CatalogoDataService.cs` (productos) y `DianDocumentDataService.cs` (documentos de salida y terceros).

### 2.1 Productos vendibles (catálogo e impuestos)

| Tabla | Alias | Columnas a leer | Uso en `DocumentoDian` / catálogo |
|---|---|---|---|
| `productos` | `p` | `IdProd`, `NomProd`, `NombreComercial`, `Descripcion`, `MarcaId`, `Referencia`, `UndComercial`, `ProductoBaseId`, `LineaId`, `CategoriaContableId`, `LlavePrecio`, `NivelPrecioVenta`, `Activo` | `Items[].Codigo` (`IdProd`), `Items[].Referencia`, base de `Items[].Detalle` |
| `marcas` | `mk` | `IdMarca`, `Nombre` | Se concatena en `Detalle` (`p.NomProd + ' ' + mk.Nombre`) |
| `productos_base` | `pb` | `IdProductoBase`, `LineaId`, `CategoriaContableId`, `NombreBase`, `SubpartidaArancelaria` | Herencia de línea y categoría contable del SKU |
| `lineas` | `l` | `IdLinea`, `Nombre`, `CategoriaContableId` | Herencia de categoría contable de respaldo |
| `unidadescom` | `u` | `IdUnidad`, `Abreviatura`, `Descripcion` | Unidad comercial informativa de apoyo |
| `precios` | `v` | `LlavePrecio`, `TipoOrigen`, `PrecioVenta01`..`PrecioVenta09`, `Activo` | Precios de lista (IVA incluido; útil para generar casos de prueba del catálogo) |
| `categorias_contables` | `cc` | `IdCategoriaContable`, `Nombre`, `SeVende`, `ConceptoFiscal` | Filtro `SeVende <> 0` y regla general de impuestos |
| `productos_impuestos` | `pi` | `Id`, `CodProducto`, `TarifaId`, `Activo` | Excepción de impuesto/tarifa específica del SKU |
| `categorias_contables_impuestos` | `cci` | `Id`, `CategoriaContableId`, `TarifaId` | Impuestos por defecto de la categoría |
| `impuestos_tarifas` | `it` | `ID`, `CodImpuesto`, `Tarifa` | Código DIAN del impuesto (`01`, `04`, etc.) y tarifa (null = excluido) |
| `impuestos` | `imp` | `Codigo`, `IncluidoEnPrecio`, `OrdenAplicacion` | Orden y modo de aplicación del impuesto |

### 2.2 Facturas, notas y datos fiscales

| Tabla | Alias | Columnas a leer | Uso en el extractor / contrato |
|---|---|---|---|
| `documentossalida` | `s` | `IdDocSal`, `TipoDocumento`, `NumeroDoc`, `Fecha`, `CodTercero`, `FormaPago`, `Plazo`, `IdentificadorPago`, `DocumentoOrigen`, `NumeroRef`, `Puesto`, `Nota`, `Anulado`, `Aprobado` | Cabecera: `TipoDocErp`, `Numero`, `FechaHoraDoc`, `FormaPago.Plazo`, `IdenficadorPago`, `OrdenRef` / `DocumentoRef` |
| `documentossalida` (auto-join origen) | `ct` | `IdDocSal`, `TipoDocumento`, `NumeroDoc`, `Fecha`, `Cufe` | `OrdenRef` (cuando origen es `'CT'`) o factura original en notas (`DocumentoRef`) |
| `dian_envios` | `de` | `DocumentoId`, `ComprobanteId`, `Numero`, `Estado`, `Cufe`, `CodigoRespuesta` | Recuperar el `Cufe` sintético/anonimizado de la factura referenciada por notas `91`/`92` |
| `formaspago` | `f` | `IdFormaPago`, `VariableFP`, `IdMedioPago` | `FormaPago.ID` (`CREDITO` vs `CONTADO`) y `FormaPago.CodigoMedioPago` |
| `puestos` | `pu` | `IdPuesto`, `Sucursal` | Enlazar venta → sucursal para resolución y tienda emisora |
| `sucursales` | `su` | `IdSucursal`, `CodigoTienda`, `Empresa` | Resolver `CodigoTienda` del emisor y filtros de resolución |
| `empresas` | `e` | `Empresa`, `Contribuyente` | Enlazar sucursal → contribuyente emisor |
| `resoluciones` | `r` | `ID`, `Activa`, `TipDoc`, `Sucursal`, `NumeroRes`, `Prefijo`, `FechaRes`, `FechaVenc`, `ConsecutivoIni`, `ConsecutivoFin`, `ClaveTecnica` | Objeto `Resolucion` (¡nota: `ClaveTecnica` debe reemplazarse siempre por marcador sintético!) |
| `itemssalidas` | `i` | `IdItemSal`, `DocRelacionado`, `Item`, `CodProducto`, `Detalle`, `Cant`, `ValorUnit`, `Excluido`, `DtoDir` | Líneas `Items[]` (`ValorUnit` antes de IVA y descuento; `DtoDir` normalizado a fracción `0..1`) |
| `itemssalidas_impuestos` | `isi` | `Id`, `ItemSalId`, `ImpuestoId`, `TarifaAplicada`, `BaseGravada`, `BaseNoGravada`, `ValorImpuesto`, `OrdenAplicacion` | `Items[].impuestos[]` (`ID`, `Porcentaje` en fracción, `ValorBase`, `ValorImp`) |
| `itemssalidas_retenciones` | `isr` | `Id`, `ItemSalId`, `RetencionId`, `BaseItem`, `BaseCalculo`, `TarifaAplicada`, `ValorRetencion` | Suma cabecera / `Items[].retenciones[]` (`05` ReteIVA, `06` ReteFuente, `07` ReteICA) |
| `terceros` | `t` | `IdTercero`, `CodigoContrib`, `EsEstablecimiento`, `Nombre`, `Apellido`, `MMercantil`, `Contacto`, `Email`, `EmailFE`, `Telefono`, `Direccion`, `CodMunicipio` | Código interno (`Codigo`), `MatriculaMercantil`, sucursal comercial (`LocacionComercial`) |
| `contribuyentes` | `c` | `NumeroDocu`, `TipoDocu`, `Persona`, `DV`, `Regimen`, `Nombre`, `Nombre2`, `Apellido`, `Apellido2`, `Contacto`, `Email`, `Telefono`, `Direccion`, `CodMunicipio` | `TipoPersona`, `NIT`, `DV`, razón social / nombres naturales y `LocacionPrincipal` |
| `responsabilidades_contrib` / `responsabilidades_terceros` / `responsabilidades_fiscales` | `rc`, `rt`, `rf` | `Codigo`, `CodigoFE`, `Descripcion` | Lista `responsabilidades_fiscales` (conservar códigos `O-*`, `R-*`, `48`, `49` sin alterar su semántica fiscal) |
| `municipios` | `mp` | `Codigo`, `Municipio`, `Departamento` | Validar códigos DANE reales (`76001`, `11001`, etc.) |
| `anualidades` / `parametros_nacionales` / `parametros_distritales` | `a`, `pn`, `pd` | `AAAA`, `ValorUVT`, bases y porcentajes de ReteIVA/ReteFuente/ReteICA | Solo si el extractor recalcula retenciones con `DianDocumentBuilder` |

---

## 3. Reglas de anonimización determinística (HMAC-SHA256 con sal por corrida)

Para mantener **integridad referencial** entre factura, nota crédito/débito y catálogo sin poder revertir los valores reales:

- Usar una **clave efímera por ejecución** (en memoria, nunca guardada en el archivo de salida): `H = HMAC-SHA256(sal_sesion, valor_normalizado)`.
- Aplicar **siempre** el mismo mapa en memoria `(tipo_entidad, valor_real) -> valor_sintético` sobre emisor, receptor, facturas y notas extraídas en el mismo lote.

| Dato / Columna origen | Regla de anonimización | Ejemplo sintético válido | Qué NO cambiar |
|---|---|---|---|
| **Consumidor final (`222222222222`)** | **Excepción fija:** no anonimizar el NIT `222222222222`; reemplazar nombre/contacto/correo por genérico de consumidor final | `NIT = "222222222222"`, `DV = "7"`, `RazonSocial = "CONSUMIDOR FINAL"` | Identificación DIAN `222222222222` |
| **NIT (`contribuyentes.NumeroDocu` con `Persona = JURIDICA`)** | Mapear a rango ficticio `900100000`–`900999999` mediante `900100000 + (uint32(H) % 899999)` evitando colisiones y NIT reales; **recalcular `DV`** con módulo 11 DIAN (`NitHelper`) | `"900999999"`, `DV: "7"` | Relación entre `documentossalida`, `terceros` y `contribuyentes` |
| **Cédula / documento natural (`Persona = NATURAL`)** | Mapear a rango ficticio `1000100000`–`1099999999` (`1000100000 + (uint32(H) % 99899999)`). Si `TipoDocu` es pasaporte/CE, usar prefijo sintético estable `EXT-XXXXXX` | `"1000458210"` | `TipoIdDian` / `TipoPersona = "NATURAL"` |
| **Códigos internos (`IdTercero`, `MMercantil`)** | Emisor: `"EMP-" + 4 hex(H)`, receptor: `"CLI-" + 4 hex(H)`; matrícula mercantil sintética `"00001"`, `"00002"`... | `"CLI-4A1F"`, `"00001"` | Unicidad entre sucursales de un mismo emisor |
| **Razón social (`c.Nombre`, `NombreComercial`, `t.Nombre`)** | Personas jurídicas: `"EMPRESA EJEMPLO <N> S.A.S."` o `"CLIENTE EJEMPLO <M> S.A.S."` | `"ADQUIRIENTE DE EJEMPLO 14 S.A.S."` | `TipoPersona`, `responsabilidades_fiscales` |
| **Nombres persona natural (`Nombre`, `Nombre2`, `Apellido`, `Apellido2`, `Contacto`)** | Selección determinística desde listas cerradas de prueba (`PrimerNombre`, `SegundoNombre`, `PrimerApellido`, `SegundoApellido`) indexadas por bytes de `H` | `"CARLOS"` / `"ANDRES"` / `"PEREZ"` / `"GOMEZ"` | Campos vacíos deben seguir vacíos si así venían |
| **Teléfonos (`Telefono`)** | Prefijo ficticio reservado: fijos `"601000XXXX"` / `"602000XXXX"`, móviles `"300000XXXX"` según longitud original (4 últimos dígitos derivados de `H`) | `"3000004821"`, `"6020001904"` | Formato numérico sin espacios ni signos |
| **Correos (`Email`, `EmailFE`)** | `<rol>-<6 hex(H)>@ejemplo.example` (usar dominios reservados RFC 2606: `.example` o `.invalid`, nunca dominios reales) | `"compras-9c31a0@adquiriente.example"` | Que `Emisor.EmailContacto` y `Receptor.EmailContacto` sigan siendo correos sintácticamente válidos |
| **Direcciones (`Direccion`)** | Reemplazar nomenclatura de calle por dirección ficticia determinística (`"CALLE <1..99> # <1..99>-<1..99>"`) sin referencias reales a locales/barrios/bodegas | `"CARRERA 14 # 23-45"` | **Conservar `CodMunicipio` de 5 dígitos DANE** (`11001`, `76001`, etc.) y `CodigoDepartamento` para no romper catálogos DIAN |
| **Resolución y `ClaveTecnica`** | `NumeroResolucion` → `"18760000001"` + offset fijo; `ClaveTecnica` → **`"<CLAVE_TECNICA_DE_LA_RESOLUCION>"`** (NUNCA exportar claves técnicas reales); prefijos de habilitación `"SETP"` o ficticios `"EJEM"` | `NumeroResolucion = "18760000001"`, `ClaveTecnica = "<CLAVE_TECNICA_DE_LA_RESOLUCION>"` | Consistencia entre `Resolucion.Prefijo`, `Desde`/`Hasta` y rango sintético de `Numero` |
| **Números y CUFE de documentos (`NumeroDoc`, `Cufe`)** | Reenumerar consecutivos dentro del rango sintético asignado (`SETP990001001`...) y generar `CUFE` de prueba de 96 caracteres hexadecimales con `SHA-384(H)` para referencias entre factura y nota | `"SETP990001001"`, `CUFE` (96 hex ficticios) | Prefijo coincidente con `Resolucion.Prefijo` y vínculo factura ↔ nota |
| **Notas libres (`s.Nota`)** | **Eliminar o reemplazar** texto libre por marcador neutro (`"Nota de ejemplo anonimizada."`) para evitar fuga de nombres, placas, cédulas o teléfonos escritos a mano en observaciones | `"Documento de ejemplo. Datos ficticios."` | Retenciones en notas si existieran se recalculan aparte |
| **Productos (`IdProd`, `Referencia`, `NomProd`, `marcas.Nombre`)** | Opcionalmente reemplazar marca comercial propia por `"Marca N"` y SKU por `"SKU-0001"`, `"SKU-0002"` si el catálogo identifica al cliente; mantener descripción genérica del tipo de bien y conservar precios/impuestos reales | `"SKU-0042"`, `"Camiseta de prueba - Marca 3"` | `Cant`, `ValorUnit`, `DtoDir`, `Excluido`, código y tarifa de impuesto |

---

## 4. Matriz de compatibilidad: `QuantyERP.DianWorker` vs API unificada (`DIAN.Unificado`)

Análisis de código entre `QuantyERP.DianWorker` (`DianRequestJsonBuilder.cs`, `RealDianCloudClient.cs`, `DianQueueWorker.cs`, `DianWorkerOptions.cs`), `WS_V2_HttpClientLib` (`DianDocumentBuilder.cs`, `DianApiClient.cs`) y la API nueva (`DocumentoController.cs`, `TraductorFactura.cs`, `TraductorNota.cs`, [autenticacion.md](autenticacion.md), [documentos-soportados.md](documentos-soportados.md), [referencia-api.md](referencia-api.md)):

### 4.1 Brecha de autenticación, transporte y ciclo asíncrono

| Componente | `QuantyERP.DianWorker` (actual / legado) | Contrato nuevo (`DIAN.Unificado`) | Brecha / Acción requerida |
|---|---|---|---|
| **Autenticación** | `RealDianCloudClient` L34-53 llama `DianApiClient.LoginAsync()` → `POST api/acceso/login` con `{ password, _id, matricula_mercantil }` (`ApiUsuario`, `ApiClave`, `ApiMatriculaMercantil`) y manda header `Authorization: <token>`. Default `ApiScheme = "http"` (`DianWorkerOptions.cs` L27). | `autenticacion.md`: **no existe** `api/acceso/login` ni tokens JWT. Requiere cabecera **`x-api-key: <CLAVE>`** por **HTTPS** en cada petición. | **Bloqueante.** Eliminar `LoginAsync`, reemplazar usuario/clave/matrícula por `ApiKey` (`x-api-key`) y forzar `https`. |
| **Respuesta de `POST /api/documento/agregar`** | `DianApiClient.EnviarJsonAsync` L104 espera `RespStatusDoc` (`IsValid`, `StatusCode`, `UUID`, `ZipKey`, `Errores`). | `DocumentoController.Agregar` L97 responde **HTTP 202** con `DocumentoRegistro` (`Secuencia`, `Numero`, `Estado`, `CUFE`, `Mensaje`, `Errores`, `Pdf`, `SecuenciaCambio`...). | **Bloqueante.** Al no traer `IsValid`, `RespStatusDoc.IsValid` resulta `false` y `RealDianCloudClient.MapResult` L95 marca el documento como **`Rechazado`** recién enviado, además de leer `UUID`/`ZipKey` en lugar de **`CUFE`**. |
| **Seguimiento de estado (`polling`)** | `DianQueueWorker` espera aprobación/rechazo síncronos en `EnviarAsync`. `DianApiClient` aún apunta a rutas heredadas inexistentes: `actualizardoc`, `DescargarDocumento`, `EnviarCorreo`, `consultarpdf`. | Asíncrono: tras `202` (`Estado = "Recibido"`), consultar `GET /api/documento/estado/{numero}` o `GET /api/documento/cambios?desde=N`, descargar archivos en `GET /api/documento/pdf/{numero}`, `xml/{numero}`, `respuesta-dian/{numero}`, y reintentar con `POST reintentar/{numero}` o reenvío a `agregar`. | **Bloqueante.** Desacoplar en `dian_envios` el envío inicial (`ENVIADO`/`Recibido`) de la confirmación (`Aprobado`/`Rechazado`/`Error`) según [erp-y-sdk.md](erp-y-sdk.md). |
| **Validación del set (`X-Envio-Set`)** | Sin soporte en `RealDianCloudClient` ni `DianApiClient`. | `set-de-pruebas-produccion.md`: `X-Envio-Set: true` devuelve `ResultadoSet` (`Estado`, `Mensaje`, `ZipKey`, `Numero`, `FechaUtc`, `CodigoDian`, `Uuid`). | Agregar soporte si el ERP disparará pruebas del set por bloque. |

### 4.2 Diferencias campo a campo en el JSON (`DianRequestJsonBuilder` vs `TraductorFactura` / `TraductorNota`)

| Campo / Regla | Qué envía o omite `QuantyERP` (`DianRequestJsonBuilder` / `DianDocumentBuilder`) | Qué espera `DIAN.Unificado` (`TraductorFactura` / `TraductorNota`) | Estado / Riesgo |
|---|---|---|---|
| **`DocumentoRef` en notas `91` y `92`** | `DianRequestJsonBuilder.BuildAsync` **no llama `ConDocumentoReferencia`**. `AgregarResolucionAsync` L147 sale antes si es `91`/`92`. Queda `DocumentoRef` con `Numero = null` y `CUFE = null` (y `SQL_DOCUMENTO_SALIDA` ni siquiera consulta `dian_envios.Cufe`). | `TraductorNota.cs` L88-90 exige `DocumentoRef.Numero` y `DocumentoRef.CUFE` (CUFE de 96 hex de la factura origen); si faltan responde **400**. También usa `DocumentoRef.Concepto` (opcional, por defecto `"1"` NC / `"4"` ND) y `DocumentoRef.Fecha`. | **Falla 400 en toda NC/ND.** |
| **`Resolucion` en notas `91` y `92`** | `AgregarResolucionAsync` L147-150 hace `return` para `91` y `92`, dejando `Resolucion.NumeroResolucion = null` y `Prefijo = null`. | `TraductorNota.cs` L26, L85 y `TraductorFactura.cs` L107 exigen `Resolucion.NumeroResolucion` numérica válida (`long.Parse`) y leen `Prefijo`, `Desde`, `Hasta`, `FechaExpedicion`, `FechaVencimiento` (solo `ClaveTecnica` es opcional en notas). | **Falla 400/excepción en toda NC/ND.** |
| **Tipos `EX` (`02` exportación) y `TG` (`03` contingencia)** | `DianRequestJsonBuilder.MapTipoDocDian` L344-345 envía `"02"` y `"03"`. | `TraductorFactura.cs` L87 y `DocumentoServicio.cs` L176 solo admiten `"01"`, `"91"` y `"92"`. | **Falla 400** para `EX` y `TG`. |
| **Impuesto ID `'14'` en `SQL_IMPUESTOS_SALIDA`** | `DianDocumentDataService.cs` L184 mapea `WHEN '14' THEN 'INC'` con `ID = "14"`. | `TraductorFactura.cs` L278 busca `TipoImpuesto.GetFromCodigo(i.ID)`. En `Unificado/src/Shared/Metadatos/Tipos.cs` L420-444 el código `"14"` **no existe** (`INC` es `"04"`). | **Falla 400** si existe una línea con `ImpuestoId = '14'`. |
| **`Emisor.TipoIdDian` y `Receptor.TipoIdDian`** | `DianDocumentBuilder.cs` L553 y L591 fijan `"13"` para toda persona natural y `"31"` para jurídica sin leer `contribuyentes.TipoDocu`. | `TraductorFactura.Adquiriente` L210 usa `TipoIdDian` del receptor (por defecto `"31"` si falta). | Cédula de extranjería/pasaporte de naturales sale marcada como `"13"`. |
| **`ReadyDevTime` y cálculo de `impuestos[]` / `retenciones[]`** | `DianRequestJsonBuilder` no lee `itemssalidas_retenciones` en `Items`; pasa `ReteIVA/ReteFuente/ReteICA` + `ParametrosFiscales` y depende de `DocumentoDian.Totalizar()` en `DianDocumentBuilder.Construir(validar: true)` usando `DeveloperKey` de `SecretosLocales`. | `TraductorFactura.cs` L260-332 **no ejecuta `Totalizar()`** ni usa `ReteIVA`/`ValorDoc`/`SubTotal` de cabecera: lee directamente `Items[].BaseGral`, `Items[].impuestos[]` y `Items[].retenciones[]`. | Si `ReadyDevTime` fuera `false` en el worker (p. ej. sin `secretos.json`), `Totalizar()` retorna sin poblar `retenciones[]` ni `ValorImp` y el documento saldría incompleto. |
| **Campos enviados por el ERP que la API ignora** | `DocumentoDian` serializa `ModoEnvio`, `Folio`, `ValorDoc`, `SubTotal`, `BaseGravada`, `BaseNoGravada`, `BaseTotal`, `BaseRetefuente`, `BaseReteIVA`, `BaseReteICA`, `ValorImp`, `IVA`, `ReteIVA`, `ReteFuente`, `ReteICA`, `TotaRetenciones`, `Impuestos`, `Retenciones`, `ImpuestosList`, `Errores`, `TipoDocObj`, `ReadyDevTime`, `Rol`, `IdTipoPersona`, `NombreComercial` (en emisor se reemplaza por `Nombre_o_RazonSocial`), `CargoContacto`, `NombreContacto`, `TipoCodigo`, `SchemeName`, `SchemeAgencyID`, `UsoTipCod`, `ValorIVA`, `ValorTotal`, `OrdenRef` (en `91`/`92`). | Ignorados en la traducción (`TraductorFactura` / `TraductorNota`). | Sin impacto funcional (ver [documentos-soportados.md](documentos-soportados.md#L47)); documentado para limpiar el adaptador futuro. |
