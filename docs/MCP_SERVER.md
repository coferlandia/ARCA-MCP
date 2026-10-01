# dcArca.McpServer

`dcArca.McpServer` expone las operaciones de facturación electrónica de `dcArca.Core` como tools MCP sobre HTTP, protegidas con API keys Bearer y scopes.

## Arquitectura

```text
SecretarIA -- API key --> dcArca.McpServer
   |
   v
dcArca.Core
   |
   +--> WSAA
   +--> WSFEv1
   +--> Padrón ARCA
   +--> creadorpdf
```

El servidor administra credenciales propias con secreto visible una sola vez, hash persistido, scopes y revocación inmediata. Las emisiones recomendadas además usan una `idempotencyKey` durable para impedir que un retry produzca otro comprobante.

## Requisitos

- .NET 10 SDK para compilar desde código;
- certificado ARCA/PFX válido para el ambiente elegido;
- servicios ARCA correspondientes autorizados para ese certificado;
- un punto de venta habilitado para WSFE.

## Compilar

```bash
dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
dotnet build dcArca.McpServer/dcArca.McpServer.csproj -c Release
```

Docker:

```bash
docker build -f dcArca.McpServer/Dockerfile -t dcarca-mcpserver .
```

El `docker-compose.yml` de la raíz es deliberadamente genérico y no contiene dominios, redes privadas ni secretos de producción.

## Configuración

Partir de `dcArca.McpServer/appsettings.example.json`.

Configuración principal:

```json
{
  "dcArcaConfig": {
    "Cuit": "TU_CUIT",
    "CertificatePath": "/certs/certificado.pfx",
    "CertificatePassword": "DESDE_SECRET_MANAGER",
    "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
    "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
    "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5",
    "PuntoVenta": 1
  },
  "ApiKeys": { "Directory": "/data" },
  "EmissionIdempotency": { "Directory": "/data/emission-idempotency" },
  "Pdf": {
    "BaseUrl": "http://creadorpdf:8080",
    "ApiKey": "DESDE_SECRET_MANAGER"
  }
}
```

Variables de entorno equivalentes:

```bash
ApiKeys__Directory=/data
EmissionIdempotency__Directory=/data/emission-idempotency
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=...
dcArcaConfig__Cuit=...
dcArcaConfig__CertificatePath=/certs/certificado.pfx
dcArcaConfig__CertificatePassword=...
dcArcaConfig__WsaaUrl=...
dcArcaConfig__WsfeUrl=...
dcArcaConfig__PadronUrl=...
dcArcaConfig__PuntoVenta=1
```

No guardar PFX, passwords, tokens, client secrets ni configuración privada en el repositorio.

### Homologación vs producción

Use certificado y endpoints del mismo ambiente. No mezcle un certificado de homologación con endpoints productivos.

## API keys y scopes

Scopes soportados:

- `arca:consultar`: lectura y regeneración de documentos existentes;
- `arca:facturar`: emisión.

Son independientes. `arca:facturar` **no implica** `arca:consultar`.

La gestión se realiza con `dcArca.Cli create-key`, `list-keys` y `revoke-key`. El secreto pertenece al backend consumidor y nunca a un navegador o usuario final.

## Endpoint MCP

El transporte HTTP es stateless y se publica en la raíz `/`.

Sin una key válida el endpoint devuelve 401. Cuando un tool requiere un scope que la key no posee, el SDK MCP lo oculta de `tools/list` y rechaza llamadas directas.

## Tools

### Lectura — scope `arca:consultar`

#### `consultar_ultimo_comprobante`

Consulta el último número autorizado para el tipo indicado y el punto de venta configurado.

#### `consultar_comprobante`

Consulta un comprobante ya emitido. La respuesta puede incluir CAE, importes, moneda/cotización, IVA, tributos, fechas y observaciones.

#### `generar_pdf_comprobante`

Genera o regenera el PDF de un comprobante ya autorizado.

Parámetros principales:

```text
numeroComprobante
tipoComprobante
templateId
templateVersion
templateData
```

El flujo es estrictamente:

```text
FECompConsultar -> FiscalDocumentSnapshot -> creadorpdf
```

Nunca ejecuta `FECAESolicitar` ni el sequencer de emisión. Sirve tanto para el render separado inmediatamente después de emitir como para reenvíos históricos.

#### `consultar_condiciones_iva`

Consulta condiciones de IVA válidas para un receptor y tipo de comprobante.

#### `consultar_padron`

Consulta información registral de un CUIT en el padrón autorizado.

### Emisión — scope `arca:facturar`

Las tres tools recomendadas de emisión requieren `idempotencyKey`:

```text
emitir_comprobante
emitir_comprobante_avanzado
emitir_comprobante_con_pdf
```

