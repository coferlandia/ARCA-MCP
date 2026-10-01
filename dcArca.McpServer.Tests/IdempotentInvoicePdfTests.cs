using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class IdempotentInvoicePdfTests
{
    [Fact]
    public async Task PdfFallaYRetry_MismaKey_NoVuelveAEmitir()
    {
        var wsfe = new FakeWsfeClient();
        var store = new MemoryStore();
        var config = Config();
        var issuer = new McpInvoiceSequencer(wsfe, config, store);
        var renderer = new SequencedRenderer(
            new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_UNAVAILABLE", "down"),
            new PdfRenderResult(PdfRenderStatus.Rendered, "JVBERg==", null, null));
        var service = new InvoicePdfService(issuer, renderer, config);

        var first = await service.EmitAsync(
            Request(), "payment-123", new PdfTemplateReference("tpl", "1"), JsonSerializer.SerializeToElement(new { }));
        var second = await service.EmitAsync(
            Request(), "payment-123", new PdfTemplateReference("tpl", "1"), JsonSerializer.SerializeToElement(new { }));

        Assert.True(first.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.Failed, first.Pdf.Status);
        Assert.True(second.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.Rendered, second.Pdf.Status);
        Assert.Equal(1, wsfe.EmitCallCount);
        Assert.Equal(2, renderer.RenderCallCount);
        Assert.Equal(first.Fiscal.NumeroComprobante, second.Fiscal.NumeroComprobante);
    }

    [Fact]
    public async Task NuevaInstancia_MismaKey_RecuperaResultadoSinEmitir()
    {
        using var temp = new TempDirectory();
        var wsfe = new FakeWsfeClient();
        var config = Config();
        var first = new McpInvoiceSequencer(
            wsfe,
            config,
            new FileSystemEmissionIdempotencyStore(temp.Path));

        var emitted = await first.EmitAsync(Request(), "restart-key");
        Assert.True(emitted.Success);
        Assert.Equal(1, wsfe.EmitCallCount);

        var second = new McpInvoiceSequencer(
            wsfe,
            config,
            new FileSystemEmissionIdempotencyStore(temp.Path));
        var replay = await second.EmitAsync(Request(), "restart-key");

        Assert.True(replay.Success);
        Assert.Equal(emitted.NumeroComprobante, replay.NumeroComprobante);
        Assert.Equal(emitted.Cae, replay.Cae);
        Assert.Equal(1, wsfe.EmitCallCount);
    }

    private static dcArcaConfig Config() => new() { Cuit = "20123456786", PuntoVenta = 7 };

    private static dcFacturaRequest Request() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        FechaComprobante = "20261001"
    };

    private sealed class FakeWsfeClient : IdcWsfeClient
    {
        private long _last;
        public int EmitCallCount { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse { Success = true, NumeroComprobante = _last });

        public Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCallCount++;
            _last = factura.NumeroComprobante!.Value;
            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = _last,
                Cae = "CAE" + _last,
                CaeVencimiento = "20261011",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            });
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse
            {
                Success = numeroComprobante <= _last,
                NumeroComprobante = numeroComprobante,
                Cae = numeroComprobante <= _last ? "CAE" + numeroComprobante : string.Empty,
                CaeVencimiento = "20261011",
                Resultado = numeroComprobante <= _last ? "A" : string.Empty
            });

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());
    }

    private sealed class SequencedRenderer(params PdfRenderResult[] results) : IPdfDocumentRenderer
    {
        private int _index;
        public int RenderCallCount { get; private set; }

        public void ValidateRequest(PdfTemplateReference template, JsonElement templateData) { }
        public void ValidateConfiguration() { }

        public Task<PdfRenderResult> RenderAsync(
            FiscalDocumentSnapshot fiscal,
            PdfTemplateReference template,
            JsonElement templateData,
            CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            var result = results[Math.Min(_index, results.Length - 1)];
            _index++;
            return Task.FromResult(result);
        }
    }

    private sealed class MemoryStore : IEmissionIdempotencyStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, EmissionIdempotencyRecord> _records = new();

        public Task<EmissionIdempotencyRecord> GetOrCreateAsync(
            string keyHash,
            string requestHash,
            dcTipoComprobante tipoComprobante,
            int puntoVenta,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_records.TryGetValue(keyHash, out var existing))
                {
                    if (existing.RequestHash != requestHash) throw new EmissionIdempotencyConflictException();
                    return Task.FromResult(existing);
                }
                var now = DateTimeOffset.UtcNow;
                var created = new EmissionIdempotencyRecord(
                    keyHash, requestHash, (int)tipoComprobante, puntoVenta, null,
                    EmissionIdempotencyState.Created, null, now, now);
                _records[keyHash] = created;
                return Task.FromResult(created);
            }
        }

        public Task<EmissionIdempotencyRecord?> GetAsync(string keyHash, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _records.TryGetValue(keyHash, out var value);
                return Task.FromResult(value);
            }
        }

        public Task SaveAsync(EmissionIdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _records[record.KeyHash] = record;
                return Task.CompletedTask;
            }
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "arca-restart-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
