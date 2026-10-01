using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace dcArca.McpServer;

public sealed record PdfTemplateReference(string Id, string Version);

public interface IPdfClient
{
    Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default);
}

public sealed class PdfClient(HttpClient httpClient, IConfiguration configuration) : IPdfClient
{
    public async Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Pdf:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Falta configurar Pdf:ApiKey.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/documents")
        {
            Content = JsonContent.Create(new { template = new { id = template.Id, version = template.Version }, data })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"creadorpdf respondió {(int)response.StatusCode}.", null, response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
            throw new InvalidDataException("creadorpdf devolvió un contenido inesperado.");
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
