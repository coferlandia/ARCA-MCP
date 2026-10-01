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
            Assert.NotNull(validated?.LastUsedAt);
            Assert.True(await store.RevokeAsync(record.Id));
            Assert.Null(await store.ValidateAsync(rawKey));
            Assert.False(await store.RevokeAsync(record.Id));
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

    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "dcArca-tests", Guid.NewGuid().ToString("N"));
    private static void Cleanup(string directory) { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
