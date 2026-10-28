using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV9VisualGeometryTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void NombreFantasiaLargo_ConservaGeometriaBase()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARCA_PDF_VISUAL_TESTS")))
            return;

        var pdfPath = Render();
        try
        {
            using var measurement = Measure(pdfPath);
            var root = measurement.RootElement;

            Assert.Equal(3, root.GetProperty("summary").GetProperty("total_pages").GetInt32());

            foreach (var page in root.GetProperty("pages").EnumerateArray())
            {
                var elements = page.GetProperty("elements");
                var voucher = elements.GetProperty("voucher_letter");
                var voucherWidth = voucher.GetProperty("x1").GetDouble()
                    - voucher.GetProperty("x0").GetDouble();
                Assert.InRange(voucherWidth, 44, 50);

                Assert.True(elements.TryGetProperty("qr_code", out _));

                if (elements.TryGetProperty("totals_box", out var box)
                    && elements.TryGetProperty("totals_values", out var values))
                {
                    foreach (var value in values.EnumerateArray())
                    {
                        Assert.InRange(
                            value.GetProperty("x0").GetDouble(),
                            box.GetProperty("x0").GetDouble() - 2,
                            box.GetProperty("x1").GetDouble() + 2);
                        Assert.InRange(
                            value.GetProperty("x1").GetDouble(),
                            box.GetProperty("x0").GetDouble() - 2,
                            box.GetProperty("x1").GetDouble() + 2);
                    }
                }
            }
        }
        finally
        {
            try { File.Delete(pdfPath); } catch { }
        }
    }

    private static string Render()
    {
        var harness = Path.Combine(Root, "scripts", "dev", "pdf_local_render.sh");
        var outputDir = Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{harness}\" \"factura-c-nombre-fantasia-largo\" \"{outputDir}\" \"factura-ar-v9\"",
            WorkingDirectory = Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        if (!process.WaitForExit(TimeSpan.FromSeconds(120)))
        {
            process.Kill();
            throw new InvalidOperationException("Harness timed out after 120 seconds");
        }

        var output = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, output);

        var line = output.Split('\n')
            .FirstOrDefault(x => x.Contains("PDF location:", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(line), output);

        var pdfPath = line![(line.LastIndexOf(':') + 1)..].Trim();
        Assert.True(File.Exists(pdfPath), $"No existe {pdfPath}");
        return pdfPath;
    }

    private static JsonDocument Measure(string pdfPath)
    {
        var script = Path.Combine(Root, "scripts", "dev", "pdf_measure.py");
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{script}\" \"{pdfPath}\"",
            WorkingDirectory = Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            process.Kill();
            throw new InvalidOperationException("PDF measurement timed out after 30 seconds");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, stderr);
        return JsonDocument.Parse(stdout);
    }
}
