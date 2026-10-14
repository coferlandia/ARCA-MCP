using System.Text;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ArcaQrImageGeneratorTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void GeneratePngDataUri_DevuelveDataUriPngValido()
    {
        var dataUri = new ArcaQrImageGenerator().GeneratePngDataUri("https://www.arca.gob.ar/fe/qr/?p=abc123");

        Assert.StartsWith("data:image/png;base64,", dataUri);

        var base64 = dataUri["data:image/png;base64,".Length..];
        var bytes = Convert.FromBase64String(base64);

        Assert.True(bytes.Length >= PngSignature.Length);
        Assert.Equal(PngSignature, bytes[..PngSignature.Length]);
    }

    [Fact]
    public void GeneratePngDataUri_EsDeterministicoParaLaMismaUrl()
    {
        var generator = new ArcaQrImageGenerator();
        var url = "https://www.arca.gob.ar/fe/qr/?p=xyz789";

        var first = generator.GeneratePngDataUri(url);
        var second = generator.GeneratePngDataUri(url);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GeneratePngDataUri_UrlVacia_FallaCerrado()
    {
        var generator = new ArcaQrImageGenerator();

        Assert.Throws<ArgumentException>(() => generator.GeneratePngDataUri(string.Empty));
        Assert.Throws<ArgumentException>(() => generator.GeneratePngDataUri("   "));
    }

    [Fact]
    public void GeneratePngDataUri_UrlDistinta_ProduceImagenDistinta()
    {
        var generator = new ArcaQrImageGenerator();

        var first = generator.GeneratePngDataUri("https://www.arca.gob.ar/fe/qr/?p=aaa");
        var second = generator.GeneratePngDataUri("https://www.arca.gob.ar/fe/qr/?p=bbb");

        Assert.NotEqual(first, second);
    }
}
