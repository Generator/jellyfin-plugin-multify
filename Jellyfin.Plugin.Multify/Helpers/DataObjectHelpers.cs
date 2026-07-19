using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Destinations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Session;

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

        // Add provider IDs for MDBList integration
        if (item.ProviderIds.TryGetValue("Imdb", out var imdbId) && !string.IsNullOrEmpty(imdbId))
        {
            data["ImdbId"] = imdbId;
        }

        if (item.ProviderIds.TryGetValue("Tmdb", out var tmdbId) && int.TryParse(tmdbId, out _))
        {
            data["TmdbId"] = tmdbId;
        }

        if (item is MediaBrowser.Controller.Entities.Movies.Movie movie)
        {
            data["Year"] = movie.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            data["Overview"] = movie.Overview ?? string.Empty;
        }

        if (item is MediaBrowser.Controller.Entities.TV.Episode episode)
        {
            data["SeasonNumber"] = episode.ParentIndexNumber?.ToString(CultureInfo.InvariantCulture) ?? "0";
            data["EpisodeNumber"] = episode.IndexNumber?.ToString(CultureInfo.InvariantCulture) ?? "0";
            data["SeriesName"] = episode.SeriesName ?? "Unknown";
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
        return data;
    }
}
