# Integración con creadorpdf

La tool `emitir_comprobante_con_pdf` ejecuta un único flujo de negocio:

1. asigna la numeración y solicita el CAE;
2. si ARCA rechaza la emisión, devuelve el resultado fiscal sin PDF;
3. si ARCA autoriza, agrega el resultado bajo el campo reservado `fiscal`;
4. envía esos datos y una referencia de template publicado a creadorpdf;
5. devuelve el resultado fiscal y `pdfBase64` a SecretarIA.

`templateData` debe ser un objeto JSON y no puede definir `fiscal`. Esto impide que el caller reemplace CAE, número o importes autorizados.

Configuración:

```text
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=sk-creadorpdf-...
```

La clave de creadorpdf pertenece exclusivamente a ARCA-MCP y necesita `templates:read` y `documents:render`. SecretarIA nunca recibe esa credencial.
