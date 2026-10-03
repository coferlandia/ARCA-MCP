# Contextos fiscales representados y asignaciones de credencial

ARCA-MCP separa cuatro identidades que no deben confundirse:

1. `consumerId`: identidad estable del sistema consumidor. No es una API key.
2. `contextId`: identidad administrativa estable de un contexto fiscal representado.
3. identidad fiscal: `(ambiente, CUIT representado, punto de venta)` y, por operación, tipo de comprobante.
4. `credentialId`: referencia opaca server-owned a una credencial capaz de operar para ese contexto.

El titular técnico del certificado puede ser distinto del CUIT representado. La autorización real se valida contra ARCA antes de activar una nueva asignación.

## Contexto fiscal

Un contexto persiste:

- `contextId`;
- `environment`;
- `representedCuit`;
- `pointOfSale`;
- `contextRevision`;
- `operationalState`: `Active`, `ReadOnly` o `Disabled`;
- historial de asignaciones de credencial.

`contextId`, ambiente, CUIT representado, punto de venta y revisión son identidad inmutable del contexto. Para otra identidad fiscal se crea otro contexto.

Dos contextos administrativos pueden representar la misma serie fiscal. Eso no crea dos numeradores: la reserva de #17 sigue usando `(ambiente, CUIT representado, PV, tipo)` y por lo tanto ambos contextos comparten la misma autoridad de serie.

## Asignación de credencial

Una asignación contiene:

- `assignmentRevision` estable;
- `credentialId` opaco;
- estado `Candidate`, `Validated`, `Active`, `Historical` o `Disabled`;
- evidencia de validación no emisora;
- timestamps y actor de las transiciones.

Sólo puede existir una asignación `Active` por contexto. Una nueva asignación nace `Candidate` y no puede activarse sin pasar primero a `Validated`.

La validación usa `FEParamGetPtosVenta`, autenticado con la credencial candidata y enviando el CUIT representado. Para considerarla válida, ARCA debe devolver el punto de venta configurado, no bloqueado y sin fecha de baja. El probe no emite comprobantes ni solicita CAE.

## Credenciales y secretos

`credentialId` nunca contiene el PFX, password ni ruta secreta. El host resuelve referencias como:

```text
FiscalCredentials__empresa-2026__CertificatePath=/run/secrets/empresa-2026.pfx
FiscalCredentials__empresa-2026__CertificatePassword=...
```

Los ambientes también son server-owned:

```text
FiscalEnvironments__homologacion__WsaaUrl=...
FiscalEnvironments__homologacion__WsfeUrl=...
FiscalEnvironments__homologacion__PadronUrl=...
```

El caller MCP sólo puede enviar `contextId`. Nunca puede elegir `credentialId`, ruta de certificado, password, endpoint ni CUIT representado.

El cache WSAA queda namespaced por `ambiente + credentialId + servicio`, por lo que dos certificados que representen el mismo CUIT no comparten token/sign.

## Consumers, API keys y grants

Una API key moderna contiene:

- un `consumerId` estable;
- scopes generales (`arca:consultar`, `arca:facturar`);
- grants de contexto por operación, por ejemplo `ctx-a:consultar` y `ctx-a:facturar`.

Rotar la API key no cambia `consumerId`. Por ello, la idempotencia sigue perteneciendo al mismo consumidor aun cuando cambie el secreto de autenticación.

Agregar un contexto al catálogo no modifica grants existentes. Una key sólo accede a los contextos que se le asignaron explícitamente.

Si una operación tiene exactamente un contexto autorizado, `contextId` puede omitirse. Si tiene más de uno, omitirlo falla con `FISCAL_CONTEXT_REQUIRED`; el servidor nunca elige uno arbitrariamente.

## Compatibilidad single-context

En una instalación existente, si todavía no existe el catálogo de contextos, el servidor bootstrappea una única entrada desde `FiscalContext` + `dcArcaConfig`:

- conserva el `consumerId` y `contextId` actuales;
- usa el CUIT/PV actuales;
- crea una asignación activa `legacy-default` con la revisión configurada;
- las API keys antiguas sin `consumerId` ni grants sólo reciben acceso a ese contexto legacy.

Una vez creado el catálogo, el bootstrap no agrega nuevos contextos implícitos. Esto evita que una actualización de configuración expanda permisos accidentalmente.

## Migración certificado personal → certificado empresa

Para migrar un contexto ya existente:

```bash
dcArca.Cli add-assignment \
  --context tenant-fiscal \
  --revision empresa-v2 \
  --credential empresa-2026 \
  --actor diego

dcArca.Cli validate-assignment \
  --context tenant-fiscal \
  --revision empresa-v2 \
  --actor diego

dcArca.Cli activate-assignment \
  --context tenant-fiscal \
  --revision empresa-v2 \
  --actor diego
```

La activación transforma la asignación anterior en `Historical`. Las operaciones nuevas usan `empresa-v2`.

Una operación idempotente creada antes del cambio conserva para siempre su `assignmentRevision` original. Si se reintenta después de la migración, el runtime vuelve a resolver esa revisión histórica. No hace fallback a la credencial nueva.

Si la credencial histórica se deshabilita durante una operación pendiente, el retry falla con `HISTORICAL_ASSIGNMENT_INTERVENTION_REQUIRED`; hace falta una resolución explícita del incidente.

## Alta gradual de un contexto nuevo

Un contexto nuevo nace `Disabled` y sin credencial activa:

```bash
dcArca.Cli add-context \
  --id tenant-b \
  --environment homologacion \
  --cuit 30XXXXXXXXX \
  --point-of-sale 4
```

Luego se agrega, valida y activa su assignment. Finalmente se habilita el contexto:

```bash
dcArca.Cli set-context-state \
  --context tenant-b \
  --state Active \
  --actor diego
```

Los grants se agregan aparte a cada API key:

```bash
dcArca.Cli set-key-grants key_xxx \
  --consumer secretaria \
  --grant tenant-a:consultar,tenant-a:facturar,tenant-b:consultar,tenant-b:facturar
```

No existe propagación automática de grants.

## Estados operativos

- `Active`: consultas y emisiones nuevas permitidas según grants.
- `ReadOnly`: consultas permitidas, emisiones nuevas bloqueadas.
- `Disabled`: consultas y emisiones nuevas bloqueadas.

La identidad idempotente histórica y el historial de assignments no se borran al cambiar de estado.

## Comandos administrativos

El CLI soporta:

- `create-key` y `set-key-grants` con `consumerId`/grants;
- `list-contexts`;
- `add-context`;
- `add-assignment`;
- `validate-assignment`;
- `activate-assignment`;
- `disable-assignment`;
- `set-context-state`.

`validate-assignment` puede devolver código de salida `2` cuando la comprobación remota no queda verificada. En ese caso `activate-assignment` continúa bloqueado.
