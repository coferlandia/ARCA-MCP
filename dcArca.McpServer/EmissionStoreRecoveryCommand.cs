using System.Text.Json;

namespace dcArca.McpServer;

public static class EmissionStoreRecoveryCommand
{
    public static async Task<bool> TryRunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0) return false;

        try
        {
            switch (args[0])
            {
                case "backup-emission-store":
                {
                    var result = await EmissionStoreBackupRecovery.CreateBackupAsync(
                        Required(args, "--directory"),
                        Required(args, "--backup"),
                        cancellationToken);
                    Write(result);
                    return true;
                }
                case "restore-emission-store":
                {
                    var gate = new FileSystemEmissionRecoveryGate(Required(args, "--recovery-directory"));
                    var result = await EmissionStoreBackupRecovery.RestoreAsync(
                        Required(args, "--backup"),
                        Required(args, "--directory"),
                        gate,
                        cancellationToken);
                    Write(result);
                    Console.Error.WriteLine("Restore aplicado en modo bloqueado. Reconciliar la ventana no cubierta y ejecutar complete-emission-restore con una referencia de evidencia antes de autorizar nuevas emisiones.");
                    return true;
                }
                case "emission-recovery-status":
                {
                    var gate = new FileSystemEmissionRecoveryGate(Required(args, "--recovery-directory"));
                    var block = await gate.GetBlockAsync(cancellationToken);
                    Write(new { blocked = block is not null, recovery = block });
                    return true;
                }
                case "complete-emission-restore":
                {
                    var gate = new FileSystemEmissionRecoveryGate(Required(args, "--recovery-directory"));
                    var completion = await gate.CompleteAsync(
                        Required(args, "--evidence-reference"),
                        Option(args, "--confirmed-by"),
                        Option(args, "--note"),
                        cancellationToken);
                    Write(completion);
                    return true;
                }
                default:
                    return false;
            }
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine($"Error de recovery: {exception.Message}");
            return true;
        }
    }

    private static void Write<T>(T value)
        => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    private static string Required(string[] args, string name)
        => Option(args, name) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Falta {name} <valor>.");

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1].Trim() : null;
    }
}
