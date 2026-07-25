using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
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
    private readonly IWebhookClient<TelegramOption> _telegramClient;
    private readonly IWebhookClient<GotifyOption> _gotifyClient;
    private readonly IWebhookClient<NtfyOption> _ntfyClient;
    private readonly IWebhookClient<GenericWebhookOption> _genericClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifyTestService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifyTestService}"/> interface.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    public MultifyTestService(
        ILogger<MultifyTestService> logger,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient)
    {
        _logger = logger;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
    }

    /// <inheritdoc />
    public async Task<TestNotificationResponse> SendTestNotificationAsync(TestNotificationRequest request)
    {
        try
        {
            var data = CreateTestData();
            var option = ParseOption(request);

            if (option == null)
            {
                return new TestNotificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Unsupported destination type: {request.DestinationType}"
                };
            }

            // Ensure webhook is enabled for test and use template (not raw JSON)
            option.EnableWebhook = true;
            option.SendAllProperties = false;

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

    private static Dictionary<string, object> CreateTestData()
    {
        return new Dictionary<string, object>
        {
            // Base Variables
            ["Title"] = "Test Notification",
            ["Body"] = "This is a test message to verify your notification configuration is working correctly.",
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
            ["ServerName"] = "Jellyfin Server",
            ["NotificationType"] = "PlaybackStart",

            // Item Variables
            ["ItemId"] = "3c9cf20670bedf5866ff224850824948",
            ["ItemName"] = "Test Movie (2024)",
            ["ItemType"] = "Movie",
            ["LibraryName"] = "Movies",
            ["LibraryId"] = "lib123",
            ["ItemUrl"] = "https://jellyfin.example.com/web/#/details?id=3c9cf20670bedf5866ff224850824948",
            ["ItemShortId"] = "3c9cf20670",
            ["ProductionYear"] = "2024",
            ["Overview"] = "A test movie overview for template verification.",
            ["Genres"] = "Action, Sci-Fi, Thriller",
            ["PremiereDate"] = "2024-01-15",
            ["Runtime"] = "2h 28m",
            ["OfficialRating"] = "PG-13",
            ["CommunityRating"] = "8.8",
            ["CriticRating"] = "74",
            ["Tagline"] = "Your mind is the scene of the crime",
            ["OriginalTitle"] = "Test Movie",
            ["Studios"] = "Warner Bros., Legendary",
            ["ProductionLocations"] = "USA, UK",
            ["Tags"] = "favorite, sci-fi",
            ["Path"] = "/media/movies/Test Movie.mkv",
            ["Container"] = "mkv",
            ["DateCreated"] = "2024-01-15T10:30:00.000Z",

            // TV Show Variables
            ["SeriesName"] = "Test Series",
            ["SeasonNumber"] = "1",
            ["SeasonNumber00"] = "01",
            ["SeasonNumber000"] = "001",
            ["SeasonName"] = "Season 1",
            ["EpisodeNumber"] = "1",
            ["EpisodeNumber00"] = "01",
            ["EpisodeNumber000"] = "001",
            ["SeriesStatus"] = "Continuing",

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
            ["PrimaryImageUrl"] = "https://jellyfin.example.com/Items/3c9cf20670bedf5866ff224850824948/Images/Primary",
            ["BackdropImageUrl"] = "https://jellyfin.example.com/Items/3c9cf20670bedf5866ff224850824948/Images/Backdrop",
            ["ThumbImageUrl"] = "https://jellyfin.example.com/Items/3c9cf20670bedf5866ff224850824948/Images/Thumbnail",
            ["LogoImageUrl"] = "https://jellyfin.example.com/Items/3c9cf20670bedf5866ff224850824948/Images/Logo",
            ["BannerImageUrl"] = "https://jellyfin.example.com/Items/3c9cf20670bedf5866ff224850824948/Images/Banner",

            // Trailer Variables
            ["TrailerUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            ["TrailerYtId"] = "dQw4w9WgXcQ",

            // TMDb Image Variables
            ["TmdbPosterUrl"] = "https://image.tmdb.org/t/p/w500/9gk7adSYeDvHkCSEhniJIsaVti8.jpg",
            ["TmdbBackdropUrl"] = "https://image.tmdb.org/t/p/w1280/9gk7adSYeDvHkCSEhniJIsaVti8.jpg",
            ["TmdbProfileUrl"] = "https://image.tmdb.org/t/p/w185/9gk7adSYeDvHkCSEhniJIsaVti8.jpg",
            ["TmdbStillUrl"] = "https://image.tmdb.org/t/p/w300/9gk7adSYeDvHkCSEhniJIsaVti8.jpg",
            ["TmdbLogoUrl"] = "https://image.tmdb.org/t/p/w500/9gk7adSYeDvHkCSEhniJIsaVti8.jpg",

            // TVDB Image Variables
            ["TvdbPosterUrl"] = "https://artworks.thetvdb.com/banners/posters/73255-1.jpg",
            ["TvdbBannerUrl"] = "https://artworks.thetvdb.com/banners/graphical/73255-g1.jpg",
            ["TvdbFanartUrl"] = "https://artworks.thetvdb.com/banners/fanart/original/73255-1.jpg",
            ["TvdbSmallUrl"] = "https://artworks.thetvdb.com/banners/posters/73255-1.jpg",
            ["TvdbSeasonUrl"] = "https://artworks.thetvdb.com/banners/seasons/73255-1.jpg",

            // Task Variables
            ["TaskName"] = "Refresh Library",
            ["TaskId"] = "task123",
            ["Status"] = "Completed",
            ["StartTime"] = "2024-01-15T10:00:00.000Z",
            ["EndTime"] = "2024-01-15T10:05:00.000Z",
            ["Duration"] = "00:05:00",

            // Plugin Variables
            ["PluginName"] = "Intro Skipper",
            ["PluginId"] = "plugin123",
            ["NewVersion"] = "1.2.3",

            // Year (used in examples but not in table)
            ["Year"] = "2024"
        };
    }

    private BaseOption? ParseOption(TestNotificationRequest request)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

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
