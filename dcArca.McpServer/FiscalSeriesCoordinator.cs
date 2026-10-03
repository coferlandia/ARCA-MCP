using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dcArca.McpServer;

public sealed record FiscalSeriesReservation(
    int SchemaVersion,
    string SeriesKey,
    string OwnerKeyHash,
    long NumeroComprobante,
    DateTimeOffset ReservedAt,
    DateTimeOffset UpdatedAt);

public sealed class FiscalSeriesBlockedException : Exception
{
    public FiscalSeriesBlockedException(string message) : base(message) { }
}

public sealed class FiscalSeriesWriterBusyException : Exception
{
    public FiscalSeriesWriterBusyException()
        : base("SERIES_WRITER_BUSY") { }
}

public interface IFiscalSeriesCoordinator : IDisposable
{
    Task InitializeAsync(IEmissionIdempotencyStore store, CancellationToken cancellationToken = default);
    Task<FiscalSeriesReservation?> GetActiveAsync(FiscalOperationIdentity identity, CancellationToken cancellationToken = default);
    Task<FiscalSeriesReservation> ReserveAsync(FiscalOperationIdentity identity, string ownerKeyHash, long numeroComprobante, CancellationToken cancellationToken = default);
    Task ReleaseAsync(FiscalOperationIdentity identity, string ownerKeyHash, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinator used by compatibility constructors/tests. The hosted server does not use it.
/// It serializes reservations in-process but provides no inter-process exclusion.
/// </summary>
public sealed class InMemoryFiscalSeriesCoordinator : IFiscalSeriesCoordinator
{
    private readonly object _sync = new();
    private readonly Dictionary<string, FiscalSeriesReservation> _reservations = new(StringComparer.Ordinal);

    public Task InitializeAsync(IEmissionIdempotencyStore store, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<FiscalSeriesReservation?> GetActiveAsync(FiscalOperationIdentity identity, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _reservations.TryGetValue(identity.SeriesKey, out var reservation);
            return Task.FromResult(reservation);
        }
    }

    public Task<FiscalSeriesReservation> ReserveAsync(FiscalOperationIdentity identity, string ownerKeyHash, long numeroComprobante, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_reservations.TryGetValue(identity.SeriesKey, out var existing))
            {
                if (!string.Equals(existing.OwnerKeyHash, ownerKeyHash, StringComparison.Ordinal) || existing.NumeroComprobante != numeroComprobante)
                    throw new FiscalSeriesBlockedException("SERIES_RESERVATION_BLOCKED");
                return Task.FromResult(existing);
            }

            var now = DateTimeOffset.UtcNow;
            var created = new FiscalSeriesReservation(1, identity.SeriesKey, ownerKeyHash, numeroComprobante, now, now);
            _reservations[identity.SeriesKey] = created;
            return Task.FromResult(created);
        }
    }

    public Task ReleaseAsync(FiscalOperationIdentity identity, string ownerKeyHash, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_reservations.TryGetValue(identity.SeriesKey, out var existing))
            {
                if (!string.Equals(existing.OwnerKeyHash, ownerKeyHash, StringComparison.Ordinal))
                    throw new FiscalSeriesBlockedException("SERIES_RESERVATION_OWNER_MISMATCH");
                _reservations.Remove(identity.SeriesKey);
            }
            return Task.CompletedTask;
        }
    }

    public void Dispose() { }
}

/// <summary>
/// Durable single-writer coordinator for the filesystem topology supported by ARCA-MCP V1.
/// A process lease excludes a second writer over the same store. Reservations are persisted
/// independently from operation records and rebuilt/validated from those records after restart.
/// </summary>
public sealed class FileSystemFiscalSeriesCoordinator : IFiscalSeriesCoordinator
{
    public const int CurrentSchemaVersion = 1;
    private readonly string _storeDirectory;
    private readonly string _seriesDirectory;
    private readonly string _reservationsDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _seriesLocks = new(StringComparer.Ordinal);
    private FileStream? _writerLease;
    private bool _initialized;
    private bool _disposed;

