namespace Jellyfin.Plugin.Multify.Destinations.Generic;

/// <summary>
/// A key-value pair for generic webhook headers or fields.
/// </summary>
public class GenericOptionValue
{
    /// <summary>Gets or sets the key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the value.</summary>
    public string Value { get; set; } = string.Empty;
}
