using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Updates;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for plugin updated events.
/// </summary>
public class PluginUpdatedNotifier : IEventConsumer<PluginUpdatedEventArgs>
{
    private readonly ILogger<PluginUpdatedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUpdatedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PluginUpdatedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public PluginUpdatedNotifier(ILogger<PluginUpdatedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(PluginUpdatedEventArgs eventArgs)
    {
        var pluginInfo = eventArgs.Argument;
        if (pluginInfo is null)
        {
            return;
        }

        _logger.LogDebug("Plugin updated event received: {PluginName} ({PluginId})", pluginInfo.Name, pluginInfo.Id);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PluginUpdated);
        data["PluginName"] = pluginInfo.Name ?? "Unknown";
        data["PluginId"] = pluginInfo.Id.ToString();
        data["PluginVersion"] = pluginInfo.Version?.ToString() ?? "Unknown";

        await _webhookSender.SendNotification(
            NotificationType.PluginUpdated,
            data).ConfigureAwait(false);

        _logger.LogInformation("Plugin updated notification sent for {PluginName}", pluginInfo.Name);

        await _dashboardAlert.LogAsync(
            $"Plugin updated: {pluginInfo.Name}",
            "MultifyPluginUpdated").ConfigureAwait(false);
    }
}