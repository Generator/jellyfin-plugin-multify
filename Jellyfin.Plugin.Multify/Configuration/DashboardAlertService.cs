using System.Threading.Tasks;
using MediaBrowser.Controller.Activity;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Logs notification events to the Jellyfin admin dashboard activity feed.
/// </summary>
public class DashboardAlertService
{
    private readonly IActivityManager _activityManager;
    private readonly AdvancedOption _settings;
    private readonly ILogger<DashboardAlertService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardAlertService"/> class.
    /// </summary>
    /// <param name="activityManager">Instance of the <see cref="IActivityManager"/> interface.</param>
    /// <param name="settings">Instance of the <see cref="AdvancedOption"/>.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{DashboardAlertService}"/> interface.</param>
    public DashboardAlertService(IActivityManager activityManager, AdvancedOption settings, ILogger<DashboardAlertService> logger)
    {
        _activityManager = activityManager;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Logs a notification event to the dashboard activity feed if alerts are enabled.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <param name="type">The activity type (e.g. "MultifyPlaybackStart").</param>
    /// <param name="overview">Optional overview text.</param>
    /// <param name="severity">The log severity. Default is Information.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LogAsync(string name, string type, string? overview = null, LogSeverity severity = LogSeverity.Information)
    {
        if (!_settings.EnableDashboardAlerts)
        {
            return;
        }

        try
        {
            var entry = new ActivityLog
            {
                Name = name,
                Type = type,
                Overview = overview,
                LogSeverity = severity
            };

            await _activityManager.CreateAsync(entry).ConfigureAwait(false);
            _logger.LogDebug("Dashboard alert created: {Name} ({Type})", name, type);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create dashboard alert: {Name}", name);
        }
    }
}
