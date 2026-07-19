using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using Jellyfin.Plugin.Multify.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify;

/// <summary>
/// Routes notifications to configured destinations.
/// </summary>
public class MultifySender : IWebhookSender
{
    private readonly ILogger<MultifySender> _logger;
    private readonly PluginConfiguration _configuration;
    private readonly IWebhookClient<TelegramOption> _telegramClient;
    private readonly IWebhookClient<GotifyOption> _gotifyClient;
    private readonly IWebhookClient<NtfyOption> _ntfyClient;
    private readonly IWebhookClient<GenericWebhookOption> _genericClient;
    private readonly MdblistService? _mdblistService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifySender"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifySender}"/> interface.</param>
    /// <param name="configuration">Instance of the <see cref="PluginConfiguration"/>.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    /// <param name="mdblistService">Instance of the <see cref="MdblistService"/>.</param>
    public MultifySender(
        ILogger<MultifySender> logger,
        PluginConfiguration configuration,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient,
        MdblistService? mdblistService = null)
    {
        _logger = logger;
        _configuration = configuration;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
        _mdblistService = mdblistService;
    }

    /// <inheritdoc />
    public async Task SendNotification(NotificationType notificationType, Dictionary<string, object> itemData, Type? itemType = null)
    {
        // Enrich data with MDBList ratings if configured
        if (_mdblistService != null && !string.IsNullOrEmpty(_configuration.MdblistApiKey))
        {
            await EnrichWithMdblistRatings(itemData).ConfigureAwait(false);
        }

        var tasks = new List<Task>();

        foreach (var option in _configuration.TelegramOptions.Where(o => o.NotificationTypes.Contains(notificationType)))
        {
            tasks.Add(SendNotification(_telegramClient, option, itemData, itemType));
        }

        foreach (var option in _configuration.GotifyOptions.Where(o => o.NotificationTypes.Contains(notificationType)))
        {
            tasks.Add(SendNotification(_gotifyClient, option, itemData, itemType));
        }

        foreach (var option in _configuration.NtfyOptions.Where(o => o.NotificationTypes.Contains(notificationType)))
        {
            tasks.Add(SendNotification(_ntfyClient, option, itemData, itemType));
        }

        foreach (var option in _configuration.GenericWebhookOptions.Where(o => o.NotificationTypes.Contains(notificationType)))
        {
            tasks.Add(SendNotification(_genericClient, option, itemData, itemType));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task EnrichWithMdblistRatings(Dictionary<string, object> data)
    {
        try
        {
            // Try to get IMDb ID
            if (data.TryGetValue("ImdbId", out var imdbIdObj) && imdbIdObj is string imdbId && !string.IsNullOrEmpty(imdbId))
            {
                var mediaType = GetMediaType(data);
                var ratings = await _mdblistService!.GetRatingsAsync(_configuration.MdblistApiKey, imdbId, mediaType).ConfigureAwait(false);
                if (ratings != null)
                {
                    foreach (var rating in ratings)
                    {
                        data[rating.Key] = rating.Value;
                    }
                }
            }
            // Try to get TMDb ID
            else if (data.TryGetValue("TmdbId", out var tmdbIdObj) && tmdbIdObj is string tmdbIdStr && int.TryParse(tmdbIdStr, out var tmdbId))
            {
                var mediaType = GetMediaType(data);
                var ratings = await _mdblistService!.GetRatingsByTmdbAsync(_configuration.MdblistApiKey, tmdbId, mediaType).ConfigureAwait(false);
                if (ratings != null)
                {
                    foreach (var rating in ratings)
                    {
                        data[rating.Key] = rating.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enriching data with MDBList ratings");
        }
    }

    private static string GetMediaType(Dictionary<string, object> data)
    {
        if (data.TryGetValue("ItemType", out var itemTypeObj) && itemTypeObj is string itemType)
        {
            return itemType.Contains("Movie", StringComparison.OrdinalIgnoreCase) ? "movie" : "show";
        }

        return "movie";
    }

    private static bool NotifyOnItem<T>(T baseOptions, Type? itemType) where T : BaseOption
    {
        if (itemType is null)
        {
            return true;
        }

        if (baseOptions.EnableMovies && itemType == typeof(MediaBrowser.Controller.Entities.Movies.Movie))
        {
            return true;
        }

        if (baseOptions.EnableEpisodes && itemType == typeof(MediaBrowser.Controller.Entities.TV.Episode))
        {
            return true;
        }

        if (baseOptions.EnableSeries && itemType == typeof(MediaBrowser.Controller.Entities.TV.Series))
        {
            return true;
        }

        if (baseOptions.EnableSeasons && itemType == typeof(MediaBrowser.Controller.Entities.TV.Season))
        {
            return true;
        }

        if (baseOptions.EnableAlbums && itemType == typeof(MediaBrowser.Controller.Entities.Music.MusicAlbum))
        {
            return true;
        }

        if (baseOptions.EnableSongs && itemType == typeof(MediaBrowser.Controller.Entities.Audio.Audio))
        {
            return true;
        }

        if (baseOptions.EnableVideos && itemType == typeof(MediaBrowser.Controller.Entities.Video.Video))
        {
            return true;
        }

        return false;
    }

    private async Task SendNotification<TOption>(IWebhookClient<TOption> client, TOption option, Dictionary<string, object> itemData, Type? itemType) where TOption : BaseOption
    {
        if (!NotifyOnItem(option, itemType))
        {
            return;
        }

        var data = new Dictionary<string, object>(itemData);
        try
        {
            await client.SendAsync(option, data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending notification to {WebhookName}", option.WebhookName);
        }
    }
}
