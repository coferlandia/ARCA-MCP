using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionIdempotencyStoreTests
{
    [Fact]
    public async Task RegistroAutorizado_SobreviveNuevaInstancia()
    {
        using var temp = new TempDirectory();
        var keyHash = new string('a', 64);
        var requestHash = new string('b', 64);
        var firstStore = new FileSystemEmissionIdempotencyStore(temp.Path);

        var created = await firstStore.GetOrCreateAsync(
            keyHash,
            requestHash,
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(Request()));
        var response = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 123,
            Cae = "CAE123",
            CaeVencimiento = "20261011",
            Resultado = "A",
            EmissionOutcome = dcEmissionOutcome.Authorized
        };
        await firstStore.SaveAsync(created with
        {
            NumeroComprobante = 123,
            State = EmissionIdempotencyState.Authorized,
            FiscalResult = StoredFiscalResult.FromResponse(response),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var secondStore = new FileSystemEmissionIdempotencyStore(temp.Path);
        var recovered = await secondStore.GetAsync(keyHash);

        Assert.NotNull(recovered);
        Assert.Equal(FileSystemEmissionIdempotencyStore.CurrentSchemaVersion, recovered!.SchemaVersion);
        Assert.Equal(EmissionIdempotencyState.Authorized, recovered.State);
        Assert.Equal(123, recovered.NumeroComprobante);
        Assert.Equal("CAE123", recovered.FiscalResult!.Cae);
        Assert.Equal("consumer-a", recovered.Identity.ConsumerId);
        Assert.Equal("ctx-a", recovered.Identity.ContextId);
        Assert.Equal(FiscalEvidenceState.Complete, recovered.FiscalEvidence.State);
    }

    [Fact]
    public async Task MismaKeyHashConOtroRequestHash_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var keyHash = new string('a', 64);

        await store.GetOrCreateAsync(
            keyHash,
            new string('b', 64),
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(Request()));

        await Assert.ThrowsAsync<EmissionIdempotencyConflictException>(() =>
            store.GetOrCreateAsync(
                keyHash,
                new string('c', 64),
                EmissionRequestFingerprint.CanonicalizationVersion,
                Identity(),
                StoredFiscalEvidence.FromRequest(Request())));
    }

    [Fact]
    public async Task MismaOperacionConOtroConsumidor_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var keyHash = new string('a', 64);
        var requestHash = new string('b', 64);

        await store.GetOrCreateAsync(
            keyHash,
            requestHash,
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(Request()));

        await Assert.ThrowsAsync<EmissionIdempotencyConflictException>(() =>
            store.GetOrCreateAsync(
                keyHash,
                requestHash,
                EmissionRequestFingerprint.CanonicalizationVersion,
                Identity(consumerId: "consumer-b"),
                StoredFiscalEvidence.FromRequest(Request())));
    }

    [Fact]
    public async Task RotacionDeCredencial_NoCambiaIdentidadDeOperacionExistente()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var keyHash = new string('a', 64);
        var requestHash = new string('b', 64);

        var original = await store.GetOrCreateAsync(
            keyHash,
            requestHash,
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(credentialRevision: "personal-v1"),
            StoredFiscalEvidence.FromRequest(Request()));

        var replay = await store.GetOrCreateAsync(
            keyHash,
            requestHash,
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(credentialRevision: "empresa-v2"),
            StoredFiscalEvidence.FromRequest(Request()));

        Assert.Equal(original.CreatedAt, replay.CreatedAt);
        Assert.Equal("personal-v1", replay.Identity.CredentialAssignmentRevision);
    }

    [Fact]
    public async Task ContextIdNoPuedeReasignarseAOtroCuit()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);

        await store.EnsureContextAsync(Identity().Context);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.EnsureContextAsync(Identity(cuit: 30999999991).Context));
        Assert.Equal("FISCAL_CONTEXT_IDENTITY_MISMATCH", exception.Message);
    }

    [Fact]
    public async Task JsonCorrupto_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var keyHash = new string('a', 64);
        File.WriteAllText(System.IO.Path.Combine(temp.Path, keyHash + ".json"), "{invalid-json");
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(keyHash));
    }

    [Fact]
    public async Task RegistroLegacySinMigrar_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var keyHash = new string('a', 64);
        File.WriteAllText(
            System.IO.Path.Combine(temp.Path, keyHash + ".json"),
            $$"""
            {
              "KeyHash":"{{keyHash}}",
              "RequestHash":"{{new string('b', 64)}}",
              "TipoComprobante":6,
              "PuntoVenta":7,
              "NumeroComprobante":null,
              "State":0,
              "FiscalResult":null,
              "CreatedAt":"2026-10-01T00:00:00+00:00",
              "UpdatedAt":"2026-10-01T00:00:00+00:00"
            }
            """);
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(keyHash));
    }

    private static FiscalOperationIdentity Identity(
        string consumerId = "consumer-a",
        string contextId = "ctx-a",
        string environment = "homologacion",
        long cuit = 20123456786,
        int puntoVenta = 7,
        int tipoComprobante = 6,
        int contextRevision = 1,
        string credentialRevision = "cred-v1")
        => new(
            consumerId,
            contextId,
            environment,
            cuit,
            puntoVenta,
            tipoComprobante,
            contextRevision,
            credentialRevision);

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
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261001"
    };

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-idempotency-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
