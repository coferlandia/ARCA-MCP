using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class PdfTemplateReferenceResolverTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", "\t")]
    public void EmptyCallerPair_UsesRuntimeDefaults(string? templateId, string? templateVersion)
    {
        var resolver = new PdfTemplateReferenceResolver(Configuration("factura-ar", "5"));

        var result = resolver.Resolve(templateId, templateVersion);

        Assert.Equal("factura-ar", result.Key);
        Assert.Equal("5", result.Version);
    }

    [Fact]
    public void ExplicitCallerPair_OverridesRuntimeDefaults()
    {
        var resolver = new PdfTemplateReferenceResolver(Configuration("factura-ar", "5"));

        var result = resolver.Resolve("custom-template", "17");

        Assert.Equal("custom-template", result.Key);
        Assert.Equal("17", result.Version);
    }

    [Theory]
    [InlineData("factura-ar", null)]
    [InlineData(null, "5")]
    [InlineData("factura-ar", " ")]
    [InlineData(" ", "5")]
    public void PartialCallerPair_IsRejected(string? templateId, string? templateVersion)
    {
        var resolver = new PdfTemplateReferenceResolver(Configuration("factura-ar", "5"));

        var exception = Assert.Throws<PdfTemplateReferenceResolutionException>(
            () => resolver.Resolve(templateId, templateVersion));

        Assert.Equal("PDF_TEMPLATE_REFERENCE_INVALID", exception.Code);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("factura-ar", null)]
    [InlineData(null, "5")]
    [InlineData(" ", "5")]
    [InlineData("factura-ar", " ")]
    public void MissingOrIncompleteRuntimeDefaults_AreRejected(
        string? defaultKey,
        string? defaultVersion)
    {
        var resolver = new PdfTemplateReferenceResolver(Configuration(defaultKey, defaultVersion));

        var exception = Assert.Throws<PdfTemplateReferenceResolutionException>(
            () => resolver.Resolve(null, null));

        Assert.Equal("PDF_TEMPLATE_DEFAULT_NOT_CONFIGURED", exception.Code);
    }

    private static IConfiguration Configuration(string? key, string? version)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:TemplateKey"] = key,
                ["Pdf:TemplateVersion"] = version
            })
            .Build();
}
