using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.McpServer;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpInvoiceSequencerTests
{
    [Fact]
    public async Task MismaIdempotencyKey_Concurrente_EmiteUnaSolaVez()
    {
        var fake = new FakeWsfeClient(delayMs: 50);
        var store = new MemoryStore();
        var sequencer = new McpInvoiceSequencer(fake, Config(1), store);

        var first = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "same-key");
        var second = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "same-key");

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.Success));
        Assert.Single(fake.EmittedNumbers);
        Assert.Equal(results[0].NumeroComprobante, results[1].NumeroComprobante);
    }

    [Fact]
    public async Task KeysDiferentes_RecibenNumerosConsecutivos()
    {
        var fake = new FakeWsfeClient();
        var sequencer = new McpInvoiceSequencer(fake, Config(1), new MemoryStore());

        var first = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "key-1");
        var second = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "key-2");

        Assert.Equal([1L, 2L], new[] { first.NumeroComprobante, second.NumeroComprobante });
        Assert.Equal([1L, 2L], fake.EmittedNumbers.ToArray());
    }

    [Fact]
    public async Task MismaKey_RequestDistinto_SeRechazaSinEmitirOtraVez()
    {
        var fake = new FakeWsfeClient();
        var sequencer = new McpInvoiceSequencer(fake, Config(1), new MemoryStore());

        var first = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "business-key");
        var changed = Request(dcTipoComprobante.FacturaB);
        changed.ImporteTotal = 122m;

        var second = await sequencer.EmitAsync(changed, "business-key");

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", second.Codigo);
        Assert.Single(fake.EmittedNumbers);
    }

    [Fact]
    public async Task MismaKey_OtroContextoFiscal_NoReusaResultadoAnterior()
    {
        var fake = new FakeWsfeClient();
        var store = new MemoryStore();
        var firstSequencer = new McpInvoiceSequencer(fake, Config(1), store, Provider(Config(1), "consumer-a", "ctx-1"));
        var secondSequencer = new McpInvoiceSequencer(fake, Config(2), store, Provider(Config(2), "consumer-a", "ctx-2"));

        var first = await firstSequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "same-business-key");
        var second = await secondSequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "same-business-key");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, fake.EmittedNumbers.Count);
        Assert.NotEqual(
            EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-1", "same-business-key"),
            EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-2", "same-business-key"));
    }

    [Fact]
    public async Task MismaKey_OtroConsumidor_NoAccedeALaOperacionAnterior()
    {
        var fake = new FakeWsfeClient();
        var store = new MemoryStore();
        var config = Config(1);
        var firstSequencer = new McpInvoiceSequencer(fake, config, store, Provider(config, "consumer-a", "ctx-1"));
        var secondSequencer = new McpInvoiceSequencer(fake, config, store, Provider(config, "consumer-b", "ctx-1"));

        var first = await firstSequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "shared-text-key");
        var second = await secondSequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "shared-text-key");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, fake.EmittedNumbers.Count);
    }

    [Fact]
    public async Task TiposDiferentes_NoCompartenLock()
    {
        var fake = new FakeWsfeClient(delayMs: 100);
        var store = new MemoryStore();
        var sequencer = new McpInvoiceSequencer(fake, Config(1), store);

        var a = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaA), "key-a");
        var b = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "key-b");

        await Task.WhenAll(a, b);

        Assert.True(fake.MaxConcurrentOperations >= 2);
    }

    [Fact]
    public async Task ResultadoIncierto_SeReconciliaConElMismoNumero()
    {
        var fake = new FakeWsfeClient { ReturnUncertainOnNextIssue = true };
        var sequencer = new McpInvoiceSequencer(fake, Config(1), new MemoryStore());

        var first = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "uncertain-key");
        Assert.Equal(dcEmissionOutcome.Uncertain, first.EmissionOutcome);
        Assert.Single(fake.EmittedNumbers);

        fake.ConsultedAuthorizedNumbers.Add(first.NumeroComprobante);
        var retry = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "uncertain-key");

        Assert.True(retry.Success);
        Assert.Equal(dcEmissionOutcome.RecoveredSuccess, retry.EmissionOutcome);
        Assert.Single(fake.EmittedNumbers);
    }

    [Fact]
    public async Task FalloNoClasificado_QuedaUncertain_YNoSeMarcaComoRechazoTerminal()
    {
        var fake = new FakeWsfeClient { ReturnUnclassifiedFailureOnNextIssue = true };
        var store = new MemoryStore();
        var sequencer = new McpInvoiceSequencer(fake, Config(1), store);

        var first = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "technical-failure-key");
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            "test-consumer",
            "test-context",
            "technical-failure-key");
        var stored = await store.GetAsync(keyHash);

        Assert.False(first.Success);
        Assert.Equal(dcEmissionOutcome.None, first.EmissionOutcome);
        Assert.NotNull(stored);
        Assert.Equal(EmissionIdempotencyState.Uncertain, stored!.State);

        var retry = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), "technical-failure-key");
        Assert.Equal(dcEmissionOutcome.Uncertain, retry.EmissionOutcome);
        Assert.Single(fake.EmittedNumbers);
    }

    private static dcFacturaRequest Request(dcTipoComprobante tipo) => new()
    {
        TipoComprobante = tipo,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        FechaComprobante = "20260930"
    };

    private static dcArcaConfig Config(int puntoVenta) => new()
    {
        Cuit = "20123456786",
        PuntoVenta = puntoVenta,
        WsfeUrl = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx"
    };

    private static IFiscalOperationIdentityProvider Provider(
        dcArcaConfig config,
        string consumerId,
        string contextId)
        => new SingleFiscalOperationIdentityProvider(
            config,
            new SingleFiscalContextOptions(
                consumerId,
                contextId,
                "homologacion",
                1,
                "cred-v1"));

    private sealed class MemoryStore : IEmissionIdempotencyStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, EmissionIdempotencyRecord> _records = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FiscalContextDescriptor> _contexts = new(StringComparer.Ordinal);

        public Task EnsureContextAsync(FiscalContextDescriptor context, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_contexts.TryGetValue(context.ContextId, out var existing)
                    && existing != context)
                    throw new InvalidDataException("FISCAL_CONTEXT_IDENTITY_MISMATCH");
                _contexts[context.ContextId] = context;
                return Task.CompletedTask;
            }
        }

        public Task<EmissionIdempotencyRecord> GetOrCreateAsync(
            string keyHash,
            string requestHash,
            int requestCanonicalizationVersion,
            FiscalOperationIdentity identity,
            StoredFiscalEvidence fiscalEvidence,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_records.TryGetValue(keyHash, out var existing))
                {
                    if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
                        || existing.RequestCanonicalizationVersion != requestCanonicalizationVersion
                        || !existing.Identity.MatchesImmutableIdentity(identity))
                        throw new EmissionIdempotencyConflictException();
                    return Task.FromResult(existing);
                }

                var now = DateTimeOffset.UtcNow;
                var created = new EmissionIdempotencyRecord(
                    FileSystemEmissionIdempotencyStore.CurrentSchemaVersion,
                    keyHash,
                    requestHash,
                    requestCanonicalizationVersion,
                    identity,
                    fiscalEvidence,
                    null,
                    EmissionIdempotencyState.Created,
                    null,
                    now,
                    now);
                _records[keyHash] = created;
                return Task.FromResult(created);
            }
        }

        public Task<EmissionIdempotencyRecord?> GetAsync(string keyHash, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _records.TryGetValue(keyHash, out var record);
                return Task.FromResult(record);
            }
        }

        public Task SaveAsync(EmissionIdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_records.TryGetValue(record.KeyHash, out var existing)
                    && (!string.Equals(existing.RequestHash, record.RequestHash, StringComparison.Ordinal)
                        || !existing.Identity.MatchesImmutableIdentity(record.Identity)))
                    throw new EmissionIdempotencyConflictException();
                _records[record.KeyHash] = record;
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FakeWsfeClient : IdcWsfeClient
    {
        private readonly int _delayMs;
        private long _lastNumber;
        private int _activeOperations;
        private int _maxConcurrentOperations;

        internal FakeWsfeClient(int delayMs = 0) => _delayMs = delayMs;

        internal List<long> EmittedNumbers { get; } = new();
        internal HashSet<long> ConsultedAuthorizedNumbers { get; } = new();
        internal int MaxConcurrentOperations => _maxConcurrentOperations;
        internal bool ReturnUncertainOnNextIssue { get; set; }
        internal bool ReturnUnclassifiedFailureOnNextIssue { get; set; }

        public async Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _activeOperations);
            UpdateMax(active);
            try
            {
                if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
                return new dcFacturaResponse { Success = true, NumeroComprobante = Interlocked.Read(ref _lastNumber) };
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        public async Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
            var number = factura.NumeroComprobante!.Value;
            Interlocked.Exchange(ref _lastNumber, Math.Max(Interlocked.Read(ref _lastNumber), number));
            lock (EmittedNumbers) EmittedNumbers.Add(number);

            if (ReturnUncertainOnNextIssue)
            {
                ReturnUncertainOnNextIssue = false;
                return new dcFacturaResponse
                {
                    Success = false,
                    NumeroComprobante = number,
                    Codigo = "EMISSION_UNCERTAIN",
                    EmissionOutcome = dcEmissionOutcome.Uncertain
                };
            }

            if (ReturnUnclassifiedFailureOnNextIssue)
            {
                ReturnUnclassifiedFailureOnNextIssue = false;
                return new dcFacturaResponse
                {
                    Success = false,
                    NumeroComprobante = number,
                    Codigo = "TECHNICAL_ERROR",
                    Mensaje = "simulated technical failure",
                    EmissionOutcome = dcEmissionOutcome.None
                };
            }

            return new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = number,
                Cae = "CAE" + number,
                CaeVencimiento = "20261011",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            };
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            if (ConsultedAuthorizedNumbers.Contains(numeroComprobante))
            {
                return Task.FromResult(new dcFacturaResponse
                {
                    Success = true,
                    NumeroComprobante = numeroComprobante,
                    Cae = "CAE" + numeroComprobante,
                    CaeVencimiento = "20261011",
                    Resultado = "A"
                });
            }

            return Task.FromResult(new dcFacturaResponse { Success = false, NumeroComprobante = numeroComprobante });
        }

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());

        private void UpdateMax(int active)
        {
            int snapshot;
            do
            {
                snapshot = _maxConcurrentOperations;
                if (active <= snapshot) return;
            } while (Interlocked.CompareExchange(ref _maxConcurrentOperations, active, snapshot) != snapshot);
        }
    }
}
