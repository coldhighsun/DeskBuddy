namespace DeskBuddy.Application.Tests.Settings;

/// <summary>
/// Tests for <see cref="SettingsEditor"/>.
/// </summary>
public class SettingsEditorTests : IDisposable
{
    /// <summary>
    /// The fake storage the service saves to.
    /// </summary>
    private readonly FakeSettingsStore _store = new();

    /// <summary>
    /// The saved settings the editor works on.
    /// </summary>
    private readonly SettingsService _service;

    /// <summary>
    /// The editor under test.
    /// </summary>
    private readonly SettingsEditor _editor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsEditorTests"/> class.
    /// </summary>
    public SettingsEditorTests()
    {
        _service = new SettingsService(_store);
        _service.InitializeAsync().GetAwaiter().GetResult();
        _editor = new SettingsEditor(_service);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _editor.Dispose();
    }

    /// <summary>
    /// A new editor shows the saved settings and has nothing to save.
    /// </summary>
    [Fact]
    public void Constructor_Default_IsCleanAndShowsSavedSettings()
    {
        // Assert
        _editor.Draft.Should().Be(_service.Current);
        _editor.IsDirty.Should().BeFalse();
        _editor.CanSave.Should().BeFalse();
    }

    /// <summary>
    /// An edit changes the draft but leaves the saved settings alone.
    /// </summary>
    [Fact]
    public void Set_Edit_ChangesDraftOnly()
    {
        // Act
        _editor.Set("climb", s => s with { ClimbEdges = true });

        // Assert
        _editor.Draft.ClimbEdges.Should().BeTrue();
        _service.Current.ClimbEdges.Should().BeFalse();
        _editor.IsDirty.Should().BeTrue();
        _editor.CanSave.Should().BeTrue();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Editing the same field again replaces the earlier change instead of adding to it.
    /// </summary>
    [Fact]
    public void Set_SameKeyTwice_KeepsTheLastValue()
    {
        // Act
        _editor.Set("sleep", s => s with { SleepAfter = TimeSpan.FromMinutes(7) });
        _editor.Set("sleep", s => s with { SleepAfter = TimeSpan.FromMinutes(9) });

        // Assert
        _editor.Draft.SleepAfter.Should().Be(TimeSpan.FromMinutes(9));
    }

    /// <summary>
    /// Changing a value and changing it back is not an unsaved change.
    /// </summary>
    [Fact]
    public void Set_BackToSavedValue_IsNotDirty()
    {
        // Arrange
        var saved = _service.Current.ChatProcesses;

        // Act
        _editor.Set("chat", s => s with { ChatProcesses = ["other"] });
        _editor.Set("chat", s => s with { ChatProcesses = [.. saved] });

        // Assert
        _editor.IsDirty.Should().BeFalse();
    }

    /// <summary>
    /// Clearing the edit of one field leaves the other edits.
    /// </summary>
    [Fact]
    public void Clear_OneEdit_KeepsTheOthers()
    {
        // Arrange
        _editor.Set("climb", s => s with { ClimbEdges = true });
        _editor.Set("chatter", s => s with { ChatterEnabled = true });

        // Act
        _editor.Clear("climb");

        // Assert
        _editor.Draft.ClimbEdges.Should().BeFalse();
        _editor.Draft.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Reverting drops every edit and error.
    /// </summary>
    [Fact]
    public void Revert_AfterEdits_ReturnsToSavedSettings()
    {
        // Arrange
        _editor.Set("climb", s => s with { ClimbEdges = true });
        _editor.SetError("sleep", "bad");

        // Act
        _editor.Revert();

        // Assert
        _editor.Draft.Should().Be(_service.Current);
        _editor.IsDirty.Should().BeFalse();
        _editor.HasErrors.Should().BeFalse();
    }

    /// <summary>
    /// A field with an error blocks saving until the error is gone.
    /// </summary>
    [Fact]
    public void SetError_WhileDirty_BlocksSaving()
    {
        // Arrange
        _editor.Set("climb", s => s with { ClimbEdges = true });

        // Act
        _editor.SetError("sleep", "Enter a number of minutes.");

        // Assert
        _editor.CanSave.Should().BeFalse();
        _editor.ErrorFor("sleep").Should().Be("Enter a number of minutes.");

        _editor.SetError("sleep", null);
        _editor.CanSave.Should().BeTrue();
        _editor.ErrorFor("sleep").Should().BeEmpty();
    }

    /// <summary>
    /// Saving puts the draft into effect and writes it.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEdits_AppliesAndStores()
    {
        // Arrange
        _editor.Set("climb", s => s with { ClimbEdges = true });

        // Act
        var saved = await _editor.SaveAsync();

        // Assert
        saved.Should().BeTrue();
        _service.Current.ClimbEdges.Should().BeTrue();
        _store.Stored!.ClimbEdges.Should().BeTrue();
        _editor.IsDirty.Should().BeFalse();
        _editor.SaveFailure.Should().BeNull();
    }

    /// <summary>
    /// Saving with an invalid field does nothing.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithError_DoesNothing()
    {
        // Arrange
        _editor.Set("climb", s => s with { ClimbEdges = true });
        _editor.SetError("sleep", "bad");

        // Act
        var saved = await _editor.SaveAsync();

        // Assert
        saved.Should().BeFalse();
        _service.Current.ClimbEdges.Should().BeFalse();
        _store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// When writing fails the settings stay in effect in memory, the failure is kept and the edits are done with.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WriteFails_KeepsFailureAndAppliesInMemory()
    {
        // Arrange
        _store.SaveException = new IOException("disk full");
        _editor.Set("climb", s => s with { ClimbEdges = true });

        // Act
        var saved = await _editor.SaveAsync();

        // Assert
        saved.Should().BeFalse();
        _editor.SaveFailure.Should().BeOfType<IOException>();
        _service.Current.ClimbEdges.Should().BeTrue();
        _editor.IsDirty.Should().BeFalse();
    }

    /// <summary>
    /// A change of the saved settings elsewhere reaches the fields the user did not edit and leaves the edited ones.
    /// </summary>
    [Fact]
    public async Task Draft_SavedSettingsChangeElsewhere_FollowsUntouchedFieldsOnly()
    {
        // Arrange
        _editor.Set("size", s => s with { Size = PetSize.Large });

        // Act
        await _service.UpdateAsync(s => s with { Size = PetSize.Small, ClimbEdges = true });

        // Assert
        _editor.Draft.ClimbEdges.Should().BeTrue();
        _editor.Draft.Size.Should().Be(PetSize.Large);
        _editor.IsDirty.Should().BeTrue();
    }

    /// <summary>
    /// A change elsewhere is kept when the edits are saved.
    /// </summary>
    [Fact]
    public async Task SaveAsync_SavedSettingsChangedMeanwhile_KeepsTheOtherChange()
    {
        // Arrange
        _editor.Set("chatter", s => s with { ChatterEnabled = true });
        await _service.UpdateAsync(s => s with { ClimbEdges = true });

        // Act
        await _editor.SaveAsync();

        // Assert
        _service.Current.ClimbEdges.Should().BeTrue();
        _service.Current.ChatterEnabled.Should().BeTrue();
    }

    /// <summary>
    /// An edit made while a save is running is not lost: only the edits that were being saved are dropped.
    /// </summary>
    [Fact]
    public async Task SaveAsync_EditMadeWhileSaving_StaysInTheDraft()
    {
        // Arrange
        var release = new TaskCompletionSource();
        _store.SaveBlocker = release.Task;
        _editor.Set("climb", s => s with { ClimbEdges = true });

        // Act
        var saving = _editor.SaveAsync();
        _editor.Set("chatter", s => s with { ChatterEnabled = true });
        release.SetResult();
        await saving;

        // Assert
        _service.Current.ClimbEdges.Should().BeTrue();
        _service.Current.ChatterEnabled.Should().BeFalse();
        _editor.Draft.ChatterEnabled.Should().BeTrue();
        _editor.IsDirty.Should().BeTrue();
    }

    /// <summary>
    /// Settings that could not be read at startup are never overwritten.
    /// </summary>
    [Fact]
    public async Task CanSave_SettingsFailedToLoad_IsFalse()
    {
        // Arrange
        var store = new FakeSettingsStore { LoadException = new IOException("unreadable") };
        var service = new SettingsService(store);
        await service.InitializeAsync();
        using var editor = new SettingsEditor(service);

        // Act
        editor.Set("climb", s => s with { ClimbEdges = true });

        // Assert
        editor.CanSave.Should().BeFalse();
        (await editor.SaveAsync()).Should().BeFalse();
        store.SaveCount.Should().Be(0);
    }

    /// <summary>
    /// Listeners hear about edits, errors and saves.
    /// </summary>
    [Fact]
    public async Task Changed_EditErrorAndSave_AreRaised()
    {
        // Arrange
        var raised = 0;
        _editor.Changed += (_, _) => raised++;

        // Act
        _editor.Set("climb", s => s with { ClimbEdges = true });
        var afterEdit = raised;
        _editor.SetError("sleep", "bad");
        var afterError = raised;
        _editor.SetError("sleep", null);
        await _editor.SaveAsync();

        // Assert
        afterEdit.Should().BeGreaterThan(0);
        afterError.Should().BeGreaterThan(afterEdit);
        raised.Should().BeGreaterThan(afterError);
    }

    /// <summary>
    /// In-memory settings store with failure hooks.
    /// </summary>
    private sealed class FakeSettingsStore : ISettingsStore
    {
        /// <summary>
        /// Gets the stored settings.
        /// </summary>
        public PetSettings? Stored { get; private set; }

        /// <summary>
        /// Gets how many times <see cref="SaveAsync"/> was called.
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Gets or sets an exception to throw from <see cref="LoadAsync"/>.
        /// </summary>
        public Exception? LoadException { get; set; }

        /// <summary>
        /// Gets or sets an exception to throw from <see cref="SaveAsync"/>.
        /// </summary>
        public Exception? SaveException { get; set; }

        /// <summary>
        /// Gets or sets a task <see cref="SaveAsync"/> waits for before it finishes.
        /// </summary>
        public Task? SaveBlocker { get; set; }

        /// <inheritdoc />
        public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return LoadException is null
                ? Task.FromResult(Stored ?? PetSettings.Default)
                : Task.FromException<PetSettings>(LoadException);
        }

        /// <inheritdoc />
        public async Task SaveAsync(PetSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (SaveBlocker is not null)
            {
                await SaveBlocker;
            }

            if (SaveException is not null)
            {
                throw SaveException;
            }

            Stored = settings;
        }
    }
}
