using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcApiKeyStoreTests
{
    [Fact]
    public async Task CreateValidateRevoke_CompletaElCicloSinPersistirElSecreto()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            var (record, rawKey) = await store.CreateAsync("secretaria", ["arca:consultar"]);
            Assert.StartsWith("sk-arca-", rawKey);
            Assert.DoesNotContain(rawKey, await File.ReadAllTextAsync(Path.Combine(directory, "api_keys.json")));

            var validated = await store.ValidateAsync(rawKey);
            Assert.Equal(record.Id, validated?.Id);
            Assert.Null(validated?.ConsumerId);
            Assert.Empty(validated?.Grants ?? []);
            Assert.NotNull(validated?.LastUsedAt);
            Assert.True(await store.RevokeAsync(record.Id));
            Assert.Null(await store.ValidateAsync(rawKey));
            Assert.False(await store.RevokeAsync(record.Id));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task KeysRotadas_PuedenCompartirConsumerYGrantsSinCompartirSecreto()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            var grants = new[]
            {
                new ApiKeyContextGrant("ctx-a", ["consultar", "facturar"])
            };

            var first = await store.CreateForConsumerAsync("secretaria-v1", "consumer-secretaria", ["arca:consultar", "arca:facturar"], grants);
            var second = await store.CreateForConsumerAsync("secretaria-v2", "consumer-secretaria", ["arca:consultar", "arca:facturar"], grants);

            Assert.NotEqual(first.RawKey, second.RawKey);
            Assert.NotEqual(first.Record.KeyHash, second.Record.KeyHash);
            Assert.Equal(first.Record.ConsumerId, second.Record.ConsumerId);
            Assert.Equal(first.Record.Grants, second.Record.Grants);
            Assert.DoesNotContain(first.RawKey, await File.ReadAllTextAsync(Path.Combine(directory, "api_keys.json")));
            Assert.DoesNotContain(second.RawKey, await File.ReadAllTextAsync(Path.Combine(directory, "api_keys.json")));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task AgregarOtroContexto_NoAmpliaGrantsDeUnaKeyExistente()
    {
        var root = TempDirectory();
        var keyDirectory = Path.Combine(root, "keys");
        var contextDirectory = Path.Combine(root, "contexts");
        try
        {
            var keyStore = new FileSystemApiKeyStore(keyDirectory);
            var contextStore = new FileSystemRepresentedFiscalContextStore(contextDirectory);
            var created = await keyStore.CreateForConsumerAsync(
                "tenant",
                "consumer-1",
                ["arca:consultar"],
                [new ApiKeyContextGrant("ctx-a", ["consultar"])]);

            await contextStore.AddContextAsync(Context("ctx-a", 20123456786));
            await contextStore.AddContextAsync(Context("ctx-b", 30999999991));

            var reloaded = (await keyStore.ListAsync()).Single(x => x.Id == created.Record.Id);
            var grant = Assert.Single(reloaded.Grants);
            Assert.Equal("ctx-a", grant.ContextId);
            Assert.DoesNotContain(reloaded.Grants, x => x.ContextId == "ctx-b");
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public async Task SetConsumerAndGrants_ConvierteKeyLegacySinCambiarHash()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            var legacy = await store.CreateAsync("legacy", ["arca:consultar"]);

            Assert.True(await store.SetConsumerAndGrantsAsync(
                legacy.Record.Id,
                "consumer-1",
                [new ApiKeyContextGrant("ctx-a", ["consultar"])]));

            var updated = Assert.Single(await store.ListAsync());
            Assert.Equal(legacy.Record.KeyHash, updated.KeyHash);
            Assert.Equal("consumer-1", updated.ConsumerId);
            Assert.Equal("ctx-a", Assert.Single(updated.Grants).ContextId);
            Assert.NotNull(await store.ValidateAsync(legacy.RawKey));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task CreateConcurrente_NoPierdeRegistros()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(index => store.CreateAsync($"cliente-{index}", ["arca:consultar"])));
            var all = await store.ListAsync();
            Assert.Equal(8, all.Count);
            Assert.Equal(8, all.Select(record => record.Id).Distinct().Count());
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task Create_SinScopes_EsRechazada()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("cliente", ["", " "]));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task Store_UsaPermisosMinimosEnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemApiKeyStore(directory);
            await store.CreateAsync("cliente", ["arca:consultar"]);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(directory, "api_keys.json")));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
        }
        finally { Cleanup(directory); }
    }

    private static RepresentedFiscalContextRecord Context(string id, long cuit)
        => new(
            id,
            "homologacion",
            cuit,
            1,
            FiscalContextOperationalState.Disabled,
            1,
            false,
            []);

    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "dcArca-tests", Guid.NewGuid().ToString("N"));
    private static void Cleanup(string directory) { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
