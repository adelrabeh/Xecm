using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.ValueObjects;

public sealed class RetentionPeriodTests
{
    [Fact] public void ComputeExpiry_5Years_AddsCorrectly()
    { var r = new RetentionPeriod(5, "CreationDate"); Assert.Equal(new DateOnly(2025, 1, 1), r.ComputeExpiry(new DateOnly(2020, 1, 1))); }
    [Fact] public void IsExpired_OldDate_ReturnsTrue()
    { var r = new RetentionPeriod(1, "CreationDate"); Assert.True(r.IsExpired(new DateOnly(2000, 1, 1))); }
    [Fact] public void IsExpired_FutureDate_ReturnsFalse()
    { var r = new RetentionPeriod(100, "CreationDate"); Assert.False(r.IsExpired(new DateOnly(2020, 1, 1))); }
    [Fact] public void Permanent_NeverExpires()
    { var r = RetentionPeriod.Permanent; Assert.Equal(DateOnly.MaxValue, r.ComputeExpiry(DateOnly.FromDateTime(DateTime.UtcNow))); Assert.False(r.IsExpired(new DateOnly(1900, 1, 1))); }
    [Fact] public void NegativeYears_Throws() => Assert.Throws<ArgumentException>(() => new RetentionPeriod(-1, "CreationDate"));
    [Fact] public void Equality_SameValues_AreEqual() => Assert.Equal(new RetentionPeriod(5, "CreationDate"), new RetentionPeriod(5, "CreationDate"));
    [Fact] public void Equality_DifferentValues_NotEqual() => Assert.NotEqual(new RetentionPeriod(5, "CreationDate"), new RetentionPeriod(10, "CreationDate"));
}
