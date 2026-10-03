# Plan de integración: SecretarIA + ARCA-MCP + creadorpdf

**Fecha:** 3 de octubre de 2026  
**Destinatarios:** Diego, Pedro y equipo Cadencia  
**Estado:** infraestructura desplegada; contrato fiscal/idempotencia y hardening documental implementados; pendiente integración funcional y homologación fiscal

## Actualización 2026-10-03 — contrato de templates y fallos PDF

SecretarIA debe trabajar conceptualmente con una referencia lógica estable:

```text
templateKey = factura-ar
templateVersion = 3
```

El schema MCP conserva temporalmente el nombre de parámetro `templateId` para no romper consumidores, pero su semántica actual es `templateKey`. ARCA-MCP resuelve internamente el `tpl_*` físico visible para su credencial runtime.

SecretarIA debe persistir:

```text
templateKey
templateVersion
templateData snapshot
```

y no debe persistir como dependencia de negocio:

```text
tpl_* físico
owner_id de creadorpdf
API key
BaseUrl del provider
```

ARCA-MCP cachea la resolución lógica. Si un ID físico cacheado desaparece y creadorpdf devuelve `404 unknown_template`, invalida la entrada, vuelve a resolver y reintenta una sola vez **la etapa documental**. Nunca repite numeración ni CAE.

Los fallos documentales están normalizados a códigos públicos estables (`PDF_TEMPLATE_NOT_FOUND`, `PDF_PROVIDER_FORBIDDEN`, `PDF_INVALID_REQUEST`, `PDF_RATE_LIMITED`, `PDF_TIMEOUT`, `PDF_UNAVAILABLE`, etc.) y pueden incluir metadata diagnóstica segura (`providerStatusCode`, `providerErrorCode`). Un fallo PDF jamás modifica un resultado fiscal ya autorizado.

## 1. Objetivo

Implementar un flujo único para que SecretarIA pueda solicitar facturas de Cadencia y, posteriormente, permitir que cada cliente de SecretarIA facture a sus propios clientes.

El resultado de una operación exitosa debe contener:

- autorización fiscal de ARCA;
- CAE y vencimiento;
- número y datos definitivos del comprobante;
- PDF generado con una plantilla publicada;
- información suficiente para almacenar, descargar y entregar la factura.

## 2. Arquitectura acordada

```text
Mercado Pago o solicitud manual
              |
              v
         SecretarIA
              |
              | API key + solicitud idempotente
              v
          ARCA-MCP ----------------> ARCA
              |
              | templateKey + version
              | resolver interno -> tpl_* opaco
              | JSON fiscal autorizado
              v
         creadorpdf
              |
              | PDF
              v
          ARCA-MCP
              |
              | resultado fiscal + resultado PDF
              v
         SecretarIA
              |
              v
        almacenamiento / SIM / entrega
```

### Responsabilidades

| Sistema | Responsabilidad |
| --- | --- |
| SecretarIA | Decidir cuándo facturar, identificar tenant y cliente, crear la idempotency key, persistir el estado comercial, `templateKey/version/templateData` y entregar el resultado |
| ARCA-MCP | Validar la solicitud, coordinar numeración, emitir o reconciliar con ARCA, resolver la referencia lógica de template, incorporar los datos fiscales definitivos y solicitar el PDF |
| creadorpdf | Resolver su ID físico interno, validar el JSON contra el schema de la plantilla publicada y devolver el PDF |
| SIM | Ejecutar acciones posteriores como email, WhatsApp, avisos y reintentos de entrega |

SecretarIA no llama directamente a creadorpdf y nunca recibe su API key.

## 3. Estado actual

### Ya implementado

- creadorpdf desplegado en `https://pdf.cadencia.com.ar`;
- ARCA-MCP desplegado en `https://arca.cadencia.com.ar`;
- comunicación privada ARCA-MCP → creadorpdf mediante la red Docker `red-cadencia`;
- API keys propias en ARCA-MCP, sin Keycloak/JWT/OIDC;
- scopes independientes `arca:consultar` y `arca:facturar`;
- creación, listado y revocación de claves mediante `dcArca.Cli`;
- secretos visibles una sola vez y persistencia exclusiva del hash;
- idempotencia fiscal durable y reconciliación de resultados inciertos;
- templates versionados y publicados en creadorpdf;
- manifest de deploy versionado para templates;
- aislamiento de templates por propietario;
- referencia lógica `templateKey + templateVersion` con resolución interna de `tpl_*`;
- cache de resolución e invalidación/re-resolución única ante `unknown_template`;
- compatibilidad temporal con `tpl_*` legacy;
- tool MCP `emitir_comprobante_con_pdf`;
- tool MCP `generar_pdf_comprobante` query-only;
- campo `fiscal` reservado e incorporado por ARCA-MCP después de la autorización;
- resultado fiscal separado del resultado documental;
- taxonomía estructurada de fallos PDF y metadata diagnóstica segura;
- retorno del PDF en Base64 para el MVP;
- prueba productiva no fiscal de ARCA-MCP → creadorpdf aprobada;
- backups previos al despliegue disponibles en el servidor.

