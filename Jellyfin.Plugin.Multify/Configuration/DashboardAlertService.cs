using System;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.Multify.Configuration;

/// <summary>
/// Logs notification events to the Jellyfin admin dashboard activity feed.
/// </summary>
public class DashboardAlertService
{
    private readonly MediaBrowser.Model.Activity.IActivityManager _activityManager;
    private readonly ILogger<DashboardAlertService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardAlertService"/> class.
    /// </summary>
    /// <param name="activityManager">Instance of the <see cref="MediaBrowser.Model.Activity.IActivityManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{DashboardAlertService}"/> interface.</param>
    public DashboardAlertService(MediaBrowser.Model.Activity.IActivityManager activityManager, ILogger<DashboardAlertService> logger)
    {
        _activityManager = activityManager;
        _logger = logger;
    }

    private AdvancedOption Settings => MultifyPlugin.Instance?.Configuration.AdvancedSettings ?? new AdvancedOption();

    /// <summary>
    /// Logs a notification event to the dashboard activity feed if alerts are enabled.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <param name="type">The activity type (e.g. "MultifyPlaybackStart").</param>
    /// <param name="overview">Optional overview text.</param>
    /// <param name="severity">The log severity. Default is Information.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LogAsync(string name, string type, string? overview = null, LogLevel severity = LogLevel.Information)
    {
        if (!Settings.EnableDashboardAlerts)
        {
            return;
        }

        try
        {
            var entry = new ActivityLog(name, type, Guid.Empty)
            {
                Overview = overview,
                LogSeverity = severity
            };

            await _activityManager.CreateAsync(entry).ConfigureAwait(false);
            _logger.LogDebug("Dashboard alert created: {Name} ({Type})", name, type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create dashboard alert: {Name}", name);
        }
    }
}
