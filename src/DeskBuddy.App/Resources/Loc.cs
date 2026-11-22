using System.Reflection;
using System.Windows.Data;
using System.Windows.Markup;

namespace DeskBuddy.App.Resources;

/// <summary>
/// Markup extension for text in XAML: <c>{res:Loc AppName}</c> shows <c>Strings.AppName</c> and follows the interface
/// language when it changes, which <c>{x:Static res:Strings.AppName}</c> does not, as that is read once.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class Loc : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Loc"/> class.
    /// </summary>
    /// <param name="key">The name of the text in <c>Strings.resx</c>.</param>
    public Loc(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Gets the name of the text in <c>Strings.resx</c>.
    /// </summary>
    [ConstructorArgument("key")]
    public string Key { get; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // The keys are the names of the properties of Strings, which {x:Static} used to check at build time; a typo would
        // otherwise only show as !Key! on screen. Failing when the markup loads makes it show the first time it is opened.
        if (typeof(Strings).GetProperty(Key, BindingFlags.Public | BindingFlags.Static) is null)
        {
            throw new InvalidOperationException($"There is no text named '{Key}' in Strings.");
        }

        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizedStrings.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
