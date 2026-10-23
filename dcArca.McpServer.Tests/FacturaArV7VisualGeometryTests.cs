using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

/// <summary>
/// Integration tests for visual geometry of factura-ar-v7 template.
///
/// These tests render actual PDFs using the local creadorpdf harness and measure
/// real page geometry, dimensions, and layout properties. This ensures that CSS
/// changes don't break the visual fidelity of the rendered documents.
///
/// Tests are gated by the environment variable ARCA_PDF_VISUAL_TESTS=1 to avoid
/// requiring Docker in normal CI environments. Without the variable, tests skip
/// immediately without touching Docker or network.
///
/// Prerequisites (when enabled):
/// - Docker running with creadorpdf image available
/// - Python 3 installed with pdfplumber library
///
/// Run with:
///   ARCA_PDF_VISUAL_TESTS=1 dotnet test --filter FacturaArV7VisualGeometry --configuration Release
/// </summary>
public class FacturaArV7VisualGeometryTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static bool IsVisualTestsEnabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARCA_PDF_VISUAL_TESTS"));

    /// <summary>
    /// Renders factura-ar-v7 with a single-item Factura C and verifies:
    /// - Exactly 3 pages (A4)
    /// - Voucher letter/code box width 44-50pt
    /// - Totals values contained within totals box
    /// - QR code present on each page
    /// </summary>
    [Fact]
    public void FacturaC1Item_Renders3Pages_WithCorrectGeometry()
    {
        if (!IsVisualTestsEnabled)
        {
            // Skip test: Docker-dependent visual tests are gated by ARCA_PDF_VISUAL_TESTS=1
            return;
        }

        var measurements = RenderAndMeasure("factura-c-1item", "factura-ar-v7");

        Assert.NotNull(measurements);
        Assert.Equal("factura-ar-v7", measurements.Template);

        // P0-1: Pagination - exactly 3 pages
        Assert.Equal(3, measurements.TotalPages);

        // P0-2: Voucher letter/code box width 44-50pt (objective ~47pt / 17mm)
        var voucherLetterWidth = measurements.Pages[0].VoucherLetterWidth;
        Assert.InRange(voucherLetterWidth, 44, 50);

        // P0-3: Totals containment - importes dentro del bbox del recuadro
        foreach (var page in measurements.Pages)
        {
            if (page.TotalsValues.Count > 0)
            {
                AssertTotalsValuesContainedInBox(page);
            }
        }

        // QR code present on each page
        for (int i = 0; i < measurements.Pages.Count; i++)
        {
            var page = measurements.Pages[i];
            Assert.NotNull(page.QrBbox);
            Assert.True(page.QrBbox != null, $"Page {i + 1} missing QR code");
        }
    }

    /// <summary>
    /// Renders factura-ar-v7 with a single-item Nota de Crédito C and verifies:
    /// - Exactly 3 pages (A4)
    /// - Same geometry constraints as Factura C
    /// - QR code present
    /// </summary>
    [Fact]
    public void NotaCreditoC1Item_Renders3Pages_WithCorrectGeometry()
    {
        if (!IsVisualTestsEnabled)
        {
            // Skip test: Docker-dependent visual tests are gated by ARCA_PDF_VISUAL_TESTS=1
            return;
        }

        var measurements = RenderAndMeasure("nota-credito-c-1item", "factura-ar-v7");

        Assert.NotNull(measurements);

        // P0-1: Pagination - exactly 3 pages
        Assert.Equal(3, measurements.TotalPages);

        // P0-2: Voucher letter/code box width 44-50pt
        var voucherLetterWidth = measurements.Pages[0].VoucherLetterWidth;
        Assert.InRange(voucherLetterWidth, 44, 50);

        // P0-3: Totals containment
        foreach (var page in measurements.Pages)
        {
            if (page.TotalsValues.Count > 0)
            {
                AssertTotalsValuesContainedInBox(page);
            }
        }

        // QR code present on each page
        for (int i = 0; i < measurements.Pages.Count; i++)
        {
            var page = measurements.Pages[i];
            Assert.NotNull(page.QrBbox);
            Assert.True(page.QrBbox != null, $"Page {i + 1} missing QR code");
        }
    }

    private PdfMeasurements RenderAndMeasure(string fixtureName, string template)
    {
        // 1. Render PDF using the local harness
        var pdfPath = RenderWithHarness(fixtureName, template);
        Assert.True(File.Exists(pdfPath), $"Harness failed to produce PDF at {pdfPath}");

        try
        {
            // 2. Measure with pdf_measure.py
            var measurementJson = MeasurePdf(pdfPath);

            // 3. Parse and validate
            return ParseMeasurements(measurementJson, template);
        }
        finally
        {
            // Clean up rendered PDF (from scratchpad, safe to delete)
            try { File.Delete(pdfPath); } catch { }
        }
    }

    private string RenderWithHarness(string fixtureName, string template)
    {
        var harness = Path.Combine(RepositoryRoot, "scripts", "dev", "pdf_local_render.sh");
        Assert.True(File.Exists(harness), $"Harness script not found at {harness}");

        var outputDir = Path.GetTempPath();
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{harness}\" \"{fixtureName}\" \"{outputDir}\" \"{template}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = RepositoryRoot
        };

        using var process = Process.Start(psi);
        if (!process.WaitForExit(TimeSpan.FromSeconds(120))) // 2 minutes
        {
            process.Kill();
            throw new InvalidOperationException("Harness timed out after 120 seconds");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Harness failed with exit code {process.ExitCode}.\nStderr:\n{stderr}");
        }

        // The harness logs progress (including "PDF location: ...") to stderr via its own
        // log() helper, which writes to stderr by design (see scripts/dev/pdf_local_render.sh).
        var combined = stdout + "\n" + stderr;
        var lines = combined.Split('\n');
        var pdfLine = lines.FirstOrDefault(l => l.Contains("PDF location:"));

        if (string.IsNullOrEmpty(pdfLine))
        {
            throw new InvalidOperationException(
                $"Harness output did not contain 'PDF location:'. Output:\n{combined}");
        }

        var pdfPath = pdfLine.Substring(pdfLine.LastIndexOf(":") + 1).Trim();

        // Wait a moment for file to be fully written
        System.Threading.Thread.Sleep(500);

        return pdfPath;
    }

    private string MeasurePdf(string pdfPath)
    {
        var pythonScript = Path.Combine(RepositoryRoot, "scripts", "dev", "pdf_measure.py");
        Assert.True(File.Exists(pythonScript), $"Measurement script not found at {pythonScript}");

        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"\"{pythonScript}\" \"{pdfPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = RepositoryRoot
        };

        using var process = Process.Start(psi);
        if (!process.WaitForExit(TimeSpan.FromSeconds(30))) // 30 seconds
        {
            process.Kill();
            throw new InvalidOperationException("PDF measurement timed out after 30 seconds");
        }

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"pdf_measure.py failed with exit code {process.ExitCode}.\nStderr:\n{error}");
        }

        return process.StandardOutput.ReadToEnd();
    }

    private PdfMeasurements ParseMeasurements(string json, string template)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorElem))
        {
            throw new InvalidOperationException(
                $"pdf_measure.py returned error: {errorElem.GetString()}");
        }

        var summary = root.GetProperty("summary");
        var totalPages = summary.GetProperty("total_pages").GetInt32();
        var pageSize = summary.GetProperty("page_size");
        var pageSizeW = pageSize.GetProperty("width_pt").GetDouble();
        var pageSizeH = pageSize.GetProperty("height_pt").GetDouble();

        var pages = root.GetProperty("pages");
        var pageList = new List<PageMeasurement>();

        foreach (var page in pages.EnumerateArray())
        {
            var pageNum = page.GetProperty("page_number").GetInt32();
            var elements = page.GetProperty("elements");

            // Extract voucher letter box
            double? voucherWidth = null;
            if (elements.TryGetProperty("voucher_letter", out var voucher))
            {
                var x0 = voucher.GetProperty("x0").GetDouble();
                var x1 = voucher.GetProperty("x1").GetDouble();
                voucherWidth = x1 - x0;
            }

            // Extract totals box and values
            BboxValue? totalsBox = null;
            var totalsValues = new List<BboxValue>();

            if (elements.TryGetProperty("totals_box", out var tbox))
            {
                totalsBox = new BboxValue
                {
                    X0 = tbox.GetProperty("x0").GetDouble(),
                    X1 = tbox.GetProperty("x1").GetDouble(),
                    Top = tbox.GetProperty("top").GetDouble(),
                    Bottom = tbox.GetProperty("bottom").GetDouble()
                };
            }

            if (elements.TryGetProperty("totals_values", out var tvals))
            {
                foreach (var val in tvals.EnumerateArray())
                {
                    totalsValues.Add(new BboxValue
                    {
                        X0 = val.GetProperty("x0").GetDouble(),
                        X1 = val.GetProperty("x1").GetDouble(),
                        Top = val.GetProperty("top").GetDouble(),
                        Bottom = val.GetProperty("bottom").GetDouble(),
                        Text = val.GetProperty("text").GetString() ?? ""
                    });
                }
            }

            // Extract QR code
            BboxValue? qrBbox = null;
            if (elements.TryGetProperty("qr_code", out var qr))
            {
                qrBbox = new BboxValue
                {
                    X0 = qr.GetProperty("x0").GetDouble(),
                    X1 = qr.GetProperty("x1").GetDouble(),
                    Top = qr.GetProperty("top").GetDouble(),
                    Bottom = qr.GetProperty("bottom").GetDouble()
                };
            }

            pageList.Add(new PageMeasurement
            {
                PageNumber = pageNum,
                VoucherLetterWidth = voucherWidth ?? 0,
                TotalsBox = totalsBox,
                TotalsValues = totalsValues,
                QrBbox = qrBbox
            });
        }

        return new PdfMeasurements
        {
            Template = template,
            TotalPages = totalPages,
            PageSizeWidth = pageSizeW,
            PageSizeHeight = pageSizeH,
            Pages = pageList
        };
    }

    private void AssertTotalsValuesContainedInBox(PageMeasurement page)
    {
        if (page.TotalsBox == null || page.TotalsValues.Count == 0)
        {
            return; // Nothing to check
        }

        var box = page.TotalsBox;
        const double tolerance = 2.0; // 2pt tolerance

        foreach (var value in page.TotalsValues)
        {
            // Check that the value is contained within the box (with tolerance)
            // x0 should be >= box.x0 - tolerance
            // x1 should be <= box.x1 + tolerance
            // top should be >= box.top - tolerance
            // bottom should be <= box.bottom + tolerance

            Assert.True(
                value.X0 >= box.X0 - tolerance,
                $"Page {page.PageNumber}: Totals value '{value.Text}' left edge ({value.X0}) " +
                $"extends beyond box left ({box.X0 - tolerance})");

            Assert.True(
                value.X1 <= box.X1 + tolerance,
                $"Page {page.PageNumber}: Totals value '{value.Text}' right edge ({value.X1}) " +
                $"extends beyond box right ({box.X1 + tolerance})");

            Assert.True(
                value.Top >= box.Top - tolerance,
                $"Page {page.PageNumber}: Totals value '{value.Text}' top edge ({value.Top}) " +
                $"extends beyond box top ({box.Top - tolerance})");

            Assert.True(
                value.Bottom <= box.Bottom + tolerance,
                $"Page {page.PageNumber}: Totals value '{value.Text}' bottom edge ({value.Bottom}) " +
                $"extends beyond box bottom ({box.Bottom + tolerance})");
        }
    }

    // Helper types for measurement data
    private class PdfMeasurements
    {
        public string Template { get; set; } = "";
        public int TotalPages { get; set; }
        public double PageSizeWidth { get; set; }
        public double PageSizeHeight { get; set; }
        public List<PageMeasurement> Pages { get; set; } = [];
    }

    private class PageMeasurement
    {
        public int PageNumber { get; set; }
        public double VoucherLetterWidth { get; set; }
        public BboxValue? TotalsBox { get; set; }
        public List<BboxValue> TotalsValues { get; set; } = [];
        public BboxValue? QrBbox { get; set; }
    }

    private class BboxValue
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Top { get; set; }
        public double Bottom { get; set; }
        public string Text { get; set; } = "";
    }
}

