using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Request model for test notification.
/// </summary>
public class TestNotificationRequest
{
    /// <summary>Gets or sets the destination type (telegram, gotify, ntfy, generic).</summary>
    [JsonPropertyName("destinationType")]
    public string DestinationType { get; set; } = string.Empty;

    /// <summary>Gets or sets the destination configuration as JSON.</summary>
    [JsonPropertyName("config")]
    public JsonElement Config { get; set; }
}

/// <summary>
/// Response model for test notification.
/// </summary>
public class TestNotificationResponse
{
    /// <summary>Gets or sets whether the test was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Gets or sets the error message if failed.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for the test notification service.
/// </summary>
public interface IMultifyTestService
{
    /// <summary>
    /// Sends a test notification to the specified destination.
    /// </summary>
    /// <param name="request">The test notification request.</param>
    /// <returns>A task representing the async operation.</returns>
    Task<TestNotificationResponse> SendTestNotificationAsync(TestNotificationRequest request);
}

/// <summary>
/// Service for sending test notifications.
/// </summary>
public class MultifyTestService : IMultifyTestService
{
    private readonly ILogger<MultifyTestService> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IWebhookClient<TelegramOption> _telegramClient;
    private readonly IWebhookClient<GotifyOption> _gotifyClient;
    private readonly IWebhookClient<NtfyOption> _ntfyClient;
    private readonly IWebhookClient<GenericWebhookOption> _genericClient;
    private readonly ImageEnrichmentService _imageEnrichmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifyTestService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifyTestService}"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface for querying real library items.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    /// <param name="imageEnrichmentService">Instance of the <see cref="ImageEnrichmentService"/> for image enrichment.</param>
    public MultifyTestService(
        ILogger<MultifyTestService> logger,
        ILibraryManager libraryManager,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient,
        ImageEnrichmentService imageEnrichmentService)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
        _imageEnrichmentService = imageEnrichmentService;
    }

    /// <inheritdoc />
    public async Task<TestNotificationResponse> SendTestNotificationAsync(TestNotificationRequest request)
    {
        try
        {
            var option = ParseOption(request);

            if (option == null)
            {
                return new TestNotificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Unsupported destination type: {request.DestinationType}"
                };
            }

            // Try to fetch a real item from the library first
            var data = await TryFetchRealItemAsync(option).ConfigureAwait(false);
            if (data == null)
            {
                _logger.LogDebug("No real item found, falling back to hardcoded test data");
                data = CreateTestData();
            }

            // Ensure webhook is enabled for test and use template (not raw JSON)
            option.EnableWebhook = true;
            option.SendAllProperties = false;

            // Clear TmdbId so test messages are always sent as new (never edited)
            // Test data keeps all other template variables intact
            data["TmdbId"] = string.Empty;

            await SendAsync(request.DestinationType, option, data).ConfigureAwait(false);

            _logger.LogDebug("Test notification sent successfully to {DestinationType}", request.DestinationType);

            return new TestNotificationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending test notification to {DestinationType}", request.DestinationType);

            return new TestNotificationResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Tries to fetch a real library item matching the destination's LibraryFilter,
    /// and builds test data with real metadata. Falls back to null when no items are found.
    /// </summary>
    private async Task<Dictionary<string, object>?> TryFetchRealItemAsync(BaseOption option)
    {
        try
        {
            // Build IncludeItemTypes from destination's Enable* flags
            var includeTypes = new List<BaseItemKind>();
            if (option.EnableMovies)
            {
                includeTypes.Add(BaseItemKind.Movie);
            }

            if (option.EnableSeries)
            {
                includeTypes.Add(BaseItemKind.Series);
            }

            if (option.EnableEpisodes)
            {
                includeTypes.Add(BaseItemKind.Episode);
            }

            if (option.EnableSeasons)
            {
                includeTypes.Add(BaseItemKind.Season);
            }

            // Fall back to all types if none enabled (defensive — at least one should be)
            if (includeTypes.Count == 0)
            {
                includeTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.Season];
            }

            var query = new InternalItemsQuery
            {
                Limit = 1,
                Recursive = true,
                IncludeItemTypes = [.. includeTypes]
            };

            // Apply LibraryFilter if configured
            if (option.LibraryFilter is { Length: > 0 })
            {
                var rootFolder = _libraryManager.RootFolder;
                if (rootFolder is null)
                {
                    return null;
                }

                var folderIds = new List<Guid>();
                var virtualChildren = rootFolder.VirtualChildren;
                foreach (var filterValue in option.LibraryFilter)
                {
                    if (virtualChildren is null)
                    {
                        continue;
                    }

                    // The config UI stores library IDs in LibraryFilter. Match by ID
                    // first, then fall back to name matching for older saved configs.
                    var matchedFolder = virtualChildren
                        .OfType<Folder>()
                        .FirstOrDefault(child => string.Equals(child.Id.ToString("N", CultureInfo.InvariantCulture), filterValue, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(child.Id.ToString(), filterValue, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(child.Name, filterValue, StringComparison.OrdinalIgnoreCase));

                    if (matchedFolder is not null)
                    {
                        folderIds.Add(matchedFolder.Id);
                    }
                }

                if (folderIds.Count == 0)
                {
                    _logger.LogDebug("No library folders matched LibraryFilter, falling back to hardcoded test data");
                    return null;
                }

                query.AncestorIds = [.. folderIds];
            }

            var items = _libraryManager.GetItemList(query);
            var item = items.Count > 0 ? items[0] : null;
            if (item is null)
            {
                _logger.LogDebug("No items found in library, falling back to hardcoded test data");
                return null;
            }

            _logger.LogInformation("Using real item for test notification: {ItemName} ({ItemType})", item.Name, item.GetType().Name);

            // Start with fallback defaults (ensures all possible template keys exist)
            var data = CreateDefaultTestData();

            // Overwrite with specifically-set base fields
            data["ServerName"] = "Jellyfin";
            data["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture);
            data["NotificationType"] = "PlaybackStart";
            data["Title"] = "Test Notification";
            data["Body"] = "This is a test message using real item data from your library.";

            // Overwrite with real item data from AddItemData
            var itemData = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemAdded);
            itemData = itemData.AddItemData(item);
            foreach (var kvp in itemData)
            {
                data[kvp.Key] = kvp.Value;
            }

            // Add item-type-specific fields not covered by AddItemData
            if (item is Movie movie)
            {
                data["Year"] = movie.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            }
            else if (item is Episode episode)
            {
                data["SeriesName"] = episode.SeriesName ?? "N/A";
                data["SeasonNumber"] = (episode.ParentIndexNumber ?? 0).ToString(CultureInfo.InvariantCulture);
                data["SeasonNumber00"] = (episode.ParentIndexNumber ?? 0).ToString("00", CultureInfo.InvariantCulture);
                data["SeasonNumber000"] = (episode.ParentIndexNumber ?? 0).ToString("000", CultureInfo.InvariantCulture);
                data["EpisodeNumber"] = (episode.IndexNumber ?? 0).ToString(CultureInfo.InvariantCulture);
                data["EpisodeNumber00"] = (episode.IndexNumber ?? 0).ToString("00", CultureInfo.InvariantCulture);
                data["EpisodeNumber000"] = (episode.IndexNumber ?? 0).ToString("000", CultureInfo.InvariantCulture);
                data["Year"] = episode.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            }
            else if (item is Series series)
            {
                data["Year"] = series.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
                data["SeriesStatus"] = series.Status?.ToString() ?? "N/A";
            }
            else if (item is Season season)
            {
                data["SeriesName"] = season.Series?.Name ?? "N/A";
                data["SeasonNumber"] = (season.IndexNumber ?? 0).ToString(CultureInfo.InvariantCulture);
                data["SeasonNumber00"] = (season.IndexNumber ?? 0).ToString("00", CultureInfo.InvariantCulture);
                data["SeasonNumber000"] = (season.IndexNumber ?? 0).ToString("000", CultureInfo.InvariantCulture);
                data["Year"] = season.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            }

            // Populate external provider image URLs from Jellyfin image URLs
            // (same mapping as MultifySender.EnrichImageUrls for the real flow)
            var serverUrl = MultifyPlugin.Instance?.Configuration?.ServerUrl;
            if (!string.IsNullOrEmpty(serverUrl) && data.TryGetValue("ItemId", out var itemIdObj) && itemIdObj is string itemId && !string.IsNullOrEmpty(itemId))
            {
                serverUrl = serverUrl.TrimEnd('/');
                data["ServerUrl"] = serverUrl;
                var primaryUrl = $"{serverUrl}/Items/{itemId}/Images/Primary";
                var backdropUrl = $"{serverUrl}/Items/{itemId}/Images/Backdrop";
                var thumbUrl = $"{serverUrl}/Items/{itemId}/Images/Thumbnail";
                var logoUrl = $"{serverUrl}/Items/{itemId}/Images/Logo";
                var bannerUrl = $"{serverUrl}/Items/{itemId}/Images/Banner";

                // Item URL and short ID
                var itemUrl = $"{serverUrl}/web/#/details?id={itemId}";
                var shortId = itemId.Replace("-", string.Empty, StringComparison.Ordinal);
                shortId = shortId.Length > 10 ? shortId[..10] : shortId;
                data["ItemUrl"] = itemUrl;
                data["ItemShortId"] = shortId;

                // Jellyfin image URLs
                data["PrimaryImage"] = primaryUrl;
                data["BackdropImage"] = backdropUrl;
                data["ThumbImage"] = thumbUrl;
                data["LogoImage"] = logoUrl;
                data["BannerImage"] = bannerUrl;

                // Start with Jellyfin local URLs as fallback
                data["TmdbPosterUrl"] = primaryUrl;
                data["TmdbBackdropUrl"] = backdropUrl;
                data["TmdbProfileUrl"] = primaryUrl;
                data["TmdbStillUrl"] = thumbUrl;
                data["TmdbLogoUrl"] = logoUrl;

                // Now overwrite with real TMDB CDN URLs via provider system
                await _imageEnrichmentService.EnrichWithTmdbImages(data, item).ConfigureAwait(false);

                // Enrich parent-level poster URLs (Season/Series) for hierarchical items
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    await _imageEnrichmentService.EnrichParentPosterUrls(data, item, serverUrl).ConfigureAwait(false);
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching real item for test notification, falling back to hardcoded data");
            return null;
        }
    }

    private static Dictionary<string, object> CreateDefaultTestData()
    {
        return new Dictionary<string, object>
        {
            // Base
            ["Title"] = "N/A",
            ["Body"] = "N/A",
            ["Timestamp"] = "N/A",
            ["ServerName"] = "N/A",
            ["NotificationType"] = "N/A",

            // Item
            ["ItemId"] = "N/A",
            ["ItemName"] = "N/A",
            ["ItemType"] = "N/A",
            ["LibraryName"] = "N/A",
            ["LibraryId"] = "N/A",
            ["ItemUrl"] = "N/A",
            ["ItemShortId"] = "N/A",
            ["ProductionYear"] = "N/A",
            ["Overview"] = "N/A",
            ["Genres"] = "N/A",
            ["PremiereDate"] = "N/A",
            ["Runtime"] = "N/A",
            ["OfficialRating"] = "N/A",
            ["CommunityRating"] = "N/A",
            ["CriticRating"] = "N/A",
            ["Tagline"] = "N/A",
            ["OriginalTitle"] = "N/A",
            ["Studios"] = "N/A",
            ["ProductionLocations"] = "N/A",
            ["Tags"] = "N/A",
            ["Path"] = "N/A",
            ["Container"] = "N/A",
            ["DateCreated"] = "N/A",

            // TV
            ["SeriesName"] = "N/A",
            ["SeasonNumber"] = "N/A",
            ["SeasonNumber00"] = "N/A",
            ["SeasonNumber000"] = "N/A",
            ["SeasonName"] = "N/A",
            ["EpisodeNumber"] = "N/A",
            ["EpisodeNumber00"] = "N/A",
            ["EpisodeNumber000"] = "N/A",
            ["SeriesStatus"] = "N/A",

            // Provider
            ["ImdbId"] = "N/A",
            ["TmdbId"] = "N/A",
            ["TvdbId"] = "N/A",

            // User
            ["UserId"] = "N/A",
            ["Username"] = "N/A",

            // Session
            ["Client"] = "N/A",
            ["DeviceName"] = "N/A",
            ["RemoteEndPoint"] = "N/A",
            ["SessionId"] = "N/A",
            ["PlayMethod"] = "N/A",
            ["IsPaused"] = "False",
            ["VolumeLevel"] = "N/A",
            ["IsMuted"] = "False",
            ["CanSeek"] = "N/A",
            ["AudioStreamIndex"] = "N/A",
            ["SubtitleStreamIndex"] = "N/A",
            ["RepeatMode"] = "N/A",
            ["PlaybackOrder"] = "N/A",
            ["MediaSourceId"] = "N/A",
            ["LiveStreamId"] = "N/A",

            // Playback
            ["PlaybackPositionTicks"] = "0",
            ["PlaybackPosition"] = "00:00:00",
            ["IsAutomated"] = "False",
            ["PlaySessionId"] = "N/A",
            ["PlayedToCompletion"] = "False",

            // Ratings
            ["MdblistScore"] = "N/A",
            ["ImdbRating"] = "N/A",
            ["TmdbRating"] = "N/A",
            ["RottenTomatoesRating"] = "N/A",
            ["MetacriticRating"] = "N/A",
            ["LetterboxdRating"] = "N/A",
            ["PopcornRating"] = "N/A",
            ["TraktRating"] = "N/A",
            ["MyAnimeListRating"] = "N/A",
            ["AnilistRating"] = "N/A",
            ["RogerEbertRating"] = "N/A",

            // Images
            ["PrimaryImage"] = "N/A",
            ["BackdropImage"] = "N/A",
            ["ThumbImage"] = "N/A",
            ["LogoImage"] = "N/A",
            ["BannerImage"] = "N/A",

            // Trailer
            ["TrailerUrl"] = "N/A",
            ["TrailerYtId"] = "N/A",

            // TMDB Images
            ["TmdbPosterUrl"] = "N/A",
            ["TmdbBackdropUrl"] = "N/A",
            ["TmdbProfileUrl"] = "N/A",
            ["TmdbStillUrl"] = "N/A",
            ["TmdbLogoUrl"] = "N/A",
            ["TmdbSeasonPosterUrl"] = "N/A",
            ["TmdbSeriesPosterUrl"] = "N/A",

            // Parent-level Jellyfin Images
            ["SeasonPoster"] = "N/A",
            ["SeriesPoster"] = "N/A",

            // Task
            ["TaskName"] = "N/A",
            ["TaskId"] = "N/A",
            ["Status"] = "N/A",
            ["StartTime"] = "N/A",
            ["EndTime"] = "N/A",
            ["Duration"] = "N/A",

            // Plugin
            ["PluginName"] = "N/A",
            ["PluginId"] = "N/A",
            ["NewVersion"] = "N/A",

            // Year
            ["Year"] = "N/A"
        };
    }

    /// <summary>
    /// Creates hardcoded test data with realistic metadata (used as fallback when no library items found).
    /// </summary>
    private static Dictionary<string, object> CreateTestData()
    {
        return new Dictionary<string, object>
        {
            // Base Variables
            ["Title"] = "Test Notification",
            ["Body"] = "This is a test message to verify your notification configuration is working correctly.",
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
            ["ServerName"] = "Jellyfin",
            ["NotificationType"] = "PlaybackStart",

            // Item Variables
            ["ItemId"] = "3c9cf20670bedf5866ff224850824948",
            ["ItemName"] = "Inception (2010)",
            ["ItemType"] = "Movie",
            ["LibraryName"] = "Movies",
            ["LibraryId"] = "N/A",
            ["ItemUrl"] = "N/A",
            ["ItemShortId"] = "3c9cf20670",
            ["ProductionYear"] = "2010",
            ["Overview"] = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O., but his tragic past may doom the project and his team to disaster.",
            ["Genres"] = "Action, Sci-Fi, Thriller",
            ["PremiereDate"] = "2010-07-16",
            ["Runtime"] = "2h 28m",
            ["OfficialRating"] = "PG-13",
            ["CommunityRating"] = "8.8",
            ["CriticRating"] = "74",
            ["Tagline"] = "Your mind is the scene of the crime",
            ["OriginalTitle"] = "Inception",
            ["Studios"] = "Warner Bros., Legendary",
            ["ProductionLocations"] = "USA, UK",
            ["Tags"] = "mind-bending, sci-fi",
            ["Path"] = "N/A",
            ["Container"] = "N/A",
            ["DateCreated"] = "N/A",

            // TV Show Variables
            ["SeriesName"] = "Breaking Bad",
            ["SeasonNumber"] = "1",
            ["SeasonNumber00"] = "01",
            ["SeasonNumber000"] = "001",
            ["SeasonName"] = "Season 1",
            ["EpisodeNumber"] = "1",
            ["EpisodeNumber00"] = "01",
            ["EpisodeNumber000"] = "001",
            ["SeriesStatus"] = "Ended",

            // Provider IDs
            ["ImdbId"] = "tt1375666",
            ["TmdbId"] = "27205",
            ["TvdbId"] = "12345",

            // User Variables
            ["UserId"] = Guid.Empty.ToString(),
            ["Username"] = "TestUser",

            // Session Variables
            ["Client"] = "Jellyfin Web",
            ["DeviceName"] = "Chrome on Windows",
            ["RemoteEndPoint"] = "192.168.1.100",
            ["SessionId"] = "session123",
            ["PlayMethod"] = "DirectPlay",
            ["IsPaused"] = "False",
            ["VolumeLevel"] = "80",
            ["IsMuted"] = "False",
            ["CanSeek"] = "True",
            ["AudioStreamIndex"] = "1",
            ["SubtitleStreamIndex"] = "0",
            ["RepeatMode"] = "Off",
            ["PlaybackOrder"] = "Default",
            ["MediaSourceId"] = "source123",
            ["LiveStreamId"] = "live123",

            // Playback Variables
            ["PlaybackPositionTicks"] = "1234567890",
            ["PlaybackPosition"] = "00:15:30",
            ["IsAutomated"] = "False",
            ["PlaySessionId"] = "session456",
            ["PlayedToCompletion"] = "False",

            // Rating Variables (MDBList)
            ["MdblistScore"] = "8.5",
            ["ImdbRating"] = "8.8",
            ["TmdbRating"] = "8.4",
            ["RottenTomatoesRating"] = "87",
            ["MetacriticRating"] = "74",
            ["LetterboxdRating"] = "4.2",
            ["PopcornRating"] = "8.5",
            ["TraktRating"] = "8.6",
            ["MyAnimeListRating"] = "9.0",
            ["AnilistRating"] = "8.7",
            ["RogerEbertRating"] = "4.0",

            // Jellyfin Image URLs
            ["PrimaryImage"] = "N/A",
            ["BackdropImage"] = "N/A",
            ["ThumbImage"] = "N/A",
            ["LogoImage"] = "N/A",
            ["BannerImage"] = "N/A",

            // Trailer Variables
            ["TrailerUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            ["TrailerYtId"] = "dQw4w9WgXcQ",

            // TMDb Image Variables (actual Inception poster — TMDB ID 27205)
            ["TmdbPosterUrl"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbBackdropUrl"] = "https://image.tmdb.org/t/p/w1280/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbProfileUrl"] = "https://image.tmdb.org/t/p/w185/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbStillUrl"] = "https://image.tmdb.org/t/p/w300/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbLogoUrl"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbSeasonPosterUrl"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["TmdbSeriesPosterUrl"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",

            // Parent-level Jellyfin Images
            ["SeasonPoster"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",
            ["SeriesPoster"] = "https://image.tmdb.org/t/p/w500/xlaY2zyzMfkhk0HSC5VUwzoZPU1.jpg",

            // Task Variables
            ["TaskName"] = "Refresh Library",
            ["TaskId"] = "task123",
            ["Status"] = "Completed",
            ["StartTime"] = "N/A",
            ["EndTime"] = "N/A",
            ["Duration"] = "N/A",

            // Plugin Variables
            ["PluginName"] = "Intro Skipper",
            ["PluginId"] = "plugin123",
            ["NewVersion"] = "1.2.3",

            // Year (used in examples but not in table)
            ["Year"] = "2010"
        };
    }

    /// <summary>
    /// Creates a <see cref="JsonSerializerOptions"/> configured for test notification deserialization.
    /// Uses PascalCase enum naming to match the frontend convention (e.g., <c>"SendText"</c> rather
    /// than <c>"sendText"</c>). Both string and integer enum values are accepted.
    /// </summary>
    private static JsonSerializerOptions CreateTestNotificationOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: true) }
        };
    }

    private BaseOption? ParseOption(TestNotificationRequest request)
    {
        var options = CreateTestNotificationOptions();
        return request.DestinationType.ToLowerInvariant() switch
        {
            "telegram" => request.Config.Deserialize<TelegramOption>(options),
            "gotify" => request.Config.Deserialize<GotifyOption>(options),
            "ntfy" => request.Config.Deserialize<NtfyOption>(options),
            "generic" => request.Config.Deserialize<GenericWebhookOption>(options),
            _ => null
        };
    }

    private async Task SendAsync(string destinationType, BaseOption option, Dictionary<string, object> data)
    {
        switch (destinationType.ToLowerInvariant())
        {
            case "telegram":
                await _telegramClient.SendAsync((TelegramOption)option, data).ConfigureAwait(false);
                break;
            case "gotify":
                await _gotifyClient.SendAsync((GotifyOption)option, data).ConfigureAwait(false);
                break;
            case "ntfy":
                await _ntfyClient.SendAsync((NtfyOption)option, data).ConfigureAwait(false);
                break;
            case "generic":
                await _genericClient.SendAsync((GenericWebhookOption)option, data).ConfigureAwait(false);
                break;
        }
    }
}
