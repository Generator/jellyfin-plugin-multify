using System.Xml.Serialization;

namespace Jellyfin.Plugin.Multify.Destinations.Generic;

/// <summary>
/// A key-value pair for generic webhook headers or fields.
/// </summary>
public class GenericOptionValue
{
    /// <summary>Gets or sets the key.</summary>
    [XmlElement("Key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the value.</summary>
    [XmlElement("Value")]
    public string Value { get; set; } = string.Empty;
}
