# Plan de integración: SecretarIA + ARCA-MCP + creadorpdf

**Fecha:** 1 de octubre de 2026  
**Destinatarios:** Diego, Pedro y equipo Cadencia  
**Estado:** infraestructura desplegada; pendiente integración funcional y homologación fiscal

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
              | template publicado + JSON fiscal autorizado
              v
         creadorpdf
              |
              | PDF
              v
          ARCA-MCP
              |
              | resultado fiscal + pdfBase64
              v
         SecretarIA
              |
              v
        almacenamiento / SIM / entrega
```

### Responsabilidades

| Sistema | Responsabilidad |
| --- | --- |
| SecretarIA | Decidir cuándo facturar, identificar tenant y cliente, crear la idempotency key, persistir el estado comercial y entregar el resultado |
| ARCA-MCP | Validar la solicitud, coordinar numeración, emitir o reconciliar con ARCA, incorporar los datos fiscales definitivos y solicitar el PDF |
| creadorpdf | Validar el JSON contra el schema de la plantilla publicada y devolver el PDF |
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
- registro de último uso y revocación inmediata;
- templates versionados y publicados en creadorpdf;
- aislamiento de templates por propietario;
- tool MCP `emitir_comprobante_con_pdf`;
- campo `fiscal` reservado e incorporado por ARCA-MCP después de la autorización;
- retorno del PDF en Base64 para el MVP;
- prueba productiva no fiscal de ARCA-MCP → creadorpdf aprobada;
- backups previos al despliegue disponibles en el servidor.

### Pendiente

- contrato definitivo SecretarIA → ARCA-MCP;
- idempotencia durable de la solicitud completa;
- plantilla fiscal real y su JSON Schema;
- mapeo completo entre solicitud, respuesta ARCA y template;
- persistencia del PDF o definición de object storage;
- integración en SecretarIA;
- prueba con CAE en homologación;
- reglas fiscales para pagos totales, señas y saldos;
- soporte multi-tenant de certificados y perfiles fiscales.

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
  "templateId": "tpl_...",
  "templateVersion": "1.0.0",
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

`templateData` no puede incluir `fiscal`. ARCA-MCP agrega ese campo después de recibir la respuesta autorizada:

```json
{
  "fiscal": {
    "success": true,
    "cae": "...",
    "caeVencimiento": "20261011",
    "numeroComprobante": 123,
    "puntoVenta": 1,
    "tipoComprobante": "FacturaB",
    "importeNeto": 1000.00,
    "importeIva": 210.00,
    "importeTotal": 1210.00
  },
  "pdfBase64": "JVBERi0x..."
}
```

Si ARCA rechaza la emisión, `pdfBase64` será `null` y la respuesta conservará el error fiscal normalizado.

## 5. Idempotencia y estados

Antes de usar el flujo para pagos automáticos se debe incorporar una `idempotencyKey` obligatoria, por ejemplo:

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

Diego y el equipo deben acordar el JSON Schema de la primera factura real.

Debe contemplar como mínimo:

- emisor: CUIT, razón social, domicilio y condición IVA;
- receptor: documento, razón social, domicilio y condición IVA;
- tipo, letra, punto de venta, número y fecha;
- detalle de productos o servicios;
- neto, IVA discriminado, exento, no gravado, tributos y total;
- moneda y cotización;
- CAE y vencimiento;
- datos necesarios para construir el QR oficial;
- comprobante o período asociado para notas de crédito/débito.

La versión publicada es inmutable. Cualquier cambio crea una versión nueva.

La plantilla técnica `integration-smoke:1.0.0` solo valida infraestructura y no debe usarse para facturas reales.

## 7. Fases de trabajo

### Fase 1 — Contrato y plantilla

- definir request y response definitivos;
- agregar `idempotencyKey`;
- definir schema fiscal y template HTML;
- decidir dónde se almacena el PDF;
- versionar ejemplos válidos e inválidos.

**Salida:** contrato revisado por Diego, Pedro y Fabi.

### Fase 2 — Homologación manual de Cadencia

- configurar certificado y punto de venta de homologación;
- publicar la plantilla fiscal `1.0.0`;
- emitir una factura controlada;
- verificar CAE, importes, QR y PDF;
- repetir el request y comprobar que no se genere otra factura;
- probar un rechazo fiscal y un fallo simulado de creadorpdf.

**Salida:** circuito manual completo aprobado en homologación.

### Fase 3 — Integración con SecretarIA

- guardar la API key de ARCA-MCP en el secret manager;
- implementar el cliente MCP;
- crear la orden fiscal durable;
- almacenar el resultado fiscal y PDF;
- agregar historial, descarga y reintento de render;
- auditar usuario, tenant, origen y estados.

**Salida:** Secretaría puede emitir manualmente para Cadencia.

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
- template/version por tenant o caso de uso;
- onboarding obligatorio en homologación;
- autorización y auditoría estrictas por tenant.

**Salida:** clientes habilitados pueden facturar a sus propios clientes.

## 8. Pruebas de aceptación

- acceso sin API key devuelve `401`;
- una key sin `arca:facturar` no puede emitir;
- una key revocada deja de funcionar inmediatamente;
- un request repetido produce una sola factura;
- un timeout ambiguo se reconcilia antes de reintentar;
- un fallo de creadorpdf no solicita otro CAE;
- `templateData.fiscal` es rechazado;
- un template draft o de otro propietario no puede renderizarse;
- el PDF contiene los mismos importes y datos autorizados por ARCA;
- el QR puede validarse con los datos oficiales;
- caracteres acentuados y `ñ` se conservan;
- el PDF Base64 se decodifica y comienza con `%PDF`;
- secretos, certificados, payloads fiscales y PDFs no aparecen en logs;
- dos tenants nunca acceden a credenciales o comprobantes ajenos.

## 9. Seguridad y operación

- una API key por consumidor y ambiente;
- claves almacenadas solo en secret managers o archivos `0600`;
- rotación mediante creación, migración y revocación;
- ARCA-MCP es el único sistema que conoce la key de creadorpdf;
- creadorpdf no almacena PDFs;
- certificados y claves privadas nunca viajan en requests;
- backups antes de cada migración;
- logs sin secretos ni cuerpos completos;
- homologación obligatoria antes de producción.

## 10. Decisiones pendientes

1. ¿Dónde se implementará la idempotencia durable: SecretarIA, ARCA-MCP o ambos?
2. ¿Dónde se almacenarán los PDFs definitivos?
3. ¿Qué campos no fiscales puede personalizar cada tenant en su template?
4. ¿Quién puede publicar una versión de template?
5. ¿Cómo se cifrarán y rotarán los certificados multi-tenant?
6. ¿Cuál es el tratamiento contable de señas y saldos?
7. ¿Base64 seguirá siendo el contrato definitivo o se reemplazará por una referencia firmada?

## 11. Próxima reunión / acción recomendada

Para la próxima sesión técnica:

1. aprobar el contrato del MVP;
2. diseñar el schema y la factura real;
3. asignar dueño de la idempotencia;
4. decidir almacenamiento del PDF;
5. preparar un caso de homologación con importes y receptor controlados;
6. ejecutar una emisión completa y documentar el resultado.
