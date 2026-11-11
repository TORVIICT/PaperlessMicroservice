using System;
using System.IO;
using System.Text;
using OcrAcl.Api.Translation;
using OcrAcl.Application.Models;
using Xunit;

namespace OcrAcl.Tests.Unit;

public class MonolithTranslatorTests
{
    private readonly MonolithTranslator _translator = new();

    [Fact]
    public void TranslateToContentPatch_ReturnsTextFromOcrResult()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "Extracted OCR text",
            PageCount = 3,
            DurationMs = 1500,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/123/archive",
            HasThumbnail = true,
            ThumbnailUrl = "/api/v1/ocr/jobs/123/thumbnail"
        };

        // Act
        var patch = _translator.TranslateToContentPatch(result);

        // Assert
        Assert.Equal("Extracted OCR text", patch.Content);
    }

    [Fact]
    public void TranslateToContentPatch_ReturnsEmptyString_WhenTextIsNull()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = null,
            PageCount = 0,
            DurationMs = 0,
            EngineVersion = "",
            HasArchivedPdf = false,
            HasThumbnail = false
        };

        // Act
        var patch = _translator.TranslateToContentPatch(result);

        // Assert
        Assert.Equal(string.Empty, patch.Content);
    }

    [Fact]
    public void TranslateToUploadRequest_UsesTitle_WhenProvided()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 100,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = false,
            HasThumbnail = false
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        // Act
        var request = new MonolithTranslator().TranslateToUploadRequest(
            result,
            "document.pdf",
            "application/pdf",
            stream,
            title: "Custom Title",
            created: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));

        // Assert
        Assert.Equal("Custom Title", request.Title);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero), request.Created);
        Assert.Equal("document.pdf", request.FileName);
        Assert.Equal("application/pdf", request.ContentType);
        Assert.Same(stream, request.FileContent);
    }

    [Fact]
    public void TranslateToUploadRequest_DerivesTitleFromFilename_WhenNotProvided()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 100,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = false,
            HasThumbnail = false
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        // Act
        var request = new MonolithTranslator().TranslateToUploadRequest(
            result,
            "my-document.pdf",
            "application/pdf",
            stream);

        // Assert
        Assert.Equal("my-document", request.Title);
        Assert.Null(request.Created);
    }

    [Fact]
    public void TranslateToUploadRequest_PreservesFileContentAndMetadata()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 2,
            DurationMs = 2000,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/abc/archive",
            HasThumbnail = true,
            ThumbnailUrl = "/api/v1/ocr/jobs/abc/thumbnail"
        };
        var content = Encoding.UTF8.GetBytes("PDF content");
        using var stream = new MemoryStream(content);

        // Act
        var request = new MonolithTranslator().TranslateToUploadRequest(
            result,
            "document.pdf",
            "application/pdf",
            stream);

        // Assert
        Assert.Equal("document.pdf", request.FileName);
        Assert.Equal("application/pdf", request.ContentType);
        Assert.Same(stream, request.FileContent);
        Assert.Equal(content.Length, request.FileContent.Length);
    }

    [Fact]
    public void TranslateToContentPatch_AlwaysReturnsContent_EvenWhenEmpty()
    {
        // Arrange - MON-API §5 ACL Rule R3: "Step 3 is always sent"
        var result = new OcrResult
        {
            Text = "",
            PageCount = 0,
            DurationMs = 0,
            EngineVersion = "",
            HasArchivedPdf = false,
            HasThumbnail = false
        };

        // Act
        var patch = new MonolithTranslator().TranslateToContentPatch(result);

        // Assert
        Assert.Equal(string.Empty, patch.Content);
    }

    [Fact]
    public void SelectUploadFile_ReturnsOriginalFile_WhenNoArchivedPdf()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 100,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = false,
            HasThumbnail = false
        };
        var content = Encoding.UTF8.GetBytes("original file content");
        using var stream = new MemoryStream(content);

        // Act
        var selection = new MonolithTranslator().SelectUploadFile(
            result,
            stream,
            "original.pdf",
            "application/pdf");

        // Assert
        Assert.Equal("original.pdf", selection.FileName);
        Assert.Equal("application/pdf", selection.ContentType);
        Assert.Same(stream, selection.FileContent);
        Assert.Equal(0, selection.FileContent.Position); // Position reset to 0
    }

    [Fact]
    public void SelectUploadFile_Throws_WhenHasArchivedPdfIsTrue()
    {
        // Arrange - Per MON-API §4, §5 ACL Rule R1: "Upload PDF/A with text layer when OCR service delivers one"
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 100,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = true,
            ArchiveDownloadUrl = "/api/v1/ocr/jobs/123/archive",
            HasThumbnail = true,
            ThumbnailUrl = "/api/v1/ocr/jobs/123/thumbnail"
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("original"));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new MonolithTranslator().SelectUploadFile(result, stream, "original.pdf", "application/pdf"));

        Assert.Contains("HasArchivedPdf is true but archive content not provided", ex.Message);
        Assert.Contains("IOcrClient.DownloadArchiveAsync", ex.Message);
    }

    [Fact]
    public void SelectUploadFile_ResetsStreamPosition()
    {
        // Arrange
        var result = new OcrResult
        {
            Text = "text",
            PageCount = 1,
            DurationMs = 100,
            EngineVersion = "tesseract 5.3.0",
            HasArchivedPdf = false,
            HasThumbnail = false
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        stream.Position = 10; // Set to non-zero position

        // Act
        var selection = new MonolithTranslator().SelectUploadFile(result, stream, "test.pdf", "application/pdf");

        // Assert
        Assert.Equal(0, selection.FileContent.Position);
    }
}