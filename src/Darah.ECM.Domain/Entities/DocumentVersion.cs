using Darah.ECM.Domain.Common;
using Darah.ECM.Domain.ValueObjects;

namespace Darah.ECM.Domain.Entities;

/// <summary>
/// Represents one immutable snapshot of a document's file content.
/// A new version is created on each successful check-in.
/// Previous versions have IsCurrent = false but are never deleted.
/// </summary>
public class DocumentVersion : BaseEntity
{
    public int    VersionId     { get; private set; }   // DB-generated identity — 0 before first save
    public Guid   DocumentId    { get; private set; }
    public string VersionNumber { get; private set; } = string.Empty;  // e.g. "1.0", "1.1", "2.0"
    public int    MajorVersion  { get; private set; }
    public int    MinorVersion  { get; private set; }

    /// <summary>
    /// The associated physical file. All file attributes are stored here;
    /// the Document entity does not duplicate them.
    /// </summary>
    public FileMetadata File { get; private set; } = null!;

    public string? ChangeNote   { get; private set; }
    public string? CheckInNote  { get; private set; }

    /// <summary>
    /// True for the most recent version. Set to false when a newer version is created.
    /// Exactly one version per document should have IsCurrent = true.
    /// </summary>
    public bool IsCurrent { get; private set; } = true;

    private DocumentVersion() { }

    public static DocumentVersion Create(
        Guid documentId,
        string versionNumber,
        int majorVersion,
        int minorVersion,
        FileMetadata file,
        int createdBy,
        string? changeNote  = null,
        string? checkInNote = null)
    {
        var v = new DocumentVersion
        {
            DocumentId    = documentId,
            VersionNumber = versionNumber,
            MajorVersion  = majorVersion,
            MinorVersion  = minorVersion,
            File          = file,
            ChangeNote    = changeNote,
            CheckInNote   = checkInNote,
            IsCurrent     = true
        };
        v.SetCreated(createdBy);
        return v;
    }

    /// <summary>
    /// Called when a newer version is saved.
    /// Older versions become read-only history and can never be made current again.
    /// </summary>
    public void MarkSuperseded() => IsCurrent = false;
}
