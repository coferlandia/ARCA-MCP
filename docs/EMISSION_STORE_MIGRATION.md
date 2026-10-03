# Migración del store idempotente de emisiones

El schema versionado de operaciones fiscales incorpora identidad durable de consumidor y contexto fiscal. Un store legacy no se interpreta usando silenciosamente la configuración actual: debe mapearse de forma explícita antes de habilitar el escritor.

## Precondiciones

1. Detener ARCA-MCP y cualquier otro escritor que utilice el mismo store/punto de venta.
2. Confirmar el mapping histórico correcto: `consumerId`, `contextId`, ambiente, CUIT representado y punto de venta.
3. Definir `contextRevision` y una referencia no secreta `credentialAssignmentRevision`.
4. Verificar espacio para un backup completo y el procedimiento de restore.

No usar rutas de certificados, passwords, API keys ni secretos como IDs o revisiones.

## Dry-run obligatorio

```bash
dotnet dcArca.McpServer.dll migrate-emission-store \
  --directory /data/emission-idempotency \
  --consumer secretaria \
  --context tenant-fiscal-default \
  --environment homologacion \
  --cuit 20123456789 \
  --point-of-sale 1 \
  --context-revision 1 \
  --credential-revision operador-personal-v1
```

Sin `--apply` el comando sólo inventaría el store. Informa cantidad total, legacy/current, pendientes, terminales y advertencias. No crea manifiestos, backups ni modifica registros.

El dry-run falla cerrado ante JSON corrupto, schema desconocido, hashes inválidos, registros terminales incompletos o un punto de venta que no coincida con el mapping.

## Aplicación

Con el escritor todavía detenido, repetir exactamente el mapping y agregar `--apply`. Puede fijarse el backup:

```bash
dotnet dcArca.McpServer.dll migrate-emission-store \
  --directory /data/emission-idempotency \
  --consumer secretaria \
  --context tenant-fiscal-default \
  --environment homologacion \
  --cuit 20123456789 \
  --point-of-sale 1 \
  --context-revision 1 \
  --credential-revision operador-personal-v1 \
  --backup /data/emission-idempotency.backup-before-v2 \
  --apply
```

El backup debe ser hermano del directorio activo para que la sustitución pueda hacerse por rename. La migración construye y valida primero un staging completo; sólo después renombra el store original a backup y pone el staging en su lugar. Si falla el segundo rename, intenta restaurar el original.

Las operaciones conservan request hash, número, estado, CAE y timestamps. La vieja `KeyHash` se transforma determinísticamente al namespace `consumerId + contextId + hash legacy`, por lo que un retry con la idempotency key original sigue encontrando la operación sin que la migración necesite conocer esa key en claro.

Los registros legacy no contienen una proyección fiscal comparable suficiente para #17. Se marcan `LegacyUnavailable`; especialmente `Submitting`/`Uncertain` deben seguir bloqueados para resolución manual y nunca convertirse en éxito sólo porque exista un número/CAE consultable.

## Verificación

Antes de reiniciar el escritor:

- repetir el comando sin `--apply`: debe reportar `LegacyRecords = 0`;
- verificar que los conteos y estados coincidan con el inventario previo;
- comprobar que el `contextId` corresponde al mismo ambiente/CUIT/PV;
- conservar el backup hasta completar la validación funcional y la conciliación de cualquier side effect pendiente.

Una segunda ejecución sobre un store ya migrado es idempotente: valida el mapping/contexto y no crea otro backup.

## Rollback

Rollback sólo con el escritor detenido. Si todavía no hubo nuevas operaciones en schema v2, retirar el directorio actual y restaurar el backup por rename.

Si hubo cualquier side effect fiscal posterior al backup, no hacer downgrade ciego: primero identificar y conciliar esas operaciones. Una versión antigua no debe arrancarse sobre schema v2 ni interpretar el store nuevo como vacío.

## Cambio de credencial

Rotar del certificado personal al empresarial cuando ambos representan el mismo CUIT no migra el store ni cambia `consumerId`, `contextId`, serie o namespace idempotente. Las operaciones históricas conservan la revisión de asignación con la que nacieron; la nueva revisión sólo aplica a operaciones nuevas. Cambiar el CUIT emisor, en cambio, requiere otro contexto fiscal.