Una key repetida con el mismo request fiscal recupera la operación existente. La misma key con datos fiscales diferentes devuelve `IDEMPOTENCY_KEY_REUSED` sin llamar a ARCA.

#### `emitir_comprobante`

Caso simple. No recibe número de comprobante: el servidor coordina la numeración y la persiste antes del side effect fiscal.

#### `emitir_comprobante_avanzado`

Recibe `dcFacturaRequest` completo para múltiples alícuotas, exentos/no gravados, tributos, moneda, servicios y notas. `NumeroComprobante` recibido es ignorado y se asigna server-side.

#### `emitir_comprobante_con_pdf`

Compone emisión fiscal idempotente + generación PDF.

El PDF es un side effect posterior. Un resultado:

```text
Fiscal = Authorized
PDF = Failed
```

significa que la factura ya existe. Un retry con la misma `idempotencyKey` recupera la misma autorización y reintenta sólo el render.

#### `solicitar_cae` — bajo nivel

Permite elegir manualmente el número fiscal. Se conserva para integraciones avanzadas que coordinan por su cuenta numeración e idempotencia. No es el flujo recomendado para callers generales ni retries automáticos.

## Idempotencia durable

ARCA-MCP almacena únicamente la información mínima necesaria para proteger el side effect fiscal:

```text
hash de idempotencyKey
hash determinístico del request fiscal
tipo / punto de venta / número asignado
estado
resultado fiscal mínimo
```

No almacena la key en claro, PDFs, `templateData`, datos de Mercado Pago, delivery, certificados, tokens WSAA ni payloads SOAP completos.

Estados:

```text
Created
NumberAssigned
Submitting
Authorized
FiscalRejected
Uncertain
```

El número se persiste como `NumberAssigned` antes de iniciar el side effect y `Submitting` se persiste antes de llamar a ARCA.

Ante `Submitting` o `Uncertain`, un retry consulta exclusivamente ese mismo tipo/número con `FECompConsultar`. Si ARCA lo confirma, devuelve `RecoveredSuccess`; si todavía no puede confirmarse, permanece `Uncertain` y **no solicita otro CAE**.

El fingerprint fiscal excluye número server-side, template, datos visuales y PDF. Por eso el mismo comprobante puede volver a renderizarse sin ser una nueva emisión.

Detalles operativos: `docs/MCP_IDEMPOTENCY.md`.

## Numeración y concurrencia

Las emisiones recomendadas serializan la secuencia por:

```text
(CUIT emisor, punto de venta, tipo de comprobante)
```

El lock de numeración es **in-process**. El store de idempotencia es durable en filesystem y protege retries/restarts sobre el mismo volumen, pero no convierte el sistema en un coordinador multi-host. Varias réplicas escritoras compartiendo el mismo punto de venta requieren coordinación distribuida o puntos de venta dedicados.

## Resultado incierto y reconciliación

`dcFacturaResponse.EmissionOutcome` puede indicar:

- `Authorized`;
- `FiscalRejected`;
- `RecoveredSuccess`;
- `Uncertain`.

Si el transporte falla durante `FECAESolicitar`, `dcArca.Core` ya intenta reconciliar el mismo comprobante. Además, la capa MCP persiste la identidad de la operación, por lo que un retry posterior conserva tipo/número y vuelve a consultar antes de considerar cualquier nueva emisión.

## Cache WSAA

Por defecto el TA se mantiene en memoria. No se persisten `token/sign` silenciosamente.

Si el host necesita persistencia entre procesos del mismo equipo, puede inyectar `FileSystemWsaaTokenStore`. Para protección cifrada o coordinación multi-host, implemente `IWsaaTokenStore` en el host.

## Health

- `GET /health/live`: proceso vivo.
- `GET /health/ready`: configuración esencial cargada y servidor listo.

No llaman a ARCA y no devuelven secretos ni CUIT.

## Seguridad

- TLS obligatorio en producción.
- No commitear certificados, PFX, passwords ni API keys.
- Usar una key distinta por consumidor y revocarla inmediatamente ante una exposición.
- No entregar secretos a frontend, LLM o usuario final.
- Aplicar mínimo privilegio en scopes.
- No registrar `idempotencyKey` en claro; usar su hash para correlación.
- Preferir las tools idempotentes recomendadas sobre `solicitar_cae`.
- Mantener certificados y secretos en mecanismos provistos por el host.

## Referencias internas

- `docs/MCP_AUTHORIZATION.md`
- `docs/MCP_NUMBERING.md`
- `docs/MCP_CONFIGURATION.md`
- `docs/MCP_IDEMPOTENCY.md`
- `docs/CREADORPDF_INTEGRATION.md`
- `docs/WSFE_RECONCILIATION.md`
- `docs/WSAA_CACHE.md`
- `docs/REPOSITORY_SECURITY.md`
