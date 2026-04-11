// ============================================================
// SHARED CONSTANTS
// ============================================================
namespace Darah.ECM.Shared.Constants;

public static class Permissions
{
    // Documents
    public const string DocumentRead     = "documents.read";
    public const string DocumentCreate   = "documents.create";
    public const string DocumentUpdate   = "documents.update";
    public const string DocumentDelete   = "documents.delete";
    public const string DocumentDownload = "documents.download";
    public const string DocumentPrint    = "documents.print";
    public const string DocumentCheckOut = "documents.checkout";
    public const string DocumentCheckIn  = "documents.checkin";

    // Workflow
    public const string WorkflowSubmit  = "workflow.submit";
    public const string WorkflowApprove = "workflow.approve";
    public const string WorkflowReject  = "workflow.reject";
    public const string WorkflowDelegate= "workflow.delegate";
    public const string WorkflowManage  = "workflow.manage";

    // Workspaces (xECM)
    public const string WorkspaceRead   = "workspace.read";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceUpdate = "workspace.update";
    public const string WorkspaceManage = "workspace.manage";

    // Admin
    public const string AdminUsers     = "admin.users";
    public const string AdminRoles     = "admin.roles";
    public const string AdminDocTypes  = "admin.doctypes";
    public const string AdminMetadata  = "admin.metadata";
    public const string AdminRetention = "admin.retention";
    public const string AdminSystem    = "admin.system";

    // Audit & Reports
    public const string AuditRead     = "audit.read";
    public const string AuditExport   = "audit.export";
    public const string ReportsView   = "reports.view";
    public const string ReportsExport = "reports.export";
}

public static class SystemRoles
{
    public const string SystemAdmin     = "SystemAdmin";
    public const string ContentManager  = "ContentManager";
    public const string DocumentManager = "DocumentManager";
    public const string WorkflowApprover= "WorkflowApprover";
    public const string RecordsManager  = "RecordsManager";
    public const string AuditReviewer   = "AuditReviewer";
    public const string BasicUser       = "BasicUser";
    public const string ReadOnly        = "ReadOnly";
}

public static class AuditEventTypes
{
    public const string UserLogin          = "UserLogin";
    public const string UserLogout         = "UserLogout";
    public const string LoginFailed        = "LoginFailed";
    public const string AccountLocked      = "AccountLocked";
    public const string DocumentCreated    = "DocumentCreated";
    public const string DocumentViewed     = "DocumentViewed";
    public const string DocumentUpdated    = "DocumentUpdated";
    public const string DocumentDeleted    = "DocumentDeleted";
    public const string DocumentDownloaded = "DocumentDownloaded";
    public const string DocumentCheckedOut = "DocumentCheckedOut";
    public const string DocumentCheckedIn  = "DocumentCheckedIn";
    public const string WorkflowSubmitted  = "WorkflowSubmitted";
    public const string WorkflowApproved   = "WorkflowApproved";
    public const string WorkflowRejected   = "WorkflowRejected";
    public const string SLABreached        = "SLABreached";
    public const string LegalHoldApplied   = "LegalHoldApplied";
    public const string RetentionExpired   = "RetentionExpired";
    public const string WorkspaceCreated   = "WorkspaceCreated";
    public const string WorkspaceLinked    = "WorkspaceLinkedToExternal";
    public const string WorkspaceArchived  = "WorkspaceArchived";
    public const string MetadataSynced     = "MetadataSynced";
    public const string SyncFailed         = "SyncFailed";
    public const string ConflictDetected   = "ConflictDetected";
    public const string ConflictResolved   = "ConflictResolved";
    public const string PermissionChanged  = "PermissionChanged";
}

public static class StorageProviders
{
    public const string LocalFileSystem = "LocalFileSystem";
    public const string AWSS3           = "AWSS3";
    public const string AzureBlob       = "AzureBlob";
    public const string MinIO           = "MinIO";
}

public static class ExternalSystemCodes
{
    public const string SAP          = "SAP_PROD";
    public const string Salesforce   = "SF_CRM";
    public const string OracleHR     = "HR_ORACLE";
}

public static class WorkspaceTypeCodes
{
    public const string Project    = "PROJECT";
    public const string Contract   = "CONTRACT";
    public const string Case       = "CASE";
    public const string Customer   = "CUSTOMER";
    public const string Employee   = "EMPLOYEE";
    public const string Department = "DEPARTMENT";
    public const string General    = "GENERAL";
}

// ============================================================
// EXTENSIONS
// ============================================================
namespace Darah.ECM.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string? s) => string.IsNullOrWhiteSpace(s);
    public static string TruncateAt(this string s, int maxLength)
        => s.Length <= maxLength ? s : s[..maxLength] + "...";
    public static string ToSlug(this string s)
        => s.Trim().ToLowerInvariant().Replace(' ', '-')
             .Replace("أ", "a").Replace("ب", "b"); // simplified; use Slugify package in production
}

public static class GuidExtensions
{
    public static bool IsEmpty(this Guid g) => g == Guid.Empty;
    public static string ToShortString(this Guid g) => g.ToString("N")[..8].ToUpper();
}

public static class DateExtensions
{
    public static string ToArabicDate(this DateTime dt)
        => dt.ToString("yyyy/MM/dd", new System.Globalization.CultureInfo("ar-SA"));
    public static string ToArabicDateTime(this DateTime dt)
        => dt.ToString("yyyy/MM/dd HH:mm", new System.Globalization.CultureInfo("ar-SA"));
    public static bool IsToday(this DateTime dt) => dt.Date == DateTime.Today;
    public static bool IsOverdue(this DateTime? dueDate) => dueDate.HasValue && dueDate.Value < DateTime.UtcNow;
}

public static class EnumerableExtensions
{
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
        => source ?? Enumerable.Empty<T>();

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
        => source is null || !source.Any();

    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        var batch = new List<T>(batchSize);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == batchSize) { yield return batch; batch = new List<T>(batchSize); }
        }
        if (batch.Any()) yield return batch;
    }
}

// ============================================================
// HELPERS
// ============================================================
namespace Darah.ECM.Shared.Helpers;

public static class FileHelper
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"]  = "text/plain",
        [".csv"]  = "text/csv",
        [".jpg"]  = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"]  = "image/png",
        [".tif"]  = "image/tiff",
        [".tiff"] = "image/tiff",
        [".mp4"]  = "video/mp4",
        [".mp3"]  = "audio/mpeg",
        [".zip"]  = "application/zip",
        [".msg"]  = "application/vnd.ms-outlook",
    };

    public static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return MimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    public static bool IsImageFile(string fileName)
        => new[] { ".jpg", ".jpeg", ".png", ".tif", ".tiff" }
            .Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public static bool IsPdfFile(string fileName)
        => Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid)).Trim('.');
    }
}

public static class SecurityHelper
{
    public static string GenerateRandomKey(int bytes = 32)
    {
        var data = new byte[bytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(data);
        return Convert.ToBase64String(data);
    }

    public static string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public static bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    public static bool IsStrongPassword(string password)
    {
        if (password.Length < 10) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsLower)) return false;
        if (!password.Any(char.IsDigit)) return false;
        if (!password.Any(c => !char.IsLetterOrDigit(c))) return false;
        return true;
    }
}
