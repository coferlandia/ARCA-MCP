using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class PdfTemplateManifestTests
{
    [Fact]
    public void Manifest_HasUniqueLogicalReferencesAndExistingArtifacts()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var templateDirectory = Path.Combine(repositoryRoot, "deploy", "cadencia", "pdf-templates");
        var manifestPath = Path.Combine(templateDirectory, "manifest.json");

        Assert.True(File.Exists(manifestPath), $"No existe {manifestPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());

        var templates = root.GetProperty("templates");
        Assert.Equal(JsonValueKind.Array, templates.ValueKind);
        Assert.NotEqual(0, templates.GetArrayLength());

        var logicalReferences = new HashSet<string>(StringComparer.Ordinal);
        foreach (var template in templates.EnumerateArray())
        {
            var key = RequiredString(template, "key");
            var version = RequiredString(template, "version");
            var html = RequiredString(template, "template");
            var schema = RequiredString(template, "schema");

            Assert.True(
                logicalReferences.Add($"{key}\n{version}"),
                $"Referencia lógica duplicada: {key}:{version}");
            Assert.True(File.Exists(Path.Combine(templateDirectory, html)), $"No existe template {html}");
            Assert.True(File.Exists(Path.Combine(templateDirectory, schema)), $"No existe schema {schema}");
        }
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"Falta {propertyName}");
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        var value = property.GetString();
        Assert.False(string.IsNullOrWhiteSpace(value), $"{propertyName} está vacío");
        Assert.DoesNotContain('\t', value!);
        Assert.DoesNotContain('\n', value!);
        return value!;
    }
}
