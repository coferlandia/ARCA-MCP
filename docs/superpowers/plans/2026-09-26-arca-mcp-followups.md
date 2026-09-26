# dcArca — Estado y pendientes (handoff para continuar con Codex)

> Generado el 2026-09-26. Repo: `dcARCA` (facturación electrónica argentina AFIP/ARCA). Rama: `main`, commit `3f080d5` (merge de `feat/arca-mcp-server`). Este documento resume qué está hecho y qué falta, para retomar el trabajo con otro agente sin depender del historial de esta conversación.

## 1. Qué está hecho

### 1.1 `dcArca.Core` (librería, sin cambios en esta sesión salvo lo ya mergeado antes)
- Librería .NET 10 multiplataforma (compila y corre en Linux) para AFIP/ARCA: WSFEv1 (facturación) + padrón (consulta CUIT).
- 7 tests unitarios en `dcArca.Core.Tests`, todos verdes.
- `dcArca.TestApp` es WinForms — **no compila en Linux** (`NETSDK1100`, falla preexistente y esperada, no relacionada con nada de esto).

### 1.2 `dcArca.Service` (nuevo — API REST + CLI dual-modo)
- Un solo ejecutable ASP.NET Core (`net10.0`) que reemplaza la necesidad de correr `dcArca.TestApp` (WinForms) en servidores Linux.
- Sin argumentos (o `serve`) → levanta API REST (`/health`, `/api/facturas`, `/api/facturas/ultimo-autorizado/{tipo}`, `/api/comprobantes/{numero}/{tipo}`, `/api/padron/{cuit}`, `/api/condiciones-iva`).
- Con argumentos → corre un comando puntual y termina (`facturar`, `ultimo-autorizado`, `consultar`, `padron`, `condiciones-iva`).
- `dcArca.Service/Dockerfile` (multi-stage build) + `.dockerignore` en la raíz. Instrucciones de uso en el `README.md` (sección "🐳 Docker").
- Verificado: build de imagen Docker OK, contenedor en modo API responde `200 {"status":"ok"}` en `/health`, modo CLI corre y devuelve JSON de error controlado sin certificado real.
- Commit: `aff7637`.

### 1.3 `dcArca.McpServer` (nuevo — servidor MCP con OAuth)
Implementa la Parte B del plan `docs/superpowers/plans/2026-09-24-arca-mcp-server.md` (Tasks 6–10, todas hechas y revisadas con doble review — spec compliance + code quality — vía `superpowers:subagent-driven-development`).

- **Patrón resource-server**: NO emite tokens propios. Valida JWT Bearer emitidos por un Authorization Server externo (`Jwt:Authority`/`Jwt:Audience` en config). Ver `dcArca.McpServer/Program.cs`.
- Middleware: `AddAuthentication` + `AddJwtBearer` (issuer/audience/lifetime/signing-key validation todos en `true`, sin defaults débiles) + `.AddMcp()` (resource metadata OAuth) + `MapMcp().RequireAuthorization()`.
- `ArcaTools.cs`: 5 MCP tools, passthrough puro a `dcArca.Core` (sin lógica de negocio propia):
  - `ConsultarUltimoComprobante`, `ConsultarComprobante`, `SolicitarCae`, `ConsultarCondicionesIva`, `ConsultarPadron`.
  - `tipoDocReceptor`/`docTipo` usan el enum `dcTipoDocumento` (no `int` crudo) — mejora aplicada en review.
- `dcArca.McpServer.Tests/McpAuthTests.cs`: test de integración que prueba que el endpoint MCP devuelve 401 sin token. Usa `WebApplicationFactory<Program>` + `appsettings.Testing.json` (config falsa, cert placeholder). El placeholder `.pfx` se autogenera en el constructor del test (no se commitea binario) — ver el comentario en el código, resuelve un bug real que rompía el test en un clone limpio.
- Commits: `dd96243`, `e2d2eb9`, `4300b55`, `a9b8c42`, `0b35089`, `4d1326c`, `68623d9`.
- Verificado (re-ejecutado desde limpio, bin/obj wipeados): `dcArca.Core.Tests` 7/7, `dcArca.McpServer.Tests` 1/1, ambos proyectos compilan con 0 warnings/errores.

### Comandos de verificación (todos desde la raíz del repo)
```bash
dotnet build dcArca.Core/dcArca.Core.csproj
dotnet build dcArca.Service/dcArca.Service.csproj
dotnet build dcArca.McpServer/dcArca.McpServer.csproj
dotnet test dcArca.Core.Tests/dcArca.Core.Tests.csproj
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj
# dcArca.TestApp NO compila en Linux (WinForms) — es esperado, no repararlo.
```

---

## 2. Pendientes (en el orden acordado con el usuario)

### 2.1 `SolicitarCae` no soporta Notas de Crédito/Débito ni Facturas de Servicios — ✅ HECHO (commit `7ad7ec2`)

