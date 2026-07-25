using System;
using System.Xml.Serialization;

namespace Jellyfin.Plugin.Multify.Destinations.Generic;

/// <summary>
/// Generic webhook destination option.
/// </summary>
public class GenericWebhookOption : BaseOption
{
    /// <summary>Gets or sets the custom headers.</summary>
    [XmlArray("Headers")]
    [XmlArrayItem("Header")]
    public GenericOptionValue[] Headers { get; set; } = Array.Empty<GenericOptionValue>();

    /// <summary>Gets or sets the custom fields merged into the data dictionary.</summary>
    [XmlArray("Fields")]
    [XmlArrayItem("Field")]
    public GenericOptionValue[] Fields { get; set; } = Array.Empty<GenericOptionValue>();
}
