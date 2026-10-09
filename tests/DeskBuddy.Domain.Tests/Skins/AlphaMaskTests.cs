using AwesomeAssertions;
using DeskBuddy.Domain.Skins;

namespace DeskBuddy.Domain.Tests.Skins;

/// <summary>
/// Tests for <see cref="AlphaMask"/>.
/// </summary>
public class AlphaMaskTests
{
    /// <summary>
    /// Creates a 2x2 mask where only the top-left pixel is opaque.
    /// </summary>
    /// <returns>
    /// The test mask.
    /// </returns>
    private static AlphaMask CreateMask()
    {
        return new AlphaMask(2, 2, [255, 0, 0, 0]);
    }

    /// <summary>
    /// An alpha buffer of the wrong size is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WrongAlphaLength_Throws()
    {
        // Act
        var act = () => new AlphaMask(2, 2, [255, 0, 0]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Opaque pixels hit; transparent ones do not.
    /// </summary>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, false)]
    public void IsOpaque_InsideMask_ReflectsAlpha(int x, int y, bool expected)
    {
        // Arrange
        var mask = CreateMask();

        // Act
        var result = mask.IsOpaque(x, y);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Coordinates outside the mask never hit.
    /// </summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void IsOpaque_OutsideMask_ReturnsFalse(int x, int y)
    {
        // Arrange
        var mask = CreateMask();

        // Act
        var result = mask.IsOpaque(x, y);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Alpha below the threshold counts as transparent, at the threshold as opaque.
    /// </summary>
    [Fact]
    public void IsOpaque_AlphaAroundThreshold_UsesInclusiveThreshold()
    {
        // Arrange
        var mask = new AlphaMask(2, 1, [15, 16]);

        // Act & Assert
        mask.IsOpaque(0, 0).Should().BeFalse();
        mask.IsOpaque(1, 0).Should().BeTrue();
    }

    /// <summary>
    /// Normalized positions map onto pixels regardless of the mask resolution.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, true)]
    [InlineData(0.49, 0.49, true)]
    [InlineData(0.5, 0.0, false)]
    [InlineData(0.99, 0.99, false)]
    public void IsOpaqueAtNormalized_InsideFrame_MapsToPixel(double u, double v, bool expected)
    {
        // Arrange
        var mask = CreateMask();

        // Act
        var result = mask.IsOpaqueAtNormalized(u, v);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Positions outside the frame, including the exclusive far edge, never hit.
    /// </summary>
    [Theory]
    [InlineData(-0.01, 0.0)]
    [InlineData(0.0, -0.01)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    public void IsOpaqueAtNormalized_OutsideFrame_ReturnsFalse(double u, double v)
    {
        // Arrange
        var mask = CreateMask();

        // Act
        var result = mask.IsOpaqueAtNormalized(u, v);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// The BGRA converter reads the alpha byte and honors a stride larger than the row width.
    /// </summary>
    [Fact]
    public void FromBgra32_WithPaddedStride_ReadsAlphaChannel()
    {
        // Arrange: 2x2 image, stride 12 (8 bytes of pixels + 4 bytes padding per row).
        byte[] pixels =
        [
            10, 10, 10, 255, 10, 10, 10, 0, 99, 99, 99, 99,
            10, 10, 10, 0, 10, 10, 10, 200, 99, 99, 99, 99,
        ];

        // Act
        var mask = AlphaMask.FromBgra32(2, 2, pixels, stride: 12);

        // Assert
        mask.IsOpaque(0, 0).Should().BeTrue();
        mask.IsOpaque(1, 0).Should().BeFalse();
        mask.IsOpaque(0, 1).Should().BeFalse();
        mask.IsOpaque(1, 1).Should().BeTrue();
    }
}
