# Reconciliación de emisiones WSFE

Una caída de transporte después de persistir `Submitting` no demuestra que ARCA haya descartado la solicitud. El comprobante puede haber sido autorizado aunque la respuesta no haya llegado al servidor.

ARCA-MCP no repite automáticamente un `FECAESolicitar` ambiguo. Conserva el número y la reserva de la serie y consulta exactamente ese comprobante.

## Resultado semántico

Los outcomes relevantes son:

- `Authorized`: respuesta normal autorizada;
- `FiscalRejected`: ARCA respondió y rechazó fiscalmente;
- `RecoveredSuccess`: hubo un intento incierto y `FECompConsultar` confirmó el mismo comprobante con equivalencia fiscal verificable;
- `Uncertain`: hubo un intento o evidencia ambigua y no puede probarse equivalencia completa;
- `InvalidRequest`: fallo determinístico anterior a numeración/envío;
- `FailedBeforeSubmission`: se puede demostrar que el side effect no se inició.

## Regla de recuperación

Encontrar “un comprobante con CAE” en el mismo tipo/número no alcanza para declarar éxito recuperado. La operación persiste una proyección fiscal normalizada y la reconciliación compara, cuando corresponde:

- número, punto de venta y tipo;
- concepto;
- tipo y número de documento del receptor;
- condición IVA del receptor;
- fecha del comprobante y fechas de servicio/vencimiento cuando aplican;
- moneda y cotización;
- total, neto, IVA, no gravado, exento y tributos;
- detalle de IVA normalizado;
- detalle de tributos normalizado.

Los decimales admiten diferencias de escala y una tolerancia de un centavo; el orden de IVA/tributos no altera la equivalencia.

## Tres resultados del comparador

### Equivalent

Toda la evidencia disponible requerida coincide. Sólo en este caso:

1. se marca `RecoveredSuccess`;
2. la operación pasa a `Authorized`;
3. se persiste el resultado consultado;
4. se libera la reserva de la serie.

### Mismatch

Algún dato fiscal comparable difiere. Devuelve `RECONCILIATION_MISMATCH`, mantiene `Uncertain` y conserva la reserva. No se reemite y no se permite que otra operación tome el siguiente número.

### InsufficientEvidence

Falta evidencia necesaria para demostrar equivalencia. Devuelve `RECONCILIATION_EVIDENCE_INSUFFICIENT`, mantiene `Uncertain` y conserva la reserva.

Esto incluye registros legacy migrados sin proyección fiscal estructurada. También incluye actualmente operaciones con comprobante/período asociado cuando `FECompConsultar` no expone esos campos en el DTO de consulta: el sistema prefiere bloquear antes que inferir una igualdad que no puede verificar.

## Consulta negativa

Si `FECompConsultar` no confirma un comprobante autorizado con CAE, la operación permanece `Uncertain`. Esa consulta negativa no prueba que el envío anterior nunca haya llegado; por lo tanto no habilita un segundo `FECAESolicitar` automático.

## Recuperación operativa

Ante una operación incierta:

1. conservar la misma idempotency key;
2. reintentar esa operación para ejecutar sólo la reconciliación;
3. si devuelve `RecoveredSuccess`, continuar normalmente;
4. si devuelve mismatch, evidencia insuficiente o sigue sin confirmación, detener la serie y revisar evidencia/ARCA antes de cualquier acción manual;
5. no usar la tool legacy `solicitar_cae` para “saltar” el bloqueo: está deshabilitada en el servidor MCP.

La reserva durable sobrevive reinicios y evita que un crash transforme un estado incierto en una oportunidad de emitir un número nuevo.
