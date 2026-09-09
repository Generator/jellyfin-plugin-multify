using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Multify.Destinations;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Centralized filter logic for webhook notifications.
/// </summary>
public class FilterService
{
    private readonly ILogger<FilterService> _logger;
    private readonly ILibraryManager? _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{FilterService}"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public FilterService(ILogger<FilterService> logger, ILibraryManager? libraryManager = null)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    private static string NormalizeGuid(string value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            return guid.ToString("N", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        return value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether a notification should be sent based on all filters.
    /// </summary>
    /// <param name="option">The base option with filter settings.</param>
    /// <param name="data">The notification data dictionary.</param>
    /// <returns>The filter result indicating whether notification should be sent.</returns>
    public FilterResult ShouldNotify(BaseOption option, Dictionary<string, object> data)
    {
        if (!option.EnableWebhook)
        {
            _logger.LogDebug("Webhook disabled for {WebhookName}", option.WebhookName);
            return FilterResult.DenyWebhookDisabled;
        }

        var userResult = CheckUserFilter(option, data);
        if (userResult != FilterResult.Allow)
        {
            return userResult;
        }

        var libraryResult = CheckLibraryFilter(option, data);
        if (libraryResult != FilterResult.Allow)
        {
            return libraryResult;
        }

        return FilterResult.Allow;
    }

    /// <summary>
    /// Checks the user filter against the notification data.
    /// </summary>
    /// <param name="option">The base option with user filter settings.</param>
    /// <param name="data">The notification data dictionary.</param>
    /// <returns>The filter result.</returns>
    public FilterResult CheckUserFilter(BaseOption option, Dictionary<string, object> data)
    {
        // If no user filter is defined, allow all users regardless of filter mode
        if (option.UserFilter is null || option.UserFilter.Length == 0)
        {
            return FilterResult.Allow;
        }

        var userId = data.TryGetValue("UserId", out var userIdObj)
            ? userIdObj?.ToString() ?? string.Empty
            : string.Empty;

        var normalizedUserId = NormalizeGuid(userId);
        bool isInFilter = false;
        foreach (var filter in option.UserFilter)
        {
            if (NormalizeGuid(filter) == normalizedUserId)
            {
                isInFilter = true;
                break;
            }
        }

        bool shouldSend = option.UserFilterMode == FilterMode.AllExcept ? !isInFilter : isInFilter;

        if (!shouldSend)
        {
            _logger.LogDebug(
                "User {UserId} filtered out for {WebhookName} (Mode={Mode}, IsInFilter={IsInFilter})",
                userId,
                option.WebhookName,
                option.UserFilterMode,
                isInFilter);
            return FilterResult.DenyUserFilter;
        }

        return FilterResult.Allow;
    }

    /// <summary>
    /// Checks the library filter against the notification data.
    /// </summary>
    /// <param name="option">The base option with library filter settings.</param>
    /// <param name="data">The notification data dictionary.</param>
    /// <returns>The filter result.</returns>
    public FilterResult CheckLibraryFilter(BaseOption option, Dictionary<string, object> data)
    {
        if (option.LibraryFilter is null || option.LibraryFilter.Length == 0)
        {
            return FilterResult.Allow;
        }

        var libraryId = data.TryGetValue("LibraryId", out var libraryIdObj)
            ? libraryIdObj?.ToString() ?? string.Empty
            : string.Empty;

        var libraryName = data.TryGetValue("LibraryName", out var libraryNameObj)
            ? libraryNameObj?.ToString() ?? string.Empty
            : string.Empty;

        var path = data.TryGetValue("Path", out var pathObj)
            ? pathObj?.ToString() ?? string.Empty
            : string.Empty;

        var normalizedLibraryId = NormalizeGuid(libraryId);

        bool isInFilter = false;
        foreach (var filter in option.LibraryFilter)
        {
            var normalizedFilter = NormalizeGuid(filter);

            // Direct GUID match (N vs D, case-insensitive)
            if (normalizedLibraryId == normalizedFilter && !string.IsNullOrEmpty(normalizedLibraryId))
            {
                isInFilter = true;
                break;
            }

            // Name match for older configs that stored library Name
            if (!string.IsNullOrEmpty(libraryName) && string.Equals(libraryName, filter, StringComparison.OrdinalIgnoreCase))
            {
                isInFilter = true;
                break;
            }

            // Also treat filter stored as Name matching LibraryId's folder name? handled above

            // Resolve via VirtualFolders: filter is a CollectionFolder ItemId (e.g. af92...),
            // but data LibraryId may be the physical Folder id (e.g. f7e7...) or Path.
            // If filter matches a VirtualFolder, check if item's Path is inside its Locations.
            if (_libraryManager != null && !string.IsNullOrEmpty(path))
            {
                try
                {
                    var virtualFolders = _libraryManager.GetVirtualFolders();
                    foreach (var vf in virtualFolders)
                    {
                        if (NormalizeGuid(vf.ItemId) == normalizedFilter
                            || string.Equals(vf.Name, filter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (vf.Locations != null)
                            {
                                foreach (var loc in vf.Locations)
                                {
                                    if (!string.IsNullOrEmpty(loc) && path.StartsWith(loc, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isInFilter = true;
                                        break;
                                    }
                                }
                            }

                            if (isInFilter)
                            {
                                break;
                            }
                        }
                    }

                    if (isInFilter)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error resolving VirtualFolders for library filter {Filter}", filter);
                }
            }
        }

        bool shouldSend = option.LibraryFilterMode == FilterMode.AllExcept ? !isInFilter : isInFilter;

        if (!shouldSend)
        {
            _logger.LogDebug(
                "Library {LibraryId} filtered out for {WebhookName} (Mode={Mode}, IsInFilter={IsInFilter})",
                libraryId,
                option.WebhookName,
                option.LibraryFilterMode,
                isInFilter);
            return FilterResult.DenyLibraryFilter;
        }

        return FilterResult.Allow;
    }

    /// <summary>
    /// Gets a human-readable description of the filter result.
    /// </summary>
    /// <param name="result">The filter result.</param>
    /// <returns>A description string.</returns>
    public static string GetResultDescription(FilterResult result)
    {
        return result switch
        {
            FilterResult.Allow => "Notification allowed",
            FilterResult.DenyUserFilter => "Blocked by user filter",
            FilterResult.DenyLibraryFilter => "Blocked by library filter",
            FilterResult.DenyWebhookDisabled => "Webhook disabled",
            _ => "Unknown result"
        };
    }
}
