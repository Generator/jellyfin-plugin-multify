using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Hosted service that subscribes to ILibraryManager events for item added, updated, and removed.
/// </summary>
public class LibraryEventHostedService : IHostedService
{
    private readonly ILogger<LibraryEventHostedService> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryEventHostedService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{LibraryEventHostedService}"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public LibraryEventHostedService(
        ILogger<LibraryEventHostedService> logger,
        ILibraryManager libraryManager,
        IWebhookSender webhookSender,
        DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscribing to library events");

        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
        _libraryManager.ItemRemoved += OnItemRemoved;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unsubscribing from library events");

        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _libraryManager.ItemRemoved -= OnItemRemoved;

        return Task.CompletedTask;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        _ = Task.Run(async () => await HandleItemAdded(e).ConfigureAwait(false));
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        _ = Task.Run(async () => await HandleItemUpdated(e).ConfigureAwait(false));
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        _ = Task.Run(async () => await HandleItemRemoved(e).ConfigureAwait(false));
    }

    private async Task HandleItemAdded(ItemChangeEventArgs e)
    {
        try
        {
            if (e.Item is null)
            {
                return;
            }

            _logger.LogDebug("Item added event received: {ItemName} ({ItemType})", e.Item.Name, e.Item.GetType().Name);

            var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemAdded);
            data.AddItemData(e.Item);

            await _webhookSender.SendNotification(
                NotificationType.ItemAdded,
                data,
                e.Item.GetType()).ConfigureAwait(false);

            _logger.LogInformation("Item added notification sent for {ItemName}", e.Item.Name);

            await _dashboardAlert.LogAsync(
                $"Item added: {e.Item.Name}",
                "MultifyItemAdded").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item added event");
        }
    }

    private async Task HandleItemUpdated(ItemChangeEventArgs e)
    {
        try
        {
            if (e.Item is null)
            {
                return;
            }

            _logger.LogDebug("Item updated event received: {ItemName} ({ItemType})", e.Item.Name, e.Item.GetType().Name);

            var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemUpdated);
            data.AddItemData(e.Item);

            await _webhookSender.SendNotification(
                NotificationType.ItemUpdated,
                data,
                e.Item.GetType()).ConfigureAwait(false);

            _logger.LogInformation("Item updated notification sent for {ItemName}", e.Item.Name);

            await _dashboardAlert.LogAsync(
                $"Item updated: {e.Item.Name}",
                "MultifyItemUpdated").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item updated event");
        }
    }

    private async Task HandleItemRemoved(ItemChangeEventArgs e)
    {
        try
        {
            if (e.Item is null)
            {
                return;
            }

            _logger.LogDebug("Item removed event received: {ItemName} ({ItemType})", e.Item.Name, e.Item.GetType().Name);

            var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemDeleted);
            data.AddItemData(e.Item);

            await _webhookSender.SendNotification(
                NotificationType.ItemDeleted,
                data,
                e.Item.GetType()).ConfigureAwait(false);

            _logger.LogInformation("Item deleted notification sent for {ItemName}", e.Item.Name);

            await _dashboardAlert.LogAsync(
                $"Item deleted: {e.Item.Name}",
                "MultifyItemDeleted").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item removed event");
        }
    }
}