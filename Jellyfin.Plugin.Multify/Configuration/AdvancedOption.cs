using System.Xml.Serialization;

namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Advanced plugin settings.
/// </summary>
public class AdvancedOption
{
    /// <summary>Gets or sets whether to log notification events to the Jellyfin admin dashboard activity feed.</summary>
    [XmlElement("EnableDashboardAlerts")]
    public bool EnableDashboardAlerts { get; set; }

    /// <summary>
    /// Gets or sets the delay in seconds between sequential notifications of the same service type.
    /// This helps prevent rate limiting from external services when sending multiple notifications.
    /// Range: 1-60 seconds. Default: 2 seconds.
    /// </summary>
    [XmlElement("DelaySeconds")]
    public int DelaySeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the MDBList cache TTL in hours.
    /// Default: 24 hours. Set to 0 to disable caching.
    /// </summary>
    [XmlElement("MdblistCacheTtlHours")]
    public int MdblistCacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the MDBList HTTP request timeout in seconds.
    /// Default: 10 seconds. Range: 5-60 seconds.
    /// </summary>
    [XmlElement("MdblistTimeoutSeconds")]
    public int MdblistTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for MDBList requests.
    /// Default: 3 retries. Range: 0-5.
    /// </summary>
    [XmlElement("MdblistMaxRetries")]
    public int MdblistMaxRetries { get; set; } = 3;
}
