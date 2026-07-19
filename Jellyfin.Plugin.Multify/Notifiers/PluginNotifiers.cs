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
        if (eventArgs.Plugin is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PluginUpdated);
        data["PluginName"] = eventArgs.Plugin.Name ?? "Unknown";
        data["PluginId"] = eventArgs.Plugin.Id.ToString();
        data["OldVersion"] = eventArgs.OldVersion?.ToString() ?? "Unknown";
        data["NewVersion"] = eventArgs.Plugin.Version ?? "Unknown";

        await _webhookSender.SendNotification(
            NotificationType.PluginUpdated,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Plugin updated: {eventArgs.Plugin.Name}",
            "MultifyPluginUpdated").ConfigureAwait(false);
    }
}
