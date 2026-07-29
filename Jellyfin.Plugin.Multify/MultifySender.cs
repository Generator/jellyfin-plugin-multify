using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using Jellyfin.Plugin.Multify.Helpers;
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
    private readonly IMediaSourceManager _mediaSourceManager;

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
    /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> for querying media streams.</param>
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
        IMediaSourceManager mediaSourceManager,
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
        _mediaSourceManager = mediaSourceManager;
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

        // Enrich data with media stream info (codec, resolution, framerate, etc.)
        EnrichWithMediaStreams(itemData);

        // Enrich data with people info (Director, Writers, CastList, CastJson)
        await EnrichWithPeople(itemData).ConfigureAwait(false);

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
        if (data.TryGetValue("PrimaryImage", out var primaryObj) && primaryObj is string primaryUrl && !string.IsNullOrEmpty(primaryUrl))
        {
            // If it's already a full URL, use as-is; otherwise construct from server URL
            if (!primaryUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["PrimaryImage"] = $"{serverUrl}/Items/{itemId}/Images/Primary";
            }
        }
        else
        {
            // Construct default primary image URL
            data["PrimaryImage"] = $"{serverUrl}/Items/{itemId}/Images/Primary";
        }

        // Backdrop image URL
        if (data.TryGetValue("BackdropImage", out var backdropObj) && backdropObj is string backdropUrl && !string.IsNullOrEmpty(backdropUrl))
        {
            if (!backdropUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["BackdropImage"] = $"{serverUrl}/Items/{itemId}/Images/Backdrop";
            }
        }
        else
        {
            data["BackdropImage"] = $"{serverUrl}/Items/{itemId}/Images/Backdrop";
        }

        // Thumbnail image URL
        if (data.TryGetValue("ThumbImage", out var thumbObj) && thumbObj is string thumbUrl && !string.IsNullOrEmpty(thumbUrl))
        {
            if (!thumbUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["ThumbImage"] = $"{serverUrl}/Items/{itemId}/Images/Thumbnail";
            }
        }
        else
        {
            data["ThumbImage"] = $"{serverUrl}/Items/{itemId}/Images/Thumbnail";
        }

        // Logo image URL
        if (data.TryGetValue("LogoImage", out var logoObj) && logoObj is string logoUrl && !string.IsNullOrEmpty(logoUrl))
        {
            if (!logoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["LogoImage"] = $"{serverUrl}/Items/{itemId}/Images/Logo";
            }
        }
        else
        {
            data["LogoImage"] = $"{serverUrl}/Items/{itemId}/Images/Logo";
        }

        // Banner image URL
        if (data.TryGetValue("BannerImage", out var bannerObj) && bannerObj is string bannerUrl && !string.IsNullOrEmpty(bannerUrl))
        {
            if (!bannerUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                data["BannerImage"] = $"{serverUrl}/Items/{itemId}/Images/Banner";
            }
        }
        else
        {
            data["BannerImage"] = $"{serverUrl}/Items/{itemId}/Images/Banner";
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
                        // Also update PrimaryImage so {{PrimaryImage}} resolves to a
                        // publicly accessible CDN URL (instead of a local Jellyfin URL that
                        // services like Telegram cannot fetch)
                        data["PrimaryImage"] = image.Url;
                        break;
                    case ImageType.Backdrop:
                        data["TmdbBackdropUrl"] = image.Url;
                        // Also update BackdropImage for the same reason
                        data["BackdropImage"] = image.Url;
                        break;
                    case ImageType.Logo:
                        data["TmdbLogoUrl"] = image.Url;
                        data["LogoImage"] = image.Url;
                        break;
                    case ImageType.Thumb:
                        data["TmdbStillUrl"] = image.Url;
                        data["ThumbImage"] = image.Url;
                        break;
                }
            }

            var urlCount = remoteImages.Count(i => !string.IsNullOrEmpty(i.Url));
            _logger.LogDebug("Enriched TMDB image URLs for item {ItemId}: {Count} URL(s)", itemId, urlCount);

            // Enrich parent-level poster URLs (Season/Series) for hierarchical items.
            // This allows users to reference {{TmdbSeasonPosterUrl}} or {{SeriesPoster}}
            // regardless of the current item type.
            await EnrichParentPosterUrls(data, item).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enriching data with TMDB image URLs for item {ItemId}", itemId);
        }
    }

    /// <summary>
    /// Enriches data with media stream information (codec, resolution, framerate, audio channels, etc.)
    /// by querying <see cref="IMediaSourceManager.GetMediaStreams(System.Guid)"/>.
    /// </summary>
    private void EnrichWithMediaStreams(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("ItemId", out var itemIdObj) || itemIdObj is not string itemIdStr || string.IsNullOrEmpty(itemIdStr))
        {
            return;
        }

        if (!Guid.TryParse(itemIdStr, out var itemId))
        {
            return;
        }

        try
        {
            var streams = _mediaSourceManager.GetMediaStreams(itemId);
            if (streams is null || streams.Count == 0)
            {
                return;
            }

            // Process first video stream
            var videoStream = streams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (videoStream is not null)
            {
                data["VideoCodec"] = videoStream.Codec ?? string.Empty;
                data["VideoProfile"] = videoStream.Profile ?? string.Empty;
                data["VideoBitrate"] = videoStream.BitRate?.ToString(CultureInfo.InvariantCulture) ?? "0";
                data["VideoBitrateText"] = DataObjectHelpers.FormatBitrate(videoStream.BitRate);
                data["VideoResolution"] = FormatResolution(videoStream);
                data["VideoRange"] = videoStream.VideoRangeType != VideoRangeType.Unknown
                    ? videoStream.VideoRangeType.ToString()
                    : videoStream.VideoRange != VideoRange.Unknown
                        ? videoStream.VideoRange.ToString()
                        : string.Empty;
                data["Framerate"] = (videoStream.RealFrameRate ?? videoStream.AverageFrameRate)?.ToString("F3", CultureInfo.InvariantCulture) ?? string.Empty;
            }

            // Process first audio stream
            var audioStream = streams.FirstOrDefault(s => s.Type == MediaStreamType.Audio);
            if (audioStream is not null)
            {
                data["AudioCodec"] = audioStream.Codec ?? string.Empty;
                data["AudioChannels"] = !string.IsNullOrEmpty(audioStream.ChannelLayout)
                    ? audioStream.ChannelLayout
                    : FormatChannels(audioStream.Channels);
                data["AudioLanguage"] = audioStream.Language ?? string.Empty;
                data["AudioBitrate"] = audioStream.BitRate?.ToString(CultureInfo.InvariantCulture) ?? "0";
                data["AudioBitrateText"] = DataObjectHelpers.FormatBitrate(audioStream.BitRate);
            }

            // Process subtitle languages
            var subtitleLanguages = streams
                .Where(s => s.Type == MediaStreamType.Subtitle && !string.IsNullOrEmpty(s.Language))
                .Select(s => s.Language)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            data["SubtitleLanguages"] = subtitleLanguages.Count > 0
                ? string.Join(", ", subtitleLanguages)
                : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enriching media stream info for item {ItemId}", itemIdStr);
        }
    }

    /// <summary>
    /// Formats a channel count to a human-readable string (e.g., 6 → "5.1", 2 → "2.0").
    /// </summary>
    private static string FormatChannels(int? channels)
    {
        if (!channels.HasValue || channels.Value <= 0)
        {
            return string.Empty;
        }

        return channels.Value switch
        {
            1 => "1.0",
            2 => "2.0",
            3 => "2.1",
            4 => "4.0",
            5 => "5.0",
            6 => "5.1",
            7 => "6.1",
            8 => "7.1",
            _ => $"{channels}.0"
        };
    }

    /// <summary>
    /// Formats video stream resolution to a human-readable string (e.g., "1080p", "4K", "720p").
    /// Classifies by the larger dimension to handle letterboxed content (e.g. 1920×800 → "1080p").
    /// </summary>
    private static string FormatResolution(MediaStream videoStream)
    {
        if (!videoStream.Width.HasValue || !videoStream.Height.HasValue)
        {
            return string.Empty;
        }

        var width = videoStream.Width.Value;
        var height = videoStream.Height.Value;
        var interlaced = videoStream.IsInterlaced ? "i" : "p";
        var maxDim = Math.Max(width, height);

        if (maxDim <= 256)
        {
            return $"144{interlaced}";
        }

        if (maxDim <= 426)
        {
            return $"240{interlaced}";
        }

        if (maxDim <= 640)
        {
            return $"360{interlaced}";
        }

        if (maxDim <= 720)
        {
            return $"404{interlaced}";
        }

        if (maxDim <= 854)
        {
            return $"480{interlaced}";
        }

        if (maxDim <= 960)
        {
            return $"540{interlaced}";
        }

        if (maxDim <= 1024)
        {
            return $"576{interlaced}";
        }

        if (maxDim <= 1280)
        {
            return $"720{interlaced}";
        }

        if (maxDim <= 1920)
        {
            return $"1080{interlaced}";
        }

        if (maxDim <= 3840)
        {
            return "4K";
        }

        if (maxDim <= 7680)
        {
            return "8K";
        }

        return $"{height}p";
    }

    /// <summary>
    /// Enriches data with people info (Director, Writers, CastList, CastJson) by querying
    /// <see cref="ILibraryManager.GetPeople(MediaBrowser.Controller.Entities.BaseItem)"/>.
    /// </summary>
    private async Task EnrichWithPeople(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("ItemId", out var itemIdObj) || itemIdObj is not string itemIdStr || string.IsNullOrEmpty(itemIdStr))
        {
            return;
        }

        if (!Guid.TryParse(itemIdStr, out var itemId))
        {
            return;
        }

        try
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                return;
            }

            var people = _libraryManager.GetPeople(item);
            if (people is null || people.Count == 0)
            {
                return;
            }

            // Director — first person of type Director
            var director = people.FirstOrDefault(p => p.Type == PersonKind.Director);
            data["Director"] = director?.Name ?? string.Empty;

            // Writers — all persons of type Writer
            var writers = people.Where(p => p.Type == PersonKind.Writer).Select(p => p.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            data["Writers"] = writers.Count > 0 ? string.Join(", ", writers) : string.Empty;

            // CastList — top N actors (default 5), comma-separated
            var castLimit = 5;
            var castMembers = people
                .Where(p => p.Type == PersonKind.Actor && !string.IsNullOrEmpty(p.Name))
                .OrderBy(p => p.SortOrder ?? int.MaxValue)
                .Take(castLimit)
                .Select(p => p.Name)
                .ToList();

            data["CastList"] = castMembers.Count > 0 ? string.Join(", ", castMembers) : string.Empty;

            // CastJson — structured JSON array of all cast members
            var castJsonList = people
                .Where(p => p.Type == PersonKind.Actor && !string.IsNullOrEmpty(p.Name))
                .OrderBy(p => p.SortOrder ?? int.MaxValue)
                .Select(p => new { name = p.Name, role = p.Role ?? string.Empty })
                .ToList();

            data["CastJson"] = castJsonList.Count > 0
                ? JsonSerializer.Serialize(castJsonList)
                : "[]";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enriching people data for item {ItemId}", itemIdStr);
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
            data[$"{prefix}Poster"] = $"{serverUrl}/Items/{parentIdStr}/Images/Primary";
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
                        data[$"{prefix}Poster"] = image.Url;
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
    /// Copies the current item's poster URLs (PrimaryImage, TmdbPosterUrl) into
    /// prefixed keys for the given <paramref name="prefix"/> (e.g. "Season", "Series").
    /// Used when the current item IS the parent (e.g. a Series item should have
    /// SeriesPoster = PrimaryImage).
    /// </summary>
    private static void CopySelfPosterToPrefixed(Dictionary<string, object> data, string prefix)
    {
        if (data.TryGetValue("PrimaryImage", out var primary) && primary is string primaryStr)
        {
            data[$"{prefix}Poster"] = primaryStr;
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
