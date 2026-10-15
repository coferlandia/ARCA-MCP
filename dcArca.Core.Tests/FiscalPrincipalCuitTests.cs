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

    [Fact]
    public void SameCredential_TwoRepresentations_SharePrincipalAndCacheIdentity()
    {
        const string principalCuit = "20123456789";
        var resolver = Resolver(BuildConfiguration(principalCuit));
        var first = resolver.Resolve(
            Context(representedCuit: 30712345678, pointOfSale: 7),
            Assignment("rev-1"));
        var second = resolver.Resolve(
            Context(representedCuit: 30611111118, pointOfSale: 12),
            Assignment("rev-2"));

        Assert.Equal(principalCuit, first.PrincipalCuit);
        Assert.Equal(principalCuit, second.PrincipalCuit);
        Assert.Equal(first.CacheIdentity, second.CacheIdentity);
        Assert.NotEqual(first.RepresentedCuit, second.RepresentedCuit);
        Assert.NotEqual(first.Config.PuntoVenta, second.Config.PuntoVenta);
    }

    [Fact]
    public void SameCredential_AcrossEnvironments_KeepsPrincipalButNamespacesCacheByEnvironment()
    {
        const string principalCuit = "20123456789";
        var resolver = Resolver(BuildConfiguration(principalCuit));
        var homo = resolver.Resolve(
            Context(environment: "homologacion"),
            Assignment());
        var prod = resolver.Resolve(
            Context(environment: "produccion"),
            Assignment());

        Assert.Equal(principalCuit, homo.PrincipalCuit);
        Assert.Equal(principalCuit, prod.PrincipalCuit);
        Assert.Equal("homologacion:cred-shared", homo.CacheIdentity);
        Assert.Equal("produccion:cred-shared", prod.CacheIdentity);
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
            ["FiscalEnvironments:homologacion:PadronUrl"] = "https://example.invalid/padron",
            ["FiscalEnvironments:produccion:WsaaUrl"] = "https://prod.example.invalid/wsaa",
            ["FiscalEnvironments:produccion:WsfeUrl"] = "https://prod.example.invalid/wsfe",
            ["FiscalEnvironments:produccion:PadronUrl"] = "https://prod.example.invalid/padron"
        };
        if (principalCuit is not null)
            values["FiscalCredentials:cred-shared:PrincipalCuit"] = principalCuit;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static RepresentedFiscalContextRecord Context(
        string environment = "homologacion",
        long representedCuit = RepresentedCuit,
        int pointOfSale = 7)
        => new(
            "operator",
            environment,
            representedCuit,
            pointOfSale,
            FiscalContextOperationalState.Active,
            1,
            false,
            [Assignment()]);

    private static CredentialAssignmentRecord Assignment(string revision = "rev-1")
        => new(
            revision,
            "cred-shared",
            CredentialAssignmentStatus.Active,
            "wsaa-ok",
            DateTimeOffset.UtcNow,
            ActivatedAt: DateTimeOffset.UtcNow,
            Actor: "test");
}
