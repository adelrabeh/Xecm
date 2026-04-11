using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.ValueObjects;

public sealed class DocumentStatusTests
{
    [Fact] public void From_ValidCode_ReturnsInstance() => Assert.Equal(DocumentStatus.Draft, DocumentStatus.From("DRAFT"));
    [Fact] public void From_CaseInsensitive_Works() => Assert.Equal(DocumentStatus.Active, DocumentStatus.From("active"));
    [Fact] public void From_InvalidCode_Throws() => Assert.Throws<ArgumentException>(() => DocumentStatus.From("INVALID"));
    [Fact] public void ImplicitConversion_ToStringWorks() { string s = DocumentStatus.Approved; Assert.Equal("APPROVED", s); }
    [Theory]
    [InlineData("DRAFT",    "ACTIVE",    true)]
    [InlineData("DRAFT",    "PENDING",   true)]
    [InlineData("DRAFT",    "ARCHIVED",  true)]
    [InlineData("DRAFT",    "APPROVED",  false)]
    [InlineData("DRAFT",    "DISPOSED",  false)]
    [InlineData("PENDING",  "APPROVED",  true)]
    [InlineData("PENDING",  "REJECTED",  true)]
    [InlineData("APPROVED", "ARCHIVED",  true)]
    [InlineData("APPROVED", "DRAFT",     false)]
    [InlineData("DISPOSED", "ACTIVE",    false)]
    [InlineData("ARCHIVED", "DISPOSED",  true)]
    public void CanTransitionTo_Matrix(string from, string to, bool expected)
        => Assert.Equal(expected, DocumentStatus.From(from).CanTransitionTo(DocumentStatus.From(to)));
    [Fact] public void Equality_SameCode_AreEqual() => Assert.Equal(DocumentStatus.Draft, DocumentStatus.From("DRAFT"));
    [Fact] public void Equality_DifferentCode_NotEqual() => Assert.NotEqual(DocumentStatus.Draft, DocumentStatus.Active);
}
