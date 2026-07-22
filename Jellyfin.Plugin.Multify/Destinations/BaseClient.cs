using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Multify.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Base client for all destination clients.
/// </summary>
public abstract class BaseClient
{
    private readonly FilterService _filterService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseClient"/> class.
    /// </summary>
    /// <param name="filterService">Instance of the <see cref="FilterService"/>.</param>
    protected BaseClient(FilterService filterService)
    {
        _filterService = filterService;
    }

    /// <summary>
    /// Checks filters and logs webhook send.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="option">The base option.</param>
    /// <param name="data">The data dictionary.</param>
    /// <returns>True if the notification should be sent; false otherwise.</returns>
    protected bool SendWebhook(ILogger logger, BaseOption option, Dictionary<string, object> data)
    {
        var result = _filterService.ShouldNotify(option, data);

        if (result != FilterResult.Allow)
        {
            logger.LogDebug(
                "Notification blocked for {WebhookName}: {Reason}",
                option.WebhookName,
                FilterService.GetResultDescription(result));
            return false;
        }

        logger.LogDebug("Sending webhook to {WebhookName}", option.WebhookName);
        return true;
    }

    /// <summary>
    /// Checks message body and logs.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="option">The base option.</param>
    /// <param name="body">The message body (may be trimmed in-place).</param>
    /// <returns>True if the message should be sent; false otherwise.</returns>
    protected bool SendMessageBody(ILogger logger, BaseOption option, ref string body)
    {
        if (option.TrimWhitespace)
        {
            body = body.Trim();
        }

        if (option.SkipEmptyMessageBody && string.IsNullOrEmpty(body))
        {
            logger.LogDebug("Empty message body for {WebhookName}, skipping", option.WebhookName);
            return false;
        }

        return true;
    }
}
