using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for subtitle download failure events.
/// </summary>
public class SubtitleDownloadFailureNotifier : IEventConsumer<SubtitleDownloadFailureEventArgs>
{
    private readonly ILogger<SubtitleDownloadFailureNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleDownloadFailureNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{SubtitleDownloadFailureNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public SubtitleDownloadFailureNotifier(ILogger<SubtitleDownloadFailureNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(SubtitleDownloadFailureEventArgs eventArgs)
    {
        if (eventArgs.Item is null)
        {
            return;
        }

        _logger.LogDebug(
            "Subtitle download failure event received for {ItemName} (Provider: {Provider})",
            eventArgs.Item.Name,
            eventArgs.Provider);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.SubtitleDownloadFailure);
        data.AddItemData(eventArgs.Item);
        data["SubtitleProvider"] = eventArgs.Provider ?? "Unknown";
        data["SubtitleDownloadError"] = eventArgs.Exception?.Message ?? "Unknown";

        await _webhookSender.SendNotification(
            NotificationType.SubtitleDownloadFailure,
            data,
            eventArgs.Item.GetType()).ConfigureAwait(false);

        _logger.LogInformation("Subtitle download failure notification sent for {ItemName}", eventArgs.Item.Name);

        await _dashboardAlert.LogAsync(
            $"Subtitle download failed: {eventArgs.Item.Name}",
            "MultifySubtitleDownloadFailure",
            $"Provider: {eventArgs.Provider ?? "Unknown"}").ConfigureAwait(false);
    }
}
