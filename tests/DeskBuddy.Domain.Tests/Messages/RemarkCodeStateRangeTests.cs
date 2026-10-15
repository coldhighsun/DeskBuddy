using DeskBuddy.Domain.Messages;

namespace DeskBuddy.Domain.Tests.Messages;

/// <summary>
/// Guards the assumption of <see cref="RemarkCode"/> that every state fits below its factor.
/// </summary>
public class RemarkCodeStateRangeTests
{
    /// <summary>
    /// Every state survives packing together with a variant.
    /// </summary>
    [Fact]
    public void EncodeDecode_EveryState_ComesBackWithItsVariant()
    {
        // Act
        var broken = Enum.GetValues<PetState>()
            .Where(state => RemarkCode.Decode(RemarkCode.Encode(state, 3)) != (state, 3))
            .ToList();

        // Assert
        broken.Should().BeEmpty("RemarkCode keeps the state in the lowest two digits");
    }
}
