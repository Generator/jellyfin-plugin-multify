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
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify;

/// <summary>
/// Routes notifications to configured destinations.
/// </summary>
public class MultifySender : IWebhookSender
{
    /// <summary>
    /// Default delay in seconds between sequential notifications of the same service type.
    /// Used when AdvancedSettings.DelaySeconds is not configured.
    /// </summary>
    private const int DefaultDelaySeconds = 2;

    private readonly ILogger<MultifySender> _logger;
    private readonly IWebhookClient<TelegramOption> _telegramClient;
    private readonly IWebhookClient<GotifyOption> _gotifyClient;
    private readonly IWebhookClient<NtfyOption> _ntfyClient;
    private readonly IWebhookClient<GenericWebhookOption> _genericClient;
    private readonly MdblistService? _mdblistService;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ImageEnrichmentService _imageEnrichmentService;

    // Live configuration — read per call from plugin instance so UI Save (UpdateConfiguration)
    // is reflected without restart (like WebhookSender in jellyfin-plugin-webhook).
    // Injected PluginConfiguration would be stale when held by a singleton (LibraryEventHostedService).
    private static PluginConfiguration Configuration => MultifyPlugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifySender"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifySender}"/> interface.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/>.</param>
    /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> for querying media streams.</param>
    /// <param name="imageEnrichmentService">Instance of the <see cref="ImageEnrichmentService"/> for image enrichment.</param>
    /// <param name="mdblistService">Instance of the <see cref="MdblistService"/>.</param>
    public MultifySender(
        ILogger<MultifySender> logger,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        ImageEnrichmentService imageEnrichmentService,
        MdblistService? mdblistService = null)
    {
        _logger = logger;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _imageEnrichmentService = imageEnrichmentService;
        _mdblistService = mdblistService;
    }

    private static readonly string[] MdblistTemplateKeys =
    [
        "MdblistScore",
        "ImdbRating",
        "TmdbRating",
        "RottenTomatoesRating",
        "MetacriticRating",
        "LetterboxdRating",
        "PopcornRating",
        "AnilistRating",
        "TraktRating",
        "MyAnimeListRating",
        "RogerEbertRating"
    ];

    private static bool TemplateContainsMdblistKey(string? base64Template)
    {
        if (string.IsNullOrEmpty(base64Template))
        {
            return false;
        }

        string template;
        try
        {
            var bytes = Convert.FromBase64String(base64Template);
            template = System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            template = base64Template;
        }

        return RawTemplateContainsMdblistKey(template);
    }

    private static bool RawTemplateContainsMdblistKey(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return false;
        }

