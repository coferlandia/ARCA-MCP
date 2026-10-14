using QRCoder;

namespace dcArca.McpServer;

public interface IArcaQrImageGenerator
{
    string GeneratePngDataUri(string verificationUrl);
}

public sealed class ArcaQrImageGenerator : IArcaQrImageGenerator
{
    private const int PixelsPerModule = 5;

    public string GeneratePngDataUri(string verificationUrl)
    {
        if (string.IsNullOrWhiteSpace(verificationUrl))
            throw new ArgumentException("La URL de verificación del QR es obligatoria.", nameof(verificationUrl));

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrData);
        var png = pngQrCode.GetGraphic(PixelsPerModule);

        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
