namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="AlphaMask"/>.
/// </summary>
public class AlphaMaskTests
{
    /// <summary>
    /// Creates a 2x2 mask where only the top-left pixel is opaque.
    /// </summary>
    /// <returns>The test mask.</returns>
    private static AlphaMask CreateMask()
    {
        return new AlphaMask(2, 2, [255, 0, 0, 0]);
    }

    /// <summary>
    /// The sprite's top is the first row that has an opaque pixel, as a fraction of the frame height.
    /// </summary>
    [Fact]
    public void TopOpaqueFraction_EmptyRowsAbove_ReturnsFirstOpaqueRow()
    {
        // Arrange
        var mask = new AlphaMask(2, 4, [0, 0, 0, 0, 0, 255, 255, 255]);

        // Act
        var top = mask.TopOpaqueFraction();

        // Assert
        top.Should().Be(0.5);
    }

    /// <summary>
    /// A sprite that touches the top edge starts at zero.
    /// </summary>
    [Fact]
    public void TopOpaqueFraction_OpaqueFirstRow_ReturnsZero()
    {
        // Act
        var top = CreateMask().TopOpaqueFraction();

        // Assert
        top.Should().Be(0);
    }

    /// <summary>
    /// A frame with nothing opaque has no top, so it counts as zero.
    /// </summary>
    [Fact]
    public void TopOpaqueFraction_FullyTransparent_ReturnsZero()
    {
        // Arrange
        var mask = new AlphaMask(2, 2, [0, 0, 0, 0]);

        // Act
        var top = mask.TopOpaqueFraction();

        // Assert
        top.Should().Be(0);
    }

    /// <summary>
    /// The sprite's bottom is the end of the last row that has an opaque pixel, as a fraction of the frame height.
    /// </summary>
    [Fact]
    public void BottomOpaqueFraction_EmptyRowsBelow_ReturnsEndOfLastOpaqueRow()
    {
        // Arrange
        var mask = new AlphaMask(2, 4, [0, 0, 0, 255, 0, 0, 0, 0]);

        // Act
        var bottom = mask.BottomOpaqueFraction();

        // Assert
        bottom.Should().Be(0.5);
    }

    /// <summary>
    /// A sprite that touches the bottom edge ends at one.
    /// </summary>
    [Fact]
    public void BottomOpaqueFraction_OpaqueLastRow_ReturnsOne()
    {
        // Arrange
        var mask = new AlphaMask(2, 2, [0, 0, 0, 255]);

        // Act
        var bottom = mask.BottomOpaqueFraction();

        // Assert
        bottom.Should().Be(1);
    }

    /// <summary>
    /// A frame with nothing opaque has no bottom, so it counts as one.
    /// </summary>
    [Fact]
    public void BottomOpaqueFraction_FullyTransparent_ReturnsOne()
    {
        // Arrange
        var mask = new AlphaMask(2, 2, [0, 0, 0, 0]);

        // Act
        var bottom = mask.BottomOpaqueFraction();

        // Assert
        bottom.Should().Be(1);
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

    /// <summary>
    /// A larger image is reduced to a mask no longer than the limit, and a single opaque pixel is not lost in the
    /// reduction.
    /// </summary>
    [Fact]
    public void FromBgra32_LargerThanMaxEdge_ReducesButKeepsASinglePixel()
    {
        // Arrange: 8x8 image with one opaque pixel at (5, 2).
        var pixels = new byte[8 * 8 * 4];
        pixels[(((2 * 8) + 5) * 4) + 3] = 255;

        // Act
        var mask = AlphaMask.FromBgra32(8, 8, pixels, stride: 8 * 4, maxEdge: 4);

        // Assert
        mask.Width.Should().Be(4);
        mask.Height.Should().Be(4);
        mask.IsOpaqueAtNormalized(5.5 / 8, 2.5 / 8).Should().BeTrue();
        mask.IsOpaqueAtNormalized(0.1, 0.1).Should().BeFalse();
    }

    /// <summary>
    /// The reduction keeps the proportions of an image that is not square, and rounds up so no edge pixel is dropped.
    /// </summary>
    [Fact]
    public void FromBgra32_NonSquareNotDivisible_KeepsProportionsAndEdges()
    {
        // Arrange: 9x5 image, only the last pixel (8, 4) opaque; the limit 4 makes blocks of 3.
        var pixels = new byte[9 * 5 * 4];
        pixels[(((4 * 9) + 8) * 4) + 3] = 255;

        // Act
        var mask = AlphaMask.FromBgra32(9, 5, pixels, stride: 9 * 4, maxEdge: 4);

        // Assert
        mask.Width.Should().Be(3);
        mask.Height.Should().Be(2);
        mask.IsOpaqueAtNormalized(0.99, 0.99).Should().BeTrue();
    }

    /// <summary>
    /// An image that is within the limit is not reduced.
    /// </summary>
    [Fact]
    public void FromBgra32_WithinMaxEdge_KeepsResolution()
    {
        // Arrange
        byte[] pixels = [0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255];

        // Act
        var mask = AlphaMask.FromBgra32(2, 2, pixels, stride: 8, maxEdge: 64);

        // Assert
        mask.Width.Should().Be(2);
        mask.IsOpaque(1, 0).Should().BeFalse();
        mask.IsOpaque(1, 1).Should().BeTrue();
    }

    /// <summary>
    /// The top and bottom of the sprite are the same fractions after a reduction whose blocks line up with the sprite.
    /// </summary>
    [Fact]
    public void FromBgra32_Reduced_KeepsTopAndBottomFractions()
    {
        // Arrange: 8x8 image, opaque rows 2..5.
        var pixels = new byte[8 * 8 * 4];
        for (var y = 2; y <= 5; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[(((y * 8) + x) * 4) + 3] = 255;
            }
        }

        // Act
        var full = AlphaMask.FromBgra32(8, 8, pixels, stride: 8 * 4);
        var reduced = AlphaMask.FromBgra32(8, 8, pixels, stride: 8 * 4, maxEdge: 4);

        // Assert
        reduced.TopOpaqueFraction().Should().Be(full.TopOpaqueFraction());
        reduced.BottomOpaqueFraction().Should().Be(full.BottomOpaqueFraction());
    }

    /// <summary>
    /// A limit that is not a size is a programming error.
    /// </summary>
    [Fact]
    public void FromBgra32_NonPositiveMaxEdge_Throws()
    {
        // Act
        var act = () => AlphaMask.FromBgra32(2, 2, new byte[16], stride: 8, maxEdge: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
