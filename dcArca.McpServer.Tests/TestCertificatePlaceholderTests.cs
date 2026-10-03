using Xunit;

namespace dcArca.McpServer.Tests;

public class TestCertificatePlaceholderTests
{
    [Fact]
    public void IndependentPlaceholders_DoNotSharePath()
    {
        using var first = new TestCertificatePlaceholder();
        using var second = new TestCertificatePlaceholder();

        Assert.NotEqual(first.Path, second.Path);
        Assert.True(File.Exists(first.Path));
        Assert.True(File.Exists(second.Path));
    }
}
