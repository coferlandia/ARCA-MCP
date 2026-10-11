using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace dcArca.McpServer;

/// <summary>
/// Stable, client-safe error payload for MCP tool failures.
/// </summary>
public sealed record ArcaMcpToolError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("safeMessage")] string SafeMessage);

public static class ArcaMcpToolErrorBoundary
{
    public const string InternalErrorCode = "ARCA_MCP_INTERNAL_ERROR";
    public const string InternalErrorMessage =
        "Ocurrió un error interno al procesar la herramienta fiscal.";

    /// <summary>
    /// Converts expected fiscal/domain failures into MCP tool errors before the SDK
    /// replaces non-McpException failures with its generic invocation message.
    /// Register after AddAuthorizationFilters so protocol authorization behavior remains unchanged.
    /// </summary>
    public static IMcpServerBuilder AddFiscalToolErrorBoundary(this IMcpServerBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        return builder.WithRequestFilters(filters =>
            filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                try
                {
                    return await next(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var toolName = context.Params?.Name ?? "<unknown>";
                    var contextId = SafeContextId(context.Params);
                    var correlationId = context.Services?
                        .GetService<IHttpContextAccessor>()?
                        .HttpContext?
                        .TraceIdentifier;
                    var logger = context.Services?
                        .GetService<ILoggerFactory>()?
                        .CreateLogger("dcArca.McpServer.ArcaMcpToolErrorBoundary");

                    if (TryClassifyExpected(exception, out var code, out var safeMessage))
                    {
                        logger?.LogWarning(
                            "MCP fiscal tool rejected request. Tool={ToolName} ErrorCode={ErrorCode} ContextId={ContextId} CorrelationId={CorrelationId}",
                            toolName, code, contextId, correlationId);
                        return Error(code, safeMessage);
                    }

                    logger?.LogError(
                        exception,
                        "Unexpected MCP fiscal tool failure. Tool={ToolName} ContextId={ContextId} CorrelationId={CorrelationId}",
                        toolName, contextId, correlationId);
                    return Error(InternalErrorCode, InternalErrorMessage);
                }
            }));
    }

    private static CallToolResult Error(string code, string safeMessage)
    {
        var error = new ArcaMcpToolError(code, safeMessage);
        return new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonSerializer.SerializeToElement(error),
            Content =
            [
                new TextContentBlock
                {
                    Text = $"{code}: {safeMessage}"
                }
            ]
        };
    }

    private static bool TryClassifyExpected(
        Exception exception,
        out string code,
        out string safeMessage)
    {
        if (exception is FiscalContextAccessException fiscal)
        {
            code = fiscal.Code;
            safeMessage = fiscal.Message;
            return true;
        }

        // The V2 filesystem catalog still exposes a small set of repository-owned
        // machine codes as InvalidOperationException.Message. Normalize only this
        // exact whitelist; never parse arbitrary exception text or use regex matching.
        if (exception is InvalidOperationException)
        {
            switch (exception.Message)
            {
                case "FISCAL_CONTEXT_NOT_FOUND":
                    code = "FISCAL_CONTEXT_NOT_FOUND";
                    safeMessage = "No existe el contexto fiscal solicitado.";
                    return true;
                case "FISCAL_REPRESENTATION_NOT_FOUND":
                    code = "FISCAL_REPRESENTATION_NOT_FOUND";
                    safeMessage = "La representación fiscal solicitada no existe.";
                    return true;
                case "FISCAL_REPRESENTATION_ALREADY_EXISTS":
                    code = "FISCAL_REPRESENTATION_ALREADY_EXISTS";
                    safeMessage = "La representación fiscal ya existe.";
                    return true;
                case "FISCAL_REPRESENTATION_STATE_INVALID":
                    code = "FISCAL_REPRESENTATION_STATE_INVALID";
                    safeMessage = "La representación fiscal no está en un estado válido para esta operación.";
                    return true;
                case "FISCAL_REPRESENTATION_NOT_VERIFIED":
                    code = "FISCAL_REPRESENTATION_NOT_VERIFIED";
                    safeMessage = "La representación fiscal todavía no fue verificada.";
                    return true;
            }
        }

        code = string.Empty;
        safeMessage = string.Empty;
        return false;
    }

    private static string? SafeContextId(CallToolRequestParams? parameters)
    {
        if (parameters?.Arguments is null ||
            !parameters.Arguments.TryGetValue("contextId", out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;

        var contextId = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(contextId))
            return null;
        return contextId.Length <= 128 ? contextId : contextId[..128];
    }
}
