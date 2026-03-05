using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;

namespace JD.AI.Core.Tools;

/// <summary>
/// Multimodal analysis tools — image metadata, PDF text extraction, and media inspection.
/// </summary>
[ToolPlugin("multimodal")]
public sealed class MultimodalTools
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".svg",
    };

    private static readonly HashSet<string> AllowedPdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const int MaxOutputChars = 50_000;

    // ── Image analysis ───────────────────────────────────

    [KernelFunction("image_analyze")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Analyze an image file and return metadata (dimensions, format, file size). " +
                 "For vision-capable models, returns base64 data for further inspection. " +
                 "Supports PNG, JPG, GIF, BMP, WebP, TIFF, SVG.")]
    public static async Task<string> AnalyzeImageAsync(
        [Description("Path to a local image file or an HTTP(S) URL")] string source,
        [Description("If true, include base64-encoded image data for vision models (default: false)")] bool includeData = false,
        [Description("Maximum width/height to resize for vision (0 = no resize, default: 1024)")] int maxDimension = 1024)
    {
        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                return await AnalyzeRemoteImageAsync(uri, includeData, maxDimension).ConfigureAwait(false);
            }

            return AnalyzeLocalImage(source, includeData, maxDimension);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error analyzing image: {ex.Message}";
        }
    }

    // ── PDF analysis ─────────────────────────────────────

    [KernelFunction("pdf_analyze")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Extract text and metadata from a PDF file. Returns page count, text content, " +
                 "and document metadata. Supports local files and HTTP(S) URLs.")]
    public static async Task<string> AnalyzePdfAsync(
        [Description("Path to a local PDF file or an HTTP(S) URL")] string source,
        [Description("Starting page number (1-based, default: 1)")] int startPage = 1,
        [Description("Ending page number (1-based, 0 = all pages, default: 0)")] int endPage = 0,
        [Description("Maximum characters of extracted text to return (default: 50000)")] int maxChars = MaxOutputChars)
    {
        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                return await AnalyzeRemotePdfAsync(uri, startPage, endPage, maxChars).ConfigureAwait(false);
            }

            return AnalyzeLocalPdf(source, startPage, endPage, maxChars);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error analyzing PDF: {ex.Message}";
        }
    }

    // ── Media view (unified inspection) ──────────────────

    [KernelFunction("media_view")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Inspect a media file and return its type, metadata, and a brief summary. " +
                 "Auto-detects whether the file is an image, PDF, or other document type.")]
    public static async Task<string> MediaViewAsync(
        [Description("Path to a local file or an HTTP(S) URL")] string source)
    {
        try
        {
            var ext = Path.GetExtension(source);

            if (AllowedImageExtensions.Contains(ext))
            {
                return await AnalyzeImageAsync(source, includeData: false).ConfigureAwait(false);
            }

            if (AllowedPdfExtensions.Contains(ext))
            {
                return await AnalyzePdfAsync(source).ConfigureAwait(false);
            }

            // For other files, show basic file info
            if (File.Exists(source))
            {
                var fi = new FileInfo(source);
                return new StringBuilder()
                    .AppendLine(CultureInfo.InvariantCulture, $"## File Info: {fi.Name}")
                    .AppendLine(CultureInfo.InvariantCulture, $"- **Type**: {ext}")
                    .AppendLine(CultureInfo.InvariantCulture, $"- **Size**: {FormatBytes(fi.Length)}")
                    .AppendLine(CultureInfo.InvariantCulture, $"- **Modified**: {fi.LastWriteTimeUtc:u}")
                    .AppendLine()
                    .AppendLine("_Not a recognized image or PDF. Use `read_file` for text content._")
                    .ToString();
            }

            return OutputFormatter.Error($"File not found or unsupported: {source}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"Error inspecting media: {ex.Message}";
        }
    }

    // ── Private helpers ──────────────────────────────────

    private static string AnalyzeLocalImage(string path, bool includeData, int maxDimension)
    {
        if (!File.Exists(path))
            return OutputFormatter.Error($"File not found: {path}");

        var ext = Path.GetExtension(path);
        if (!AllowedImageExtensions.Contains(ext))
            return OutputFormatter.Error($"Unsupported image format: {ext}. Supported: {string.Join(", ", AllowedImageExtensions)}");

        var fi = new FileInfo(path);
        if (fi.Length > MaxFileSizeBytes)
            return OutputFormatter.Error($"File too large ({FormatBytes(fi.Length)}). Maximum: {FormatBytes(MaxFileSizeBytes)}");

        // SVG is text-based, handle separately
        if (string.Equals(ext, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSvgInfo(path, fi);
        }

        using var image = Image.Load(path);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Image Analysis: {fi.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Dimensions**: {image.Width} × {image.Height} px");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Format**: {image.Metadata.DecodedImageFormat?.Name ?? ext}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **File size**: {FormatBytes(fi.Length)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Pixel type**: {image.PixelType.BitsPerPixel} bpp");

        if (image.Metadata.ExifProfile is { } exif)
        {
            sb.AppendLine("- **EXIF data**: Present");
            foreach (var tag in exif.Values.Take(10))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - {tag.Tag}: {tag.GetValue()}");
            }

            if (exif.Values.Count > 10)
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - ... and {exif.Values.Count - 10} more tags");
        }

        if (includeData)
        {
            // Resize if needed and encode as base64
            if (maxDimension > 0 && (image.Width > maxDimension || image.Height > maxDimension))
            {
                var ratio = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                image.Mutate(ctx => ctx.Resize(newWidth, newHeight));
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Resized for vision**: {newWidth} × {newHeight} px");
            }

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            sb.AppendLine();
            sb.AppendLine($"**Base64 PNG** ({FormatBytes(ms.Length)}):");
            sb.AppendLine($"```\ndata:image/png;base64,{base64}\n```");
        }

        return sb.ToString();
    }

    private static async Task<string> AnalyzeRemoteImageAsync(Uri uri, bool includeData, int maxDimension)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("jdai", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(30);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? 0;
        if (contentLength > MaxFileSizeBytes)
            return OutputFormatter.Error($"Remote file too large ({FormatBytes(contentLength)}). Maximum: {FormatBytes(MaxFileSizeBytes)}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"jdai-img-{Guid.NewGuid():N}{GetExtFromUri(uri, ".png")}");
        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fs).ConfigureAwait(false);
            }

            return AnalyzeLocalImage(tempPath, includeData, maxDimension);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string AnalyzeLocalPdf(string path, int startPage, int endPage, int maxChars)
    {
        if (!File.Exists(path))
            return OutputFormatter.Error($"File not found: {path}");

        var ext = Path.GetExtension(path);
        if (!AllowedPdfExtensions.Contains(ext))
            return OutputFormatter.Error($"Not a PDF file: {ext}");

        var fi = new FileInfo(path);
        if (fi.Length > MaxFileSizeBytes)
            return OutputFormatter.Error($"File too large ({FormatBytes(fi.Length)}). Maximum: {FormatBytes(MaxFileSizeBytes)}");

        using var doc = PdfDocument.Open(path);
        var totalPages = doc.NumberOfPages;
        var start = Math.Max(1, startPage);
        var end = endPage <= 0 ? totalPages : Math.Min(totalPages, endPage);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## PDF Analysis: {fi.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Pages**: {totalPages}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **File size**: {FormatBytes(fi.Length)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Version**: {doc.Version}");

        if (doc.Information is { } info)
        {
            if (!string.IsNullOrEmpty(info.Title))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Title**: {info.Title}");
            if (!string.IsNullOrEmpty(info.Author))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Author**: {info.Author}");
            if (!string.IsNullOrEmpty(info.Creator))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Creator**: {info.Creator}");
            if (!string.IsNullOrEmpty(info.Producer))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- **Producer**: {info.Producer}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Extracting pages**: {start}-{end}");
        sb.AppendLine();

        var charCount = 0;
        var truncated = false;
        for (var p = start; p <= end && !truncated; p++)
        {
            var page = doc.GetPage(p);
            var pageText = page.Text;

            sb.AppendLine(CultureInfo.InvariantCulture, $"### Page {p}");

            if (charCount + pageText.Length > maxChars)
            {
                var remaining = maxChars - charCount;
                sb.AppendLine(pageText[..remaining]);
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"_[Truncated at {FormatNumber(maxChars)} chars. " +
                    $"Use startPage/endPage to read specific pages.]_");
                truncated = true;
            }
            else
            {
                sb.AppendLine(pageText);
                charCount += pageText.Length;
            }

            sb.AppendLine();
        }

        var wordCount = sb.ToString().Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        sb.AppendLine(CultureInfo.InvariantCulture, $"---\n**Summary**: {totalPages} pages, ~{FormatNumber(wordCount)} words extracted");

        return sb.ToString();
    }

    private static async Task<string> AnalyzeRemotePdfAsync(Uri uri, int startPage, int endPage, int maxChars)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("jdai", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(60);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? 0;
        if (contentLength > MaxFileSizeBytes)
            return OutputFormatter.Error($"Remote file too large ({FormatBytes(contentLength)}). Maximum: {FormatBytes(MaxFileSizeBytes)}");

        var tempPath = Path.Combine(Path.GetTempPath(), $"jdai-pdf-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fs).ConfigureAwait(false);
            }

            return AnalyzeLocalPdf(tempPath, startPage, endPage, maxChars);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static string BuildSvgInfo(string path, FileInfo fi)
    {
        var content = File.ReadAllText(path);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## SVG Analysis: {fi.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Format**: SVG (vector)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **File size**: {FormatBytes(fi.Length)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Content length**: {FormatNumber(content.Length)} chars");

        // Try to extract viewBox/width/height
        if (content.Contains("viewBox", StringComparison.OrdinalIgnoreCase))
        {
            var vbStart = content.IndexOf("viewBox", StringComparison.OrdinalIgnoreCase);
            var quoteStart = content.IndexOf('"', vbStart);
            if (quoteStart > 0)
            {
                var quoteEnd = content.IndexOf('"', quoteStart + 1);
                if (quoteEnd > quoteStart)
                {
                    var viewBox = content[(quoteStart + 1)..quoteEnd];
                    sb.AppendLine(CultureInfo.InvariantCulture, $"- **viewBox**: {viewBox}");
                }
            }
        }

        // Show first 500 chars of SVG content
        if (content.Length > 500)
        {
            sb.AppendLine();
            sb.AppendLine("**Preview** (first 500 chars):");
            sb.AppendLine($"```xml\n{content[..500]}\n```");
        }

        return sb.ToString();
    }

    private static string GetExtFromUri(Uri uri, string fallback)
    {
        var ext = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrEmpty(ext) ? fallback : ext;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F1} KB"),
        < 1024 * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):F1} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):F2} GB"),
    };

    private static string FormatNumber(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
}
