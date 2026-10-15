using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.Core.Tests;

public sealed class FiscalPrincipalCuitTests
{
    private const long RepresentedCuit = 30712345678;

    [Fact]
    public void Resolve_V2CredentialWithoutPrincipalCuit_FailsClosed()
    {
        var resolver = Resolver(BuildConfiguration(principalCuit: null));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(Context(), Assignment()));

        Assert.Equal("CREDENTIAL_PRINCIPAL_CUIT_REQUIRED", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2012345678")]
    [InlineData("201234567890")]
    [InlineData("20A23456789")]
    [InlineData(" 20123456789 ")]
    public void Resolve_V2CredentialWithInvalidPrincipalCuit_FailsClosed(string principalCuit)
    {
        var resolver = Resolver(BuildConfiguration(principalCuit));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(Context(), Assignment()));

        Assert.Equal("CREDENTIAL_PRINCIPAL_CUIT_INVALID", exception.Message);
    }

    [Fact]
    public void Resolve_SeparatesPrincipalAndRepresentedCuit()
    {
        const string principalCuit = "20123456789";
        var resolver = Resolver(BuildConfiguration(principalCuit));

        var binding = resolver.Resolve(Context(), Assignment());

        var bindingPrincipal = binding.GetType().GetProperty("PrincipalCuit");
        var bindingRepresented = binding.GetType().GetProperty("RepresentedCuit");
        var configPrincipal = typeof(dcArcaConfig).GetProperty("PrincipalCuit");
        var configRepresented = typeof(dcArcaConfig).GetProperty("RepresentedCuit");

        Assert.NotNull(bindingPrincipal);
        Assert.NotNull(bindingRepresented);
        Assert.NotNull(configPrincipal);
        Assert.NotNull(configRepresented);
        Assert.Equal(principalCuit, bindingPrincipal!.GetValue(binding));
        Assert.Equal(RepresentedCuit.ToString(), bindingRepresented!.GetValue(binding));
        Assert.Equal(principalCuit, configPrincipal!.GetValue(binding.Config));
        Assert.Equal(RepresentedCuit.ToString(), configRepresented!.GetValue(binding.Config));
        Assert.Equal(RepresentedCuit.ToString(), binding.Config.Cuit);
    }

    private static FiscalCredentialHostBindingResolver Resolver(IConfiguration configuration)
        => new(
            configuration,
            new dcArcaConfig(),
            FiscalCredentialHostBindingResolver.NoLegacyFallback);

    private static IConfiguration BuildConfiguration(string? principalCuit)
    {
        var values = new Dictionary<string, string?>
        {
            ["FiscalCredentials:cred-shared:CertificatePath"] = "/tmp/test-placeholder.pfx",
            ["FiscalCredentials:cred-shared:CertificatePassword"] = "",
            ["FiscalEnvironments:homologacion:WsaaUrl"] = "https://example.invalid/wsaa",
            ["FiscalEnvironments:homologacion:WsfeUrl"] = "https://example.invalid/wsfe",
            ["FiscalEnvironments:homologacion:PadronUrl"] = "https://example.invalid/padron"
        };
        if (principalCuit is not null)
            values["FiscalCredentials:cred-shared:PrincipalCuit"] = principalCuit;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static RepresentedFiscalContextRecord Context()
        => new(
            "operator-homo",
            "homologacion",
            RepresentedCuit,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [Assignment()]);

    private static CredentialAssignmentRecord Assignment()
        => new(
            "rev-1",
            "cred-shared",
            CredentialAssignmentStatus.Active,
            "wsaa-ok",
            DateTimeOffset.UtcNow,
            ActivatedAt: DateTimeOffset.UtcNow,
            Actor: "test");
}
