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
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
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
        _logger.LogDebug("SendNotification called for {NotificationType}, ItemType={ItemType}", notificationType, itemType?.Name ?? "null");

        // Debug: Log loaded notification types
        foreach (var opt in _configuration.NtfyOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogInformation(
                "Loaded ntfy option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.TelegramOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogInformation(
                "Loaded telegram option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.GotifyOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogInformation(
                "Loaded gotify option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.GenericWebhookOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogInformation(
                "Loaded generic option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        // Enrich data with MDBList ratings if configured
        if (_mdblistService != null && !string.IsNullOrEmpty(_configuration.MdblistApiKey))
        {
            await EnrichWithMdblistRatings(itemData).ConfigureAwait(false);
        }

        // Enrich data with item URL if ServerUrl is configured
        if (!string.IsNullOrEmpty(_configuration.ServerUrl))
        {
            EnrichWithItemUrl(itemData);
        }

        // Enrich data with trailer URLs
        EnrichTrailerUrls(itemData);

        var tasks = new List<Task>();

        var telegramCount = _configuration.TelegramOptions.Count(o => o.NotificationTypes.Contains(notificationType));
        var gotifyCount = _configuration.GotifyOptions.Count(o => o.NotificationTypes.Contains(notificationType));
        var ntfyCount = _configuration.NtfyOptions.Count(o => o.NotificationTypes.Contains(notificationType));
        var genericCount = _configuration.GenericWebhookOptions.Count(o => o.NotificationTypes.Contains(notificationType));

        _logger.LogDebug(
            "Matching destinations: Telegram={Telegram}, Gotify={Gotify}, ntfy={Ntfy}, Generic={Generic}",
            telegramCount,
            gotifyCount,
            ntfyCount,
            genericCount);

        // Get delay from advanced settings (convert seconds to milliseconds)
        var delayMs = Math.Max(0, (_configuration.AdvancedSettings?.DelaySeconds ?? 2) * 1000);

        // Send notifications sequentially within each service type, with delay between them
        // Different service types still run in parallel

        var telegramOptions = _configuration.TelegramOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var gotifyOptions = _configuration.GotifyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var ntfyOptions = _configuration.NtfyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var genericOptions = _configuration.GenericWebhookOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();

        // Fire all service types in parallel
        tasks.Add(SendNotificationsSequentially(_telegramClient, telegramOptions, itemData, itemType, delayMs, "Telegram"));
        tasks.Add(SendNotificationsSequentially(_gotifyClient, gotifyOptions, itemData, itemType, delayMs, "Gotify"));
        tasks.Add(SendNotificationsSequentially(_ntfyClient, ntfyOptions, itemData, itemType, delayMs, "Ntfy"));
        tasks.Add(SendNotificationsSequentially(_genericClient, genericOptions, itemData, itemType, delayMs, "Generic"));

        if (tasks.Count == 0)
        {
            _logger.LogDebug("No matching destinations for {NotificationType}", notificationType);
            return;
        }

        _logger.LogDebug("Sending to {DestinationCount} destination(s) for {NotificationType}", tasks.Count, notificationType);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _logger.LogInformation("Completed sending {NotificationType} to {DestinationCount} destination(s)", notificationType, tasks.Count);
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

    private void EnrichWithItemUrl(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("ItemId", out var itemIdObj) || itemIdObj is not string itemId || string.IsNullOrEmpty(itemId))
        {
            return;
        }

        var serverUrl = _configuration.ServerUrl.TrimEnd('/');
        var itemUrl = $"{serverUrl}/web/#/details?id={itemId}";
        data["ItemUrl"] = itemUrl;

        // Generate short ID (first 10 chars of GUID without dashes)
        var shortId = itemId.Replace("-", string.Empty, StringComparison.Ordinal)[..10];
        data["ItemShortId"] = shortId;

        // Enrich image URLs with full server paths
        EnrichImageUrls(data, serverUrl, itemId);
    }

    private static void EnrichImageUrls(Dictionary<string, object> data, string serverUrl, string itemId)
    {
        // Primary image URL
        if (data.TryGetValue("PrimaryImageUrl", out var primaryObj) && primaryObj is string primaryUrl && !string.IsNullOrEmpty(primaryUrl))
        {
            // If it's already a full URL, use as-is; otherwise construct from server URL
            if (!primaryUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["PrimaryImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Primary";
            }
        }
        else
        {
            // Construct default primary image URL
            data["PrimaryImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Primary";
        }

        // Backdrop image URL
        if (data.TryGetValue("BackdropImageUrl", out var backdropObj) && backdropObj is string backdropUrl && !string.IsNullOrEmpty(backdropUrl))
        {
            if (!backdropUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["BackdropImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Backdrop";
            }
        }
        else
        {
            data["BackdropImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Backdrop";
        }

        // Thumbnail image URL
        if (data.TryGetValue("ThumbImageUrl", out var thumbObj) && thumbObj is string thumbUrl && !string.IsNullOrEmpty(thumbUrl))
        {
            if (!thumbUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["ThumbImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Thumbnail";
            }
        }
        else
        {
            data["ThumbImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Thumbnail";
        }

        // Logo image URL
        if (data.TryGetValue("LogoImageUrl", out var logoObj) && logoObj is string logoUrl && !string.IsNullOrEmpty(logoUrl))
        {
            if (!logoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["LogoImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Logo";
            }
        }
        else
        {
            data["LogoImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Logo";
        }

        // Banner image URL
        if (data.TryGetValue("BannerImageUrl", out var bannerObj) && bannerObj is string bannerUrl && !string.IsNullOrEmpty(bannerUrl))
        {
            if (!bannerUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["BannerImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Banner";
            }
        }
        else
        {
            data["BannerImageUrl"] = $"{serverUrl}/Items/{itemId}/Images/Banner";
        }
    }

    private static void EnrichTrailerUrls(Dictionary<string, object> data)
    {
        // Note: RemoteTrailers are not directly available in the data dictionary
        // They need to be extracted from the BaseItem when creating the data object
        // This method is a placeholder for future enrichment if needed
        // The actual extraction happens in DataObjectHelpers.AddItemData()
    }

    private static string GetMediaType(Dictionary<string, object> data)
    {
        if (data.TryGetValue("ItemType", out var itemTypeObj) && itemTypeObj is string itemType)
        {
            return itemType.Contains("Movie", StringComparison.OrdinalIgnoreCase) ? "movie" : "show";
        }

        return "movie";
    }

    private static bool NotifyOnItem<T>(T baseOptions, Type? itemType)
        where T : BaseOption
    {
        if (itemType is null)
        {
            return true;
        }

        if (baseOptions.EnableMovies && itemType == typeof(Movie))
        {
            return true;
        }

        if (baseOptions.EnableEpisodes && itemType == typeof(Episode))
        {
            return true;
        }

        if (baseOptions.EnableSeries && itemType == typeof(Series))
        {
            return true;
        }

        if (baseOptions.EnableSeasons && itemType == typeof(Season))
        {
            return true;
        }

        if (baseOptions.EnableAlbums && itemType == typeof(MusicAlbum))
        {
            return true;
        }

        if (baseOptions.EnableSongs && itemType == typeof(Audio))
        {
            return true;
        }

        if (baseOptions.EnableVideos && itemType == typeof(Video))
        {
            return true;
        }

        return false;
    }

    private async Task SendNotification<TOption>(IWebhookClient<TOption> client, TOption option, Dictionary<string, object> itemData, Type? itemType)
        where TOption : BaseOption
    {
        if (!NotifyOnItem(option, itemType))
        {
            _logger.LogDebug("Skipping {WebhookName} — item type {ItemType} not enabled", option.WebhookName, itemType?.Name ?? "null");
            return;
        }

        var data = new Dictionary<string, object>(itemData);
        try
        {
            _logger.LogDebug("Sending to {WebhookName} ({ClientType})", option.WebhookName, typeof(TOption).Name);
            await client.SendAsync(option, data).ConfigureAwait(false);
            _logger.LogDebug("Successfully sent to {WebhookName}", option.WebhookName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending notification to {WebhookName}", option.WebhookName);
        }
    }

    private async Task SendNotificationsSequentially<TOption>(IWebhookClient<TOption> client, IReadOnlyList<TOption> options, Dictionary<string, object> itemData, Type? itemType, int delayMs, string serviceName)
        where TOption : BaseOption
    {
        if (options.Count == 0)
        {
            return;
        }

        for (var i = 0; i < options.Count; i++)
        {
            await SendNotification(client, options[i], itemData, itemType).ConfigureAwait(false);

            // Add delay between notifications (but not after the last one)
            if (delayMs > 0 && i < options.Count - 1)
            {
                _logger.LogDebug("Waiting {DelayMs}ms before next {ServiceName} notification", delayMs, serviceName);
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }
    }
}
