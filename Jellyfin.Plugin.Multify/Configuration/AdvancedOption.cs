namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Advanced plugin settings.
/// </summary>
public class AdvancedOption
{
    /// <summary>
    /// Gets or sets the minimum log level for plugin output.
    /// Note: This setting is for reference only. Actual log filtering is controlled by the Jellyfin server's
    /// logging configuration. To see Debug messages, configure the server's logging level to include Debug.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>Gets or sets whether to log notification events to the Jellyfin admin dashboard activity feed.</summary>
    public bool EnableDashboardAlerts { get; set; }
}
