namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Result of filter evaluation for webhook notifications.
/// </summary>
public enum FilterResult
{
    /// <summary>
    /// Notification should be sent (all filters passed).
    /// </summary>
    Allow,

    /// <summary>
    /// Notification blocked by user filter.
    /// </summary>
    DenyUserFilter,

    /// <summary>
    /// Notification blocked by library filter.
    /// </summary>
    DenyLibraryFilter,

    /// <summary>
    /// Notification blocked because webhook is disabled.
    /// </summary>
    DenyWebhookDisabled
}
