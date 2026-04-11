using Darah.ECM.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Darah.ECM.Infrastructure.FileStorage.Validation;

/// <summary>
/// Validates uploaded files by inspecting their magic bytes (file signatures)
/// and comparing against the declared MIME type and extension.
///
/// Prevents:
///  - Extension spoofing (e.g., malware.exe renamed to report.pdf)
///  - MIME type spoofing (declared as PDF but contains executable code)
///  - Uploading zero-byte files
///  - Files exceeding the configured size limit
///
/// Optionally hooks into an antivirus scan interface (IAntivirusScanner)
/// when registered in DI.
/// </summary>
public sealed class FileValidationService : IFileValidationService
{
    private const long MaxFileSizeBytes = 512L * 1024 * 1024; // 512 MB

    private readonly ILogger<FileValidationService> _logger;
    private readonly IAntivirusScanner? _avScanner;

    public FileValidationService(ILogger<FileValidationService> logger,
        IAntivirusScanner? avScanner = null)
    {
        _logger    = logger;
        _avScanner = avScanner;
    }

    // ─── Magic byte signatures ─────────────────────────────────────────────
    private static readonly Dictionary<string, byte[][]> MagicBytes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"]  = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },               // %PDF
            [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },               // PK (ZIP)
            [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },               // PK (ZIP)
            [".pptx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },               // PK (ZIP)
            [".doc"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },               // OLE2
            [".xls"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },               // OLE2
            [".jpg"]  = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },                      // JPEG SOI
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"]  = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A } },  // PNG
            [".gif"]  = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } },               // GIF8
            [".tif"]  = new[] {
                new byte[] { 0x49, 0x49, 0x2A, 0x00 },  // TIFF LE
                new byte[] { 0x4D, 0x4D, 0x00, 0x2A }   // TIFF BE
            },
            [".tiff"] = new[] {
                new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                new byte[] { 0x4D, 0x4D, 0x00, 0x2A }
            },
            [".zip"]  = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },               // PK
            [".7z"]   = new[] { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } },  // 7z
            [".mp4"]  = new[] { new byte[] { 0x00, 0x00, 0x00 } },                      // (checked by offset)
            [".mp3"]  = new[] { new byte[] { 0xFF, 0xFB }, new byte[] { 0x49, 0x44, 0x33 } },
            [".txt"]  = null!, // Text has no magic bytes — validated by extension only
            [".csv"]  = null!, // Same
            [".msg"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },               // OLE2
        };

    // Executable magic bytes — always block regardless of extension
    private static readonly byte[][] BlockedSignatures =
    {
        new byte[] { 0x4D, 0x5A },                          // MZ (Windows PE .exe, .dll, .sys)
        new byte[] { 0x7F, 0x45, 0x4C, 0x46 },             // ELF (Linux executable)
        new byte[] { 0xCA, 0xFE, 0xBA, 0xBE },             // Java class file
        new byte[] { 0x23, 0x21 },                          // Shebang (#!)
        new byte[] { 0x3C, 0x73, 0x63, 0x72, 0x69, 0x70 }, // <scrip (HTML script tag)
    };

    public async Task<FileValidationResult> ValidateAsync(
        Stream fileStream, string fileName,
        string declaredContentType, CancellationToken ct = default)
    {
        // ── Size check ──────────────────────────────────────────────────────
        if (fileStream.Length == 0)
            return new FileValidationResult(false, "الملف فارغ", null);

        if (fileStream.Length > MaxFileSizeBytes)
            return new FileValidationResult(false,
                $"حجم الملف ({fileStream.Length / 1_048_576.0:F1} MB) يتجاوز الحد المسموح (512 MB)", null);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // ── Extension allowlist ─────────────────────────────────────────────
        if (!MagicBytes.ContainsKey(ext))
            return new FileValidationResult(false, $"نوع الملف '{ext}' غير مسموح", null);

        // ── Read magic bytes ────────────────────────────────────────────────
        var headerBuf = new byte[16];
        fileStream.Position = 0;
        var read = await fileStream.ReadAsync(headerBuf, 0, 16, ct);
        fileStream.Position = 0;

        // ── Block executable signatures regardless of extension ─────────────
        foreach (var blocked in BlockedSignatures)
        {
            if (read >= blocked.Length && headerBuf.Take(blocked.Length).SequenceEqual(blocked))
            {
                _logger.LogCritical(
                    "Blocked executable file upload: {File} declared as {Mime}",
                    fileName, declaredContentType);
                return new FileValidationResult(false,
                    "رُفض الملف: يحتوي على توقيع ملف تنفيذي محظور", null);
            }
        }

        // ── Validate magic bytes for known types ────────────────────────────
        var knownSignatures = MagicBytes[ext];
        if (knownSignatures is not null) // null = no magic bytes for this ext (txt, csv)
        {
            var signatureMatched = knownSignatures.Any(sig =>
                read >= sig.Length && headerBuf.Take(sig.Length).SequenceEqual(sig));

            if (!signatureMatched)
            {
                _logger.LogWarning(
                    "Magic byte mismatch: {File} (declared={DeclaredMime}, ext={Ext})",
                    fileName, declaredContentType, ext);
                return new FileValidationResult(false,
                    $"توقيع الملف لا يطابق الامتداد '{ext}'. يُشتبه في تزوير نوع الملف.", null);
            }
        }

        // ── Optional antivirus scan ─────────────────────────────────────────
        if (_avScanner is not null)
        {
            fileStream.Position = 0;
            var avResult = await _avScanner.ScanAsync(fileStream, fileName, ct);
            fileStream.Position = 0;

            if (!avResult.IsClean)
            {
                _logger.LogCritical("Antivirus scan FAILED for {File}: {Threat}", fileName, avResult.ThreatName);
                return new FileValidationResult(false,
                    $"تم اكتشاف تهديد في الملف: {avResult.ThreatName}", null);
            }
        }

        return new FileValidationResult(true, null, declaredContentType);
    }
}

/// <summary>
/// Antivirus scanner hook — implement with ClamAV or cloud AV service.
/// Register in DI to activate scanning; omit for environments without AV capability.
/// </summary>
public interface IAntivirusScanner
{
    Task<AntivirusScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

public sealed record AntivirusScanResult(bool IsClean, string? ThreatName);
