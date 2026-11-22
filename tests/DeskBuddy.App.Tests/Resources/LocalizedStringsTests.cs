using System.Globalization;
using DeskBuddy.App.Resources;

namespace DeskBuddy.App.Tests.Resources;

/// <summary>
/// Tests for <see cref="LocalizedStrings"/> and <see cref="Loc"/>.
/// </summary>
[Collection("InterfaceLanguage")]
public class LocalizedStringsTests : IDisposable
{
    /// <summary>
    /// The interface culture before the test, put back afterwards.
    /// </summary>
    private readonly CultureInfo _original = CultureInfo.CurrentUICulture;

    /// <inheritdoc />
    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _original;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The text of a key is read in the interface language in use when it is asked for, not when the instance was made.
    /// </summary>
    [Fact]
    public void Indexer_CultureChanged_ReturnsTextInTheNewLanguage()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var english = LocalizedStrings.Instance[nameof(Strings.TraySettings)];

        // Act
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
        var chinese = LocalizedStrings.Instance[nameof(Strings.TraySettings)];

        // Assert
        english.Should().NotBe(chinese);
        chinese.Should().Be(Strings.TraySettings);
    }

    /// <summary>
    /// A key without a text shows as the key between exclamation marks, as <see cref="Strings"/> does.
    /// </summary>
    [Fact]
    public void Indexer_UnknownKey_ShowsTheKeyBetweenExclamationMarks()
    {
        // Act
        var text = LocalizedStrings.Instance["NoSuchKey"];

        // Assert
        text.Should().Be("!NoSuchKey!");
    }

    /// <summary>
    /// Telling that the language changed makes the bindings read again (the indexer changed) and informs the listeners.
    /// </summary>
    [Fact]
    public void NotifyLanguageChanged_Called_RaisesBothEvents()
    {
        // Arrange
        var changedProperties = new List<string?>();
        var languageChanges = 0;
        void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => changedProperties.Add(e.PropertyName);
        void OnLanguageChanged(object? sender, EventArgs e) => languageChanges++;
        LocalizedStrings.Instance.PropertyChanged += OnPropertyChanged;
        LocalizedStrings.Instance.LanguageChanged += OnLanguageChanged;
        try
        {
            // Act
            LocalizedStrings.Instance.NotifyLanguageChanged();
        }
        finally
        {
            LocalizedStrings.Instance.PropertyChanged -= OnPropertyChanged;
            LocalizedStrings.Instance.LanguageChanged -= OnLanguageChanged;
        }

        // Assert
        changedProperties.Should().Contain("Item[]");
        languageChanges.Should().Be(1);
    }

    /// <summary>
    /// A listener that was removed is not called any more.
    /// </summary>
    [Fact]
    public void LanguageChanged_ListenerRemoved_IsNotCalled()
    {
        // Arrange
        var listener = new Listener();
        LocalizedStrings.Instance.LanguageChanged += listener.Handle;
        LocalizedStrings.Instance.LanguageChanged -= listener.Handle;

        // Act
        LocalizedStrings.Instance.NotifyLanguageChanged();

        // Assert
        listener.Calls.Should().Be(0);
    }

    /// <summary>
    /// A listener that nobody else refers to is not kept alive by the event, so one that forgot to remove itself does not
    /// leak.
    /// </summary>
    [Fact]
    public void LanguageChanged_ListenerNotReferenced_IsCollected()
    {
        // Arrange
        var reference = Subscribe();

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        LocalizedStrings.Instance.NotifyLanguageChanged();

        // Assert
        reference.TryGetTarget(out _).Should().BeFalse();
    }

    /// <summary>
    /// A key that is not a text of <see cref="Strings"/> fails when the markup is loaded, not as <c>!Key!</c> on screen.
    /// </summary>
    [Fact]
    public void ProvideValue_UnknownKey_Throws()
    {
        // Act
        var act = () => new Loc("NoSuchKey").ProvideValue(null!);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*NoSuchKey*");
    }

    /// <summary>
    /// A key that is a text of <see cref="Strings"/> gives a binding to it.
    /// </summary>
    [Fact]
    public void ProvideValue_KnownKey_GivesABinding()
    {
        // Act
        var value = new Loc(nameof(Strings.AppName)).ProvideValue(null!);

        // Assert
        value.Should().BeOfType<System.Windows.Data.Binding>();
    }

    /// <summary>
    /// Subscribes a listener and lets go of it, in a method of its own so that nothing on the stack keeps it alive.
    /// </summary>
    /// <returns>A weak reference to the listener.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<Listener> Subscribe()
    {
        var listener = new Listener();
        LocalizedStrings.Instance.LanguageChanged += listener.Handle;
        return new WeakReference<Listener>(listener);
    }

    /// <summary>
    /// Counts how often it was told that the language changed.
    /// </summary>
    private sealed class Listener
    {
        /// <summary>
        /// Gets how many times <see cref="Handle"/> was called.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Counts a call.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">Event data.</param>
        public void Handle(object? sender, EventArgs e)
        {
            Calls++;
        }
    }
}