Se agregaron los parámetros opcionales (`fechaServicioDesde/Hasta`, `fechaVencimiento`, `tipoComprobanteAsociado`, `puntoVentaAsociado`, `numeroAsociado`, `periodoAsociadoDesde/Hasta`) mapeados directo a `dcFacturaRequest`. Se dejaron afuera deliberadamente `CbteAsociadoCuit`/`CbteAsociadoFecha` (opcionales en Core, no requeridos por la regla 10197). Build y ambos test suites verdes. Detalle original de la tarea abajo, dejado para referencia.


**Archivo:** `dcArca.McpServer/ArcaTools.cs`, método `SolicitarCae` (líneas 37-66).

**El problema:** el método solo expone estos parámetros escalares: `tipoComprobante`, `numeroComprobante`, `concepto`, `cuitReceptor`, `tipoDocReceptor`, `condicionIvaReceptor`, `importeNeto`, `importeIva`, `importeTotal`, `fechaComprobante`. Pero `dcFacturaRequest` (en `dcArca.Core/Models/dcFacturaRequest.cs`) tiene más campos que son **obligatorios** en ciertos casos, y hoy quedan siempre `null`:

- Si `Concepto` es `Servicios` (2) o `ProductosYServicios` (3) → hacen falta `FechaServicioDesde`, `FechaServicioHasta`, `FechaVencimiento` (propiedades `string?` en `dcFacturaRequest`, líneas ~72-85).
- Si `TipoComprobante` es una Nota (`NotaDebitoA/B/C/M`, `NotaCreditoA/B/C/M` — ver `EsNota()` en `dcFacturaRequest.cs:141`) → regla AFIP 10197: hace falta informar `CbteAsociadoTipo`/`CbteAsociadoPtoVta`/`CbteAsociadoNro` (+ opcionalmente `CbteAsociadoCuit`/`CbteAsociadoFecha`) **o alternativamente** `PeriodoAsocDesde`/`PeriodoAsocHasta`. Ver `CumpleReglaNotas10197()` en `dcFacturaRequest.cs:149`.

Hoy, si un cliente MCP pide una Nota o una factura de Servicios vía `SolicitarCae`, `dcWsfeClient.FECAESolicitarAsync` (que sí valida esto — no hay que tocar esa lógica) va a devolver `Success=false` con un error de validación, y el tool no tiene forma de que el LLM lo arregle porque no existen los parámetros.

**Qué hacer:** agregar a `SolicitarCae` los parámetros opcionales que faltan (nullable, con `[Description(...)]` clara indicando cuándo son obligatorios) y mapearlos al `dcFacturaRequest` que ya se construye en el método. No tocar `dcArca.Core` (`dcFacturaRequest`, `dcWsfeClient` ya están completos y testeados) — esto es 100% cambio en `ArcaTools.cs`.

Sugerido (ajustar descripciones a gusto):
```csharp
[Description("Fecha de servicio desde, formato YYYYMMDD (obligatorio si concepto es Servicios o ProductosYServicios).")] string? fechaServicioDesde,
[Description("Fecha de servicio hasta, formato YYYYMMDD (obligatorio si concepto es Servicios o ProductosYServicios).")] string? fechaServicioHasta,
[Description("Fecha de vencimiento de pago, formato YYYYMMDD (obligatorio si concepto es Servicios o ProductosYServicios).")] string? fechaVencimiento,
[Description("Tipo del comprobante asociado (obligatorio en Notas de Crédito/Débito, salvo que se use período asociado).")] int? cbteAsociadoTipo,
[Description("Punto de venta del comprobante asociado.")] int? cbteAsociadoPtoVta,
[Description("Número del comprobante asociado.")] long? cbteAsociadoNro,
[Description("Fecha desde del período asociado, alternativa a comprobante asociado (formato YYYYMMDD).")] string? periodoAsocDesde,
[Description("Fecha hasta del período asociado, alternativa a comprobante asociado (formato YYYYMMDD).")] string? periodoAsocHasta,
```
(evaluar si conviene mantener `CbteAsociadoCuit`/`CbteAsociadoFecha` también — son opcionales según el código de Core, se puede omitir por ahora si se quiere el mínimo viable).

**Reusar el patrón de review ya aplicado en esta rama** (subagent implementador → spec reviewer → code quality reviewer) si se sigue con `superpowers:subagent-driven-development`, o simplemente implementar directo si se prefiere ir más rápido — es un cambio acotado a un archivo.

### 2.2 Scopes OAuth decorativos — sin autorización por scope — ✅ HECHO (commit `b9b19f8`)

