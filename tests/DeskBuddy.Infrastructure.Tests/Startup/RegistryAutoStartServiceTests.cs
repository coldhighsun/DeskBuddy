using System.Runtime.Versioning;
using DeskBuddy.Infrastructure.Startup;
using Microsoft.Win32;

namespace DeskBuddy.Infrastructure.Tests.Startup;

/// <summary>
/// Tests for <see cref="RegistryAutoStartService"/>, using throwaway keys under the current user instead of the real
/// <c>Run</c> key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAutoStartServiceTests : IDisposable
{
    /// <summary>
    /// A program path with a space in it, as installed programs often have.
    /// </summary>
    private const string ProgramPath = @"C:\Program Files\DeskBuddy\DeskBuddy.exe";

    /// <summary>
    /// The throwaway root key of this test instance.
    /// </summary>
    private readonly string _root = $@"Software\DeskBuddyTests\{Guid.NewGuid():N}";

    /// <summary>
    /// The service under test, working on the throwaway keys.
    /// </summary>
    private readonly RegistryAutoStartService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryAutoStartServiceTests"/> class.
    /// </summary>
    public RegistryAutoStartServiceTests()
    {
        _service = Create(ProgramPath);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\DeskBuddyTests", throwOnMissingSubKey: false);
    }

    /// <summary>
    /// Gets the path of the throwaway <c>Run</c> key.
    /// </summary>
    private string RunKey => $@"{_root}\Run";

    /// <summary>
    /// Gets the path of the throwaway Task Manager flag key.
    /// </summary>
    private string ApprovedKey => $@"{_root}\Approved";

    /// <summary>
    /// Creates a service for a program that works on the throwaway keys.
    /// </summary>
    /// <param name="program">
    /// The program to start at sign-in.
    /// </param>
    /// <returns>
    /// The service.
    /// </returns>
    private RegistryAutoStartService Create(string program)
    {
        return new RegistryAutoStartService(program, RunKey, ApprovedKey);
    }

    /// <summary>
    /// Writes the Task Manager flag for the application.
    /// </summary>
    /// <param name="firstByte">
    /// The first byte of the flag: even for enabled, odd for disabled.
    /// </param>
    private void WriteApprovedFlag(byte firstByte)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedKey, writable: true);
        var flag = new byte[12];
        flag[0] = firstByte;
        key.SetValue(RegistryAutoStartService.ValueName, flag, RegistryValueKind.Binary);
    }

    /// <summary>
    /// Reads the command stored for the application in the throwaway <c>Run</c> key.
    /// </summary>
    /// <returns>
    /// The stored command, or <see langword="null"/> if there is none.
    /// </returns>
    private string? ReadRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RegistryAutoStartService.ValueName) as string;
    }

    /// <summary>
    /// With nothing registered the application does not start at sign-in.
    /// </summary>
    [Fact]
    public void IsEnabled_NothingRegistered_ReturnsFalse()
    {
        // Assert
        _service.IsEnabled().Should().BeFalse();
    }

    /// <summary>
    /// Turning it on registers the quoted program path and reads back as enabled.
    /// </summary>
    [Fact]
    public void TrySet_Enable_RegistersQuotedPath()
    {
        // Act
        var done = _service.TrySet(true, out var error);

        // Assert
        done.Should().BeTrue();
        error.Should().BeNull();
        ReadRunValue().Should().Be($"\"{ProgramPath}\"");
        _service.IsEnabled().Should().BeTrue();
    }

    /// <summary>
    /// Turning it off removes the entry.
    /// </summary>
    [Fact]
    public void TrySet_Disable_RemovesEntry()
    {
        // Arrange
        _service.TrySet(true, out _);

        // Act
        var done = _service.TrySet(false, out var error);

        // Assert
        done.Should().BeTrue();
        error.Should().BeNull();
        ReadRunValue().Should().BeNull();
        _service.IsEnabled().Should().BeFalse();
    }

    /// <summary>
    /// Turning it off when it was never on is not an error.
    /// </summary>
    [Fact]
    public void TrySet_DisableWhenNotRegistered_Succeeds()
    {
        // Act
        var done = _service.TrySet(false, out var error);

        // Assert
        done.Should().BeTrue();
        error.Should().BeNull();
    }

    /// <summary>
    /// An entry that points to a different installation does not count as enabled for this one.
    /// </summary>
    [Fact]
    public void IsEnabled_EntryPointsElsewhere_ReturnsFalse()
    {
        // Arrange
        Create(@"D:\Old\DeskBuddy.exe").TrySet(true, out _);

        // Act
        var enabled = _service.IsEnabled();

        // Assert
        enabled.Should().BeFalse();
    }

    /// <summary>
    /// Turning it on replaces an entry that points elsewhere with this installation.
    /// </summary>
    [Fact]
    public void TrySet_EnableOverOldEntry_PointsToThisInstallation()
    {
        // Arrange
        Create(@"D:\Old\DeskBuddy.exe").TrySet(true, out _);

        // Act
        _service.TrySet(true, out _);

        // Assert
        _service.IsEnabled().Should().BeTrue();
        ReadRunValue().Should().Be($"\"{ProgramPath}\"");
    }

    /// <summary>
    /// An entry the user disabled in Task Manager counts as off.
    /// </summary>
    [Fact]
    public void IsEnabled_DisabledInTaskManager_ReturnsFalse()
    {
        // Arrange
        _service.TrySet(true, out _);
        WriteApprovedFlag(0x03);

        // Act
        var enabled = _service.IsEnabled();

        // Assert
        enabled.Should().BeFalse();
    }

    /// <summary>
    /// An entry Task Manager marks as enabled counts as on.
    /// </summary>
    [Fact]
    public void IsEnabled_ApprovedInTaskManager_ReturnsTrue()
    {
        // Arrange
        _service.TrySet(true, out _);
        WriteApprovedFlag(0x02);

        // Act
        var enabled = _service.IsEnabled();

        // Assert
        enabled.Should().BeTrue();
    }

    /// <summary>
    /// Turning it on again after the user disabled it in Task Manager clears the flag.
    /// </summary>
    [Fact]
    public void TrySet_EnableAfterDisabledInTaskManager_ClearsFlag()
    {
        // Arrange
        _service.TrySet(true, out _);
        WriteApprovedFlag(0x03);

        // Act
        _service.TrySet(true, out _);

        // Assert
        _service.IsEnabled().Should().BeTrue();
    }
}
