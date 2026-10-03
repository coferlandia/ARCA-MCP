using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConfigurationHelperTests
{
    [Fact]
    public void LoadFromConfiguration_HonorsCertificateOverride()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dcarca-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var certificatePath = Path.Combine(directory, "placeholder.pfx");
        File.WriteAllBytes(certificatePath, Array.Empty<byte>());

        try
        {
            var values = new Dictionary<string, string?>
            {
                ["dcArcaConfig:Cuit"] = "20123456786",
                ["dcArcaConfig:CertificatePath"] = certificatePath,
                ["dcArcaConfig:CertificatePassword"] = "",
                ["dcArcaConfig:WsaaUrl"] = "https://example.invalid/wsaa",
                ["dcArcaConfig:WsfeUrl"] = "https://example.invalid/wsfe",
                ["dcArcaConfig:PadronUrl"] = "https://example.invalid/padron",
                ["dcArcaConfig:PuntoVenta"] = "1"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var config = dcConfigurationHelper.LoadFromConfiguration(configuration);

            Assert.Equal(certificatePath, config.CertificatePath);
            Assert.Equal("20123456786", config.Cuit);
            Assert.Equal(1, config.PuntoVenta);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
