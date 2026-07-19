using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Interface for the webhook sender.
/// </summary>
public interface IWebhookSender
{
    /// <summary>
    /// Sends a notification to all configured destinations.
    /// </summary>
    /// <param name="notificationType">The notification type.</param>
    /// <param name="itemData">The item data dictionary.</param>
    /// <param name="itemType">The item type (optional).</param>
    /// <returns>A task representing the async operation.</returns>
    Task SendNotification(NotificationType notificationType, Dictionary<string, object> itemData, Type? itemType = null);
}
