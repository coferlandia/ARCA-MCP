using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionRecoveryGateTests
{
    [Fact]
    public async Task CompletionHistoryWrittenBeforeBlockDelete_IsRecoveredIdempotently()
    {
        using var temp = new TempDirectory();
        var gate = new FileSystemEmissionRecoveryGate(temp.Path);
        var block = await gate.MarkRestoreRequiredAsync(
            "restore-a",
            "backup-a",
            DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
            "test restore");

        var completion = new EmissionRecoveryCompletion(
            FileSystemEmissionRecoveryGate.CurrentSchemaVersion,
            block.RestoreId,
            block.BackupId,
            block.BackupCreatedAt,
            block.RestoredAt,
            DateTimeOffset.Parse("2026-10-03T11:00:00Z"),
            "OPS-RECOVERY-1",
            "operator",
            "evidence already persisted before simulated crash");
        var historyPath = System.IO.Path.Combine(
            temp.Path,
            "history",
            "restore-a.completed.json");
        await File.WriteAllTextAsync(historyPath, JsonSerializer.Serialize(completion));

        var recovered = await gate.CompleteAsync(
            "DIFFERENT-EVIDENCE-MUST-NOT-REPLACE-DURABLE-HISTORY",
            "retrying-operator");

        Assert.Equal(completion, recovered);
        Assert.Null(await gate.GetBlockAsync());
        var persisted = JsonSerializer.Deserialize<EmissionRecoveryCompletion>(
            await File.ReadAllTextAsync(historyPath));
        Assert.Equal("OPS-RECOVERY-1", persisted!.EvidenceReference);
    }

    [Fact]
    public async Task CorruptCompletionHistory_KeepsGateBlocked()
    {
        using var temp = new TempDirectory();
        var gate = new FileSystemEmissionRecoveryGate(temp.Path);
        await gate.MarkRestoreRequiredAsync(
            "restore-b",
            "backup-b",
            DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
            "test restore");
        var historyPath = System.IO.Path.Combine(
            temp.Path,
            "history",
            "restore-b.completed.json");
        await File.WriteAllTextAsync(historyPath, "{broken");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            gate.CompleteAsync("OPS-RECOVERY-2"));

        Assert.NotNull(await gate.GetBlockAsync());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-recovery-gate-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
