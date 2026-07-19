namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Advanced plugin settings.
/// </summary>
public class AdvancedOption
{
    /// <summary>Gets or sets whether to log notification events to the Jellyfin admin dashboard activity feed.</summary>
    public bool EnableDashboardAlerts { get; set; }
}
