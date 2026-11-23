# Guía de Coordinación para IAs: AgentBridge + MCP Agent Mail (`DIAN` ↔ `QuantyERP`)

> **Uso rápido:** Esta guía está diseñada para entregársela directamente a **Claude**, **Codex** o cualquier otra IA conectada por **MCP** para coordinar trabajo en tiempo real con **Antigravity** sin quemar tokens en *polling* ni pegar historiales completos.

---

## 1. Qué es AgentBridge y qué reemplaza

- **Bus compartido (`mcp-agent-mail`):** servidor MCP en `http://127.0.0.1:8765/mcp/` con buzones por agente, hilos (`thread_id`) y bloqueos de archivos (*file reservations*).
- **Orquestador (`AgentBridge`):** servicio local en `http://127.0.0.1:3847` que vigila los buzones sin gastar tokens de LLM y despierta automáticamente por CLI a los agentes autónomos cuando les llega un mensaje nuevo, reutilizando su `session_id` (`--resume`) para aprovechar *prompt caching*.
- **Reemplazo de `ntfy.sh`:** para coordinar `DIAN` y `QuantyERP` en este equipo se usa **`mcp-agent-mail`** en lugar de `ntfy.sh`.

### Regla de oro (0 tokens en espera)
**NUNCA** hagas *polling* ni bucles con `sleep` esperando respuesta de otra IA. Envía tu mensaje por `mcp-agent-mail` y termina tu turno. `AgentBridge` despertará al destinatario automáticamente cuando corresponda.

---

## 2. Identidades y Proyecto de Coordinación

`mcp-agent-mail` valida nombres estilo **Adjetivo+Sustantivo**. Usa exactamente estas identidades ya registradas y configuradas con política de contacto `open`:

| Identidad MCP (`agent_name`) | Quién es | Alcance / Responsabilidad | Modo |
| :--- | :--- | :--- | :--- |
| **`AmberFox`** | **Claude** (Claude Code) | Terminar proyecto **`DIAN`** (`DIAN.Unificado`, `Admin.Web`, `quanty-lab`, docs e integraciones del lado DIAN) | Autónomo vía `AgentBridge` o sesión interactiva |
| **`CobaltHawk`** | **Antigravity** | Integración de `DIAN.Unificado` dentro de **`QuantyERP`** (`QuantyERP.DianWorker`, `DianDocumentDataService`, builders y pruebas) | Interactivo con Eduardo |
| **`SilverOwl`** | **OpenAI Codex** | Revisor / ejecutor de pruebas o apoyo técnico opcional | Autónomo vía `AgentBridge` |
| **`JadeWolf`** | **Eduardo** (Humano / Dashboard) | Aprobaciones, credenciales, despliegues y supervisión | Tablero `http://127.0.0.1:3847` |

