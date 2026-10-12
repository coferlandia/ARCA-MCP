using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed record FiscalContextDescriptor(
    string ContextId,
    string Environment,
    long Cuit,
    int PuntoVenta);

public sealed record FiscalRepresentationProvenance(
    DateTimeOffset CreatedAt,
    DateTimeOffset ActivatedAt);

public sealed record FiscalOperationIdentity(
    string ConsumerId,
    string ContextId,
    string Environment,
    long Cuit,
    int PuntoVenta,
    int TipoComprobante,
    int ContextRevision,
    string CredentialAssignmentRevision,
    FiscalRepresentationProvenance? RepresentationProvenance = null)
{
    public FiscalContextDescriptor Context => new(ContextId, Environment, Cuit, PuntoVenta);

    public string SeriesKey =>
        $"{FiscalEnvironmentId.RequireCanonical(Environment)}:{Cuit}:{PuntoVenta}:{TipoComprobante}";

    public bool MatchesImmutableIdentity(FiscalOperationIdentity other)
        => string.Equals(ConsumerId, other.ConsumerId, StringComparison.Ordinal)
            && string.Equals(ContextId, other.ContextId, StringComparison.Ordinal)
            && string.Equals(
                FiscalEnvironmentId.RequireCanonical(Environment),
                FiscalEnvironmentId.RequireCanonical(other.Environment),
                StringComparison.Ordinal)
            && Cuit == other.Cuit
            && PuntoVenta == other.PuntoVenta
            && TipoComprobante == other.TipoComprobante
            && (RepresentationProvenance is null
                || other.RepresentationProvenance is null
                || RepresentationProvenance == other.RepresentationProvenance);
}

public sealed record SingleFiscalContextOptions(
    string ConsumerId,
    string ContextId,
    string Environment,
    int ContextRevision,
    string CredentialAssignmentRevision,
    FiscalRepresentationProvenance? RepresentationProvenance = null)
{
    public static SingleFiscalContextOptions FromConfiguration(IConfiguration configuration)
    {
        static string Required(IConfiguration configuration, string key)
            => configuration[key]?.Trim() is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException($"Falta configuración obligatoria FiscalContext:{key}.");

        var revisionText = configuration["ContextRevision"]?.Trim();
        var revision = string.IsNullOrWhiteSpace(revisionText)
            ? 1
            : int.TryParse(revisionText, out var parsed) && parsed > 0
                ? parsed
                : throw new InvalidOperationException("FiscalContext:ContextRevision debe ser un entero positivo.");

        return new SingleFiscalContextOptions(
            Required(configuration, "ConsumerId"),
            Required(configuration, "ContextId"),
            FiscalEnvironmentId.Normalize(Required(configuration, "Environment")),
            revision,
            Required(configuration, "CredentialAssignmentRevision"));
    }
}

public interface IFiscalOperationIdentityProvider
{
    FiscalOperationIdentity For(dcTipoComprobante tipoComprobante);
}

public sealed class SingleFiscalOperationIdentityProvider : IFiscalOperationIdentityProvider
{
    private readonly dcArcaConfig _arcaConfig;
    private readonly SingleFiscalContextOptions _options;
    private readonly long _cuit;

    public SingleFiscalOperationIdentityProvider(dcArcaConfig arcaConfig, SingleFiscalContextOptions options)
    {
        _arcaConfig = arcaConfig;
        _options = options;
        if (!long.TryParse(arcaConfig.Cuit, out _cuit) || _cuit <= 0)
            throw new InvalidOperationException("dcArcaConfig:Cuit debe ser un CUIT numérico para construir la identidad fiscal.");
        if (arcaConfig.PuntoVenta <= 0)
            throw new InvalidOperationException("dcArcaConfig:PuntoVenta debe ser positivo para construir la identidad fiscal.");
    }

    public FiscalOperationIdentity For(dcTipoComprobante tipoComprobante)
        => new(
            _options.ConsumerId,
            _options.ContextId,
            FiscalEnvironmentId.Normalize(_options.Environment),
            _cuit,
            _arcaConfig.PuntoVenta,
            (int)tipoComprobante,
            _options.ContextRevision,
            _options.CredentialAssignmentRevision,
            _options.RepresentationProvenance);

    public static SingleFiscalOperationIdentityProvider ForTests(dcArcaConfig arcaConfig)
        => new(
            arcaConfig,
            new SingleFiscalContextOptions(
                "test-consumer",
                "test-context",
                arcaConfig.WsfeUrl.Contains("homo", StringComparison.OrdinalIgnoreCase) ? "homologacion" : "produccion",
                1,
                "test-credential-1"));
}
