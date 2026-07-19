using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for playback stop events.
/// </summary>
public class PlaybackStopNotifier : IEventConsumer<PlaybackStopEventArgs>
{
    private readonly ILogger<PlaybackStopNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStopNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackStopNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public PlaybackStopNotifier(ILogger<PlaybackStopNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStopEventArgs eventArgs)
    {
        if (eventArgs.Item is null)
        {
            return;
        }

        if (eventArgs.Users.Count == 0)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PlaybackStop);
        data.AddItemData(eventArgs.Item);

        if (eventArgs.Session is not null)
        {
            data.AddSessionInfo(eventArgs.Session);
        }

        foreach (var user in eventArgs.Users)
        {
            var userData = new Dictionary<string, object>(data)
            {
                ["NotificationUsername"] = user.Username ?? "Unknown",
                ["UserId"] = user.Id.ToString()
            };

            await _webhookSender.SendNotification(
                NotificationType.PlaybackStop,
                userData,
                eventArgs.Item.GetType()).ConfigureAwait(false);
        }

        await _dashboardAlert.LogAsync(
            $"Playback stopped: {eventArgs.Item.Name}",
            "MultifyPlaybackStop",
            $"User(s): {string.Join(", ", eventArgs.Users.ConvertAll(u => u.Username ?? "Unknown"))}").ConfigureAwait(false);
    }
}
