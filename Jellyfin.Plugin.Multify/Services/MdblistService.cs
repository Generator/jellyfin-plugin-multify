using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for fetching ratings from MDBList API.
/// </summary>
public class MdblistService
{
    private const string ApiBaseUrl = "https://api.mdblist.com";

    private readonly ILogger<MdblistService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MdblistService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MdblistService}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    public MdblistService(ILogger<MdblistService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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

        try
        {
            var uri = new Uri($"{ApiBaseUrl}/imdb/{mediaType}/{imdbId}/");
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var client = _httpClientFactory.CreateClient(NamedClient.Default);
            using var response = await client.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch ratings from MDBList for {ImdbId}: {StatusCode}", imdbId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            return ExtractRatings(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching ratings from MDBList for {ImdbId}", imdbId);
            return null;
        }
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

        try
        {
            var uri = new Uri($"{ApiBaseUrl}/tmdb/{mediaType}/{tmdbId}/");
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var client = _httpClientFactory.CreateClient(NamedClient.Default);
            using var response = await client.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch ratings from MDBList for TMDb {TmdbId}: {StatusCode}", tmdbId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            return ExtractRatings(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching ratings from MDBList for TMDb {TmdbId}", tmdbId);
            return null;
        }
    }

    private static Dictionary<string, object> ExtractRatings(JsonElement result)
    {
        var ratings = new Dictionary<string, object>();

        // Add MDBList score
        if (result.TryGetProperty("score", out var scoreElement))
        {
            var score = scoreElement.GetDouble();
            if (score > 0)
            {
                ratings["MdblistScore"] = score;
            }
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

                    if (!string.IsNullOrEmpty(source) && ratingScore > 0)
                    {
                        // Normalize source name
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
}
