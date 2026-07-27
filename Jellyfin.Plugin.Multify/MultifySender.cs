using System;
using System.Collections.Generic;
using System.Globalization;
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
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
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
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifySender"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifySender}"/> interface.</param>
    /// <param name="configuration">Instance of the <see cref="PluginConfiguration"/>.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/>.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/>.</param>
    /// <param name="mdblistService">Instance of the <see cref="MdblistService"/>.</param>
    public MultifySender(
        ILogger<MultifySender> logger,
        PluginConfiguration configuration,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient,
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        MdblistService? mdblistService = null)
    {
        _logger = logger;
        _configuration = configuration;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
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
            _logger.LogDebug(
                "Loaded ntfy option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.TelegramOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogDebug(
                "Loaded telegram option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.GotifyOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogDebug(
                "Loaded gotify option: {WebhookName}, NotificationTypes=[{Types}], EnableWebhook={EnableWebhook}",
                opt.WebhookName,
                types,
                opt.EnableWebhook);
        }

        foreach (var opt in _configuration.GenericWebhookOptions)
        {
            var types = string.Join(",", opt.NotificationTypes);
            _logger.LogDebug(
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

        // Enrich data with TMDB image URLs via Jellyfin's provider system
        await EnrichWithTmdbImages(itemData).ConfigureAwait(false);

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

        var totalDestinations = telegramOptions.Count + gotifyOptions.Count + ntfyOptions.Count + genericOptions.Count;
        _logger.LogDebug("Sending to {DestinationCount} destination(s) for {NotificationType}", totalDestinations, notificationType);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _logger.LogInformation("Completed sending {NotificationType} to {DestinationCount} destination(s)", notificationType, totalDestinations);
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

    private async Task EnrichWithTmdbImages(Dictionary<string, object> data)
    {
        // Need ItemId to look up the real item via ILibraryManager
        if (!data.TryGetValue("ItemId", out var itemIdObj) || itemIdObj is not string itemIdStr || !Guid.TryParse(itemIdStr, out var itemId))
        {
            _logger.LogTrace("No ItemId in data, skipping TMDB image enrichment");
            return;
        }

        // Look up the actual BaseItem from the library
        var item = _libraryManager.GetItemById(itemId);
        if (item == null)
        {
            _logger.LogTrace("Item {ItemId} not found in library, skipping TMDB image enrichment", itemId);
            return;
        }

        try
        {
            // Query Jellyfin's provider system for TMDB remote images
            var query = new RemoteImageQuery("TheMovieDb")
            {
                IncludeAllLanguages = true,
                IncludeDisabledProviders = false
            };

            var remoteImages = await _providerManager
                .GetAvailableRemoteImages(item, query, default)
                .ConfigureAwait(false);

            if (remoteImages == null)
            {
                _logger.LogDebug("No TMDB remote images available for item {ItemId}", itemId);
                return;
            }

            // Map RemoteImageInfo results by Type into TMDB URL variables
            foreach (var image in remoteImages)
            {
                if (string.IsNullOrEmpty(image.Url))
                {
                    continue;
                }

                switch (image.Type)
                {
                    case ImageType.Primary:
                        data["TmdbPosterUrl"] = image.Url;
                        data["TmdbProfileUrl"] = image.Url;
                        // Also update PrimaryImageUrl so {{PrimaryImageUrl}} resolves to a
                        // publicly accessible CDN URL (instead of a local Jellyfin URL that
                        // services like Telegram cannot fetch)
                        data["PrimaryImageUrl"] = image.Url;
                        break;
                    case ImageType.Backdrop:
                        data["TmdbBackdropUrl"] = image.Url;
                        // Also update BackdropImageUrl for the same reason
                        data["BackdropImageUrl"] = image.Url;
                        break;
                    case ImageType.Logo:
                        data["TmdbLogoUrl"] = image.Url;
                        data["LogoImageUrl"] = image.Url;
                        break;
                    case ImageType.Thumb:
                        data["TmdbStillUrl"] = image.Url;
                        data["ThumbImageUrl"] = image.Url;
                        break;
                }
            }

            var urlCount = remoteImages.Count(i => !string.IsNullOrEmpty(i.Url));
            _logger.LogDebug("Enriched TMDB image URLs for item {ItemId}: {Count} URL(s)", itemId, urlCount);

            // Enrich parent-level poster URLs (Season/Series) for hierarchical items.
            // This allows users to reference {{TmdbSeasonPosterUrl}} or {{SeriesPrimaryImageUrl}}
            // regardless of the current item type.
            await EnrichParentPosterUrls(data, item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enriching data with TMDB image URLs for item {ItemId}", itemId);
        }
    }

    /// <summary>
    /// Enriches data with poster URLs for parent items (Season, Series) when the current item
    /// is an Episode or Season. Sets both Jellyfin local URLs and TMDB CDN URLs.
    /// </summary>
    private async Task EnrichParentPosterUrls(Dictionary<string, object> data, BaseItem item)
    {
        var serverUrl = _configuration.ServerUrl?.TrimEnd('/');

        if (item is Episode episode)
        {
            // Season poster
            if (episode.SeasonId != Guid.Empty)
            {
                var seasonItem = _libraryManager.GetItemById(episode.SeasonId);
                if (seasonItem != null)
                {
                    await EnrichSingleParentPoster(data, seasonItem, "Season", serverUrl).ConfigureAwait(false);
                }
            }

            // Series poster
            if (episode.SeriesId != Guid.Empty)
            {
                var seriesItem = _libraryManager.GetItemById(episode.SeriesId);
                if (seriesItem != null)
                {
                    await EnrichSingleParentPoster(data, seriesItem, "Series", serverUrl).ConfigureAwait(false);
                }
            }
        }
        else if (item is Season season)
        {
            // Current item IS the Season — copy poster URLs to Season-prefixed keys
            CopySelfPosterToPrefixed(data, "Season");

            // Series poster (parent)
            if (season.SeriesId != Guid.Empty)
            {
                var seriesItem = _libraryManager.GetItemById(season.SeriesId);
                if (seriesItem != null)
                {
                    await EnrichSingleParentPoster(data, seriesItem, "Series", serverUrl).ConfigureAwait(false);
                }
            }
        }
        else if (item is Series)
        {
            // Current item IS the Series — copy poster URLs to Series-prefixed keys
            CopySelfPosterToPrefixed(data, "Series");
        }
        // For Movie items, parent posters remain empty (no season/series concept)
    }

    /// <summary>
    /// Enriches a single parent item's poster into the data dictionary with both Jellyfin
    /// and TMDB URLs, keyed by the given <paramref name="prefix"/> (e.g. "Season", "Series").
    /// </summary>
    private async Task EnrichSingleParentPoster(Dictionary<string, object> data, BaseItem parentItem, string prefix, string? serverUrl)
    {
        var parentId = parentItem.Id;
        var parentIdStr = parentId.ToString("N", CultureInfo.InvariantCulture);

        // Set Jellyfin local URL as fallback
        if (!string.IsNullOrEmpty(serverUrl))
        {
            data[$"{prefix}PrimaryImageUrl"] = $"{serverUrl}/Items/{parentIdStr}/Images/Primary";
        }

        // Query TMDB remote images for the parent item
        try
        {
            var query = new RemoteImageQuery("TheMovieDb")
            {
                IncludeAllLanguages = true,
                IncludeDisabledProviders = false
            };

            var remoteImages = await _providerManager
                .GetAvailableRemoteImages(parentItem, query, default)
                .ConfigureAwait(false);

            if (remoteImages != null)
            {
                foreach (var image in remoteImages)
                {
                    if (string.IsNullOrEmpty(image.Url))
                    {
                        continue;
                    }

                    if (image.Type == ImageType.Primary)
                    {
                        data[$"Tmdb{prefix}PosterUrl"] = image.Url;
                        // Overwrite Jellyfin URL with TMDB CDN URL for public access
                        data[$"{prefix}PrimaryImageUrl"] = image.Url;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error enriching {Prefix} poster for parent item {ParentId}", prefix, parentId);
        }
    }

    /// <summary>
    /// Copies the current item's poster URLs (PrimaryImageUrl, TmdbPosterUrl) into
    /// prefixed keys for the given <paramref name="prefix"/> (e.g. "Season", "Series").
    /// Used when the current item IS the parent (e.g. a Series item should have
    /// SeriesPrimaryImageUrl = PrimaryImageUrl).
    /// </summary>
    private static void CopySelfPosterToPrefixed(Dictionary<string, object> data, string prefix)
    {
        if (data.TryGetValue("PrimaryImageUrl", out var primary) && primary is string primaryStr)
        {
            data[$"{prefix}PrimaryImageUrl"] = primaryStr;
        }

        if (data.TryGetValue("TmdbPosterUrl", out var tmdb) && tmdb is string tmdbStr)
        {
            data[$"Tmdb{prefix}PosterUrl"] = tmdbStr;
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

        var data = DeepCopyDict(itemData);
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

    private static Dictionary<string, object> DeepCopyDict(Dictionary<string, object> source)
    {
        var copy = new Dictionary<string, object>(source.Count, source.Comparer);
        foreach (var kvp in source)
        {
            // Strings and value types are immutable — safe to share
            if (kvp.Value is string or int or long or float or double or bool or Guid or DateTime or Enum)
            {
                copy[kvp.Key] = kvp.Value;
            }
            else if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                copy[kvp.Key] = DeepCopyDict(new Dictionary<string, object>(nestedDict));
            }
            else if (kvp.Value is System.Collections.IList list)
            {
                var listCopy = new List<object>(list.Count);
                foreach (var item in list)
                {
                    listCopy.Add(item);
                }
                copy[kvp.Key] = listCopy;
            }
            else
            {
                // Unknown reference type — share reference (best effort)
                copy[kvp.Key] = kvp.Value;
            }
        }

        return copy;
    }
}
