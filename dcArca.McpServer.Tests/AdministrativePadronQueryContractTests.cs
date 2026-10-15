using System.Reflection;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class AdministrativePadronQueryContractTests
{
    [Fact]
    public void Administration_ExposesPadronQueryIndependentOfRepresentation()
    {
        var method = typeof(FiscalRepresentationAdministration).GetMethod(
            "QueryPadronAsync",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
    }

    [Fact]
    public void CredentialMaterializer_ExposesTechnicalPadronMaterialization()
    {
        var method = typeof(IFiscalCredentialMaterializer).GetMethod(
            "MaterializePadron",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
    }
}
