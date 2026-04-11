using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.ValueObjects;

public sealed class FileMetadataTests
{
    [Fact] public void Create_AllowedExtension_Succeeds()
    { var fm = FileMetadata.Create("k", "doc.pdf", "application/pdf", 1024, "abc", "local"); Assert.Equal(".pdf", fm.FileExtension); }
    [Fact] public void Create_DisallowedExtension_Throws()
        => Assert.Throws<ArgumentException>(() => FileMetadata.Create("k", "mal.exe", "application/octet-stream", 100, "h", "local"));
    [Fact] public void Create_EmptyStorageKey_Throws()
        => Assert.Throws<ArgumentException>(() => FileMetadata.Create("", "doc.pdf", "application/pdf", 100, "h", "local"));
    [Fact] public void IsPdf_True_ForPdf() { var fm = FileMetadata.Create("k", "doc.pdf", "application/pdf", 1, "h", "l"); Assert.True(fm.IsPdf); }
    [Fact] public void IsImage_True_ForJpg() { var fm = FileMetadata.Create("k", "img.jpg", "image/jpeg", 1, "h", "l"); Assert.True(fm.IsImage); }
    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(2_621_440, "2.5 MB")]
    public void FriendlySize_FormatsCorrectly(long bytes, string expected)
    { var fm = FileMetadata.Create("k", "file.pdf", "application/pdf", bytes, "h", "l"); Assert.Equal(expected, fm.FriendlySize); }
}
