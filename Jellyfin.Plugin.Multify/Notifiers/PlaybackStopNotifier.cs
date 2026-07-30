using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Events;
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
    private readonly PlaybackBitrateService _playbackBitrateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStopNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackStopNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    /// <param name="playbackBitrateService">Instance of the <see cref="PlaybackBitrateService"/> for querying source bitrate.</param>
    public PlaybackStopNotifier(
        ILogger<PlaybackStopNotifier> logger,
        IWebhookSender webhookSender,
        DashboardAlertService dashboardAlert,
        PlaybackBitrateService playbackBitrateService)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
        _playbackBitrateService = playbackBitrateService;
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

        _logger.LogDebug(
            "Playback stop event received for {ItemName} by {UserCount} user(s)",
            eventArgs.Item.Name,
            eventArgs.Users.Count);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.PlaybackStop);
        data.AddItemData(eventArgs.Item);
        data.AddPlaybackStopData(eventArgs);

        if (eventArgs.Session is not null)
        {
            data.AddSessionInfo(eventArgs.Session);
        }

        // Populate source bitrate for DirectPlay/DirectStream (transcode bitrate is
        // already populated by AddSessionInfo from Session.TranscodingInfo.Bitrate)
        if (eventArgs.Users.Count > 0 && !data.ContainsKey("PlaybackBitrate"))
        {
            await _playbackBitrateService.AddSourceBitrateAsync(data, eventArgs.Item, eventArgs.Users[0]).ConfigureAwait(false);
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
                NotificationType.PlaybackStop,
                userData,
                eventArgs.Item.GetType()).ConfigureAwait(false);
        }

        _logger.LogInformation("Playback stop notification sent for {ItemName}", eventArgs.Item.Name);

        await _dashboardAlert.LogAsync(
            $"Playback stopped: {eventArgs.Item.Name}",
            "MultifyPlaybackStop",
            $"User(s): {string.Join(", ", eventArgs.Users.ConvertAll(u => u.Username ?? "Unknown"))}").ConfigureAwait(false);
    }
}
