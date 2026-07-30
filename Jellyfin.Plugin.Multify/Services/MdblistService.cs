using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for fetching ratings from MDBList API.
/// </summary>
public class MdblistService
{
    private const string ApiBaseUrl = "https://api.mdblist.com";

    private readonly ILogger<MdblistService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AdvancedOption _advancedOptions;

    // In-memory cache for ratings (key: "imdb:{mediaType}:{imdbId}" or "tmdb:{mediaType}:{tmdbId}")
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MdblistService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MdblistService}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    /// <param name="advancedOptions">Advanced plugin options.</param>
    public MdblistService(ILogger<MdblistService> logger, IHttpClientFactory httpClientFactory, IOptions<AdvancedOption> advancedOptions)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _advancedOptions = advancedOptions.Value;
    }

    /// <summary>
    /// Gets ratings for a media item by IMDb ID.
    /// </summary>
    /// <param name="apiKey">The MDBList API key.</param>
    /// <param name="imdbId">The IMDb ID (e.g., tt1375666).</param>
    /// <param name="mediaType">The media type (movie or show).</param>
    /// <returns>A dictionary of ratings from different providers, or null if not found.</returns>
    public async Task<Dictionary<string, object>?> GetRatingsAsync(string apiKey, string imdbId, string mediaType)
    {
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(imdbId))
        {
            return null;
        }

        var cacheKey = $"imdb:{mediaType}:{imdbId}";
        return await GetRatingsInternalAsync(apiKey, cacheKey, () => new Uri($"{ApiBaseUrl}/imdb/{mediaType}/{imdbId}/")).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets ratings for a media item by TMDb ID.
    /// </summary>
    /// <param name="apiKey">The MDBList API key.</param>
    /// <param name="tmdbId">The TMDb ID.</param>
    /// <param name="mediaType">The media type (movie or show).</param>
    /// <returns>A dictionary of ratings from different providers, or null if not found.</returns>
    public async Task<Dictionary<string, object>?> GetRatingsByTmdbAsync(string apiKey, int tmdbId, string mediaType)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var cacheKey = $"tmdb:{mediaType}:{tmdbId}";
        return await GetRatingsInternalAsync(apiKey, cacheKey, () => new Uri($"{ApiBaseUrl}/tmdb/{mediaType}/{tmdbId}/")).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, object>?> GetRatingsInternalAsync(string apiKey, string cacheKey, Func<Uri> uriFactory)
    {
        // Check cache first
        if (_advancedOptions.MdblistCacheTtlHours > 0 && _cache.TryGetValue(cacheKey, out var cachedEntry))
        {
            if (DateTime.UtcNow < cachedEntry.Expiry)
            {
                _logger.LogDebug("MDBList cache hit for {CacheKey}", cacheKey);
                return cachedEntry.Ratings;
            }
            else
            {
                // Expired, remove from cache
                _cache.TryRemove(cacheKey, out _);
            }
        }

        // Fetch with retry logic
        var ratings = await FetchWithRetryAsync(apiKey, uriFactory, cacheKey).ConfigureAwait(false);

        // Cache the result if successful
        if (ratings != null && _advancedOptions.MdblistCacheTtlHours > 0)
        {
            var expiry = DateTime.UtcNow.AddHours(_advancedOptions.MdblistCacheTtlHours);
            _cache[cacheKey] = new CacheEntry { Ratings = ratings, Expiry = expiry };
            _logger.LogDebug("Cached MDBList ratings for {CacheKey} (TTL: {TtlHours}h)", cacheKey, _advancedOptions.MdblistCacheTtlHours);
        }

        return ratings;
    }

    private async Task<Dictionary<string, object>?> FetchWithRetryAsync(string apiKey, Func<Uri> uriFactory, string cacheKey)
    {
        var maxRetries = Math.Max(0, Math.Min(5, _advancedOptions.MdblistMaxRetries));
        var timeout = TimeSpan.FromSeconds(Math.Max(5, Math.Min(60, _advancedOptions.MdblistTimeoutSeconds)));

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var uri = uriFactory();
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var client = _httpClientFactory.CreateClient(NamedClient.Default);
                client.Timeout = timeout;

                using var response = await client.SendAsync(request).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var result = JsonSerializer.Deserialize<JsonElement>(json);
                    return ExtractRatings(result);
                }

                // Handle rate limiting (HTTP 429)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                    _logger.LogWarning(
                "MDBList rate limited (429) for {CacheKey}. Retrying after {RetryAfter}s (attempt {Attempt}/{MaxRetries})",
                cacheKey,
                retryAfter.TotalSeconds,
                attempt + 1,
                maxRetries + 1);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryAfter).ConfigureAwait(false);
                        continue;
                    }
                }

                // Log specific error details
                var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning(
                "Failed to fetch ratings from MDBList for {CacheKey}: {StatusCode} - {ErrorContent}",
                cacheKey,
                response.StatusCode,
                errorContent);

                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning(
                "MDBList request timeout for {CacheKey} after {Timeout}s (attempt {Attempt}/{MaxRetries})",
                cacheKey,
                timeout.TotalSeconds,
                attempt + 1,
                maxRetries + 1);

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2); // Exponential backoff: 2s, 4s, 8s...
                    await Task.Delay(delay).ConfigureAwait(false);
                    continue;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                ex,
                "MDBList HTTP error for {CacheKey} (attempt {Attempt}/{MaxRetries})",
                cacheKey,
                attempt + 1,
                maxRetries + 1);

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                    await Task.Delay(delay).ConfigureAwait(false);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                ex,
                "Unexpected error fetching ratings from MDBList for {CacheKey}",
                cacheKey);
                return null;
            }
        }

        return null;
    }

    private static Dictionary<string, object> ExtractRatings(JsonElement result)
    {
        var ratings = new Dictionary<string, object>();

        // Add MDBList score (allow zero scores)
        if (result.TryGetProperty("score", out var scoreElement))
        {
            var score = scoreElement.GetDouble();
            // Include zero scores as they may be valid
            ratings["MdblistScore"] = score;
        }

        // Add ratings from different providers
        if (result.TryGetProperty("ratings", out var ratingsElement) && ratingsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var rating in ratingsElement.EnumerateArray())
            {
                if (rating.TryGetProperty("source", out var sourceElement) &&
                    rating.TryGetProperty("score", out var ratingScoreElement))
                {
                    var source = sourceElement.GetString();
                    var ratingScore = ratingScoreElement.GetDouble();

                    if (!string.IsNullOrEmpty(source))
                    {
                        // Include zero scores as they may be valid
                        var normalizedName = source.ToLowerInvariant() switch
                        {
                            "imdb" => "ImdbRating",
                            "tmdb" => "TmdbRating",
                            "rt" or "rottentomatoes" => "RottenTomatoesRating",
                            "mc" or "metacritic" => "MetacriticRating",
                            "lb" or "letterboxd" => "LetterboxdRating",
                            "popcorn" or "popcorntime" => "PopcornRating",
                            "anilist" => "AnilistRating",
                            "rogerebert" or "rogerebertcom" => "RogerEbertRating",
                            "trakt" => "TraktRating",
                            "mal" or "myanimelist" => "MyAnimeListRating",
                            _ => $"{source}Rating"
                        };

                        ratings[normalizedName] = ratingScore;
                    }
                }
            }
        }

        return ratings;
    }

    private sealed class CacheEntry
    {
        public Dictionary<string, object> Ratings { get; set; } = new();
        public DateTime Expiry { get; set; }
    }
}