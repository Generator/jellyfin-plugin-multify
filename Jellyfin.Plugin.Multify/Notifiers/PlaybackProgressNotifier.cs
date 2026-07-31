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
    private readonly PlaybackBitrateService _playbackBitrateService;
    private readonly UserDataService _userDataService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackProgressNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackProgressNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    /// <param name="playbackBitrateService">Instance of the <see cref="PlaybackBitrateService"/> for querying source bitrate.</param>
    /// <param name="userDataService">Instance of the <see cref="UserDataService"/> for per-user data.</param>
    public PlaybackProgressNotifier(
        ILogger<PlaybackProgressNotifier> logger,
        IWebhookSender webhookSender,
        DashboardAlertService dashboardAlert,
        PlaybackBitrateService playbackBitrateService,
        UserDataService userDataService)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
        _playbackBitrateService = playbackBitrateService;
        _userDataService = userDataService;
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

            // Populate per-user data (PlayCount, IsFavorite, Played, UserRating)
            _userDataService.AddUserData(userData, user, eventArgs.Item);

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