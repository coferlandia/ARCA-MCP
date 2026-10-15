namespace dcArca.McpServer;

public sealed class PdfTemplateReferenceResolutionException : Exception
{
    public string Code { get; }
    public string SafeMessage { get; }

    public PdfTemplateReferenceResolutionException(string code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
        SafeMessage = safeMessage;
    }
}

public sealed class PdfTemplateReferenceResolver(IConfiguration configuration)
{
    public PdfTemplateReference Resolve(string? templateId, string? templateVersion)
    {
        var hasTemplateId = !string.IsNullOrWhiteSpace(templateId);
        var hasTemplateVersion = !string.IsNullOrWhiteSpace(templateVersion);

        if (hasTemplateId != hasTemplateVersion)
        {
            throw new PdfTemplateReferenceResolutionException(
                "PDF_TEMPLATE_REFERENCE_INVALID",
                "templateId y templateVersion deben informarse juntos o dejarse ambos vacíos.");
        }

        if (hasTemplateId)
            return new PdfTemplateReference(templateId!, templateVersion!);

        var defaultKey = configuration["Pdf:TemplateKey"];
        var defaultVersion = configuration["Pdf:TemplateVersion"];
        if (string.IsNullOrWhiteSpace(defaultKey) || string.IsNullOrWhiteSpace(defaultVersion))
        {
            throw new PdfTemplateReferenceResolutionException(
                "PDF_TEMPLATE_DEFAULT_NOT_CONFIGURED",
                "La plantilla PDF por defecto no está configurada completamente.");
        }

        return new PdfTemplateReference(defaultKey, defaultVersion);
    }
}
