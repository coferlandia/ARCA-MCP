using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FiscalSeriesCoordinatorTests
{
    [Fact]
    public async Task Reservation_BlocksOtherOwner_ButIsIdempotentForSameOwner()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var identity = Identity();
        var owner = new string('a', 64);

        var first = await coordinator.ReserveAsync(identity, owner, 10);
        var replay = await coordinator.ReserveAsync(identity, owner, 10);

        Assert.Equal(first, replay);
        await Assert.ThrowsAsync<FiscalSeriesBlockedException>(() =>
            coordinator.ReserveAsync(identity, new string('b', 64), 11));
    }

    [Fact]
    public async Task SecondWriterOverSameStore_IsRejected()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var first = new FileSystemFiscalSeriesCoordinator(temp.Path);
        using var second = new FileSystemFiscalSeriesCoordinator(temp.Path);

        await first.InitializeAsync(store);
        var exception = await Assert.ThrowsAsync<FiscalSeriesWriterBusyException>(() => second.InitializeAsync(store));

        Assert.Equal("SERIES_WRITER_BUSY", exception.Message);
    }

    [Fact]
    public async Task Restart_RepairsCrashAfterReservationBeforeNumberAssigned()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var identity = Identity();
        var keyHash = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));

        using (var first = new FileSystemFiscalSeriesCoordinator(temp.Path))
        {
            await first.InitializeAsync(store);
            await first.ReserveAsync(identity, keyHash, 42);
        }

        using var restarted = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await restarted.InitializeAsync(store);
        var repaired = await store.GetAsync(keyHash);
        var reservation = await restarted.GetActiveAsync(identity);

        Assert.NotNull(repaired);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, repaired!.State);
        Assert.Equal(42, repaired.NumeroComprobante);
        Assert.NotNull(reservation);
        Assert.Equal(keyHash, reservation!.OwnerKeyHash);
        Assert.Equal(42, reservation.NumeroComprobante);
    }

    [Fact]
    public async Task Restart_RebuildsMissingReservationFromPendingOperation()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var identity = Identity();
        var keyHash = new string('a', 64);
        var request = Request();
        var created = await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await store.SaveAsync(created with
        {
            NumeroComprobante = 7,
            State = EmissionIdempotencyState.NumberAssigned,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var reservation = await coordinator.GetActiveAsync(identity);

        Assert.NotNull(reservation);
        Assert.Equal(keyHash, reservation!.OwnerKeyHash);
        Assert.Equal(7, reservation.NumeroComprobante);
    }

    [Fact]
    public async Task Restart_CleansReservationOwnedByTerminalOperation()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var identity = Identity();
        var keyHash = new string('a', 64);
        var request = Request();
        var created = await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));

        using (var first = new FileSystemFiscalSeriesCoordinator(temp.Path))
        {
            await first.InitializeAsync(store);
            await first.ReserveAsync(identity, keyHash, 5);
            await store.SaveAsync(created with
            {
                NumeroComprobante = 5,
                State = EmissionIdempotencyState.Authorized,
                FiscalResult = StoredFiscalResult.FromResponse(new dcFacturaResponse
                {
                    Success = true,
                    NumeroComprobante = 5,
                    Cae = "CAE5",
                    Resultado = "A",
                    EmissionOutcome = dcEmissionOutcome.Authorized
                }),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        using var restarted = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await restarted.InitializeAsync(store);

        Assert.Null(await restarted.GetActiveAsync(identity));
    }

    private static FiscalOperationIdentity Identity() => new(
        "consumer",
        "context",
        "homologacion",
        20123456786,
        7,
        (int)dcTipoComprobante.FacturaB,
        1,
        "cred-1");

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
        FechaComprobante = "20261003"
    };

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-series-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