### Pendiente

- integración funcional en SecretarIA usando la referencia lógica;
- persistencia del PDF o definición de object storage;
- prueba con CAE en homologación;
- reglas fiscales para pagos totales, señas y saldos;
- soporte multi-tenant de certificados y perfiles fiscales;
- política definitiva de delivery/retry fuera de ARCA-MCP.

## 4. Contrato propuesto para el MVP

SecretarIA debe invocar la tool MCP `emitir_comprobante_con_pdf` con una API key que posea `arca:facturar`.

Ejemplo conceptual:

```json
{
  "factura": {
    "tipoComprobante": "FacturaB",
    "concepto": "Servicios",
    "cuitReceptor": 20123456786,
    "tipoDocReceptor": 80,
    "condicionIvaReceptor": "ConsumidorFinal",
    "importeNeto": 1000.00,
    "importeIva": 210.00,
    "importeTotal": 1210.00,
    "alicuotaIva": "Veintiuno",
    "fechaComprobante": "20261001",
    "fechaServicioDesde": "20261001",
    "fechaServicioHasta": "20261031",
    "fechaVencimiento": "20261110"
  },
  "templateId": "factura-ar",
  "templateVersion": "3",
  "templateData": {
    "emisor": {
      "razonSocial": "Cadencia",
      "domicilio": "..."
    },
    "receptor": {
      "razonSocial": "Cliente Ejemplo"
    },
    "items": [
      {
        "descripcion": "Servicio mensual",
        "cantidad": 1,
        "precioUnitario": 1000.00
      }
    ]
  }
}
```

> `templateId` mantiene ese nombre sólo por compatibilidad MCP. Para SecretarIA el valor `factura-ar` es la `templateKey` lógica, no un ID físico de creadorpdf.

`templateData` no puede incluir `fiscal`. ARCA-MCP agrega ese campo después de recibir la respuesta autorizada.

La respuesta separa explícitamente:

```text
Fiscal
Pdf
```

Por ejemplo, una autorización fiscal puede ser exitosa aunque el renderer falle:

```json
{
  "fiscal": {
    "success": true,
    "cae": "...",
    "numeroComprobante": 123
  },
  "pdf": {
    "status": "Failed",
    "base64": null,
    "errorCode": "PDF_TEMPLATE_NOT_FOUND",
    "provider": "creadorpdf",
    "providerStatusCode": 404,
    "providerErrorCode": "unknown_template"
  }
}
```

Eso significa que **la factura ya existe** y sólo debe corregirse/reintentarse la etapa documental.

## 5. Idempotencia y estados

La `idempotencyKey` es obligatoria para el flujo recomendado, por ejemplo:

```text
tenant_id + payment_id + invoice_reason
```

Ejemplos:

```text
tenant-123:mp-payment-456:full-payment
tenant-123:mp-payment-456:deposit
tenant-123:mp-payment-789:balance
tenant-123:manual-request-001:manual
```

Estados sugeridos en SecretarIA:

```text
draft
  -> pending_authorization
  -> authorized
  -> rendering_pdf
  -> ready
  -> delivering
  -> delivered
```

Estados de intervención:

```text
authorization_failed
authorization_unknown
render_failed
delivery_failed
manual_review
```

Reglas:

- un retry del PDF nunca debe solicitar otro CAE;
- una respuesta fiscal ambigua debe reconciliarse antes de reemitir;
- un webhook repetido de Mercado Pago debe recuperar la operación existente;
- un fallo de entrega no debe volver a autorizar ni renderizar;
- los cambios de estado deben quedar auditados.

## 6. Plantilla fiscal

La primera referencia productiva validada es conceptualmente:

```text
factura-ar:3
```

La versión publicada es inmutable. Cualquier cambio de HTML o schema crea una versión nueva:

```text
factura-ar:3 -> factura-ar:4
```

El deploy se declara en:

```text
deploy/cadencia/pdf-templates/manifest.json
```

La credencial runtime de ARCA-MCP debe limitarse a lectura/render. Crear/publicar templates usa una credencial de gestión temporal del mismo owner, que no queda instalada como secret permanente.

## 7. Fases de trabajo

### Fase 1 — Contrato y plantilla

- mantener el request fiscal y `idempotencyKey` estabilizados;
- usar `templateKey/version` como identidad durable;
- versionar schema fiscal y template HTML;
- decidir dónde se almacena el PDF;
- versionar ejemplos válidos e inválidos.

