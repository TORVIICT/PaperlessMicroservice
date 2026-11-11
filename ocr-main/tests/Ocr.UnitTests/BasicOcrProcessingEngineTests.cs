using System.Text;
using Ocr.Application.Ports;
using Ocr.Infrastructure.Engines;

namespace Ocr.UnitTests;

public class BasicOcrProcessingEngineTests
{
    private readonly BasicOcrProcessingEngine _engine = new();

    [Fact]
    public async Task ProcessAsync_ReturnsResultWithTextAndPageCount()
    {
        var content = Encoding.UTF8.GetBytes("%PDF-1.4 sample invoice text content for testing");

        var result = await _engine.ProcessAsync(content, "invoice.pdf", "eng");

        Assert.NotNull(result);
        Assert.NotEmpty(result.Text);
        Assert.True(result.PageCount >= 1);
        Assert.Equal(BasicOcrProcessingEngine.Version, result.EngineVersion);
    }

    [Fact]
    public async Task ProcessAsync_SpanishLanguage_UsesSpanishVocabulary()
    {
        var content = new byte[] { 0x1, 0x2, 0x3, 0x4, 0x5 };

        var result = await _engine.ProcessAsync(content, "documento.bin", "spa");

        Assert.NotNull(result);
        Assert.NotEmpty(result.Text);
        Assert.Contains(result.Text.Split(' ')[0], new[]
        {
            "factura", "cliente", "empresa", "total", "importe", "servicio",
            "documento", "fecha", "numero", "pago", "descripcion", "iva"
        });
    }

    [Fact]
    public async Task ProcessAsync_WithOptions_GeneratesArchivedPdfAndThumbnail()
    {
        var content = Encoding.UTF8.GetBytes("%PDF-1.4 sample document with graphics and text");
        var tempOutDir = Path.Combine(Path.GetTempPath(), "unit_test_artifacts_" + Guid.NewGuid().ToString("N"));
        var testJobId = Guid.NewGuid();

        try
        {
            var options = new OcrProcessingOptions(
                Language: "eng",
                Mode: "force",
                OutputDirectory: tempOutDir,
                JobId: testJobId);

            var result = await _engine.ProcessAsync(content, "document.pdf", options);

            Assert.NotNull(result);
            Assert.NotNull(result.ArchivedPdfPath);
            Assert.True(File.Exists(result.ArchivedPdfPath));

            Assert.NotNull(result.ThumbnailPath);
            Assert.True(File.Exists(result.ThumbnailPath));
        }
        finally
        {
            if (Directory.Exists(tempOutDir))
            {
                Directory.Delete(tempOutDir, recursive: true);
            }
        }
    }
}
