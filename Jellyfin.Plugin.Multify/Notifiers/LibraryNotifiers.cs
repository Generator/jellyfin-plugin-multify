using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for item added events.
/// TODO: ItemChangeEventArgs does not inherit from EventArgs in Jellyfin 10.11,
/// so it cannot be used with IEventConsumer&lt;T&gt;. Implement IItemAddedManager
/// pattern (see jellyfin-plugin-webhook) for proper library event handling.
/// </summary>
public class ItemAddedNotifier
{
    private readonly ILogger<ItemAddedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemAddedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{ItemAddedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public ItemAddedNotifier(ILogger<ItemAddedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <summary>
    /// Processes an item added event.
    /// </summary>
    /// <param name="item">The added item.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task OnItemAdded(MediaBrowser.Controller.Entities.BaseItem item)
    {
        if (item is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemAdded);
        data.AddItemData(item);

        await _webhookSender.SendNotification(
            NotificationType.ItemAdded,
            data,
            item.GetType()).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Item added: {item.Name}",
            "MultifyItemAdded").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for item deleted events.
/// TODO: ItemChangeEventArgs does not inherit from EventArgs in Jellyfin 10.11,
/// so it cannot be used with IEventConsumer&lt;T&gt;. Implement IItemDeletedManager
/// pattern (see jellyfin-plugin-webhook) for proper library event handling.
/// </summary>
public class ItemDeletedNotifier
{
    private readonly ILogger<ItemDeletedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemDeletedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{ItemDeletedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public ItemDeletedNotifier(ILogger<ItemDeletedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <summary>
    /// Processes an item deleted event.
    /// </summary>
    /// <param name="item">The deleted item.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task OnItemDeleted(MediaBrowser.Controller.Entities.BaseItem item)
    {
        if (item is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemDeleted);
        data.AddItemData(item);

        await _webhookSender.SendNotification(
            NotificationType.ItemDeleted,
            data,
            item.GetType()).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Item deleted: {item.Name}",
            "MultifyItemDeleted").ConfigureAwait(false);
    }
}
