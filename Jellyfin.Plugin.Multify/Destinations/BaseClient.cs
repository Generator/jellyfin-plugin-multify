using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Base client for all destination clients.
/// </summary>
public abstract class BaseClient
{
    /// <summary>
    /// Checks user filter and logs webhook send.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="option">The base option.</param>
    /// <param name="data">The data dictionary.</param>
    /// <returns>True if the notification should be sent; false otherwise.</returns>
    protected bool SendWebhook(ILogger logger, BaseOption option, Dictionary<string, object> data)
    {
        if (!option.EnableWebhook)
        {
            logger.LogDebug("Webhook disabled for {WebhookName}", option.WebhookName);
            return false;
        }

        if (option.UserFilter.Length > 0)
        {
            var userId = data.TryGetValue("UserId", out var userIdObj)
                ? Guid.Parse(userIdObj.ToString()!)
                : Guid.Empty;

            if (Array.IndexOf(option.UserFilter, userId) == -1)
            {
                logger.LogDebug("User {UserId} not in filter for {WebhookName}", userId, option.WebhookName);
                return false;
            }
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
