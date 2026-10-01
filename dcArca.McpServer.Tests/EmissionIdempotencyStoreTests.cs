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
            keyHash, requestHash, dcTipoComprobante.FacturaB, 7);
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
        Assert.Equal(EmissionIdempotencyState.Authorized, recovered!.State);
        Assert.Equal(123, recovered.NumeroComprobante);
        Assert.Equal("CAE123", recovered.FiscalResult!.Cae);
    }

    [Fact]
    public async Task MismaKeyHashConOtroRequestHash_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var keyHash = new string('a', 64);

        await store.GetOrCreateAsync(
            keyHash, new string('b', 64), dcTipoComprobante.FacturaB, 7);

        await Assert.ThrowsAsync<EmissionIdempotencyConflictException>(() =>
            store.GetOrCreateAsync(
                keyHash, new string('c', 64), dcTipoComprobante.FacturaB, 7));
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
