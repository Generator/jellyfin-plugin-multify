using System;

namespace Jellyfin.Plugin.Multify.Destinations.Generic;

/// <summary>
/// Generic webhook destination option.
/// </summary>
public class GenericWebhookOption : BaseOption
{
    /// <summary>Gets or sets the custom headers.</summary>
    public GenericOptionValue[] Headers { get; set; } = Array.Empty<GenericOptionValue>();

    /// <summary>Gets or sets the custom fields merged into the data dictionary.</summary>
    public GenericOptionValue[] Fields { get; set; } = Array.Empty<GenericOptionValue>();
}
