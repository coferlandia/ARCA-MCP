using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace dcArca.McpServer;

internal static class OperationalReference
{
    public static string Context(string value) => HashPrefix(value);
    public static string Operation(string value)
        => string.IsNullOrWhiteSpace(value) ? "none" : value[..Math.Min(12, value.Length)].ToLowerInvariant();

    private static string HashPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "none";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}

public sealed class ObservableEmissionIdempotencyStore : IEmissionIdempotencyStore
{
    private readonly FileSystemEmissionIdempotencyStore _inner;
    private readonly ILogger<ObservableEmissionIdempotencyStore> _logger;

    public ObservableEmissionIdempotencyStore(
        FileSystemEmissionIdempotencyStore inner,
        ILogger<ObservableEmissionIdempotencyStore> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    internal FileSystemEmissionIdempotencyStore Inner => _inner;

    public async Task EnsureContextAsync(FiscalContextDescriptor context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.EnsureContextAsync(context, cancellationToken);
        }
        catch (Exception exception)
        {
            LogStoreError("ensure-context", exception, context.ContextId, null);
            throw;
        }
    }

    public async Task<EmissionIdempotencyRecord> GetOrCreateAsync(
        string keyHash,
        string requestHash,
        int requestCanonicalizationVersion,
        FiscalOperationIdentity identity,
        StoredFiscalEvidence fiscalEvidence,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var record = await _inner.GetOrCreateAsync(
                keyHash,
                requestHash,
                requestCanonicalizationVersion,
                identity,
                fiscalEvidence,
                cancellationToken);
            _logger.LogInformation(
                "Fiscal operation observed OperationRef={OperationRef} ContextRef={ContextRef} State={State} Outcome={Outcome}",
                OperationalReference.Operation(record.KeyHash),
                OperationalReference.Context(record.Identity.ContextId),
                record.State,
                record.FiscalResult?.EmissionOutcome.ToString() ?? "None");
            return record;
        }
        catch (Exception exception)
        {
            LogStoreError("get-or-create", exception, identity.ContextId, keyHash);
            throw;
        }
    }

    public async Task<EmissionIdempotencyRecord?> GetAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _inner.GetAsync(keyHash, cancellationToken);
        }
        catch (Exception exception)
        {
            LogStoreError("get", exception, null, keyHash);
            throw;
        }
    }

    public async Task SaveAsync(
        EmissionIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.SaveAsync(record, cancellationToken);
            _logger.LogInformation(
                "Fiscal operation persisted OperationRef={OperationRef} ContextRef={ContextRef} State={State} Outcome={Outcome} Code={Code}",
                OperationalReference.Operation(record.KeyHash),
                OperationalReference.Context(record.Identity.ContextId),
                record.State,
                record.FiscalResult?.EmissionOutcome.ToString() ?? "None",
                record.FiscalResult?.Codigo ?? "none");
        }
        catch (Exception exception)
        {
            LogStoreError("save", exception, record.Identity.ContextId, record.KeyHash);
            throw;
        }
    }

    private void LogStoreError(string action, Exception exception, string? contextId, string? keyHash)
        => _logger.LogError(
            "Fiscal store error Action={Action} ErrorType={ErrorType} ContextRef={ContextRef} OperationRef={OperationRef}",
            action,
            exception.GetType().Name,
            OperationalReference.Context(contextId ?? string.Empty),
            OperationalReference.Operation(keyHash ?? string.Empty));
}

public sealed class ObservableFiscalSeriesCoordinator : IFiscalSeriesCoordinator
{
    private readonly FileSystemFiscalSeriesCoordinator _inner;
    private readonly ILogger<ObservableFiscalSeriesCoordinator> _logger;

    public ObservableFiscalSeriesCoordinator(
        FileSystemFiscalSeriesCoordinator inner,
        ILogger<ObservableFiscalSeriesCoordinator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Task InitializeAsync(IEmissionIdempotencyStore store, CancellationToken cancellationToken = default)
        => _inner.InitializeAsync(
            store is ObservableEmissionIdempotencyStore observable ? observable.Inner : store,
            cancellationToken);

    public Task<FiscalSeriesReservation?> GetActiveAsync(
        FiscalOperationIdentity identity,
        CancellationToken cancellationToken = default)
        => _inner.GetActiveAsync(identity, cancellationToken);

    public async Task<FiscalSeriesReservation> ReserveAsync(
        FiscalOperationIdentity identity,
        string ownerKeyHash,
        long numeroComprobante,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _inner.ReserveAsync(identity, ownerKeyHash, numeroComprobante, cancellationToken);
            _logger.LogInformation(
                "Fiscal series reservation active ContextRef={ContextRef} OperationRef={OperationRef} Environment={Environment} InvoiceType={InvoiceType}",
                OperationalReference.Context(identity.ContextId),
                OperationalReference.Operation(ownerKeyHash),
                identity.Environment,
                identity.TipoComprobante);
            return result;
        }
        catch (FiscalSeriesBlockedException)
        {
            _logger.LogWarning(
                "Fiscal series reservation blocked ContextRef={ContextRef} OperationRef={OperationRef} Environment={Environment} InvoiceType={InvoiceType}",
                OperationalReference.Context(identity.ContextId),
                OperationalReference.Operation(ownerKeyHash),
                identity.Environment,
                identity.TipoComprobante);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Fiscal series error Action=reserve ErrorType={ErrorType} ContextRef={ContextRef} OperationRef={OperationRef}",
                exception.GetType().Name,
                OperationalReference.Context(identity.ContextId),
                OperationalReference.Operation(ownerKeyHash));
            throw;
        }
    }

    public async Task ReleaseAsync(
        FiscalOperationIdentity identity,
        string ownerKeyHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.ReleaseAsync(identity, ownerKeyHash, cancellationToken);
            _logger.LogInformation(
                "Fiscal series reservation released ContextRef={ContextRef} OperationRef={OperationRef} Environment={Environment} InvoiceType={InvoiceType}",
                OperationalReference.Context(identity.ContextId),
                OperationalReference.Operation(ownerKeyHash),
                identity.Environment,
                identity.TipoComprobante);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Fiscal series error Action=release ErrorType={ErrorType} ContextRef={ContextRef} OperationRef={OperationRef}",
                exception.GetType().Name,
                OperationalReference.Context(identity.ContextId),
                OperationalReference.Operation(ownerKeyHash));
            throw;
        }
    }

    // The concrete singleton is owned/disposed separately by DI.
    public void Dispose() { }
}

public sealed class ObservablePdfDocumentRenderer : IPdfDocumentRenderer
{
    private readonly PdfDocumentRenderer _inner;
    private readonly ILogger<ObservablePdfDocumentRenderer> _logger;

    public ObservablePdfDocumentRenderer(
        PdfDocumentRenderer inner,
        ILogger<ObservablePdfDocumentRenderer> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public void ValidateRequest(PdfTemplateReference template, JsonElement templateData)
        => _inner.ValidateRequest(template, templateData);

    public void ValidateConfiguration() => _inner.ValidateConfiguration();

    public async Task<PdfRenderResult> RenderAsync(
        FiscalDocumentSnapshot fiscal,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _inner.RenderAsync(fiscal, template, templateData, cancellationToken);
            _logger.LogInformation(
                "Fiscal PDF render completed Status={Status} Code={Code} Provenance={Provenance}",
                result.Status,
                result.ErrorCode ?? "none",
                fiscal.Provenance);
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Fiscal PDF render error ErrorType={ErrorType} Provenance={Provenance}",
                exception.GetType().Name,
                fiscal.Provenance);
            throw;
        }
    }
}
