using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Multify.Destinations;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Centralized filter logic for webhook notifications.
/// </summary>
public class FilterService
{
    private readonly ILogger<FilterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{FilterService}"/> interface.</param>
    public FilterService(ILogger<FilterService> logger)
    {
        _logger = logger;
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
        if (option.UserFilter.Length == 0)
        {
            return FilterResult.Allow;
        }

        var userId = data.TryGetValue("UserId", out var userIdObj)
            ? Guid.Parse(userIdObj.ToString()!)
            : Guid.Empty;

        bool isInFilter = Array.IndexOf(option.UserFilter, userId) != -1;
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
        if (option.LibraryFilter.Length == 0)
        {
            return FilterResult.Allow;
        }

        var libraryId = data.TryGetValue("LibraryId", out var libraryIdObj)
            ? Guid.Parse(libraryIdObj.ToString()!)
            : Guid.Empty;

        bool isInFilter = Array.IndexOf(option.LibraryFilter, libraryId) != -1;
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