**Salida:** contrato revisado por Diego, Pedro y Fabi.

### Fase 2 — Homologación manual de Cadencia

- configurar certificado y punto de venta de homologación;
- asegurar que `factura-ar:3` esté publicada y visible para la key runtime;
- emitir una factura controlada;
- verificar CAE, importes, QR y PDF;
- repetir el request y comprobar que no se genere otra factura;
- probar `404 unknown_template`, 403 y un 5xx del renderer sin perder el resultado fiscal.

**Salida:** circuito manual completo aprobado en homologación.

### Fase 3 — Integración con SecretarIA

- guardar la API key de ARCA-MCP en el secret manager;
- implementar el cliente MCP;
- crear la orden fiscal durable;
- persistir `templateKey`, `templateVersion` y snapshot de `templateData`;
- almacenar el resultado fiscal y PDF;
- agregar historial, descarga y reintento de render;
- auditar usuario, tenant, origen y estados.

**Salida:** SecretarIA puede emitir manualmente para Cadencia sin conocer IDs físicos del renderer.

### Fase 4 — Mercado Pago y SIM

- consumir pagos aprobados;
- aplicar idempotencia por pago y motivo;
- configurar total, seña y saldo por separado;
- emitir el evento `fiscal_invoice.ready`;
- entregar mediante SIM por email o WhatsApp;
- agregar alertas y cola de revisión manual.

**Salida:** emisión y entrega automáticas para Cadencia.

### Fase 5 — Multi-tenant

- perfil fiscal por tenant;
- certificados cifrados y referencias opacas;
- tokens, caché y numeración aislados por CUIT/ambiente;
- template lógico/version por tenant o caso de uso;
- onboarding obligatorio en homologación;
- autorización y auditoría estrictas por tenant.

**Salida:** clientes habilitados pueden facturar a sus propios clientes.

## 8. Pruebas de aceptación

- acceso sin API key devuelve `401`;
- una key sin `arca:facturar` no puede emitir;
- una key revocada deja de funcionar inmediatamente;
- un request repetido produce una sola factura;
- un timeout fiscal ambiguo se reconcilia antes de reintentar;
- un fallo de creadorpdf no solicita otro CAE;
- `404 unknown_template` se distingue como `PDF_TEMPLATE_NOT_FOUND`;
- 403 se distingue como `PDF_PROVIDER_FORBIDDEN`;
- 422 se distingue como `PDF_INVALID_REQUEST`;
- 429 se distingue como `PDF_RATE_LIMITED`;
- 5xx/network se distinguen como `PDF_UNAVAILABLE`;
- timeout documental se distingue como `PDF_TIMEOUT`;
- MIME no PDF se distingue como `PDF_INVALID_RESPONSE`;
- `templateData.fiscal` es rechazado;
- un template draft no puede renderizarse como published;
- más de un published para la misma key/version falla cerrado como ambiguo;
- `tpl_*` legacy funciona sólo durante la transición;
- stale cache + `unknown_template` invalida y re-resuelve una vez;
- el PDF contiene los mismos importes y datos autorizados por ARCA;
- caracteres acentuados y `ñ` se conservan;
- el PDF Base64 se decodifica y comienza con `%PDF`;
- secretos, bodies remotos, certificados, payloads fiscales y PDFs no aparecen en logs;
- dos tenants nunca acceden a credenciales o comprobantes ajenos.

## 9. Seguridad y operación

- una API key por consumidor y ambiente;
- claves almacenadas solo en secret managers o archivos `0600`;
- rotación mediante creación, migración y revocación;
- ARCA-MCP es el único sistema que conoce la key runtime de creadorpdf;
- owner del template derivado exclusivamente de la credencial runtime;
- creadorpdf no almacena PDFs;
- certificados y claves privadas nunca viajan en requests;
- backups antes de cada migración;
- logs sin secretos, bodies remotos completos ni `templateData`;
- homologación obligatoria antes de producción.

## 10. Decisiones pendientes

1. ¿Dónde se almacenarán los PDFs definitivos?
2. ¿Qué campos no fiscales puede personalizar cada tenant en su template?
3. ¿Quién puede aprobar/publicar una nueva versión lógica de template?
4. ¿Cómo se cifrarán y rotarán los certificados multi-tenant?
5. ¿Cuál es el tratamiento contable de señas y saldos?
6. ¿Base64 seguirá siendo el contrato definitivo o se reemplazará por una referencia firmada?

## 11. Próxima reunión / acción recomendada

Para la próxima sesión técnica:

1. validar el contrato SecretarIA con `factura-ar:3`;
2. ejecutar una emisión completa en homologación;
3. probar fallos documentales estructurados sin reemisión fiscal;
4. decidir almacenamiento del PDF;
5. preparar la integración funcional en SecretarIA usando sólo referencia lógica.
