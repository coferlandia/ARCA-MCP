using System.Text.Json.Serialization;

namespace dcArca.McpServer;

public sealed class ArcaQrPayloadException : InvalidOperationException
{
    public ArcaQrPayloadException(string message) : base(message)
    {
    }
}

public sealed record ArcaQrPayload(
    [property: JsonPropertyName("ver")] int Ver,
    [property: JsonPropertyName("fecha")] string Fecha,
    [property: JsonPropertyName("cuit")] long Cuit,
    [property: JsonPropertyName("ptoVta")] int PtoVta,
    [property: JsonPropertyName("tipoCmp")] int TipoCmp,
    [property: JsonPropertyName("nroCmp")] long NroCmp,
    [property: JsonPropertyName("importe")] decimal Importe,
    [property: JsonPropertyName("moneda")] string Moneda,
    [property: JsonPropertyName("ctz")] decimal Ctz,
    [property: JsonPropertyName("tipoDocRec"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TipoDocRec,
    [property: JsonPropertyName("nroDocRec"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? NroDocRec,
    [property: JsonPropertyName("tipoCodAut")] string TipoCodAut,
    [property: JsonPropertyName("codAut")] long CodAut);

public sealed record ArcaQrPresentation(
    [property: JsonPropertyName("verificationUrl")] string VerificationUrl,
    [property: JsonPropertyName("imageDataUri")] string ImageDataUri);
