namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Advanced plugin settings.
/// </summary>
public class AdvancedOption
{
    /// <summary>Gets or sets whether to log notification events to the Jellyfin admin dashboard activity feed.</summary>
    public bool EnableDashboardAlerts { get; set; }

    /// <summary>
    /// Gets or sets the delay in seconds between sequential notifications of the same service type.
    /// This helps prevent rate limiting from external services when sending multiple notifications.
    /// Range: 1-60 seconds. Default: 2 seconds.
    /// </summary>
    public int DelaySeconds { get; set; } = 2;
}
