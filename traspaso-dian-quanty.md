# Traspaso de Contexto: Integración DIAN.Unificado + QuantyERP

**Fecha de generación:** 2026-11-22  
**Fecha límite objetivo del proyecto:** 2026-11-28  
**Idioma obligatorio:** Responder y escribir resúmenes de comandos siempre en **español**.

---

## 1. Roles y Canal de Coordinación

- **Antigravity (tú):** Dueño de toda la integración en **`QuantyERP`** (`C:\Users\Eduardo\source\repos\SISTEMAS360\QuantyERP_Group\QuantyERP`, rama actual `codex/fv-costo-ventas`).
- **Claude:** Dueño de **`DIAN.Unificado`**, `Admin.Web`, servidor `quanty-lab` y envíos reales a DIAN.
- **Canal de comunicación (`ntfy`):**
  - URL: `https://ntfy.sh/sis360-dian-e183b87d8fc9e633`
  - **Último timestamp leído/publicado:** usar `since=1795328456` al consultar o iniciar el listener en segundo plano.
  - Nota de red en Windows: el sandbox bloquea DNS hacia `ntfy.sh`, por lo que `Invoke-RestMethod` / `curl.exe` contra `ntfy.sh` debe intentar sandbox primero y si falla usar `BypassSandbox: true`.
  - Mantener mensajes en `ntfy` por debajo de `3,200` bytes UTF-8.

---

## 2. Reglas Estrictas de Seguridad y Operación

1. **NO modificar código de `DIAN.Unificado`** (cualquier ajuste ahí se pide a Claude por `ntfy`). Sí puedes leerlo como referencia.
2. **NO enviar documentos reales a la DIAN** ni simular envíos reales sin autorización explícita de Eduardo.
3. **Bases de datos MySQL (MARLIT GROUP) vía `DatabaseProvider.CodexCli.dll`:**
   - Binario: `C:\Users\Eduardo\source\repos\SISTEMAS360\COMMON\Bibliotecas\DatabaseProvider\DatabaseProvider.CodexCli\bin\Release\net8.0\DatabaseProvider.CodexCli.dll`
   - **`DEV` (`MYSQL_DEV_MARLIT`, esquema `marlit2026`, IP `26.153.35.249`):** ÚNICA base permitida, y **solo lectura (`validate` / `verify` / `schema`)**.
   - **`TEST` (`MYSQL_TEST_MARLIT`) y `QA` (`MYSQL_QA_MARLIT`):** **PROHIBIDO** tocarlas (QA contiene datos reales de usuarios).
   - **Nunca exponer PII** (nombres reales, NIT/cédulas reales, correos, teléfonos, direcciones reales ni secretos/`ApiKey`). Reportar solo conteos o datos anónimos de prueba.
4. **Git:** NO ejecutar `git commit`, `git push` ni despliegues salvo que Eduardo indique explícitamente la rama destino y lo autorice (en `QuantyERP` se usan claves SSH o se delega a Claude cuando Eduardo lo pida).

---

## 3. Lo Ya Completado y Verificado en `QuantyERP` (sin commit aún)

Compilación y pruebas verificadas tras el último cambio:
- `dotnet build ..\QuantyERP_Group\QuantyERP\QuantyERP.DianWorker\QuantyERP.DianWorker.csproj --no-restore --no-dependencies -v minimal`: **0 errores, 0 advertencias**.
- `dotnet test ..\QuantyERP_Group\QuantyERP\QuantyERP.Architecture.Tests\QuantyERP.Architecture.Tests.csproj --verbosity minimal`: **124/124 pruebas superadas**, 0 con error, 0 omitidas.

