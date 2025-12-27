using System;

namespace Jellyfin.Helpers.Localization;

/// <summary>
/// Defines a localizable string with optional formatting arguments.
/// </summary>
public class LocalizableString
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizableString"/> class with the specified resource key.
    /// </summary>
    /// <param name="key">The key that identifies the localized string resource. Cannot be null or empty.</param>
    public LocalizableString(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizableString"/> class using the specified resource key and formatting
    /// arguments.
    /// </summary>
    /// <param name="key">The resource key that identifies the string to be localized. Cannot be null.</param>
    /// <param name="arguments">An array of objects to format the localized string. May be empty if no formatting is required.</param>
    public LocalizableString(string key, params object[] arguments) : this(key)
    {
        Arguments = arguments;
    }

    /// <summary>
    /// Gets the unique identifier associated with this instance.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the array of arguments associated with the current operation or context.
    /// </summary>
    public object[] Arguments { get; } = Array.Empty<object>();

    /// <summary>
    /// Defines an implicit conversion from a string to a LocalizableString instance.
    /// </summary>
    /// <remarks>This operator enables assigning a string directly to a LocalizableString variable without
    /// explicit casting. If the input string is null, the resulting LocalizableString will represent a null
    /// value.</remarks>
    /// <param name="value">The string value to convert to a LocalizableString. Can be null or empty.</param>
    public static implicit operator LocalizableString(string value)
    {
        return new LocalizableString(value);
    }
}
