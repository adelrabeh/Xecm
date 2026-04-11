namespace Darah.ECM.Domain.ValueObjects;

/// <summary>
/// Immutable record capturing everything known about a physical file at upload time.
/// Validated on creation — an invalid extension throws before any storage call is made.
/// </summary>
public sealed record FileMetadata
{
    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".csv",
            ".jpg", ".jpeg", ".png", ".tif", ".tiff",
            ".mp4", ".mp3", ".zip", ".msg"
        };

    public string StorageKey      { get; }
    public string OriginalFileName { get; }
    public string ContentType     { get; }
    public string FileExtension   { get; }
    public long   FileSizeBytes   { get; }
    public string ContentHash     { get; }   // SHA-256 hex
    public string StorageProvider { get; }

    private FileMetadata(string storageKey, string originalFileName, string contentType,
        string fileExtension, long fileSizeBytes, string contentHash, string storageProvider)
    {
        StorageKey = storageKey; OriginalFileName = originalFileName;
        ContentType = contentType; FileExtension = fileExtension;
        FileSizeBytes = fileSizeBytes; ContentHash = contentHash;
        StorageProvider = storageProvider;
    }

    /// <summary>
    /// Factory — validates extension before constructing.
    /// Callers should catch <see cref="ArgumentException"/> and surface it as a validation error.
    /// </summary>
    public static FileMetadata Create(string storageKey, string fileName, string contentType,
        long fileSizeBytes, string contentHash, string storageProvider)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File extension '{ext}' is not permitted.");
        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be positive.");
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException("Content hash is required.");

        return new FileMetadata(storageKey, fileName, contentType, ext,
            fileSizeBytes, contentHash, storageProvider);
    }

    public string FriendlySize => FileSizeBytes switch
    {
        < 1_024             => $"{FileSizeBytes} B",
        < 1_048_576         => $"{FileSizeBytes / 1_024.0:F1} KB",
        < 1_073_741_824     => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        _                   => $"{FileSizeBytes / 1_073_741_824.0:F2} GB"
    };

    public bool IsImage => new[] { ".jpg", ".jpeg", ".png", ".tif", ".tiff" }.Contains(FileExtension);
    public bool IsPdf   => FileExtension == ".pdf";
}
