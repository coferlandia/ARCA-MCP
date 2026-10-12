namespace dcArca.Core.Services;

public static class FiscalEnvironmentId
{
    public const string Homologacion = "homologacion";
    public const string Produccion = "produccion";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("FISCAL_ENVIRONMENT_UNSUPPORTED");

        return value.Trim().ToLowerInvariant() switch
        {
            Homologacion => Homologacion,
            Produccion => Produccion,
            _ => throw new InvalidOperationException("FISCAL_ENVIRONMENT_UNSUPPORTED")
        };
    }

    public static string RequireCanonical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException("FISCAL_ENVIRONMENT_NOT_CANONICAL");

        string normalized;
        try
        {
            normalized = Normalize(value);
        }
        catch (InvalidOperationException)
        {
            throw new InvalidDataException("FISCAL_ENVIRONMENT_NOT_CANONICAL");
        }

        if (!string.Equals(value, normalized, StringComparison.Ordinal))
            throw new InvalidDataException("FISCAL_ENVIRONMENT_NOT_CANONICAL");

        return normalized;
    }

    public static bool IsCanonical(string? value)
        => string.Equals(value, Homologacion, StringComparison.Ordinal)
            || string.Equals(value, Produccion, StringComparison.Ordinal);
}