### Cambios ya implementados en código y pruebas:
1. **Fase 1 (FE `01`, NC `91`, ND `92` + configuración HTTP):**
   - `QuantyERP.DianWorker/DianWorkerOptions.cs` y `appsettings.json`: `ApiBaseUrl`, `ApiKey` (`x-api-key`), `UsarSetPruebas` (`X-Envio-Set: true`), `ApiTimeoutSeconds` (`200`), `EstadoPollingSeconds` (`3`), `EstadoPollingMaxIntentos` (`10`).
   - `QuantyERP.DianWorker/RealDianCloudClient.cs`: cliente HTTP directo contra `DIAN.Unificado` (`POST /api/documento/agregar`, polling `GET /api/documento/estado/{numero}` para `202 Recibido`, `ResultadoSet` cuando `X-Envio-Set: true`).
   - `QuantyERP.DianWorker/DianQueueWorker.cs`: preserva `result.MensajeRespuesta` en `MarcarErrorAsync`.
   - `QuantyERP.DataModels/Reportes/Views/ReporteSalidaImpuesto.cs` y `DianDocumentoSalidaData.cs`: `IncluidoEnPrecio`, `DocRefCufe`, `Motivo`.
   - `QuantyERP.DianWorker/DianRequestJsonBuilder.cs`: `DesglosarValorUnitario(...)` y validación/referencia obligatoria `DocRefNumero`/`DocRefCufe`/`DocRefFecha` para `91`/`92`.
   - `QuantyERP.DataServices/Dian/Services/DianDocumentDataService.cs`: normalización INC `'02'`/`'14'` -> `'04'`, resolución por `TipDoc`, `DocRef*` en `SQL_DOCUMENTO_SALIDA`.
2. **Punto 1 — Entradas (`DSNO`, `NSDS`, `DC`, `NCE`) y Notas de Ajuste (`NATP` `94`, `NSDS` `95`):**
   - `DianDocumentDataService.cs`: fallback completo a `SQL_DOCUMENTO_ENTRADA`, `SQL_ITEMS_ENTRADA` y `SQL_IMPUESTOS_ENTRADA` (`documentosentrada`, `itemsentradas`, `itemsentradas_impuestos`, `itemsentradas_retenciones` para `05`/`06`/`07`), verificado en vivo contra `MYSQL_DEV_MARLIT` (**35/35 columnas** y ejecución limpia). En vivo en DEV ya existe `DSNO` en `comprobantes` (`marlit2026.sql` está desactualizado); `NATP` y `NSDS` aún no existen, y `resoluciones` activas para esos comprobantes tiene `0` filas en DEV.
   - `DianMetaDatosDeBuilder.cs` y `DianRequestJsonBuilder.cs`: builders `BuildPosTiqueteJson` (`20`), `BuildNotaAjustePosJson` (`94`, `CUDE-SHA384`), `BuildDocumentoSoporteJson` (`05`) y `BuildNotaAjusteDocumentoSoporteJson` (`95`, `CUDS-SHA384`, `BaseImponible=0` en líneas sin impuesto).
3. **Fase 3 (Transporte Nómina `102`/`103` y Eventos RADIAN `030`–`034`):**
   - `RealDianCloudClient.cs`: enrutamiento de nómina (`NE`/`NI`/`NM`/`NA`/`102`/`103` -> `api/v1/Nomina/EnviarNE`, aceptando `00`/`SP00` y `99`/`SP99`) y eventos RADIAN (`030`–`034`, `EV030`–`EV034`, `RADIAN_030`–`RADIAN_034` -> `api/v1/Eventos/*`).
   - `QuantyERP.DianWorker/DianEventoRadianBuilder.cs`: generador JSON de `EventoRadian` (validando `UUID` y `Causa` obligatoria en `031`).
4. **Persona Natural (`ResolverNombresPersonaNatural`):**
   - Verificado en vivo en `MYSQL_DEV_MARLIT`: de **2,456** contribuyentes `NATURAL`, **2,434 (99.1%)** tienen `Apellido` vacío y el nombre completo en un solo campo `Nombre`; solo **22** tienen `Apellido` separado.
   - `DianRequestJsonBuilder.ResolverNombresPersonaNatural` parte `Nombre` cuando `Apellido` está vacío (patrón `"APELLIDO1 [APELLIDO2] NOMBRE1 [NOMBRE2...]"`), aplicado en `DianRequestJsonBuilder.cs` (FE/NC/ND) y `DianMetaDatosDeBuilder.cs` (`BuildPersonaNatural` con `DocumentoIdentificacion` `CC`/`13` + `BuildNombreTercero`).