        foreach (var key in MdblistTemplateKeys)
        {
            if (template.Contains("{{" + key + "}}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OptionReferencesMdblist(BaseOption option)
    {
        if (option.SendAllProperties)
        {
            return true;
        }

        if (TemplateContainsMdblistKey(option.Template))
        {
            return true;
        }

        // Check option-type-specific templated fields (Title/PhotoUrlTemplate/Tags are raw strings, not base64)
        if (option is GotifyOption gotify)
        {
            if (RawTemplateContainsMdblistKey(gotify.Title) || RawTemplateContainsMdblistKey(gotify.PhotoUrlTemplate))
            {
                return true;
            }
        }
        else if (option is NtfyOption ntfy)
        {
            if (RawTemplateContainsMdblistKey(ntfy.Title) || RawTemplateContainsMdblistKey(ntfy.Tags) || RawTemplateContainsMdblistKey(ntfy.PhotoUrlTemplate))
            {
                return true;
            }
        }
        else if (option is TelegramOption telegram)
        {
            if (RawTemplateContainsMdblistKey(telegram.PhotoUrlTemplate))
            {
                return true;
            }
        }

        return false;
    }

    private bool NeedsMdblistEnrichment(NotificationType notificationType)
    {
        if (_mdblistService == null || string.IsNullOrEmpty(Configuration.MdblistApiKey))
        {
            return false;
        }

        var telegramOptions = Configuration.TelegramOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var gotifyOptions = Configuration.GotifyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var ntfyOptions = Configuration.NtfyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var genericOptions = Configuration.GenericWebhookOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();

        return NeedsMdblistEnrichmentForOptions(telegramOptions, gotifyOptions, ntfyOptions, genericOptions);
    }

#pragma warning disable CA1859 // Performance - keep BaseOption abstraction for shared logic
    private static bool NeedsMdblistEnrichmentForOptions(
        IReadOnlyList<BaseOption> telegramOptions,
        IReadOnlyList<BaseOption> gotifyOptions,
        IReadOnlyList<BaseOption> ntfyOptions,
        IReadOnlyList<BaseOption> genericOptions)
#pragma warning restore CA1859
    {
        if (telegramOptions.Count == 0 && gotifyOptions.Count == 0 && ntfyOptions.Count == 0 && genericOptions.Count == 0)
        {
            return false;
        }

        foreach (var opt in telegramOptions)
        {
            if (OptionReferencesMdblist(opt))
            {
                return true;
            }
        }

        foreach (var opt in gotifyOptions)
        {
            if (OptionReferencesMdblist(opt))
            {
                return true;
            }
        }

        foreach (var opt in ntfyOptions)
        {
            if (OptionReferencesMdblist(opt))
            {
                return true;
            }
        }

        foreach (var opt in genericOptions)
        {
            if (OptionReferencesMdblist(opt))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task SendNotification(NotificationType notificationType, Dictionary<string, object> itemData, Type? itemType = null)
    {
        _logger.LogDebug("SendNotification called for {NotificationType}, ItemType={ItemType}", notificationType, itemType?.Name ?? "null");

        // Trailer items must not generate notifications. Skip them uniformly
        // across all notification types.
        if (itemType is not null &&
            (itemType == typeof(Trailer) || itemType.Name.Equals("Trailer", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogDebug("Skipping notification for Trailer item type ({NotificationType})", notificationType);
            return;
        }

        // Pre-resolve matched destinations for this notification type — reused for
        // both the MDBList guard and the send phase to avoid double LINQ enumeration.
        var telegramOptions = Configuration.TelegramOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var gotifyOptions = Configuration.GotifyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var ntfyOptions = Configuration.NtfyOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();
        var genericOptions = Configuration.GenericWebhookOptions.Where(o => o.NotificationTypes.Contains(notificationType)).ToList();

        // Enrich data with MDBList ratings only when at least one matched destination
        // actually references an MDBList variable in its template (or sends all properties).
        // This prevents the 1 Hz PlaybackProgress hot-loop from burning the daily quota
        // when no one is listening or no template uses {{MdblistScore}}/{{ImdbRating}}/etc.
        if (_mdblistService != null
            && !string.IsNullOrEmpty(Configuration.MdblistApiKey)
            && NeedsMdblistEnrichmentForOptions(telegramOptions, gotifyOptions, ntfyOptions, genericOptions))
        {
            await EnrichWithMdblistRatings(itemData).ConfigureAwait(false);
        }

        // Enrich data with item URL if ServerUrl is configured
        if (!string.IsNullOrEmpty(Configuration.ServerUrl))
        {
            EnrichWithItemUrl(itemData);
        }

        // Single item lookup shared across enrichment methods to avoid redundant DB queries.
        // Guarded: library lookups can throw for transient/DB errors and must not
        // prevent notification delivery.
        BaseItem? item = null;
        string? itemIdStr = null;
        if (itemData.TryGetValue("ItemId", out var itemIdObj) && itemIdObj is string idStr && Guid.TryParse(idStr, out var itemIdGuid))
        {
            itemIdStr = idStr;
            try
            {
                item = _libraryManager.GetItemById(itemIdGuid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error looking up item {ItemId} for enrichment", idStr);
            }
        }

        // Correct LibraryName/LibraryId to CollectionFolder (not physical Folder) via VirtualFolders path prefix.
        // DataObjectHelpers.GetTopParent() returns physical Folder (e.g. a186… "movies") while config stores
        // CollectionFolder (e.g. 04f… "Private - Filmes"). Without correction, {{LibraryName}} shows "movies"
        // instead of "Private - Filmes" and FilterService must handle alias via path.
        CorrectLibraryInfo(itemData, item);

        // Enrich data with TMDB image URLs via Jellyfin's provider system
        if (item != null)
        {
            await _imageEnrichmentService.EnrichWithTmdbImages(itemData, item, itemIdStr).ConfigureAwait(false);

            // Enrich parent-level poster URLs (Season/Series) for hierarchical items.
            // This allows users to reference {{TmdbSeasonPosterUrl}} or {{SeriesPoster}}
            // regardless of the current item type.
            await _imageEnrichmentService.EnrichParentPosterUrls(itemData, item, Configuration.ServerUrl).ConfigureAwait(false);
        }
        else
        {
            _logger.LogTrace("No ItemId in data, skipping TMDB image enrichment");
        }

        // Enrich data with media stream info (codec, resolution, framerate, etc.)
        EnrichWithMediaStreams(itemData);

        // Enrich data with people info (Director, Writers, CastList, CastJson) — reuses item from above
        EnrichWithPeople(itemData, item);

        _logger.LogDebug(
            "DEBUG-ENRICH: ItemId={HasItemId} ItemUrl={HasItemUrl} ItemShortId={HasItemShortId} ServerUrl='{ServerUrl}'",
            itemData.ContainsKey("ItemId"),
            itemData.ContainsKey("ItemUrl"),
            itemData.ContainsKey("ItemShortId"),
            Configuration.ServerUrl);

        var tasks = new List<Task>();

        var telegramCount = telegramOptions.Count;
        var gotifyCount = gotifyOptions.Count;
        var ntfyCount = ntfyOptions.Count;
        var genericCount = genericOptions.Count;

        _logger.LogDebug(
            "Matching destinations: Telegram={Telegram}, Gotify={Gotify}, ntfy={Ntfy}, Generic={Generic}",
            telegramCount,
            gotifyCount,
            ntfyCount,
            genericCount);

        // Get delay from advanced settings (convert seconds to milliseconds)
        var delayMs = Math.Max(0, (Configuration.AdvancedSettings?.DelaySeconds ?? DefaultDelaySeconds) * 1000);

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
                var ratings = await _mdblistService!.GetRatingsAsync(Configuration.MdblistApiKey, imdbId, mediaType).ConfigureAwait(false);
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
                var ratings = await _mdblistService!.GetRatingsByTmdbAsync(Configuration.MdblistApiKey, tmdbId, mediaType).ConfigureAwait(false);
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

        var serverUrl = Configuration.ServerUrl.TrimEnd('/');
        data["ServerUrl"] = serverUrl;
        var itemUrl = $"{serverUrl}/web/#/details?id={itemId}";
        data["ItemUrl"] = itemUrl;

        // Generate short ID (first 10 chars of GUID without dashes)
        var noDash = itemId.Replace("-", string.Empty, StringComparison.Ordinal);
        var shortId = noDash.Length > 10 ? noDash[..10] : noDash;
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
            return $"480{interlaced}";
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
    /// All work is synchronous, so this is intentionally not async.
    /// </summary>
    private void EnrichWithPeople(Dictionary<string, object> data, BaseItem? item = null)
    {
        if (item == null)
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
                item = _libraryManager.GetItemById(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error looking up item for people enrichment");
                return;
            }
        }

        if (item is null)
        {
            return;
        }

        try
        {
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
            _logger.LogWarning(ex, "Error enriching people data");
        }
    }

    private static string GetMediaType(Dictionary<string, object> data)
    {
        if (data.TryGetValue("ItemType", out var itemTypeObj) && itemTypeObj is string itemType)
        {
            // Normalize to lower for exact matching; keep Contains fallback for custom/subclass names
            var lower = itemType.ToLowerInvariant();
            return lower switch
            {
                "movie" => "movie",
                "episode" => "show",
                "series" => "show",
                "season" => "show",
                _ when itemType.Contains("Movie", StringComparison.OrdinalIgnoreCase) => "movie",
                _ when itemType.Contains("Episode", StringComparison.OrdinalIgnoreCase)
                    || itemType.Contains("Series", StringComparison.OrdinalIgnoreCase)
                    || itemType.Contains("Season", StringComparison.OrdinalIgnoreCase) => "show",
                _ => "movie"
            };
        }

        return "movie";
    }

    private void CorrectLibraryInfo(Dictionary<string, object> data, BaseItem? item)
    {
        try
        {
            var path = string.Empty;
            if (data.TryGetValue("Path", out var pathObj) && pathObj is string p && !string.IsNullOrEmpty(p))
            {
                path = p;
            }
            else if (item?.Path != null)
            {
                path = item.Path;
            }

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var virtualFolders = _libraryManager.GetVirtualFolders();
            if (virtualFolders == null || virtualFolders.Count == 0)
            {
                return;
            }

            foreach (var vf in virtualFolders)
            {
                if (vf.Locations == null)
                {
                    continue;
                }

                foreach (var loc in vf.Locations)
                {
                    if (!string.IsNullOrEmpty(loc) && path.StartsWith(loc, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found matching collection — override LibraryName/LibraryId to collection (not physical Folder)
                        data["LibraryName"] = vf.Name;
                        // Use ItemId as stored in VirtualFolder (N format) — FilterService normalizes
                        data["LibraryId"] = vf.ItemId;
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error correcting library info for {Path}", data.TryGetValue("Path", out var po) ? po : "unknown");
        }
    }

    private static bool NotifyOnItem<T>(T baseOptions, Type? itemType)
        where T : BaseOption
    {
        if (itemType is null)
        {
            return true;
        }

        // Specific type checks first — each also matches subclasses so e.g. a
        // MusicVideo (a Video subclass) never double-matches a different flag.
        if (baseOptions.EnableMovies && typeof(Movie).IsAssignableFrom(itemType))
        {
            return true;
        }

        if (baseOptions.EnableEpisodes && typeof(Episode).IsAssignableFrom(itemType))
        {
            return true;
        }

        if (baseOptions.EnableSeries && typeof(Series).IsAssignableFrom(itemType))
        {
            return true;
        }

        if (baseOptions.EnableSeasons && typeof(Season).IsAssignableFrom(itemType))
        {
            return true;
        }

        if (baseOptions.EnableAlbums && typeof(MusicAlbum).IsAssignableFrom(itemType))
        {
            return true;
        }

        if (baseOptions.EnableSongs && typeof(Audio).IsAssignableFrom(itemType))
        {
            return true;
        }

        // Video is the base class of Movie/Episode, so it must be checked last to
        // preserve the specific flag precedence above.
        if (baseOptions.EnableVideos && typeof(Video).IsAssignableFrom(itemType))
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
            // Strings and value types are immutable — safe to share. Enum check works for boxed enums.
            if (kvp.Value is string or int or long or float or double or bool or Guid or DateTime or Enum)
            {
                copy[kvp.Key] = kvp.Value;
            }
            else if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                var nestedComparer = nestedDict is Dictionary<string, object> concrete
                    ? concrete.Comparer
                    : StringComparer.Ordinal;
                copy[kvp.Key] = DeepCopyDict(new Dictionary<string, object>(nestedDict, nestedComparer));
            }
            else if (kvp.Value is System.Collections.IList list)
            {
                // Deep copy list elements that are dictionaries/lists; otherwise share immutable values.
                var listCopy = new List<object>(list.Count);
                foreach (var item in list)
                {
                    if (item is IDictionary<string, object> dictItem)
                    {
                        var dictComparer = dictItem is Dictionary<string, object> concreteDict
                            ? concreteDict.Comparer
                            : StringComparer.Ordinal;
                        listCopy.Add(DeepCopyDict(new Dictionary<string, object>(dictItem, dictComparer)));
                    }
                    else if (item is System.Collections.IList innerList)
                    {
                        // Recursively copy nested lists (e.g. list of lists)
                        var innerCopy = new List<object>(innerList.Count);
                        foreach (var innerItem in innerList)
                        {
                            innerCopy.Add(innerItem);
                        }

                        listCopy.Add(innerCopy);
                    }
                    else
                    {
                        listCopy.Add(item);
                    }
                }

                copy[kvp.Key] = listCopy;
            }
            else
            {
                // Unknown reference type — share reference (best effort). Documented limitation:
                // callers must not mutate such values after DeepCopyDict.
                copy[kvp.Key] = kvp.Value;
            }
        }

        return copy;
    }
}
