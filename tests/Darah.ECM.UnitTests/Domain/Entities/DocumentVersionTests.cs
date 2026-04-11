using Darah.ECM.Domain.Entities;
using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.Entities;

public sealed class DocumentVersionTests
{
    private static FileMetadata MakeFile(string name = "report.pdf") =>
        FileMetadata.Create("2026/04/11/abc.pdf", name, "application/pdf", 1024, "sha256hash", "local");

    [Fact]
    public void Create_SetsAllFieldsCorrectly()
    {
        var docId = Guid.NewGuid();
        var file  = MakeFile();
        var v     = DocumentVersion.Create(docId, "1.0", 1, 0, file, 7, "Initial upload");

        Assert.Equal(docId, v.DocumentId);
        Assert.Equal("1.0", v.VersionNumber);
        Assert.Equal(1, v.MajorVersion);
        Assert.Equal(0, v.MinorVersion);
        Assert.True(v.IsCurrent);
        Assert.Equal("Initial upload", v.ChangeNote);
        Assert.Equal(7, v.CreatedBy);
    }

    [Fact]
    public void Create_IsCurrent_DefaultTrue()
        => Assert.True(DocumentVersion.Create(Guid.NewGuid(), "1.0", 1, 0, MakeFile(), 1).IsCurrent);

    [Fact]
    public void MarkSuperseded_SetsIsCurrentFalse()
    {
        var v = DocumentVersion.Create(Guid.NewGuid(), "1.0", 1, 0, MakeFile(), 1);
        v.MarkSuperseded();
        Assert.False(v.IsCurrent);
    }

    [Fact]
    public void FileMetadata_IsEmbedded_InVersion()
    {
        var file = MakeFile("contract.pdf");
        var v    = DocumentVersion.Create(Guid.NewGuid(), "2.1", 2, 1, file, 1);
        Assert.Equal("2026/04/11/abc.pdf", v.File.StorageKey);
        Assert.Equal(".pdf", v.File.FileExtension);
        Assert.Equal(1024, v.File.FileSizeBytes);
        Assert.Equal("sha256hash", v.File.ContentHash);
    }

    [Fact]
    public void Create_VersionNumbers_StoreCorrectly()
    {
        var v = DocumentVersion.Create(Guid.NewGuid(), "3.2", 3, 2, MakeFile(), 1);
        Assert.Equal(3, v.MajorVersion);
        Assert.Equal(2, v.MinorVersion);
    }
}
