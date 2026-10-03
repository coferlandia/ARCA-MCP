# Numeración y concurrencia de emisión MCP

El flujo seguro es `emitir_comprobante`, `emitir_comprobante_avanzado` o `emitir_comprobante_con_pdf`. El caller no elige el número fiscal.

La identidad canónica de una serie es:

```text
(ambiente, CUIT representado, punto de venta, tipo de comprobante)
```

`consumerId`, `contextId`, API keys y certificados no crean series distintas si terminan representando la misma combinación fiscal.

## Reserva durable

Para cada serie puede existir como máximo una operación pendiente propietaria de una reserva. La reserva persiste:

- serie canónica;
- hash namespaced de la operación propietaria;
- número reservado;
- timestamps.

Flujo de una emisión nueva:

1. valida el request localmente;
2. verifica que la serie no esté reservada por otra operación;
3. consulta `FECompUltimoAutorizado`;
4. calcula `último + 1`;
5. persiste la reserva durable;
6. persiste `NumberAssigned` en la operación;
7. vuelve a consultar `FECompUltimoAutorizado` inmediatamente antes del side effect;
8. sólo si ARCA sigue informando `reservado - 1`, persiste `Submitting` y ejecuta `FECAESolicitar`;
9. libera la reserva únicamente al llegar a `Authorized` o `FiscalRejected`.

Si la serie cambió entre los pasos 3 y 7, devuelve `SERIES_NUMBER_DRIFT`, no emite y mantiene la reserva para resolución explícita. Esto protege contra otro escritor externo usando el mismo punto de venta.

## Operación incierta

`Submitting` y `Uncertain` conservan la reserva. Otra idempotency key sobre la misma serie recibe `SERIES_RESERVATION_BLOCKED` y no consulta ni solicita un segundo CAE para intentar avanzar la secuencia.

La misma operación puede reintentarse: primero reconcilia exactamente el número reservado. Sólo una reconciliación fiscal equivalente puede liberar la serie como `RecoveredSuccess`.

## Recuperación tras caída

Al arrancar, el coordinator recorre operaciones y reservas antes de declarar ready:

- reserva existente + operación todavía `Created`: repara `NumberAssigned` con el número durable;
- operación `NumberAssigned`/`Submitting`/`Uncertain` sin archivo de reserva: reconstruye la reserva;
- más de un owner pendiente para la misma serie: falla cerrado;
- reserva cuyo owner terminal ya está persistido: elimina la reserva obsoleta;
- reserva corrupta, owner inexistente o número/serie incompatibles: falla cerrado.

Por lo tanto, una caída en las ventanas entre reserva, persistencia de operación y envío no convierte la serie en “libre” silenciosamente.

## Single writer V1

La topología filesystem soportada usa un lease exclusivo de proceso en el store. El servidor adquiere ese lease antes de exponer readiness. Un segundo proceso ARCA-MCP que intenta abrir el mismo store falla con `SERIES_WRITER_BUSY`.

Este lease no coordina sistemas externos que llamen a ARCA directamente. El punto de venta usado por ARCA-MCP debe considerarse administrado por este servicio. La revalidación previa al envío detecta drift, pero no sustituye la disciplina operativa de no tener otro numerador externo.

## Superficie de bajo nivel

La tool MCP `solicitar_cae`, que permitía elegir `CbteDesde/CbteHasta`, queda deshabilitada con `LOW_LEVEL_EMISSION_DISABLED`. Mantenerla habilitada permitiría saltarse la reserva durable y romper la invariante de la serie.

Las APIs Core de bajo nivel continúan existiendo para compatibilidad de biblioteca, pero no forman parte del contrato seguro de numeración del servidor MCP.

## Concurrencia

La serialización in-process sigue siendo por serie para reducir carreras dentro del mismo servidor. La autoridad de seguridad, sin embargo, es la combinación de:

- lease exclusivo del store;
- reserva durable por serie;
- operación idempotente durable;
- revalidación con ARCA antes de enviar;
- reconciliación fiscal verificada.

Series distintas pueden avanzar de forma independiente dentro del único proceso escritor.
