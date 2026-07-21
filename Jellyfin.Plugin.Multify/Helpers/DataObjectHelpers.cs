using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Destinations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
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
            ["Timestamp"] = DateTime.UtcNow.ToString("O")
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

        // Add library name if available
        var topParent = item.GetTopParent();
        if (topParent is not null)
        {
            data["LibraryName"] = topParent.Name ?? "Unknown";
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

        // Add original language
        data["OriginalLanguage"] = item.OriginalLanguage ?? string.Empty;

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
        data["DateCreated"] = item.DateCreated.ToString("O");

        // Add home page URL
        data["HomePageUrl"] = item.HomePageUrl ?? string.Empty;

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
            data["PlayMethod"] = session.PlayState.PlayMethod.ToString();
            data["IsPaused"] = session.PlayState.IsPaused.ToString(CultureInfo.InvariantCulture);
            data["VolumeLevel"] = session.PlayState.VolumeLevel?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["IsMuted"] = session.PlayState.IsMuted.ToString(CultureInfo.InvariantCulture);
            data["CanSeek"] = session.PlayState.CanSeek.ToString(CultureInfo.InvariantCulture);
            data["AudioStreamIndex"] = session.PlayState.AudioStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["SubtitleStreamIndex"] = session.PlayState.SubtitleStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["RepeatMode"] = session.PlayState.RepeatMode.ToString();
            data["PlaybackOrder"] = session.PlayState.PlaybackOrder.ToString();
            data["MediaSourceId"] = session.PlayState.MediaSourceId ?? "Unknown";
            data["LiveStreamId"] = session.PlayState.LiveStreamId ?? "Unknown";
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
        data["PlaybackPositionTicks"] = eventArgs.PlaybackPositionTicks.ToString(CultureInfo.InvariantCulture);

        // Format position as HH:MM:SS
        var positionTimeSpan = TimeSpan.FromTicks(eventArgs.PlaybackPositionTicks);
        data["PlaybackPosition"] = positionTimeSpan.ToString(@"hh\:mm\:ss");

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
        data["PlaybackPositionTicks"] = eventArgs.PlaybackPositionTicks.ToString(CultureInfo.InvariantCulture);

        // Format position as HH:MM:SS
        var positionTimeSpan = TimeSpan.FromTicks(eventArgs.PlaybackPositionTicks);
        data["PlaybackPosition"] = positionTimeSpan.ToString(@"hh\:mm\:ss");

        data["PlayedToCompletion"] = eventArgs.PlayedToCompletion.ToString(CultureInfo.InvariantCulture);
        data["MediaSourceId"] = eventArgs.MediaSourceId ?? "Unknown";
        data["PlaySessionId"] = eventArgs.PlaySessionId ?? "Unknown";

        return data;
    }
}
