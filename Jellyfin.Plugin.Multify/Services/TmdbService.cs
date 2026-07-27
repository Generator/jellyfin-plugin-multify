using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for fetching TMDB image URLs via the TMDB API.
/// </summary>
public class TmdbService
{
    private const string ApiBaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p";

    private readonly ILogger<TmdbService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PluginConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TmdbService}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    /// <param name="configuration">Plugin configuration.</param>
    public TmdbService(ILogger<TmdbService> logger, IHttpClientFactory httpClientFactory, PluginConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Fetches image URLs for a movie from TMDB.
    /// </summary>
    /// <param name="tmdbId">The TMDB movie ID.</param>
    /// <returns>Dictionary of image URL variables, or empty dict if unavailable.</returns>
    public async Task<Dictionary<string, string>> FetchMovieImageUrlsAsync(int tmdbId)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var json = await CallTmdbApiAsync($"/movie/{tmdbId}").ConfigureAwait(false);
            if (json is null)
            {
                return result;
            }

            var root = json.Value;
            var posterPath = root.GetProperty("poster_path").GetString();
            var backdropPath = root.GetProperty("backdrop_path").GetString();

            if (!string.IsNullOrEmpty(posterPath))
            {
                result["TmdbPosterUrl"] = $"{ImageBaseUrl}/w500{posterPath}";
                result["TmdbProfileUrl"] = $"{ImageBaseUrl}/w185{posterPath}";
            }

            if (!string.IsNullOrEmpty(backdropPath))
            {
                result["TmdbBackdropUrl"] = $"{ImageBaseUrl}/w1280{backdropPath}";
            }

            _logger.LogDebug("Fetched TMDB image URLs for movie {TmdbId}: poster={Poster}, backdrop={Backdrop}", tmdbId, posterPath, backdropPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching TMDB image URLs for movie {TmdbId}", tmdbId);
        }

        return result;
    }

    /// <summary>
    /// Fetches image URLs for a TV series from TMDB.
    /// </summary>
    /// <param name="tmdbId">The TMDB series ID.</param>
    /// <returns>Dictionary of image URL variables, or empty dict if unavailable.</returns>
    public async Task<Dictionary<string, string>> FetchSeriesImageUrlsAsync(int tmdbId)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var json = await CallTmdbApiAsync($"/tv/{tmdbId}").ConfigureAwait(false);
            if (json is null)
            {
                return result;
            }

            var root = json.Value;
            var posterPath = root.GetProperty("poster_path").GetString();
            var backdropPath = root.GetProperty("backdrop_path").GetString();

            if (!string.IsNullOrEmpty(posterPath))
            {
                result["TmdbPosterUrl"] = $"{ImageBaseUrl}/w500{posterPath}";
                result["TmdbProfileUrl"] = $"{ImageBaseUrl}/w185{posterPath}";
            }

            if (!string.IsNullOrEmpty(backdropPath))
            {
                result["TmdbBackdropUrl"] = $"{ImageBaseUrl}/w1280{backdropPath}";
            }

            _logger.LogDebug("Fetched TMDB image URLs for series {TmdbId}: poster={Poster}, backdrop={Backdrop}", tmdbId, posterPath, backdropPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching TMDB image URLs for series {TmdbId}", tmdbId);
        }

        return result;
    }

    private async Task<JsonElement?> CallTmdbApiAsync(string path)
    {
        var apiKey = _configuration.TmdbApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogDebug("TMDB API key not configured, skipping image fetch for path {Path}", path);
            return null;
        }

        var url = $"{ApiBaseUrl}{path}?api_key={apiKey}";
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .GetAsync(url)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TMDB API returned {StatusCode} for {Path}", (int)response.StatusCode, path);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