---

## 4. Cotejo Final Realizado y Siguientes Tareas Inmediatas

Claude ya redesplegó `quanty-lab` con `97affe8` (fix Persona Natural en FE) y documentó las secciones `8.1`/`8.2` de `Unificado/docs/integracion/guia-para-quantyerp.md` más los **44 campos obligatorios reales** en `Unificado/docs/integracion/documentos-soportados.md` (líneas 250–329).

### Resultado del cotejo ya hecho contra `QuantyERP.DianWorker`:
- **POS (`20`) y Nota de ajuste POS (`94`):** **100% cubiertos** en `DianMetaDatosDeBuilder` (`BaseGeneral`, `Porcentaje` en escala `19.00`, `Totales` sin cargos/descuentos/anticipo globales, `DetallePago`, `Emisor.DatosComerciales`, `Emisor.PrefijoRangoNumeracion`, `Adquiriente.DireccionComercial`, `CodigoCliente`, `NombreComercial`, `InformacionCaja`, `ConceptoAjuste`, `DocumentoReferencia` con `20` + `CUDE-SHA384`).
- **Documento soporte (`05`) y Nota de ajuste DS (`95`):** **100% cubiertos** (`Items[].PeriodoFacturacionLine`, `Adquiriente.TipoIdentificacion` y `RazonSocial`, `DetallePago.Metodo`, `Proveedor.DireccionFisica`, `ConceptoAjuste`, `DocumentoReferencia` con `05` + `CUDS-SHA384`, y `Totales.BaseImponible = 0` en líneas sin impuesto).
- **Eventos RADIAN (`030`–`033`):** cubiertos en `RealDianCloudClient` y `DianEventoRadianBuilder` (tener en cuenta que en `DIAN.Unificado` el evento `034` responde `109`).

### Pendientes inmediatos para el nuevo hilo:
1. **Aplicar `DV = "0"` para Persona Natural (`guia-para-quantyerp.md` sección `8.1`):**
   - En `DianRequestJsonBuilder.AgregarPersona` (emisor y receptor `NATURAL`), garantizar `DV = "0"` cuando `tercero.DV` venga vacío (y verificar en `MYSQL_DEV_MARLIT` si `contribuyentes.DV` para `NATURAL` viene vacío o nulo). Agregar prueba unitaria en `DianRequestJsonBuilderTests`.
2. **Verificar dos detalles de la lista 10/auditoría antes de pruebas reales:**
   - En `guia-para-quantyerp.md:227` vs `DianRequestJsonBuilder.AgregarResolucionAsync` (`91`/`92`): confirmar si `DianDocumentBuilder` requiere `Resolucion` además de `DocumentoRef` en notas crédito/débito `91`/`92`.
   - En `documentos-soportados.md:266`: verificar cómo mapea `RealDianCloudClient` el código `"2"` vs `"00"` cuando `EnviarDE`/`EnviarDSNO` corre en habilitación (con y sin `X-Envio-Set: true`).
3. **Coordinar con Claude por `ntfy` (`since=1795328456`):**
   - Informarle los resultados del cotejo de los 44 campos + ajuste `DV="0"` y mantener activo el listener en segundo plano.
   - Nota: La factura real `SETP990000028` sigue en espera de que **Eduardo** ejecute el `SimuladorErp` con su `DIAN_API_KEY` de MARLIT; no enviarla ni inventar claves.
4. **Pendiente posterior en DEV (`QuantyERP`):**
   - Revisar/preparar (solo cuando Eduardo lo indique, sin tocar `TEST`/`QA`) los comprobantes/flujos/resoluciones de `TV`, `DSNO`, `NATP` y `NSDS` en `QuantyERP`.
   - Preguntas abiertas de `guia-para-quantyerp.md:237` (solo para consultar con Claude/Eduardo cuando corresponda): `DocumentoReferencia` vs `DocumentoRef`, código INC (`04` vs `14`), nota `95` en `EnviarDE`, fecha de prueba real con cliente de habilitación.
