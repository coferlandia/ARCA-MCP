using Microsoft.AspNetCore.Hosting;

namespace dcArca.McpServer.Tests;

internal sealed class TestCertificatePlaceholder : IDisposable
{
    private readonly string _directory;

    public string Path { get; }

    public TestCertificatePlaceholder()
    {
        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dcarca-test-cert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "placeholder.pfx");
        File.WriteAllBytes(Path, Array.Empty<byte>());
    }

    public void Apply(IWebHostBuilder builder)
        => builder.UseSetting("dcArcaConfig:CertificatePath", Path);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
