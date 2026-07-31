using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Destinations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;

namespace Jellyfin.Plugin.Multify.Helpers;

/// <summary>
/// Helper methods for building notification data objects.
/// </summary>
public static class DataObjectHelpers
{
    /// <summary>
    /// Gets the base data object with server information.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="notificationType">The notification type.</param>
    /// <returns>A dictionary with base data.</returns>
    public static Dictionary<string, object> GetBaseDataObject(string serverName, NotificationType notificationType)
    {
        return new Dictionary<string, object>
        {
            ["ServerName"] = serverName,
            ["NotificationType"] = notificationType.ToString(),
            ["Timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Adds item data to the notification dictionary.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="item">The base item.</param>
    /// <returns>The updated dictionary.</returns>
    public static Dictionary<string, object> AddItemData(this Dictionary<string, object> data, BaseItem item)
    {
        data["ItemId"] = item.Id.ToString();
        data["ItemName"] = item.Name ?? "Unknown";
        data["ItemType"] = item.GetType().Name;

        // Add library name and ID if available
        var topParent = item.GetTopParent();
        if (topParent is not null)
        {
            data["LibraryName"] = topParent.Name ?? "Unknown";
            data["LibraryId"] = topParent.Id.ToString();
        }

        // Add provider IDs for MDBList integration
        if (item.ProviderIds.TryGetValue("Imdb", out var imdbId) && !string.IsNullOrEmpty(imdbId))
        {
            data["ImdbId"] = imdbId;
        }

        if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbId) && int.TryParse(tmdbId, out _))
        {
            data["TmdbId"] = tmdbId;
        }

        if (item.ProviderIds.TryGetValue("Tvdb", out var tvdbId) && !string.IsNullOrEmpty(tvdbId))
        {
            data["TvdbId"] = tvdbId;
        }

        // Add genres as comma-separated string
        if (item.Genres is not null && item.Genres.Length > 0)
        {
            data["Genres"] = string.Join(", ", item.Genres);
        }
        else
        {
            data["Genres"] = string.Empty;
        }

        // Add premiere date
        if (item.PremiereDate.HasValue)
        {
            data["PremiereDate"] = item.PremiereDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        else
        {
            data["PremiereDate"] = string.Empty;
        }

        // Add runtime (formatted as hours and minutes)
        if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0)
        {
            var totalMinutes = (int)(item.RunTimeTicks.Value / TimeSpan.TicksPerMinute);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            data["Runtime"] = hours > 0
                ? $"{hours}h {minutes}m"
                : $"{minutes}m";
        }
        else
        {
            data["Runtime"] = string.Empty;
        }

        // Add overview (available on all items)
        data["Overview"] = item.Overview ?? string.Empty;

        // Add production year (available on all items)
        data["ProductionYear"] = item.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

        // Add official rating (content rating)
        data["OfficialRating"] = item.OfficialRating ?? string.Empty;

        // Add community rating
        data["CommunityRating"] = item.CommunityRating?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

        // Add critic rating
        data["CriticRating"] = item.CriticRating?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

        // Add tagline
        data["Tagline"] = item.Tagline ?? string.Empty;

        // Add original title
        data["OriginalTitle"] = item.OriginalTitle ?? string.Empty;

        // Add studios as comma-separated string
        if (item.Studios is not null && item.Studios.Length > 0)
        {
            data["Studios"] = string.Join(", ", item.Studios);
        }
        else
        {
            data["Studios"] = string.Empty;
        }

        // Add production locations as comma-separated string
        if (item.ProductionLocations is not null && item.ProductionLocations.Length > 0)
        {
            data["ProductionLocations"] = string.Join(", ", item.ProductionLocations);
        }
        else
        {
            data["ProductionLocations"] = string.Empty;
        }

        // Add tags as comma-separated string
        if (item.Tags is not null && item.Tags.Length > 0)
        {
            data["Tags"] = string.Join(", ", item.Tags);
        }
        else
        {
            data["Tags"] = string.Empty;
        }

        // Add path
        data["Path"] = item.Path ?? string.Empty;

        // Add container format
        data["Container"] = item.Container ?? string.Empty;

        // Add date created
        data["DateCreated"] = item.DateCreated.ToString("O", CultureInfo.InvariantCulture);

        // Media type (Video, Audio, Book, Photo)
        data["MediaType"] = item.MediaType.ToString();

        // Video resolution (0 if not applicable)
        data["Width"] = item.Width.ToString(CultureInfo.InvariantCulture);
        data["Height"] = item.Height.ToString(CultureInfo.InvariantCulture);

        // IsHD (width ≥ 1280)
        data["IsHD"] = (item.Width >= 1280).ToString(CultureInfo.InvariantCulture);

        // Sort name
        data["SortName"] = item.SortName ?? string.Empty;

        // Parent ID (direct parent GUID)
        data["ParentId"] = item.ParentId.ToString("N", CultureInfo.InvariantCulture);

        // Location type (virtual vs physical)
        data["LocationType"] = item.IsVirtualItem ? "Virtual" : "FileSystem";

        // Video type (BluRay, DVD, Iso, VideoFile) — only applies to Video items
        if (item is Video videoItem)
        {
            data["VideoType"] = videoItem.VideoType.ToString();
        }
        else
        {
            data["VideoType"] = string.Empty;
        }

        // HasSubtitles — available on Video items and their subclasses
        if (item is Video videoWithSubtitles)
        {
            data["HasSubtitles"] = videoWithSubtitles.HasSubtitles.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            data["HasSubtitles"] = "False";
        }

        // Per-user data (PlayCount, IsFavorite, Played, UserRating) is populated per-user
        // by UserDataService for playback events. Arbitrary UserData collection entries
        // are NOT used here because they may belong to another user. Leave safe defaults
        // so templates referencing these variables always render.
        data["PlayCount"] = "0";
        data["IsFavorite"] = "False";
        data["Played"] = "False";
        data["UserRating"] = "Unknown";

        // Add image URLs (will be enriched with server URL in MultifySender)
        data["PrimaryImage"] = string.Empty;
        data["BackdropImage"] = string.Empty;
        data["ThumbImage"] = string.Empty;
        data["LogoImage"] = string.Empty;
        data["BannerImage"] = string.Empty;

        // Add trailer data — always define both keys so templates never see a missing variable
        data["TrailerUrl"] = string.Empty;
        data["TrailerYtId"] = string.Empty;
        if (item.RemoteTrailers is not null && item.RemoteTrailers.Count > 0)
        {
            var firstTrailer = item.RemoteTrailers[0];
            data["TrailerUrl"] = firstTrailer.Url ?? string.Empty;

            // Extract YouTube video ID from URL (format: https://www.youtube.com/watch?v=VIDEO_ID)
            if (!string.IsNullOrEmpty(firstTrailer.Url) && firstTrailer.Url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(firstTrailer.Url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var ytId = query.Get("v");
                    if (!string.IsNullOrEmpty(ytId))
                    {
                        data["TrailerYtId"] = ytId;
                    }
                }
                catch (UriFormatException)
                {
                    // Invalid URL, skip
                }
            }
        }

        // Add TMDb image URL variables (will be enriched with API calls if TMDb ID available)
        data["TmdbPosterUrl"] = string.Empty;
        data["TmdbBackdropUrl"] = string.Empty;
        data["TmdbProfileUrl"] = string.Empty;
        data["TmdbStillUrl"] = string.Empty;
        data["TmdbLogoUrl"] = string.Empty;

        // Parent-level poster URLs (enriched separately from current item)
        data["SeasonPoster"] = string.Empty;
        data["TmdbSeasonPosterUrl"] = string.Empty;
        data["SeriesPoster"] = string.Empty;
        data["TmdbSeriesPosterUrl"] = string.Empty;

        // Add series-specific data
        if (item is MediaBrowser.Controller.Entities.TV.Series series)
        {
            data["SeriesStatus"] = series.Status?.ToString() ?? "Unknown";
        }

        // Add movie-specific data
        if (item is MediaBrowser.Controller.Entities.Movies.Movie movie)
        {
            data["Year"] = movie.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
        }

        // Add season-specific data
        if (item is MediaBrowser.Controller.Entities.TV.Season season)
        {
            var seasonNumber = season.IndexNumber ?? 0;

            data["SeasonNumber"] = seasonNumber.ToString(CultureInfo.InvariantCulture);
            data["SeasonNumber00"] = seasonNumber.ToString("00", CultureInfo.InvariantCulture);
            data["SeasonNumber000"] = seasonNumber.ToString("000", CultureInfo.InvariantCulture);

            data["SeasonName"] = season.Name ?? "Unknown";

            // Get series info from parent series
            var seriesItem = season.Series;
            if (seriesItem is not null)
            {
                data["SeriesName"] = seriesItem.Name ?? "Unknown";
                data["SeriesStatus"] = seriesItem.Status?.ToString() ?? "Unknown";
            }
            else
            {
                data["SeriesName"] = "Unknown";
            }
        }

        // Add episode-specific data
        if (item is MediaBrowser.Controller.Entities.TV.Episode episode)
        {
            var seasonNumber = episode.ParentIndexNumber ?? 0;
            var episodeNumber = episode.IndexNumber ?? 0;

            data["SeasonNumber"] = seasonNumber.ToString(CultureInfo.InvariantCulture);
            data["SeasonNumber00"] = seasonNumber.ToString("00", CultureInfo.InvariantCulture);
            data["SeasonNumber000"] = seasonNumber.ToString("000", CultureInfo.InvariantCulture);

            data["EpisodeNumber"] = episodeNumber.ToString(CultureInfo.InvariantCulture);
            data["EpisodeNumber00"] = episodeNumber.ToString("00", CultureInfo.InvariantCulture);
            data["EpisodeNumber000"] = episodeNumber.ToString("000", CultureInfo.InvariantCulture);

            data["SeriesName"] = episode.SeriesName ?? "Unknown";

            // Try to get season name
            var seasonName = episode.FindSeasonName();
            data["SeasonName"] = seasonName ?? "Unknown";
        }

        return data;
    }

    /// <summary>
    /// Adds user data to the notification dictionary.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="user">The user.</param>
    /// <returns>The updated dictionary.</returns>
    public static Dictionary<string, object> AddUserData(this Dictionary<string, object> data, User user)
    {
        data["Username"] = user.Username ?? "Unknown";
        data["UserId"] = user.Id.ToString();
        return data;
    }

    /// <summary>
    /// Adds session info to the notification dictionary.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="session">The session info.</param>
    /// <returns>The updated dictionary.</returns>
    public static Dictionary<string, object> AddSessionInfo(this Dictionary<string, object> data, SessionInfo session)
    {
        data["Client"] = session.Client ?? "Unknown";
        data["DeviceName"] = session.DeviceName ?? "Unknown";
        data["RemoteEndPoint"] = session.RemoteEndPoint ?? "Unknown";
        data["SessionId"] = session.Id ?? "Unknown";

        // Add play state info if available
        if (session.PlayState is not null)
        {
            data["PlayMethod"] = session.PlayState.PlayMethod?.ToString() ?? "Unknown";
            data["IsPaused"] = session.PlayState.IsPaused.ToString(CultureInfo.InvariantCulture);
            data["VolumeLevel"] = session.PlayState.VolumeLevel?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["IsMuted"] = session.PlayState.IsMuted.ToString(CultureInfo.InvariantCulture);
            data["CanSeek"] = session.PlayState.CanSeek.ToString(CultureInfo.InvariantCulture);
            data["AudioStreamIndex"] = session.PlayState.AudioStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["SubtitleStreamIndex"] = session.PlayState.SubtitleStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["RepeatMode"] = session.PlayState.RepeatMode.ToString() ?? "Unknown";
            data["PlaybackOrder"] = session.PlayState.PlaybackOrder.ToString() ?? "Unknown";
            data["MediaSourceId"] = session.PlayState.MediaSourceId ?? "Unknown";
            data["LiveStreamId"] = session.PlayState.LiveStreamId ?? "Unknown";

            // Add transcode bitrate when available (populated during transcoding sessions)
            if (session.TranscodingInfo?.Bitrate.HasValue == true)
            {
                var bitrate = session.TranscodingInfo.Bitrate.Value;
                data["PlaybackBitrate"] = bitrate;
                data["PlaybackBitrateText"] = FormatBitrate(bitrate);
            }
        }

        return data;
    }

    /// <summary>
    /// Adds playback-specific data to the notification dictionary.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="eventArgs">The playback event arguments.</param>
    /// <returns>The updated dictionary.</returns>
    public static Dictionary<string, object> AddPlaybackData(this Dictionary<string, object> data, PlaybackProgressEventArgs eventArgs)
    {
        var positionTicks = eventArgs.PlaybackPositionTicks ?? 0;
        data["PlaybackPositionTicks"] = positionTicks.ToString(CultureInfo.InvariantCulture);

        // Format position as HH:MM:SS
        var positionTimeSpan = TimeSpan.FromTicks(positionTicks);
        data["PlaybackPosition"] = positionTimeSpan.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        data["IsPaused"] = eventArgs.IsPaused.ToString(CultureInfo.InvariantCulture);
        data["IsAutomated"] = eventArgs.IsAutomated.ToString(CultureInfo.InvariantCulture);
        data["MediaSourceId"] = eventArgs.MediaSourceId ?? "Unknown";
        data["PlaySessionId"] = eventArgs.PlaySessionId ?? "Unknown";

        return data;
    }

    /// <summary>
    /// Adds playback stop data to the notification dictionary.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="eventArgs">The playback stop event arguments.</param>
    /// <returns>The updated dictionary.</returns>
    public static Dictionary<string, object> AddPlaybackStopData(this Dictionary<string, object> data, PlaybackStopEventArgs eventArgs)
    {
        var positionTicks = eventArgs.PlaybackPositionTicks ?? 0;
        data["PlaybackPositionTicks"] = positionTicks.ToString(CultureInfo.InvariantCulture);

        // Format position as HH:MM:SS
        var positionTimeSpan = TimeSpan.FromTicks(positionTicks);
        data["PlaybackPosition"] = positionTimeSpan.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        data["PlayedToCompletion"] = eventArgs.PlayedToCompletion.ToString(CultureInfo.InvariantCulture);
        data["MediaSourceId"] = eventArgs.MediaSourceId ?? "Unknown";
        data["PlaySessionId"] = eventArgs.PlaySessionId ?? "Unknown";

        return data;
    }

    /// <summary>
    /// Formats a bitrate value in bits per second to a human-readable string with SI suffix.
    /// </summary>
    /// <param name="bitrate">The bitrate in bits per second. Example: 2176878 → "2.2Mbps".</param>
    /// <returns>Formatted string like "2.2Mbps", "850Kbps", or "Unknown" if null or zero.</returns>
    public static string FormatBitrate(int? bitrate)
    {
        if (!bitrate.HasValue || bitrate.Value < 0)
        {
            return "Unknown";
        }

        if (bitrate.Value == 0)
        {
            return "0 bps";
        }

        var bps = bitrate.Value;
        if (bps >= 1_000_000)
        {
            return $"{bps / 1_000_000.0:F1}Mbps";
        }

        if (bps >= 1_000)
        {
            return $"{bps / 1_000.0:F0}Kbps";
        }

        return $"{bps}bps";
    }
}
