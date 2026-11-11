using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ocr.Application.Ports;
using Ocr.Domain.Entities;

namespace Ocr.Infrastructure.Engines;

public sealed class OcrMyPdfProcessingEngine : IOcrProcessingEngine
{
    private readonly ILogger<OcrMyPdfProcessingEngine> _logger;
    private readonly IOcrProcessingEngine _fallbackEngine;
    private readonly string _ocrmypdfPath;
    private readonly TimeSpan _defaultTimeout;

    public OcrMyPdfProcessingEngine(
        ILogger<OcrMyPdfProcessingEngine> logger,
        IOcrProcessingEngine fallbackEngine,
        string? ocrmypdfPath = null,
        TimeSpan? defaultTimeout = null)
    {
        _logger = logger;
        _fallbackEngine = fallbackEngine;
        _ocrmypdfPath = string.IsNullOrWhiteSpace(ocrmypdfPath) ? "ocrmypdf" : ocrmypdfPath;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(3);
    }

    public Task<OcrJobResult> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        string language,
        CancellationToken cancellationToken = default)
    {
        return ProcessAsync(fileBytes, fileName, new OcrProcessingOptions(Language: language), cancellationToken);
    }

    public async Task<OcrJobResult> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        OcrProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Check if external OCRmyPDF binary is reachable
        if (!IsOcrMyPdfAvailable())
        {
            _logger.LogWarning(
                "OCRmyPDF executable '{Executable}' not found in system PATH. Delegating to internal BasicOcrProcessingEngine.",
                _ocrmypdfPath);
            return await _fallbackEngine.ProcessAsync(fileBytes, fileName, options, cancellationToken);
        }

        // 2. Prepare isolated working directory
        var baseDir = Path.Combine(Path.GetTempPath(), "ocr_work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "document.pdf";
        }

        var inputPath = Path.Combine(baseDir, safeFileName);
        var outputPath = Path.Combine(baseDir, "output_archived.pdf");
        var sidecarPath = Path.Combine(baseDir, "output_sidecar.txt");

        // Validate paths stay within base directory (Anti-Path-Traversal)
        ValidatePathWithinBase(inputPath, baseDir);
        ValidatePathWithinBase(outputPath, baseDir);
        ValidatePathWithinBase(sidecarPath, baseDir);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await File.WriteAllBytesAsync(inputPath, fileBytes, cancellationToken);

            // OCRmyPDF rejects images with transparency. Normalize every image
            // onto a white background and attach credible scan resolution while
            // leaving PDF inputs untouched.
            var effectiveInputPath = inputPath;
            if (!IsPdfFile(safeFileName))
            {
                effectiveInputPath = Path.Combine(baseDir, "normalized_input.png");
                ValidatePathWithinBase(effectiveInputPath, baseDir);
                await NormalizeImageAsync(inputPath, effectiveInputPath, baseDir, cancellationToken);
            }

            // 3. Configure external process safely with ArgumentList (prevents shell injection)
            var startInfo = new ProcessStartInfo
            {
                FileName = _ocrmypdfPath,
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Language
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(options.Language) ? "eng" : options.Language);

            // Output format: standard PDF/A for archiving
            startInfo.ArgumentList.Add("--output-type");
            startInfo.ArgumentList.Add("pdfa");

            // OCR mode strategy (equivalent to Paperless-ngx)
            if (string.Equals(options.Mode, "force", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("--force-ocr");
            }
            else if (string.Equals(options.Mode, "redo", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add("--redo-ocr");
            }
            else
            {
                startInfo.ArgumentList.Add("--skip-text");
            }

            // Image pre-processing & enhancement (matching legacy deskew and rotation)
            startInfo.ArgumentList.Add("--deskew");
            startInfo.ArgumentList.Add("--rotate-pages");

            // Camera images and exported PNG/JPEG files commonly declare 72
            // DPI even when they contain enough pixels for OCR. OCRmyPDF
            // rejects that metadata as non-credible unless an input DPI is
            // supplied. This option is valid only for image inputs.
            if (!IsPdfFile(safeFileName))
            {
                startInfo.ArgumentList.Add("--image-dpi");
                startInfo.ArgumentList.Add("300");

                if (IsAnimatedImageFormat(safeFileName))
                {
                    // Animated assets are usually small, stylized graphics.
                    // Treat the selected frame as one uniform text block.
                    startInfo.ArgumentList.Add("--tesseract-pagesegmode");
                    startInfo.ArgumentList.Add("6");
                }
            }

            // Output files
            startInfo.ArgumentList.Add("--sidecar");
            startInfo.ArgumentList.Add(sidecarPath);
            startInfo.ArgumentList.Add(effectiveInputPath);
            startInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = startInfo };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_defaultTimeout);

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("OCRmyPDF failed with exit code {ExitCode}. Stderr: {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException($"OCRmyPDF execution failed with exit code {process.ExitCode}: {stderr}");
            }

            // 4. Extract all text from the final PDF. OCRmyPDF sidecars only
            // contain newly recognized text; pages skipped because they already
            // had a text layer are represented by messages such as
            // "[OCR skipped on page(s) 1-8]". pdftotext reads both existing and
            // newly generated text layers, so it is the authoritative source.
            var extractedText = await TryExtractPdfTextAsync(outputPath, baseDir, cancellationToken);
            if (string.IsNullOrWhiteSpace(extractedText) && File.Exists(sidecarPath))
            {
                var sidecarText = await File.ReadAllTextAsync(sidecarPath, cancellationToken);
                extractedText = PdfToolOutputParser.NormalizeSidecarText(sidecarText);
            }

            // PDF objects may be compressed, so counting "/Type /Page" tokens
            // is not reliable. pdfinfo reads the page tree correctly.
            var pageCount = await TryGetPageCountAsync(outputPath, baseDir, cancellationToken);
            if (pageCount is null)
            {
                var outBytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
                pageCount = Math.Max(1, System.Text.RegularExpressions.Regex.Matches(
                    System.Text.Encoding.Latin1.GetString(outBytes), @"/Type\s*/Page\b").Count);
            }

            // 5. Preserve PDF/A and generate Thumbnail in shared storage
            string? finalArchivePath = null;
            string? finalThumbPath = null;

            if (!string.IsNullOrWhiteSpace(options.OutputDirectory) && File.Exists(outputPath))
            {
                Directory.CreateDirectory(options.OutputDirectory);
                var fileId = options.JobId?.ToString("D") ?? Guid.NewGuid().ToString("D");

                // Preserve PDF/A
                finalArchivePath = Path.Combine(options.OutputDirectory, $"{fileId}_archive.pdf");
                File.Copy(outputPath, finalArchivePath, overwrite: true);

                // Generate Thumbnail (first page)
                finalThumbPath = await TryGenerateThumbnailAsync(
                    outputPath,
                    baseDir,
                    options.OutputDirectory,
                    fileId,
                    cancellationToken);
            }

            stopwatch.Stop();

            return new OcrJobResult(
                Text: extractedText,
                PageCount: pageCount.Value,
                DurationMs: stopwatch.ElapsedMilliseconds,
                EngineVersion: "OCRmyPDF External Process",
                ArchivedPdfPath: finalArchivePath,
                ThumbnailPath: finalThumbPath);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("OCRmyPDF timed out after {TimeoutMs}ms processing {FileName}.", _defaultTimeout.TotalMilliseconds, fileName);
            throw new TimeoutException($"OCR processing timed out after {_defaultTimeout.TotalSeconds} seconds.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(baseDir))
                {
                    Directory.Delete(baseDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary directory {Directory}", baseDir);
            }
        }
    }

    private async Task<string> TryExtractPdfTextAsync(
        string pdfPath,
        string workingDir,
        CancellationToken cancellationToken)
    {
        var result = await RunToolAsync(
            "pdftotext",
            workingDir,
            ["-layout", "-enc", "UTF-8", pdfPath, "-"],
            cancellationToken);

        if (result.ExitCode == 0)
        {
            return result.StandardOutput.Trim();
        }

        _logger.LogWarning(
            "pdftotext failed with exit code {ExitCode}. Stderr: {Stderr}",
            result.ExitCode,
            result.StandardError);
        return string.Empty;
    }

    private async Task<int?> TryGetPageCountAsync(
        string pdfPath,
        string workingDir,
        CancellationToken cancellationToken)
    {
        var result = await RunToolAsync(
            "pdfinfo",
            workingDir,
            [pdfPath],
            cancellationToken);

        if (result.ExitCode == 0)
        {
            return PdfToolOutputParser.ParsePageCount(result.StandardOutput);
        }

        _logger.LogWarning(
            "pdfinfo failed with exit code {ExitCode}. Stderr: {Stderr}",
            result.ExitCode,
            result.StandardError);
        return null;
    }

    private static async Task<ToolResult> RunToolAsync(
        string executable,
        string workingDir,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ToolResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

    private async Task<string?> TryGenerateThumbnailAsync(
        string pdfPath,
        string workingDir,
        string outputDir,
        string fileId,
        CancellationToken cancellationToken)
    {
        // 1. Try pdftoppm (poppler-utils)
        var thumbPrefix = Path.Combine(workingDir, "thumb_render");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pdftoppm",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-png");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-l");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-scale-to");
            psi.ArgumentList.Add("500");
            psi.ArgumentList.Add(pdfPath);
            psi.ArgumentList.Add(thumbPrefix);

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(cancellationToken);
                if (proc.ExitCode == 0)
                {
                    var generatedPng = Directory.GetFiles(workingDir, "thumb_render*.png").FirstOrDefault();
                    if (generatedPng != null && File.Exists(generatedPng))
                    {
                        var dest = Path.Combine(outputDir, $"{fileId}_thumb.png");
                        File.Copy(generatedPng, dest, overwrite: true);
                        return dest;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "pdftoppm thumbnail generation skipped.");
        }

        // 2. Fallback: Ghostscript (gs)
        try
        {
            var gsOutput = Path.Combine(workingDir, "thumb_gs.png");
            var psi = new ProcessStartInfo
            {
                FileName = "gs",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-dSAFER");
            psi.ArgumentList.Add("-dBATCH");
            psi.ArgumentList.Add("-dNOPAUSE");
            psi.ArgumentList.Add("-sDEVICE=png16m");
            psi.ArgumentList.Add("-dFirstPage=1");
            psi.ArgumentList.Add("-dLastPage=1");
            psi.ArgumentList.Add("-r150");
            psi.ArgumentList.Add($"-sOutputFile={gsOutput}");
            psi.ArgumentList.Add(pdfPath);

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(cancellationToken);
                if (proc.ExitCode == 0 && File.Exists(gsOutput))
                {
                    var dest = Path.Combine(outputDir, $"{fileId}_thumb.png");
                    File.Copy(gsOutput, dest, overwrite: true);
                    return dest;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ghostscript thumbnail generation skipped.");
        }

        return null;
    }

    private bool IsOcrMyPdfAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ocrmypdfPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(1000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task NormalizeImageAsync(
        string inputPath,
        string outputPath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "convert",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(GetImageMagickInputPath(inputPath));
        startInfo.ArgumentList.Add("-background");
        startInfo.ArgumentList.Add("white");
        startInfo.ArgumentList.Add("-alpha");
        startInfo.ArgumentList.Add("remove");
        startInfo.ArgumentList.Add("-alpha");
        startInfo.ArgumentList.Add("off");
        startInfo.ArgumentList.Add("-units");
        startInfo.ArgumentList.Add("PixelsPerInch");
        startInfo.ArgumentList.Add("-density");
        startInfo.ArgumentList.Add("300");

        if (IsAnimatedImageFormat(inputPath))
        {
            startInfo.ArgumentList.Add("-resize");
            startInfo.ArgumentList.Add("400%");
        }

        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;

        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"Image normalization failed with exit code {process.ExitCode}: {stderr}");
        }
    }

    internal static bool IsPdfFile(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);

    internal static string GetImageMagickInputPath(string inputPath)
    {
        // Animated image formats can produce one output file per frame. OCR
        // treats them as a single document image, so use the representative
        // first frame and keep TIFF available for genuine multi-page handling.
        return IsAnimatedImageFormat(inputPath) ? $"{inputPath}[0]" : inputPath;
    }

    internal static bool IsAnimatedImageFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".heif", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidatePathWithinBase(string filePath, string baseDirectory)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullBase = Path.GetFullPath(baseDirectory);
        if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path traversal attempt detected: {filePath}");
        }
    }
}

internal static partial class PdfToolOutputParser
{
    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?im)^Pages:\s*(\d+)\s*$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex PagesRegex();

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?im)^\s*\[OCR skipped on page\(s\).*?\]\s*$")]
    private static partial System.Text.RegularExpressions.Regex SkippedPageMessageRegex();

    internal static int? ParsePageCount(string pdfInfoOutput)
    {
        var match = PagesRegex().Match(pdfInfoOutput);
        return match.Success && int.TryParse(match.Groups[1].Value, out var pages) && pages > 0
            ? pages
            : null;
    }

    internal static string NormalizeSidecarText(string sidecarText) =>
        SkippedPageMessageRegex().Replace(sidecarText, string.Empty).Trim();
}
