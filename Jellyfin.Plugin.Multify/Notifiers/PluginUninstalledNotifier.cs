using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events.Updates;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for plugin uninstalled events.
/// </summary>
public class PluginUninstalledNotifier : IEventConsumer<PluginUninstalledEventArgs>
{
    private readonly ILogger<PluginUninstalledNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUninstalledNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PluginUninstalledNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public PluginUninstalledNotifier(ILogger<PluginUninstalledNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(PluginUninstalledEventArgs eventArgs)
    {
        var pluginInfo = eventArgs.Argument;
        if (pluginInfo is null)
        {
            return;
        }

        _logger.LogDebug("Plugin uninstalled event received: {PluginName} ({PluginId})", pluginInfo.Name, pluginInfo.Id);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PluginUninstalled);
        data["PluginName"] = pluginInfo.Name ?? "Unknown";
        data["PluginId"] = pluginInfo.Id ?? "Unknown";
        data["PluginVersion"] = pluginInfo.Version?.ToString() ?? "Unknown";

        await _webhookSender.SendNotification(
            NotificationType.PluginUninstalled,
            data).ConfigureAwait(false);

        _logger.LogInformation("Plugin uninstalled notification sent for {PluginName}", pluginInfo.Name);

        await _dashboardAlert.LogAsync(
            $"Plugin uninstalled: {pluginInfo.Name}",
            "MultifyPluginUninstalled").ConfigureAwait(false);
    }
}