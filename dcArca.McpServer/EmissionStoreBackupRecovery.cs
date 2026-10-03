using System.Security.Cryptography;
using System.Text.Json;

namespace dcArca.McpServer;

public sealed record EmissionStoreBackupFile(string RelativePath, long Length, string Sha256);

public sealed record EmissionStoreBackupManifest(
    int SchemaVersion,
    string BackupId,
    DateTimeOffset CreatedAt,
    int OperationCount,
    int PendingCount,
    int TerminalCount,
    IReadOnlyList<EmissionStoreBackupFile> Files);

public sealed record EmissionStoreRestoreResult(
    string RestoreId,
    string BackupId,
    DateTimeOffset BackupCreatedAt,
    string? RollbackDirectory,
    EmissionRecoveryBlock RecoveryBlock);

public static class EmissionStoreBackupRecovery
{
    public const int CurrentManifestSchemaVersion = 1;
    public const string ManifestFileName = "backup-manifest.json";

    public static async Task<EmissionStoreBackupManifest> CreateBackupAsync(
        string sourceDirectory,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(sourceDirectory);
        var backup = Path.GetFullPath(backupDirectory);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"No existe el store de emisiones: {source}");
        if (Directory.Exists(backup) || File.Exists(backup))
            throw new IOException($"El destino de backup ya existe: {backup}");
        EnsureDirectoriesDoNotOverlap(source, backup);