### `project_key` único para esta prueba
Para que `AgentBridge` despierte a `AmberFox` directamente en el repo **`DIAN`** y todos compartan el mismo historial, usa **siempre** el identificador canónico (**slug**) como `project_key` en todas las llamadas MCP de coordinación (las rutas Windows con `\` pueden ser rechazadas por el servidor):

```text
c-users-eduardo-source-repos-sistemas360-dian
```

*(Si eres una IA nueva que aún no tiene configurado el servidor MCP `mcp-agent-mail`, conéctate por Streamable HTTP a `http://127.0.0.1:8765/mcp/` usando el header `Authorization: Bearer <MCP_AGENT_MAIL_TOKEN>` ubicado en `C:\Users\Eduardo\source\repos\SISTEMAS360\AgentBridge\.env` — nunca escribas ese token en archivos ni correos —, registra un alias Adjetivo+Sustantivo nuevo con `register_agent` si no eres `AmberFox`/`SilverOwl`/`CobaltHawk`, y llama `set_contact_policy` con `policy: "open"`).*

---

## 3. Herramientas MCP exactas

Usa únicamente los nombres y campos exactos expuestos por `mcp-agent-mail`:

### Leer mensajes pendientes (`fetch_inbox`)
```json
{
  "project_key": "c-users-eduardo-source-repos-sistemas360-dian",
  "agent_name": "AmberFox",
  "unread_only": true,
  "include_bodies": true,
  "mark_read": false,
  "limit": 20
}
```

### Enviar mensaje nuevo o iniciar/continuar hilo (`send_message`)
```json
{
  "project_key": "c-users-eduardo-source-repos-sistemas360-dian",
  "sender_name": "AmberFox",
  "to": ["CobaltHawk"],
  "thread_id": "dian-quanty-sync-1",
  "subject": "Contrato endpoint /api/documento/agregar listo",
  "body_md": "## Estado\n...",
  "auto_contact_if_blocked": true
}
```

### Responder a un mensaje recibido (`reply_message`)
> Nota: `message_id` es un **entero** (no string).
```json
{
  "project_key": "c-users-eduardo-source-repos-sistemas360-dian",
  "message_id": 12,
  "sender_name": "AmberFox",
  "to": ["CobaltHawk"],
  "body_md": "..."
}
```

### Marcar mensaje como leído (`mark_message_read`)
```json
{
  "project_key": "c-users-eduardo-source-repos-sistemas360-dian",
  "agent_name": "AmberFox",
  "message_id": 12
}
```
*(Cuando `AgentBridge` despierta automáticamente a `AmberFox` o `SilverOwl`, marca el mensaje leído por ti. Si consultas manualmente en una sesión interactiva, márcalo leído al procesarlo).*

### Reservar archivos antes de editar (`file_reservation_paths` / `release_file_reservations`)
Si dos agentes pueden tocar archivos cercanos dentro del mismo repositorio, reserva las rutas antes de editar y libéralas al terminar:
```json
{
  "project_key": "c-users-eduardo-source-repos-sistemas360-dian",
  "agent_name": "AmberFox",
  "paths": ["Unificado/src/**"],
  "exclusive": true,
  "ttl_seconds": 1800,
  "reason": "Cierre de endpoints DIAN"
}
```

---

## 4. Reglas de Ahorro de Tokens y Control de Flujo

1. **Idioma:** Escribe resúmenes, mensajes MCP y explicaciones en **español**.
2. **Mensajes compactos (<250 palabras):** Nunca pegues archivos completos, logs largos ni historiales de chat. Usa siempre esta plantilla de **Handoff Packet**:
   ```markdown
   ## Estado
   [Qué quedó hecho o validado]

   ## Cambios / Contratos
   - Ruta(s) o endpoint/DTO modificado (solo firmas/campos relevantes)

   ## Verificación
   - Comando ejecutado y resultado exacto (ej. `dotnet build`: 0 errores; `dotnet test`: X/X ok)

   ## Siguiente acción
   - Para `@CobaltHawk` (o `@AmberFox`): [qué debe hacer exactamente]
   ```
3. **Nunca envíes acuses vacíos** (`"ok"`, `"recibido"`, `"gracias"`): cada correo innecesario consume un turno (*hop*).
4. **Límite de rebotes (*Circuit Breaker*):** `AgentBridge` limita los hilos a `maxHops` (por defecto `4`) y un presupuesto diario máximo. Si un hilo se detiene por `hop_limit`, Eduardo lo revisa y puede dar `+4 Hops` desde el Dashboard.
5. **Regla estricta de `[DONE]` y `[NEEDS_HUMAN]`:**
   - Pon `[DONE]` **en su propia línea final** (tanto en tu correo MCP como en tu respuesta final) **SOLO** cuando toda la tarea del hilo esté terminada y nadie más deba actuar.
   - **NUNCA** pongas `[DONE]` en una línea propia cuando estés entregando trabajo pendiente a otro agente.
   - Pon `[NEEDS_HUMAN]` **en su propia línea final** si necesitas autorización de Eduardo, credenciales o una decisión de negocio.

---

## 5. División de Trabajo para la Prueba: `DIAN` + `QuantyERP`

### Fronteras de código (no cruzar sin acordarlo antes)
- **Claude (`AmberFox`) — Proyecto `DIAN`:**
  - Dueño de `C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN` (`Unificado/`, `docs/integracion/`, pruebas de `DIAN.Unificado`).
  - Puede **leer** `C:\Users\Eduardo\source\repos\SISTEMAS360\QuantyERP_Group\QuantyERP` si necesita verificar cómo `QuantyERP` consume un endpoint, pero **no edita** `QuantyERP`; solicita el ajuste a `CobaltHawk`.
- **Antigravity (`CobaltHawk`) — Integración `QuantyERP`:**
  - Dueño de `C:\Users\Eduardo\source\repos\SISTEMAS360\QuantyERP_Group\QuantyERP` (`QuantyERP.DianWorker`, `DianDocumentDataService`, builders y pruebas).
  - **No edita** código de `DIAN.Unificado`; solicita cambios o aclaraciones de contrato a `AmberFox` por `mcp-agent-mail`.

### Reglas de seguridad heredadas de `traspaso-dian-quanty.md`
1. **No enviar documentos reales a la DIAN** ni simular envíos productivos sin autorización explícita de Eduardo.
2. **Bases de datos MySQL (`MARLIT GROUP`):** `MYSQL_TEST_MARLIT` y `MYSQL_QA_MARLIT` están **PROHIBIDAS**; `MYSQL_DEV_MARLIT` solo lectura cuando aplique.
3. **Cero PII o secretos en correos MCP:** nunca incluir NITs/cédulas reales, correos, teléfonos, `ApiKey`, certificados `.p12/.pfx`, contraseñas ni el token de `mcp-agent-mail`.
4. **Git / Despliegues:** no ejecutar `git commit`, `git push` ni desplegar en servidor sin autorización explícita de Eduardo.

---

## 6. Cómo arrancar la prueba paso a paso

1. Verifica que `mcp-agent-mail` (`127.0.0.1:8765`) y `AgentBridge` (`127.0.0.1:3847`) estén activos (si no, ejecuta `powershell -ExecutionPolicy Bypass -File C:\Users\Eduardo\source\repos\SISTEMAS360\AgentBridge\scripts\start-local.ps1`).
2. Si abres **Claude** manualmente (en terminal dentro de `C:\Users\Eduardo\source\repos\SISTEMAS360\DIAN`), dale esta instrucción inicial:
   > *"Lee `@GUIA-AGENTBRIDGE-IA.md`, `@README.md` y `@traspaso-dian-quanty.md`. Tu identidad en `mcp-agent-mail` es `AmberFox` y el `project_key` es el slug `c-users-eduardo-source-repos-sistemas360-dian`. Revisa tus pendientes en `DIAN.Unificado`, termina lo que falte (compilando y probando) y cuando tengas un contrato listo o necesites que Antigravity (`CobaltHawk`) actualice `QuantyERP`, envíale un paquete corto por `mcp-agent-mail` en el hilo `dian-quanty-sync-1`."*
3. O bien despacha la tarea desde el Dashboard (`http://127.0.0.1:3847`) seleccionando proyecto **`DIAN`** → flujo **`Directo a ClaudeDev (AmberFox)`** para que `AgentBridge` lo ejecute en segundo plano y luego `CobaltHawk` (Antigravity) recoja los mensajes en su buzón.
