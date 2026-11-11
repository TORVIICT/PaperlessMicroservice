using Ocr.Infrastructure.Engines;

namespace Ocr.UnitTests;

public sealed class PdfToolOutputParserTests
{
    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("DOCUMENT.PDF", true)]
    [InlineData("scan.png", false)]
    [InlineData("photo.jpg", false)]
    public void IsPdfFile_DistinguishesPdfFromImages(string fileName, bool expected)
    {
        Assert.Equal(expected, OcrMyPdfProcessingEngine.IsPdfFile(fileName));
    }

    [Theory]
    [InlineData("/tmp/animated.gif", "/tmp/animated.gif[0]")]
    [InlineData("/tmp/animated.WEBP", "/tmp/animated.WEBP[0]")]
    [InlineData("/tmp/photo.heic", "/tmp/photo.heic[0]")]
    [InlineData("/tmp/scan.png", "/tmp/scan.png")]
    [InlineData("/tmp/document.tiff", "/tmp/document.tiff")]
    public void GetImageMagickInputPath_SelectsFirstFrameOnlyForAnimatedFormats(
        string inputPath,
        string expected)
    {
        Assert.Equal(expected, OcrMyPdfProcessingEngine.GetImageMagickInputPath(inputPath));
    }

    [Theory]
    [InlineData("animation.gif", true)]
    [InlineData("animation.WEBP", true)]
    [InlineData("photo.heif", true)]
    [InlineData("scan.tiff", false)]
    [InlineData("scan.png", false)]
    public void IsAnimatedImageFormat_DistinguishesAnimatedContainers(
        string fileName,
        bool expected)
    {
        Assert.Equal(expected, OcrMyPdfProcessingEngine.IsAnimatedImageFormat(fileName));
    }

    [Fact]
    public void ParsePageCount_ReadsPdfInfoPages()
    {
        const string output = "Title: Laboratorio 1\nPages:           8\nPDF version: 1.7";

        Assert.Equal(8, PdfToolOutputParser.ParsePageCount(output));
    }

    [Fact]
    public void NormalizeSidecarText_RemovesSkippedPageDiagnostics()
    {
        const string sidecar = "[OCR skipped on page(s) 1-8]\n";

        Assert.Equal(string.Empty, PdfToolOutputParser.NormalizeSidecarText(sidecar));
    }

    [Fact]
    public void NormalizeSidecarText_PreservesRecognizedText()
    {
        const string sidecar = "Texto reconocido\n[OCR skipped on page(s) 2]\nMás texto";

        Assert.Equal(
            "Texto reconocido\n\nMás texto",
            PdfToolOutputParser.NormalizeSidecarText(sidecar).Replace("\r\n", "\n"));
    }
}
