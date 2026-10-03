using System.Text;
using System.Text.Json;

namespace dcArca.McpServer;

public sealed record EmissionRecoveryBlock(
    int SchemaVersion,
    string RestoreId,
    string BackupId,
    DateTimeOffset BackupCreatedAt,
    DateTimeOffset RestoredAt,
    string Reason);

public sealed record EmissionRecoveryCompletion(
    int SchemaVersion,
    string RestoreId,
    string BackupId,
    DateTimeOffset BackupCreatedAt,
    DateTimeOffset RestoredAt,
    DateTimeOffset CompletedAt,
    string EvidenceReference,
    string? ConfirmedBy,
    string? Note);

public interface IEmissionRecoveryGate
{
    Task<EmissionRecoveryBlock?> GetBlockAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenEmissionRecoveryGate : IEmissionRecoveryGate
{
    public static OpenEmissionRecoveryGate Instance { get; } = new();
    private OpenEmissionRecoveryGate() { }

    public Task<EmissionRecoveryBlock?> GetBlockAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<EmissionRecoveryBlock?>(null);
}

public sealed class FileSystemEmissionRecoveryGate : IEmissionRecoveryGate
{
    public const int CurrentSchemaVersion = 1;
    private readonly string _directory;
    private readonly string _historyDirectory;

    public FileSystemEmissionRecoveryGate(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Recovery directory es obligatorio.", nameof(directory));

        _directory = Path.GetFullPath(directory);
        _historyDirectory = Path.Combine(_directory, "history");
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(_historyDirectory);
        TryHardenDirectory(_directory);
        TryHardenDirectory(_historyDirectory);
    }

    public string DirectoryPath => _directory;
    public string BlockPath => Path.Combine(_directory, "restore-reconciliation-required.json");
    public string MaintenanceLockPath => Path.Combine(_directory, "maintenance.lock");

    public FileStream AcquireRuntimeLease()
    {
        try
        {
            return new FileStream(
                MaintenanceLockPath,
                FileMode.OpenOrCreate,
                FileAccess.Read,
                FileShare.Read,
                1,
                FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new IOException("RECOVERY_MAINTENANCE_IN_PROGRESS", exception);
        }
    }

    public FileStream AcquireMaintenanceLease()
    {
        try
        {
            return new FileStream(
                MaintenanceLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new IOException("RECOVERY_RUNTIME_ACTIVE", exception);
        }
    }

    public async Task<EmissionRecoveryBlock?> GetBlockAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(BlockPath)) return null;

        try
        {
            await using var stream = new FileStream(BlockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var block = await JsonSerializer.DeserializeAsync<EmissionRecoveryBlock>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("RECOVERY_GATE_EMPTY");
            Validate(block);
            return block;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RECOVERY_GATE_CORRUPT", exception);
        }
    }

    public async Task<EmissionRecoveryBlock> MarkRestoreRequiredAsync(
        string restoreId,
        string backupId,
        DateTimeOffset backupCreatedAt,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(restoreId)) throw new ArgumentException("restoreId es obligatorio.", nameof(restoreId));
        if (string.IsNullOrWhiteSpace(backupId)) throw new ArgumentException("backupId es obligatorio.", nameof(backupId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason es obligatorio.", nameof(reason));

        var existing = await GetBlockAsync(cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"RECOVERY_GATE_ALREADY_BLOCKED:{existing.RestoreId}");

        var block = new EmissionRecoveryBlock(
            CurrentSchemaVersion,
            restoreId.Trim(),
            backupId.Trim(),
            backupCreatedAt,
            DateTimeOffset.UtcNow,
            reason.Trim());
        WriteAtomic(BlockPath, block);
        return block;
    }

    public async Task<EmissionRecoveryCompletion> CompleteAsync(
        string evidenceReference,
        string? confirmedBy = null,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(evidenceReference))
            throw new ArgumentException("evidenceReference es obligatoria para levantar el recovery gate.", nameof(evidenceReference));

        var block = await GetBlockAsync(cancellationToken)
            ?? throw new InvalidOperationException("RECOVERY_GATE_NOT_BLOCKED");
        var historyPath = HistoryPath(block.RestoreId);

        // Crash-idempotency: history is the durable proof of completion. If a previous
        // attempt wrote it but died before deleting the block, finish only the delete and
        // return the original evidence rather than replacing it.
        if (File.Exists(historyPath))
        {
            var existing = await ReadCompletionAsync(historyPath, cancellationToken);
            EnsureCompletionMatchesBlock(existing, block);
            File.Delete(BlockPath);
            return existing;
        }

        var completion = new EmissionRecoveryCompletion(
            CurrentSchemaVersion,
            block.RestoreId,
            block.BackupId,
            block.BackupCreatedAt,
            block.RestoredAt,
            DateTimeOffset.UtcNow,
            evidenceReference.Trim(),
            NormalizeOptional(confirmedBy),
            NormalizeOptional(note));

        try
        {
            WriteAtomic(historyPath, completion);
        }
        catch (IOException) when (File.Exists(historyPath))
        {
            var existing = await ReadCompletionAsync(historyPath, cancellationToken);
            EnsureCompletionMatchesBlock(existing, block);
            File.Delete(BlockPath);
            return existing;
        }

        File.Delete(BlockPath);
        return completion;
    }

    private async Task<EmissionRecoveryCompletion> ReadCompletionAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var completion = await JsonSerializer.DeserializeAsync<EmissionRecoveryCompletion>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("RECOVERY_COMPLETION_EMPTY");
            if (completion.SchemaVersion != CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(completion.RestoreId)
                || string.IsNullOrWhiteSpace(completion.BackupId)
                || string.IsNullOrWhiteSpace(completion.EvidenceReference))
                throw new InvalidDataException("RECOVERY_COMPLETION_INVALID");
            return completion;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RECOVERY_COMPLETION_CORRUPT", exception);
        }
    }

    private static void EnsureCompletionMatchesBlock(
        EmissionRecoveryCompletion completion,
        EmissionRecoveryBlock block)
    {
        if (!string.Equals(completion.RestoreId, block.RestoreId, StringComparison.Ordinal)
            || !string.Equals(completion.BackupId, block.BackupId, StringComparison.Ordinal)
            || completion.BackupCreatedAt != block.BackupCreatedAt
            || completion.RestoredAt != block.RestoredAt)
            throw new InvalidDataException("RECOVERY_COMPLETION_BLOCK_MISMATCH");
    }

    private string HistoryPath(string restoreId)
        => Path.Combine(_historyDirectory, $"{SanitizeFileName(restoreId)}.completed.json");

    private static void Validate(EmissionRecoveryBlock block)
    {
        if (block.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"RECOVERY_GATE_SCHEMA_UNSUPPORTED:{block.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(block.RestoreId)
            || string.IsNullOrWhiteSpace(block.BackupId)
            || string.IsNullOrWhiteSpace(block.Reason))
            throw new InvalidDataException("RECOVERY_GATE_INVALID");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SanitizeFileName(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    private static void WriteAtomic<T>(string path, T value)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value), new UTF8Encoding(false));
            File.Move(tempPath, path, overwrite: false);
            TryHardenFile(path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void TryHardenDirectory(string directory)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }

    private static void TryHardenFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }
}
