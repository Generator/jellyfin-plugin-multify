using System.Collections.Generic;
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
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the server URL.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Telegram options.</summary>
    public TelegramOption[] TelegramOptions { get; set; } = [];

    /// <summary>Gets or sets the Gotify options.</summary>
    public GotifyOption[] GotifyOptions { get; set; } = [];

    /// <summary>Gets or sets the ntfy options.</summary>
    public NtfyOption[] NtfyOptions { get; set; } = [];

    /// <summary>Gets or sets the generic webhook options.</summary>
    public GenericWebhookOption[] GenericWebhookOptions { get; set; } = [];

    /// <summary>Gets or sets the advanced settings.</summary>
    public AdvancedOption AdvancedSettings { get; set; } = new();

    /// <summary>Gets or sets the MDBList API key.</summary>
    public string MdblistApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether to show the plugin in the main menu sidebar.</summary>
    public bool EnableMainMenu { get; set; } = true;
}
