using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for item added events.
/// </summary>
public class ItemAddedNotifier : IEventConsumer<ItemChangeEventArgs>
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

    /// <inheritdoc />
    public async Task OnEvent(ItemChangeEventArgs eventArgs)
    {
        if (eventArgs.Item is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemAdded);
        data.AddItemData(eventArgs.Item);

        await _webhookSender.SendNotification(
            NotificationType.ItemAdded,
            data,
            eventArgs.Item.GetType()).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Item added: {eventArgs.Item.Name}",
            "MultifyItemAdded").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for item deleted events.
/// </summary>
public class ItemDeletedNotifier : IEventConsumer<ItemChangeEventArgs>
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

    /// <inheritdoc />
    public async Task OnEvent(ItemChangeEventArgs eventArgs)
    {
        if (eventArgs.Item is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemDeleted);
        data.AddItemData(eventArgs.Item);

        await _webhookSender.SendNotification(
            NotificationType.ItemDeleted,
            data,
            eventArgs.Item.GetType()).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Item deleted: {eventArgs.Item.Name}",
            "MultifyItemDeleted").ConfigureAwait(false);
    }
}
