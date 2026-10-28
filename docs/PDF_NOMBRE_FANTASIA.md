# Nombre de fantasía en PDF de comprobantes (V9)

El renderizador acepta el nombre comercial opcional del emisor dentro de `templateData.emisor.nombreFantasia`, como cadena, `null` o ausente. Los campos `razonSocial`, `domicilio` y `condicionIva` siguen siendo obligatorios en la plantilla V9.

```json
{
  "emisor": {
    "nombreFantasia": "Consultorio Ejemplo",
    "razonSocial": "CONTRIBUYENTE EJEMPLO",
    "domicilio": "Domicilio comercial",
    "condicionIva": "Responsable Monotributo"
  }
}
```

El encabezado superior izquierdo muestra `nombreFantasia` cuando tiene contenido no blanco; si falta, es `null`, está vacío o contiene sólo espacios, muestra `razonSocial`. La línea **Razón Social** se conserva en todas las copias, tanto en facturas A/B/C como en notas de crédito. El texto largo se ajusta dentro del panel del emisor.

La fuente de `nombreFantasia` es el **caller**: se trata exclusivamente de un campo de presentación. `fiscal` es generado por ARCA-MCP a partir de los datos fiscales autorizados y no debe incluirse dentro de `templateData`; CAE, QR, identificación fiscal y emisión no cambian. ARCA-MCP no puede recuperar el nombre de fantasía desde `FEParamGetPtosVenta` porque ese endpoint no lo expone. El caller debe obtener el nombre declarado/configurado por sus propios medios.

## Publicación y rollback

El manifest `deploy/cadencia/pdf-templates/manifest.json` incluye `factura-ar:9` y conserva `factura-ar:8`. Publicar V9 mediante `deploy/cadencia/bootstrap-pdf-template.sh` con credencial de gestión de plantillas y verificar que el proveedor la marque `published` y visible para la key runtime **antes** de configurar `Pdf__TemplateVersion=9` en el despliegue. La configuración por defecto permanece en 8 hasta esa verificación. Para rollback, restaurar `Pdf__TemplateVersion=8` y recrear sólo el servicio MCP.

No se requiere cambio del contrato público de `generar_pdf_comprobante`: el valor opcional se incorpora al objeto `templateData.emisor`.
