using System.Text.Json;

namespace dcArca.McpServer;

public static class EmissionStoreMigrationCommand
{
    public static async Task<bool> TryRunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || !string.Equals(args[0], "migrate-emission-store", StringComparison.Ordinal))
            return false;

        try
        {
            var directory = Required(args, "--directory");
            var mapping = new LegacyEmissionStoreMapping(
                Required(args, "--consumer"),
                Required(args, "--context"),
                Required(args, "--environment"),
                ParseLong(Required(args, "--cuit"), "--cuit"),
                ParseInt(Required(args, "--point-of-sale"), "--point-of-sale"),
                ParseInt(Required(args, "--context-revision"), "--context-revision"),
                Required(args, "--credential-revision"));
            var apply = args.Contains("--apply", StringComparer.Ordinal);
            var backup = Option(args, "--backup");

            var result = await EmissionStoreMigration.MigrateAsync(
                directory,
                mapping,
                dryRun: !apply,
                backup,
                cancellationToken);

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            if (!apply)
            {
                Console.Error.WriteLine("Dry-run completado. Repetir con --apply, con el escritor detenido y backup/restore verificados, para ejecutar la migración.");
            }
            return true;
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine($"Error de migración: {exception.Message}");
            return true;
        }
    }

    private static string Required(string[] args, string name)
        => Option(args, name) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Falta {name} <valor>.");

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1].Trim() : null;
    }

    private static int ParseInt(string value, string option)
        => int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"{option} debe ser un entero positivo.");

    private static long ParseLong(string value, string option)
        => long.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"{option} debe ser un entero positivo.");
}
