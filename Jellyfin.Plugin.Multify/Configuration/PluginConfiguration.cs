using System.Collections.Generic;
using System.Xml.Serialization;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Plugin configuration class.
/// </summary>
[XmlRoot("PluginConfiguration")]
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the server URL.</summary>
    [XmlElement("ServerUrl")]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Telegram options.</summary>
    [XmlArray("TelegramOptions")]
    [XmlArrayItem("TelegramOption")]
    public TelegramOption[] TelegramOptions { get; set; } = [];

    /// <summary>Gets or sets the Gotify options.</summary>
    [XmlArray("GotifyOptions")]
    [XmlArrayItem("GotifyOption")]
    public GotifyOption[] GotifyOptions { get; set; } = [];

    /// <summary>Gets or sets the ntfy options.</summary>
    [XmlArray("NtfyOptions")]
    [XmlArrayItem("NtfyOption")]
    public NtfyOption[] NtfyOptions { get; set; } = [];

    /// <summary>Gets or sets the generic webhook options.</summary>
    [XmlArray("GenericWebhookOptions")]
    [XmlArrayItem("GenericWebhookOption")]
    public GenericWebhookOption[] GenericWebhookOptions { get; set; } = [];

    /// <summary>Gets or sets the advanced settings.</summary>
    [XmlElement("AdvancedSettings")]
    public AdvancedOption AdvancedSettings { get; set; } = new();

    /// <summary>Gets or sets the MDBList API key.</summary>
    [XmlElement("MdblistApiKey")]
    public string MdblistApiKey { get; set; } = string.Empty;
}