Se agregó una policy `"ArcaFacturar"` (valida el claim `scope` del JWT, split por espacios) y se aplicó `AddAuthorizationFilters()` + `[Authorize(Policy = "ArcaFacturar")]` sobre `SolicitarCae`, usando el soporte nativo del SDK de MCP para autorización por-tool (`Microsoft.Extensions.DependencyInjection.HttpMcpServerBuilderExtensions.AddAuthorizationFilters`). Comportamiento verificado empíricamente (no asumido): un caller sin el scope `arca:facturar` no ve `solicitar_cae` en `tools/list`, y si lo llama igual por nombre recibe un error JSON-RPC `-32600 "Access forbidden: This tool requires authorization."` (HTTP 200, el rechazo vive en el envelope JSON-RPC, no es un 403 HTTP) — no revela que el tool existe. Los tools de solo lectura siguen accesibles con cualquier scope autenticado. 5 tests nuevos/existentes en `dcArca.McpServer.Tests`, todos verdes. Detalle original de la tarea abajo, dejado para referencia.


**Archivo:** `dcArca.McpServer/Program.cs` (líneas 55-62 y 78).

**El problema:** `ScopesSupported = ["arca:facturar", "arca:consultar"]` se anuncia en el resource metadata (línea 60), pero `MapMcp().RequireAuthorization()` (línea 78) es una política única — solo exige "autenticado", no valida qué scope trae el token. Cualquier token válido (aunque solo tenga `arca:consultar`) puede hoy llamar a `SolicitarCae`, que emite una factura real en AFIP con efecto legal/fiscal real.

**Qué hacer:** separar los tools de solo lectura (`ConsultarUltimoComprobante`, `ConsultarComprobante`, `ConsultarCondicionesIva`, `ConsultarPadron`) de la acción de escritura (`SolicitarCae`), exigiendo el scope `arca:facturar` específicamente para esta última. Esto requiere:
1. Definir políticas de autorización basadas en scope (`builder.Services.AddAuthorization(options => options.AddPolicy("RequireFacturarScope", policy => policy.RequireClaim("scope", "arca:facturar")))` — el nombre exacto del claim de scope depende de cómo el Authorization Server real emita el JWT; con IdPs estándar (Auth0/Entra/Keycloak) suele ser el claim `scope` con valores separados por espacio, a veces hace falta un `IAuthorizationHandler` custom que parsee el string en vez de `RequireClaim` directo — **investigar esto primero** antes de implementar, puede no ser tan simple como un `RequireClaim`).
2. Ver cómo el SDK de MCP para C# (`ModelContextProtocol.AspNetCore`) permite aplicar autorización per-tool — puede que `[McpServerTool]` soporte algún atributo de autorización, o puede que haya que envolver la llamada a `SolicitarCae` con una verificación manual de `HttpContext.User` dentro del método (inyectando `IHttpContextAccessor` o similar). **Revisar la documentación/sample `ProtectedMcpServer` del SDK** (mencionado en el plan original, `docs/superpowers/plans/2026-09-24-arca-mcp-server.md` línea 7) para ver si ya cubre este caso de scopes por-tool.

Este es más una tarea de investigación + diseño que un cambio mecánico — no hay un snippet "correcto" ya escrito como en el punto 2.1.

### 2.3 Authorization Server externo no implementado

**Estado:** deliberadamente fuera de alcance del plan original (`docs/superpowers/plans/2026-09-24-arca-mcp-server.md`, sección "Fuera de alcance (deliberado)"). `dcArca.McpServer` asume que ya existe un IdP externo (Auth0, Entra ID, Keycloak, o cualquiera que hable OIDC/JWT) emitiendo los tokens que los clientes MCP van a usar.

**Qué falta decidir (no es solo código, es una decisión de arquitectura/infraestructura):**
- Qué IdP se va a usar en la práctica (Cadencia ya tiene uno elegido, o hay que elegir uno).
- Cómo se van a emitir/gestionar los scopes `arca:facturar`/`arca:consultar` en ese IdP (esto se conecta directo con el punto 2.2 — sin este IdP, 2.2 no se puede probar end-to-end con tokens reales, solo con tokens fabricados a mano en tests).
- `Jwt:Authority`/`Jwt:Audience` en `dcArca.McpServer/appsettings.example.json` quedan como placeholders hasta que esto se resuelva.

**Recomendación:** antes de escribir código para este punto, confirmar con el usuario/equipo qué IdP usar — es una pregunta de producto/infra, no algo para que un agente decida solo.

---

## 3. Notas de contexto útiles para quien retome esto

- **Workflow usado en esta sesión:** `superpowers:subagent-driven-development` (un subagente implementador + subagente revisor de spec + subagente revisor de calidad, por cada tarea, dentro de un git worktree aislado en `.worktrees/<nombre>`, rama `feat/<nombre>`, luego merge a `main` y limpieza del worktree). No es obligatorio repetir este proceso, pero si se quiere mantener el mismo nivel de revisión, es el patrón a seguir.
- **Convención de branches/worktrees en este repo:** `.worktrees/<nombre-corto>` con rama `feat/<nombre>` o `fix/<nombre>`.
- **Gitignore ya cubre** `appsettings.json`/`appsettings.*.json` (excepto `*.example.json` y `appsettings.Testing.json`, explícitamente permitidos) y `*.pfx`/`*.p12`/certificados — no commitear secretos reales.
- **`dcArca.TestApp` no compila en Linux** — no es un bug a arreglar, es WinForms por diseño.
