using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for playback progress events.
/// </summary>
public class PlaybackProgressNotifier : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly ILogger<PlaybackProgressNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackProgressNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackProgressNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public PlaybackProgressNotifier(ILogger<PlaybackProgressNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackProgressEventArgs eventArgs)
    {
        if (eventArgs.Item is null)
        {
            return;
        }

        if (eventArgs.Users.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "Playback progress event received for {ItemName} by {UserCount} user(s)",
            eventArgs.Item.Name,
            eventArgs.Users.Count);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PlaybackProgress);
        data.AddItemData(eventArgs.Item);
        data.AddPlaybackData(eventArgs);

        if (eventArgs.Session is not null)
        {
            data.AddSessionInfo(eventArgs.Session);
        }

        foreach (var user in eventArgs.Users)
        {
            var userData = new Dictionary<string, object>(data)
            {
                ["Username"] = user.Username ?? "Unknown",
                ["NotificationUsername"] = user.Username ?? "Unknown",
                ["UserId"] = user.Id.ToString()
            };

            await _webhookSender.SendNotification(
                NotificationType.PlaybackProgress,
                userData,
                eventArgs.Item.GetType()).ConfigureAwait(false);
        }

        _logger.LogInformation("Playback progress notification sent for {ItemName}", eventArgs.Item.Name);

        await _dashboardAlert.LogAsync(
            $"Playback progress: {eventArgs.Item.Name}",
            "MultifyPlaybackProgress",
            $"User(s): {string.Join(", ", eventArgs.Users.ConvertAll(u => u.Username ?? "Unknown"))}").ConfigureAwait(false);
    }
}