    public FileSystemFiscalSeriesCoordinator(string storeDirectory)
    {
        _storeDirectory = Path.GetFullPath(storeDirectory);
        _seriesDirectory = Path.Combine(_storeDirectory, ".series");
        _reservationsDirectory = Path.Combine(_seriesDirectory, "reservations");
    }

    public async Task InitializeAsync(IEmissionIdempotencyStore store, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_initialized) return;
        if (store is not FileSystemEmissionIdempotencyStore fileStore
            || !PathsEqual(fileStore.DirectoryPath, _storeDirectory))
        {
            throw new InvalidOperationException("El coordinator durable debe inicializarse con el mismo FileSystemEmissionIdempotencyStore.");
        }

        Directory.CreateDirectory(_seriesDirectory);
        Directory.CreateDirectory(_reservationsDirectory);
        HardenDirectory(_seriesDirectory);
        HardenDirectory(_reservationsDirectory);

        try
        {
            _writerLease = new FileStream(
                Path.Combine(_seriesDirectory, "writer.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.None);
        }
        catch (IOException)
        {
            throw new FiscalSeriesWriterBusyException();
        }

        try
        {
            await RebuildAndValidateAsync(fileStore, cancellationToken);
            _initialized = true;
        }
        catch
        {
            _writerLease.Dispose();
            _writerLease = null;
            throw;
        }
    }

    public async Task<FiscalSeriesReservation?> GetActiveAsync(FiscalOperationIdentity identity, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var gate = _seriesLocks.GetOrAdd(identity.SeriesKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadReservationAsync(identity.SeriesKey, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FiscalSeriesReservation> ReserveAsync(FiscalOperationIdentity identity, string ownerKeyHash, long numeroComprobante, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ValidateHash(ownerKeyHash);
        if (numeroComprobante <= 0) throw new ArgumentOutOfRangeException(nameof(numeroComprobante));

        var gate = _seriesLocks.GetOrAdd(identity.SeriesKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await ReadReservationAsync(identity.SeriesKey, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.OwnerKeyHash, ownerKeyHash, StringComparison.Ordinal)
                    || existing.NumeroComprobante != numeroComprobante)
                {
                    throw new FiscalSeriesBlockedException("SERIES_RESERVATION_BLOCKED");
                }
                return existing;
            }

            var now = DateTimeOffset.UtcNow;
            var created = new FiscalSeriesReservation(
                CurrentSchemaVersion,
                identity.SeriesKey,
                ownerKeyHash,
                numeroComprobante,
                now,
                now);
            WriteAtomic(ReservationPath(identity.SeriesKey), created);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReleaseAsync(FiscalOperationIdentity identity, string ownerKeyHash, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ValidateHash(ownerKeyHash);
        var gate = _seriesLocks.GetOrAdd(identity.SeriesKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await ReadReservationAsync(identity.SeriesKey, cancellationToken);
            if (existing is null) return;
            if (!string.Equals(existing.OwnerKeyHash, ownerKeyHash, StringComparison.Ordinal))
                throw new FiscalSeriesBlockedException("SERIES_RESERVATION_OWNER_MISMATCH");
            File.Delete(ReservationPath(identity.SeriesKey));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RebuildAndValidateAsync(FileSystemEmissionIdempotencyStore store, CancellationToken cancellationToken)
    {
        var records = new Dictionary<string, EmissionIdempotencyRecord>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(_storeDirectory, "*.json", SearchOption.TopDirectoryOnly).Where(IsOperationFile))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keyHash = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var record = await store.GetAsync(keyHash, cancellationToken)
                ?? throw new InvalidDataException("SERIES_OPERATION_DISAPPEARED");
            records[keyHash] = record;
        }

        var pending = records.Values
            .Where(IsReservationOwningState)
            .ToArray();

        foreach (var invalid in pending.Where(x => !x.NumeroComprobante.HasValue || x.NumeroComprobante <= 0))
            throw new InvalidDataException($"SERIES_PENDING_WITHOUT_NUMBER:{invalid.KeyHash}");

        foreach (var group in pending.GroupBy(x => x.Identity.SeriesKey, StringComparer.Ordinal))
        {
            var owners = group.Select(x => x.KeyHash).Distinct(StringComparer.Ordinal).ToArray();
            if (owners.Length > 1)
                throw new InvalidDataException($"SERIES_MULTIPLE_PENDING_OWNERS:{group.Key}");
        }

        foreach (var path in Directory.EnumerateFiles(_reservationsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reservation = await ReadReservationFileAsync(path, cancellationToken);
            if (!records.TryGetValue(reservation.OwnerKeyHash, out var owner))
                throw new InvalidDataException("SERIES_RESERVATION_OWNER_MISSING");

            if (!string.Equals(owner.Identity.SeriesKey, reservation.SeriesKey, StringComparison.Ordinal)
                || owner.NumeroComprobante != reservation.NumeroComprobante)
                throw new InvalidDataException("SERIES_RESERVATION_OPERATION_MISMATCH");

            if (owner.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            {
                File.Delete(path);
                continue;
            }

            if (!IsReservationOwningState(owner))
                throw new InvalidDataException("SERIES_RESERVATION_OWNER_STATE_INVALID");
        }

        foreach (var operation in pending)
        {
            var existing = await ReadReservationAsync(operation.Identity.SeriesKey, cancellationToken);
            if (existing is null)
            {
                var now = DateTimeOffset.UtcNow;
                WriteAtomic(
                    ReservationPath(operation.Identity.SeriesKey),
                    new FiscalSeriesReservation(
                        CurrentSchemaVersion,
                        operation.Identity.SeriesKey,
                        operation.KeyHash,
                        operation.NumeroComprobante!.Value,
                        operation.CreatedAt,
                        now));
                continue;
            }

            if (!string.Equals(existing.OwnerKeyHash, operation.KeyHash, StringComparison.Ordinal)
                || existing.NumeroComprobante != operation.NumeroComprobante)
                throw new InvalidDataException("SERIES_REBUILD_CONFLICT");
        }
    }

    private async Task<FiscalSeriesReservation?> ReadReservationAsync(string seriesKey, CancellationToken cancellationToken)
    {
        var path = ReservationPath(seriesKey);
        return File.Exists(path) ? await ReadReservationFileAsync(path, cancellationToken) : null;
    }

    private static async Task<FiscalSeriesReservation> ReadReservationFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reservation = await JsonSerializer.DeserializeAsync<FiscalSeriesReservation>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("SERIES_RESERVATION_EMPTY");
            ValidateReservation(reservation);
            return reservation;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("SERIES_RESERVATION_CORRUPT", exception);
        }
    }

    private string ReservationPath(string seriesKey)
        => Path.Combine(_reservationsDirectory, SeriesHash(seriesKey) + ".json");

    private static string SeriesHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateReservation(FiscalSeriesReservation reservation)
    {
        if (reservation.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"SERIES_SCHEMA_UNSUPPORTED:{reservation.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(reservation.SeriesKey) || reservation.NumeroComprobante <= 0)
            throw new InvalidDataException("SERIES_RESERVATION_INVALID");
        ValidateHash(reservation.OwnerKeyHash);
    }

    private static void ValidateHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException("SERIES_OWNER_HASH_INVALID");
    }

    private static bool IsReservationOwningState(EmissionIdempotencyRecord record)
        => record.State is EmissionIdempotencyState.NumberAssigned
            or EmissionIdempotencyState.Submitting
            or EmissionIdempotencyState.Uncertain;

    private static bool IsOperationFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length == 64 && name.All(Uri.IsHexDigit);
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            HardenFile(path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void HardenDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }

    private static void HardenFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }

    private void EnsureInitialized()
    {
        ThrowIfDisposed();
        if (!_initialized) throw new InvalidOperationException("FiscalSeriesCoordinator no fue inicializado.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileSystemFiscalSeriesCoordinator));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writerLease?.Dispose();
        _writerLease = null;
        foreach (var gate in _seriesLocks.Values) gate.Dispose();
    }
}
