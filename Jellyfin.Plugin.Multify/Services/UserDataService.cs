using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for adding per-user user data (PlayCount, IsFavorite, Played, UserRating)
/// to notification data dictionaries. Populated per-user so notifications always show
/// the acting user's own data instead of an arbitrary entry from the item's
/// UserData collection.
/// </summary>
public class UserDataService
{
    private readonly ILogger<UserDataService> _logger;
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDataService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserDataService}"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/>.</param>
    public UserDataService(ILogger<UserDataService> logger, IUserDataManager userDataManager)
    {
        _logger = logger;
        _userDataManager = userDataManager;
    }

    /// <summary>
    /// Adds the per-user data for <paramref name="user"/> and <paramref name="item"/>
    /// to <paramref name="data"/>. Safe defaults are used if lookup fails so templates
    /// never see missing variables.
    /// </summary>
    /// <param name="data">The data dictionary to populate.</param>
    /// <param name="user">The user whose data should be used.</param>
    /// <param name="item">The media item.</param>
    public void AddUserData(Dictionary<string, object> data, User user, BaseItem item)
    {
        // Initialize safe defaults first so templates never see missing variables
        // even when the lookup fails.
        data["PlayCount"] = "0";
        data["IsFavorite"] = "False";
        data["Played"] = "False";
        data["UserRating"] = "Unknown";

        try
        {
            var userData = _userDataManager.GetUserDataDto(item, user);
            data["PlayCount"] = (userData?.PlayCount ?? 0).ToString(CultureInfo.InvariantCulture);
            data["IsFavorite"] = (userData?.IsFavorite ?? false).ToString(CultureInfo.InvariantCulture);
            data["Played"] = (userData?.Played ?? false).ToString(CultureInfo.InvariantCulture);
            data["UserRating"] = userData?.Rating?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding per-user data for {Username} on {ItemName}", user.Username, item.Name);
        }
    }
}
