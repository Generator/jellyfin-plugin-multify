using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Updates;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for plugin installed events.
/// </summary>
public class PluginInstalledNotifier : IEventConsumer<PluginInstalledEventArgs>
{
    private readonly ILogger<PluginInstalledNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInstalledNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PluginInstalledNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public PluginInstalledNotifier(ILogger<PluginInstalledNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(PluginInstalledEventArgs eventArgs)
    {
        var installationInfo = eventArgs.Argument;
        if (installationInfo is null)
        {
            return;
        }

        _logger.LogDebug("Plugin installed event received: {PluginName} ({PluginId})", installationInfo.Name, installationInfo.Id);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PluginInstalled);
        data["PluginName"] = installationInfo.Name ?? "Unknown";
        data["PluginId"] = installationInfo.Id ?? "Unknown";
        data["PluginVersion"] = installationInfo.Version?.ToString() ?? "Unknown";
        data["SourceUrl"] = installationInfo.SourceUrl ?? string.Empty;

        await _webhookSender.SendNotification(
            NotificationType.PluginInstalled,
            data).ConfigureAwait(false);

        _logger.LogInformation("Plugin installed notification sent for {PluginName}", installationInfo.Name);

        await _dashboardAlert.LogAsync(
            $"Plugin installed: {installationInfo.Name}",
            "MultifyPluginInstalled").ConfigureAwait(false);
    }
}