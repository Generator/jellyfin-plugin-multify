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
    /// The plugin uses ILogger which delegates to the server's logging pipeline — this value does not
    /// filter logs itself.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>Gets or sets whether to log notification events to the Jellyfin admin dashboard activity feed.</summary>
    public bool EnableDashboardAlerts { get; set; }

    /// <summary>
    /// Gets or sets the delay in seconds between sequential notifications of the same service type.
    /// This helps prevent rate limiting from external services when sending multiple notifications.
    /// Range: 1-60 seconds. Default: 2 seconds.
    /// </summary>
    public int DelaySeconds { get; set; } = 2;
}
