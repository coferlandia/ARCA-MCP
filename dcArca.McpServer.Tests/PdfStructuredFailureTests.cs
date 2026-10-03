using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class PdfStructuredFailureTests
{
    [Theory]
    [InlineData(PdfFailureKind.InvalidRequest, "PDF_INVALID_REQUEST", 422, "validation_error")]
    [InlineData(PdfFailureKind.Forbidden, "PDF_PROVIDER_FORBIDDEN", 403, "forbidden")]
    [InlineData(PdfFailureKind.TemplateNotFound, "PDF_TEMPLATE_NOT_FOUND", 404, "unknown_template")]
    [InlineData(PdfFailureKind.RateLimited, "PDF_RATE_LIMITED", 429, "rate_limited")]
    [InlineData(PdfFailureKind.Unavailable, "PDF_UNAVAILABLE", 503, "service_unavailable")]
    public async Task StructuredProviderError_MapsToStablePublicResult(
        PdfFailureKind kind,
        string expectedCode,
        int status,
        string providerCode)
    {
        var client = new ThrowingPdfClient(new CreadorPdfException(
            kind,
            PdfFailureContract.ToSafeMessage(kind),
            status,
            providerCode));
        var renderer = new PdfDocumentRenderer(client);

        var result = await renderer.RenderAsync(
            Snapshot(),
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { cliente = "seguro" }));

        Assert.Equal(PdfRenderStatus.Failed, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("creadorpdf", result.Provider);
        Assert.Equal(status, result.ProviderStatusCode);
        Assert.Equal(providerCode, result.ProviderErrorCode);
        Assert.Equal(kind, result.FailureKind);
        Assert.Null(result.Base64);
    }

    [Fact]
    public async Task ExplicitCallerCancellation_IsNotConvertedToPdfFailure()
    {
        var renderer = new PdfDocumentRenderer(new CancellationPdfClient());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderAsync(
            Snapshot(),
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { }),
            cancellation.Token));
    }

    private static FiscalDocumentSnapshot Snapshot() => new()
    {
        Environment = "produccion",
        Provenance = new FiscalSnapshotProvenance("authorized-context", "test", "test"),
        AvailableFields = ["importeTotal", "cae"],
        EmisorCuit = "20123456786",
        PuntoVenta = 7,
        TipoComprobante = 6,
        NumeroComprobante = 123,
        Concepto = 1,
        DocumentoReceptorTipo = 80,
        DocumentoReceptorNumero = 20333444559,
        FechaComprobante = "20261001",
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private sealed class ThrowingPdfClient(Exception exception) : IPdfClient
    {
        public void ValidateConfiguration() { }

        public Task<byte[]> RenderAsync(
            PdfTemplateReference template,
            JsonElement data,
            CancellationToken cancellationToken = default)
            => Task.FromException<byte[]>(exception);
    }

    private sealed class CancellationPdfClient : IPdfClient
    {
        public void ValidateConfiguration() { }

        public Task<byte[]> RenderAsync(
            PdfTemplateReference template,
            JsonElement data,
            CancellationToken cancellationToken = default)
            => Task.FromCanceled<byte[]>(cancellationToken);
    }
}
