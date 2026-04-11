using Darah.ECM.Domain.ValueObjects;
using Xunit;

namespace Darah.ECM.UnitTests.Domain.ValueObjects;

public sealed class ClassificationLevelTests
{
    [Fact] public void FromOrder_1_ReturnsPublic() => Assert.Equal(ClassificationLevel.Public, ClassificationLevel.FromOrder(1));
    [Fact] public void FromOrder_4_ReturnsSecret() => Assert.Equal(ClassificationLevel.Secret, ClassificationLevel.FromOrder(4));
    [Fact] public void FromOrder_InvalidOrder_Throws() => Assert.Throws<ArgumentException>(() => ClassificationLevel.FromOrder(99));
    [Fact] public void From_ValidCode_Works() => Assert.Equal(ClassificationLevel.Confidential, ClassificationLevel.From("CONFIDENTIAL"));
    [Fact] public void Secret_NoDownload_RequiresWatermark() { Assert.False(ClassificationLevel.Secret.AllowDownload); Assert.True(ClassificationLevel.Secret.RequireWatermark); }
    [Fact] public void Public_AllowsDownload_NoWatermark() { Assert.True(ClassificationLevel.Public.AllowDownload); Assert.False(ClassificationLevel.Public.RequireWatermark); }
    [Fact] public void Confidential_RequiresWatermark() => Assert.True(ClassificationLevel.Confidential.RequireWatermark);
    [Fact] public void Secret_IsMoreRestrictive_ThanInternal() => Assert.True(ClassificationLevel.Secret.IsMoreRestrictiveThan(ClassificationLevel.Internal));
    [Fact] public void Public_IsNotMoreRestrictive_ThanInternal() => Assert.False(ClassificationLevel.Public.IsMoreRestrictiveThan(ClassificationLevel.Internal));
}