        await using var writerProof = AcquireWriterStoppedProof(source);
        var store = new FileSystemEmissionIdempotencyStore(source);
        var operations = await ReadOperationsAsync(store, source, cancellationToken);
        var pending = operations.Count(record => IsPending(record.State));
        var staging = backup + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);

        try
        {
            var files = new List<EmissionStoreBackupFile>();
            foreach (var sourcePath in EnumerateDurableFiles(source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, sourcePath);
                var destination = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(sourcePath, destination, overwrite: false);
                files.Add(new EmissionStoreBackupFile(
                    NormalizeRelativePath(relative),
                    new FileInfo(destination).Length,
                    await Sha256Async(destination, cancellationToken)));
            }

            var manifest = new EmissionStoreBackupManifest(
                CurrentManifestSchemaVersion,
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                operations.Count,
                pending,
                operations.Count - pending,
                files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
            await File.WriteAllTextAsync(
                Path.Combine(staging, ManifestFileName),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            Directory.Move(staging, backup);
            return manifest;
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch { }
            }
            throw;
        }
    }

    public static async Task<EmissionStoreRestoreResult> RestoreAsync(
        string backupDirectory,
        string targetDirectory,
        FileSystemEmissionRecoveryGate recoveryGate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveryGate);
        var backup = Path.GetFullPath(backupDirectory);
        var target = Path.GetFullPath(targetDirectory);
        if (!Directory.Exists(backup))
            throw new DirectoryNotFoundException($"No existe el backup de emisiones: {backup}");
        EnsureDirectoriesDoNotOverlap(backup, target);
        EnsureDirectoriesDoNotOverlap(target, recoveryGate.DirectoryPath);

        // This lease lives outside the store being swapped. Current server versions acquire a
        // shared runtime lease for their full lifetime, so restore cannot race a live host or a
        // host starting while the store path is being replaced.
        await using var maintenanceLease = recoveryGate.AcquireMaintenanceLease();
        var manifest = await ReadAndVerifyBackupAsync(backup, cancellationToken);

        FileStream? writerProof = null;
        if (Directory.Exists(target))
            writerProof = AcquireWriterStoppedProof(target);

        var parent = Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException("No se pudo resolver el directorio padre del store destino.");
        Directory.CreateDirectory(parent);
        var targetName = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var restoreId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(parent, targetName + ".restore-" + restoreId);
        var rollback = Directory.Exists(target)
            ? Path.Combine(parent, targetName + ".pre-restore-" + restoreId)
            : null;
        if (Directory.Exists(staging) || (rollback is not null && Directory.Exists(rollback)))
        {
            writerProof?.Dispose();
            throw new IOException("El staging/rollback de restore ya existe.");
        }

        // The gate lives outside the restored store and is set before staging/swap. Any
        // unexpected restart after this point fails closed for emission until reconciliation.
        EmissionRecoveryBlock block;
        try
        {
            block = await recoveryGate.MarkRestoreRequiredAsync(
                restoreId,
                manifest.BackupId,
                manifest.CreatedAt,
                "Restored emission store requires reconciliation of the uncovered window before new authorizations.",
                cancellationToken);
        }
        catch
        {
            writerProof?.Dispose();
            throw;
        }

        try
        {
            Directory.CreateDirectory(staging);
            foreach (var file in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = SafeCombine(backup, file.RelativePath);
                var destination = SafeCombine(staging, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: false);
            }

            await VerifyFilesAsync(staging, manifest.Files, cancellationToken);
            await ValidateRestoredStoreAsync(staging, cancellationToken);

            // Keep the original store writer lock for all expensive work. Release it only after
            // staging has been fully verified, because Windows cannot rename a directory that
            // contains our open handle. The recovery maintenance lease still prevents a current
            // MCP host from starting in this narrow swap window.
            writerProof?.Dispose();
            writerProof = null;

            if (rollback is not null)
                Directory.Move(target, rollback);
            try
            {
                Directory.Move(staging, target);
            }
            catch
            {
                if (rollback is not null && !Directory.Exists(target) && Directory.Exists(rollback))
                    Directory.Move(rollback, target);
                throw;
            }

            return new EmissionStoreRestoreResult(
                restoreId,
                manifest.BackupId,
                manifest.CreatedAt,
                rollback,
                block);
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch { }
            }
            // Keep the recovery gate blocked after any restore failure: the operator must
            // inspect the active/rollback directories before explicitly completing recovery.
            throw;
        }
        finally
        {
            writerProof?.Dispose();
        }
    }

    public static async Task<EmissionStoreBackupManifest> ReadAndVerifyBackupAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        var backup = Path.GetFullPath(backupDirectory);
        var manifestPath = Path.Combine(backup, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("BACKUP_MANIFEST_MISSING");

        EmissionStoreBackupManifest manifest;
        try
        {
            await using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            manifest = await JsonSerializer.DeserializeAsync<EmissionStoreBackupManifest>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("BACKUP_MANIFEST_EMPTY");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("BACKUP_MANIFEST_CORRUPT", exception);
        }

        if (manifest.SchemaVersion != CurrentManifestSchemaVersion)
            throw new InvalidDataException($"BACKUP_SCHEMA_UNSUPPORTED:{manifest.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(manifest.BackupId) || manifest.Files is null)
            throw new InvalidDataException("BACKUP_MANIFEST_INVALID");

        await VerifyFilesAsync(backup, manifest.Files, cancellationToken);
        return manifest;
    }

    private static async Task ValidateRestoredStoreAsync(string directory, CancellationToken cancellationToken)
    {
        var store = new FileSystemEmissionIdempotencyStore(directory);
        _ = await ReadOperationsAsync(store, directory, cancellationToken);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(directory);
        await coordinator.InitializeAsync(store, cancellationToken);
    }

    private static async Task<List<EmissionIdempotencyRecord>> ReadOperationsAsync(
        FileSystemEmissionIdempotencyStore store,
        string directory,
        CancellationToken cancellationToken)
    {
        var records = new List<EmissionIdempotencyRecord>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(IsOperationFile)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyHash = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            records.Add(await store.GetAsync(keyHash, cancellationToken)
                ?? throw new InvalidDataException("BACKUP_OPERATION_DISAPPEARED"));
        }
        return records;
    }

    private static IEnumerable<string> EnumerateDurableFiles(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !IsEphemeralFile(path))
            .Order(StringComparer.Ordinal);

    private static bool IsEphemeralFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperationFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length == 64 && name.All(Uri.IsHexDigit);
    }

    private static bool IsPending(EmissionIdempotencyState state)
        => state is not (EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected);

    private static FileStream AcquireWriterStoppedProof(string storeDirectory)
    {
        var seriesDirectory = Path.Combine(storeDirectory, ".series");
        Directory.CreateDirectory(seriesDirectory);
        try
        {
            return new FileStream(
                Path.Combine(seriesDirectory, "writer.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.None);
        }
        catch (IOException exception)
        {
            throw new FiscalSeriesWriterBusyExceptionWithInner(exception);
        }
    }

    private static async Task VerifyFilesAsync(
        string root,
        IReadOnlyList<EmissionStoreBackupFile> files,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file.RelativePath) || !seen.Add(file.RelativePath))
                throw new InvalidDataException("BACKUP_FILE_LIST_INVALID");
            var path = SafeCombine(root, file.RelativePath);
            if (!File.Exists(path)) throw new InvalidDataException($"BACKUP_FILE_MISSING:{file.RelativePath}");
            var info = new FileInfo(path);
            if (info.Length != file.Length) throw new InvalidDataException($"BACKUP_FILE_LENGTH_MISMATCH:{file.RelativePath}");
            var hash = await Sha256Async(path, cancellationToken);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"BACKUP_FILE_HASH_MISMATCH:{file.RelativePath}");
        }
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SafeCombine(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("BACKUP_PATH_INVALID");
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!combined.StartsWith(rootFull, comparison)) throw new InvalidDataException("BACKUP_PATH_ESCAPE");
        return combined;
    }

    private static string NormalizeRelativePath(string relative)
        => relative.Replace(Path.DirectorySeparatorChar, '/');

    private static void EnsureDirectoriesDoNotOverlap(string left, string right)
    {
        var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(a, b, comparison)
            || b.StartsWith(a + Path.DirectorySeparatorChar, comparison)
            || a.StartsWith(b + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("BACKUP_RESTORE_DIRECTORIES_OVERLAP");
    }

    private sealed class FiscalSeriesWriterBusyExceptionWithInner : IOException
    {
        public FiscalSeriesWriterBusyExceptionWithInner(Exception inner)
            : base("SERIES_WRITER_BUSY", inner) { }
    }
}
