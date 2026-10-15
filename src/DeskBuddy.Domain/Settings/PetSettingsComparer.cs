using System.Reflection;

namespace DeskBuddy.Domain.Settings;

/// <summary>
/// Compares settings by their values. The record's own equality compares the lists it holds by reference, so two
/// settings with the same process names would count as different; this comparer looks at the items instead. It goes
/// through the properties, so a setting added later is compared without changing anything here.
/// </summary>
public sealed class PetSettingsComparer : IEqualityComparer<PetSettings>
{
    /// <summary>
    /// The properties that make up the settings.
    /// </summary>
    private static readonly PropertyInfo[] Properties = typeof(PetSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0)
        .ToArray();

    /// <summary>
    /// Gets the comparer to use.
    /// </summary>
    public static PetSettingsComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(PetSettings? x, PetSettings? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return Properties.All(property => ValuesEqual(property.GetValue(x), property.GetValue(y)));
    }

    /// <inheritdoc />
    public int GetHashCode(PetSettings obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var hash = new HashCode();
        foreach (var property in Properties)
        {
            switch (property.GetValue(obj))
            {
                case string text:
                    hash.Add(text);
                    break;
                case IEnumerable<string> items:
                    foreach (var item in items)
                    {
                        hash.Add(item);
                    }

                    break;
                case { } value:
                    hash.Add(value);
                    break;
            }
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Compares two property values, looking at the items when both are lists of names.
    /// </summary>
    /// <param name="left">
    /// The first value.
    /// </param>
    /// <param name="right">
    /// The second value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the values are equal.
    /// </returns>
    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is string || right is string)
        {
            return Equals(left, right);
        }

        if (left is IEnumerable<string> leftItems && right is IEnumerable<string> rightItems)
        {
            return leftItems.SequenceEqual(rightItems);
        }

        return Equals(left, right);
    }
}
