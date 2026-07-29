using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
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
    private readonly IMediaSourceManager _mediaSourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStopNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackStopNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> interface for querying source bitrate.</param>
    public PlaybackStopNotifier(
        ILogger<PlaybackStopNotifier> logger,
        IWebhookSender webhookSender,
        DashboardAlertService dashboardAlert,
        IMediaSourceManager mediaSourceManager)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
        _mediaSourceManager = mediaSourceManager;
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
            await AddSourceBitrateAsync(data, eventArgs.Item, eventArgs.Users[0]).ConfigureAwait(false);
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

    /// <summary>
    /// Queries the source media bitrate via <see cref="IMediaSourceManager"/> for
    /// DirectPlay/DirectStream sessions (transcode bitrate is already handled by
    /// <see cref="DataObjectHelpers.AddSessionInfo"/>).
    /// </summary>
    private async Task AddSourceBitrateAsync(Dictionary<string, object> data, BaseItem item, User user)
    {
        try
        {
            var sources = await _mediaSourceManager
                .GetPlaybackMediaSources(item, user, false, false, CancellationToken.None)
                .ConfigureAwait(false);

            var source = sources?.FirstOrDefault(s => s.Bitrate.HasValue && s.Bitrate.Value > 0);
            if (source?.Bitrate.HasValue == true)
            {
                var bitrate = source.Bitrate.Value;
                data["PlaybackBitrate"] = bitrate;
                data["PlaybackBitrateText"] = DataObjectHelpers.FormatBitrate(bitrate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying source bitrate for {ItemName}", item.Name);
        }
    }
}
