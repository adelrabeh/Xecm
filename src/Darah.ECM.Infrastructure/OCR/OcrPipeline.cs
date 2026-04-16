using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Darah.ECM.Infrastructure.OCR;

public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(Stream fileStream, string contentType,
        CancellationToken ct = default);
}

public record OcrResult(
    bool Success,
    string ExtractedText,
    string DetectedLanguage,
    double Confidence,
    IEnumerable<OcrPage> Pages,
    string? Error = null);

public record OcrPage(int PageNumber, string Text, double Confidence);

/// <summary>
/// Production OCR using Azure Cognitive Services (Document Intelligence).
/// Falls back to Tesseract for on-premise deployments.
/// </summary>
public sealed class AzureDocumentIntelligenceOcrService : IOcrService
{
    private readonly HttpClient _http;
    private readonly ILogger<AzureDocumentIntelligenceOcrService> _log;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public AzureDocumentIntelligenceOcrService(
        HttpClient http,
        ILogger<AzureDocumentIntelligenceOcrService> log,
        IConfiguration config)
    {
        _http = http;
        _log = log;
        _endpoint = config["Azure:DocumentIntelligence:Endpoint"]
            ?? throw new InvalidOperationException("Azure:DocumentIntelligence:Endpoint not set");
        _apiKey = config["Azure:DocumentIntelligence:ApiKey"]
            ?? throw new InvalidOperationException("Azure:DocumentIntelligence:ApiKey not set");
    }

    public async Task<OcrResult> ExtractTextAsync(Stream fileStream,
        string contentType, CancellationToken ct)
    {
        try
        {
            // Step 1: Submit document for analysis
            var analyzeUrl = $"{_endpoint}/documentintelligence/documentModels/" +
                             $"prebuilt-read:analyze?api-version=2024-02-29-preview";

            using var content = new StreamContent(fileStream);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            _http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
            var response = await _http.PostAsync(analyzeUrl, content, ct);
            response.EnsureSuccessStatusCode();

            // Step 2: Poll for results
            var operationUrl = response.Headers.GetValues("Operation-Location").First();
            OcrApiResponse? result = null;

            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(2000, ct);
                var pollResponse = await _http.GetAsync(operationUrl, ct);
                var json = await pollResponse.Content.ReadAsStringAsync(ct);
                result = JsonSerializer.Deserialize<OcrApiResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Status == "succeeded") break;
                if (result?.Status == "failed") throw new Exception("OCR failed");
            }

            if (result?.AnalyzeResult == null)
                return new OcrResult(false, "", "unknown", 0, [], "Timeout");

            var pages = result.AnalyzeResult.Pages.Select((p, i) =>
                new OcrPage(i + 1, string.Join(" ", p.Words.Select(w => w.Content)),
                    p.Words.Average(w => w.Confidence)));

            var allText = string.Join("\n\n", pages.Select(p => p.Text));
            var avgConfidence = pages.Any() ? pages.Average(p => p.Confidence) : 0;

            return new OcrResult(true, allText,
                DetectLanguage(allText), avgConfidence, pages);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OCR extraction failed");
            return new OcrResult(false, "", "unknown", 0, [], ex.Message);
        }
    }

    private static string DetectLanguage(string text)
    {
        // Simple Arabic detection — 30%+ Arabic chars = Arabic
        var arabicChars = text.Count(c => c >= '\u0600' && c <= '\u06FF');
        return arabicChars > text.Length * 0.3 ? "ar" : "en";
    }

    private record OcrApiResponse(string Status, AnalyzeResult? AnalyzeResult);
    private record AnalyzeResult(IList<PageResult> Pages);
    private record PageResult(IList<WordResult> Words);
    private record WordResult(string Content, double Confidence);
}

/// <summary>
/// Tesseract-based OCR for on-premise / air-gapped environments.
/// Requires: apt-get install tesseract-ocr tesseract-ocr-ara
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private readonly ILogger<TesseractOcrService> _log;

    public TesseractOcrService(ILogger<TesseractOcrService> log) => _log = log;

    public async Task<OcrResult> ExtractTextAsync(Stream fileStream,
        string contentType, CancellationToken ct)
    {
        try
        {
            var tmpPath = Path.GetTempFileName() + ".png";
            await using (var fs = File.Create(tmpPath))
                await fileStream.CopyToAsync(fs, ct);

            // Run Tesseract with Arabic + English language packs
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"{tmpPath} stdout -l ara+eng",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            File.Delete(tmpPath);

            if (process.ExitCode != 0)
                return new OcrResult(false, "", "unknown", 0, [], "Tesseract failed");

            var page = new OcrPage(1, output.Trim(), 0.85);
            var lang = output.Count(c => c >= '\u0600' && c <= '\u06FF') >
                       output.Length * 0.3 ? "ar" : "en";

            return new OcrResult(true, output.Trim(), lang, 0.85, new[] { page });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tesseract OCR failed");
            return new OcrResult(false, "", "unknown", 0, [], ex.Message);
        }
    }
}
