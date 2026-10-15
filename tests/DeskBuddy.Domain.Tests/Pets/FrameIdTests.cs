namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="FrameId"/>.
/// </summary>
public class FrameIdTests
{
    /// <summary>
    /// The name of a picture that is an area of a file splits back into the path and the area.
    /// </summary>
    [Fact]
    public void Parse_CreatedName_GivesPathAndAreaBack()
    {
        // Arrange
        var id = FrameId.Create(@"C:\pets\cat\atlas.png", 128, 256, 128, 128);

        // Act
        var (path, area) = FrameId.Parse(id);

        // Assert
        path.Should().Be(@"C:\pets\cat\atlas.png");
        area.Should().Be((128, 256, 128, 128));
    }

    /// <summary>
    /// A path that contains the separator is not mistaken for an area, and an area after it is still found.
    /// </summary>
    [Fact]
    public void Parse_PathWithSeparator_KeepsItInThePath()
    {
        // Arrange
        var id = FrameId.Create(@"C:\C#\atlas.png", 0, 0, 64, 64);

        // Act
        var (path, area) = FrameId.Parse(id);

        // Assert
        path.Should().Be(@"C:\C#\atlas.png");
        area.Should().Be((0, 0, 64, 64));
    }

    /// <summary>
    /// A name that is only a path stands for the whole file.
    /// </summary>
    /// <param name="id">
    /// The name.
    /// </param>
    [Theory]
    [InlineData(@"C:\pets\cat\idle.png")]
    [InlineData(@"C:\C#\idle.png")]
    [InlineData(@"C:\pets#1,2,3\idle.png")]
    [InlineData("atlas.png#1,2,x,4")]
    [InlineData("atlas.png#-1,2,3,4")]
    public void Parse_PathWithoutArea_GivesNoArea(string id)
    {
        // Act
        var (path, area) = FrameId.Parse(id);

        // Assert
        path.Should().Be(id);
        area.Should().BeNull();
    }
}
