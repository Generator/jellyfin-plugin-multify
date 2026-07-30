using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for enriching notification data with remote image URLs (TMDB, etc.)
/// and parent-level poster URLs (Season/Series).
/// Shared across MultifySender and MultifyTestService to eliminate code duplication.
/// </summary>
public class ImageEnrichmentService
{
    private readonly ILogger<ImageEnrichmentService> _logger;
    private readonly IProviderManager _providerManager;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageEnrichmentService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{ImageEnrichmentService}"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface for querying remote images.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface for parent item lookups.</param>
    public ImageEnrichmentService(
        ILogger<ImageEnrichmentService> logger,
        IProviderManager providerManager,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _providerManager = providerManager;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Enriches the data dictionary with TMDB image URLs queried via Jellyfin's provider system.
    /// Maps remote image results to variables like <c>TmdbPosterUrl</c>, <c>TmdbBackdropUrl</c>,
    /// and overwrites local Jellyfin image URLs (<c>PrimaryImage</c>, <c>BackdropImage</c>, etc.)
    /// with publicly accessible CDN URLs.
    /// </summary>
    /// <param name="data">The data dictionary to populate with image URLs.</param>
    /// <param name="item">The media item to query remote images for.</param>
    /// <param name="logItemId">Optional item identifier for logging (e.g., a GUID string).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnrichWithTmdbImages(Dictionary<string, object> data, BaseItem item, string? logItemId = null)
    {
        try
        {
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
                var id = logItemId ?? item.Id.ToString();
                _logger.LogDebug("No TMDB remote images available for item {ItemId}", id);
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
            var refId = logItemId ?? item.Id.ToString();
            _logger.LogDebug("Enriched TMDB image URLs for item {ItemId}: {Count} URL(s)", refId, urlCount);
        }
        catch (Exception ex)
        {
            var id = logItemId ?? item.Id.ToString();
            _logger.LogWarning(ex, "Error enriching data with TMDB image URLs for item {ItemId}", id);
        }
    }

    /// <summary>
    /// Enriches data with poster URLs for parent items (Season, Series) when the current item
    /// is an Episode or Season. Sets both Jellyfin local URLs and TMDB CDN URLs.
    /// </summary>
    /// <param name="data">The data dictionary to populate with poster URLs.</param>
    /// <param name="item">The current media item (Episode, Season, Series, or Movie).</param>
    /// <param name="serverUrl">The server base URL for constructing Jellyfin image URLs. May be null.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnrichParentPosterUrls(Dictionary<string, object> data, BaseItem item, string? serverUrl)
    {
        serverUrl = serverUrl?.TrimEnd('/');

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
}
