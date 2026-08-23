using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Hosted service that subscribes to ILibraryManager events for item added, updated, and removed.
/// </summary>
/// <remarks>
/// <see cref="ILibraryManager.ItemAdded"/> fires before remote metadata providers
/// run, so <c>ProviderIds</c> are usually empty at that point. Added items are queued
/// and processed by <see cref="LibraryEventScheduledTask"/> once metadata is available.
/// </remarks>
public sealed class LibraryEventHostedService : IHostedService, IDisposable
{
    private const int MaxRetries = 10;

    // Item types treated as real episodes (excludes virtual/missing-episode placeholders).
    private static readonly BaseItemKind[] EpisodeItemTypes = { BaseItemKind.Episode };

    private readonly ILogger<LibraryEventHostedService> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;
    private readonly ConcurrentDictionary<Guid, QueuedItem> _queue = new();
    private readonly ConcurrentDictionary<Guid, byte> _notifiedItems = new();
    private CancellationTokenSource? _cancellationTokenSource;

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
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
        _libraryManager.ItemRemoved += OnItemRemoved;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unsubscribing from library events");

        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _libraryManager.ItemRemoved -= OnItemRemoved;

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            _cancellationTokenSource.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is null)
        {
            return;
        }

        var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await HandleItemAdded(e, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown in progress — nothing to do.
                }
            },
            token);
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is null)
        {
            return;
        }

        var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await HandleItemUpdated(e, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown in progress — nothing to do.
                }
            },
            token);
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is null)
        {
            return;
        }

        var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await HandleItemRemoved(e, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown in progress — nothing to do.
                }
            },
            token);
    }

    /// <summary>
    /// Processes the item added queue. Called by <see cref="LibraryEventScheduledTask"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        var snapshot = _queue.ToArray();
        _logger.LogDebug("Processing queue: {Count} items pending", snapshot.Length);

        foreach (var (itemId, queued) in snapshot)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Re-fetch from library to get the latest metadata state.
            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                // Item no longer exists — clean up queue entry.
                _queue.TryRemove(itemId, out _);
                continue;
            }

            // Metadata not refreshed yet and under retry limit.
            if (!HasRequiredMetadata(item) && queued.RetryCount < MaxRetries)
            {
                _logger.LogDebug("Requeue {ItemName}, no provider ids (retry {RetryCount})", item.Name, queued.RetryCount + 1);
                var updated = queued with { RetryCount = queued.RetryCount + 1 };
                _queue.AddOrUpdate(itemId, updated, (_, __) => updated);
                continue;
            }

            // Skip placeholder seasons that have no real episodes. Metadata providers
            // (e.g. TVDB) create virtual seasons for missing episodes; these fire
            // ItemAdded but should not produce "Season Added" notifications. Real
            // seasons with files will have episodes by the time the queue is processed.
            if (item is Season)
            {
                var hasRealEpisodes = false;
                try
                {
                    var episodes = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        ParentId = item.Id,
                        IncludeItemTypes = EpisodeItemTypes
                    });

                    foreach (var episode in episodes)
                    {
                        if (!string.IsNullOrEmpty(episode.Path))
                        {
                            hasRealEpisodes = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking episodes for season {ItemName}, requeuing", item.Name);
                }

                if (!hasRealEpisodes)
                {
                    if (queued.RetryCount < MaxRetries)
                    {
                        _logger.LogDebug("Requeue {ItemName}, season has no real episodes yet (retry {RetryCount})", item.Name, queued.RetryCount + 1);
                        var updated = queued with { RetryCount = queued.RetryCount + 1 };
                        _queue.AddOrUpdate(itemId, updated, (_, __) => updated);
                        continue;
                    }

                    _logger.LogDebug("Skipping {ItemName} — season has no real episodes (placeholder season)", item.Name);
                    _queue.TryRemove(itemId, out _);
                    continue;
                }
            }

            // Metadata is ready or retries exhausted — atomically mark as notified before sending.
            if (!_notifiedItems.TryAdd(item.Id, 0))
            {
                _logger.LogDebug("Skipping {ItemName} — already notified", item.Name);
                _queue.TryRemove(itemId, out _);
                continue;
            }

            // Remove from queue and send notification.
            _queue.TryRemove(itemId, out _);

            try
            {
                await SendItemAddedNotificationAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification for {ItemName}", item.Name);
                // Remove from notified set so we can retry later.
                _notifiedItems.TryRemove(item.Id, out _);
            }
        }
    }

    private async Task HandleItemAdded(ItemChangeEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            // Re-fetch from library to get the latest state; e.Item may be stale.
            var item = _libraryManager.GetItemById(e.Item.Id);
            if (item is null)
            {
                return;
            }

            _logger.LogDebug("Item added event received: {ItemName} ({ItemType}) [ID: {ItemId}]", item.Name, item.GetType().Name, item.Id);

            cancellationToken.ThrowIfCancellationRequested();

            // Always queue — never send immediately. Metadata may still be populating.
            _queue.TryAdd(item.Id, new QueuedItem(item.Id, NotificationType.ItemAdded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item added event");
        }
    }

    private async Task HandleItemUpdated(ItemChangeEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            // Re-fetch from library to get the latest state; e.Item may be stale.
            var item = _libraryManager.GetItemById(e.Item.Id);
            if (item is null)
            {
                return;
            }

            _logger.LogDebug("Item updated event received: {ItemName} ({ItemType}) [ID: {ItemId}]", item.Name, item.GetType().Name, item.Id);

            cancellationToken.ThrowIfCancellationRequested();

            var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemUpdated);
            data.AddItemData(item);

            await _webhookSender.SendNotification(
                NotificationType.ItemUpdated,
                data,
                item.GetType()).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Item updated notification sent for {ItemName}", item.Name);

            await _dashboardAlert.LogAsync(
                $"Item updated: {item.Name}",
                "MultifyItemUpdated").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item updated event");
        }
    }

    private async Task HandleItemRemoved(ItemChangeEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            // Re-fetch from library to get the latest state; e.Item may be stale.
            var item = _libraryManager.GetItemById(e.Item.Id);
            if (item is null)
            {
                return;
            }

            _logger.LogDebug("Item removed event received: {ItemName} ({ItemType})", item.Name, item.GetType().Name);

            cancellationToken.ThrowIfCancellationRequested();

            var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemDeleted);
            data.AddItemData(item);

            await _webhookSender.SendNotification(
                NotificationType.ItemDeleted,
                data,
                item.GetType()).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Item deleted notification sent for {ItemName}", item.Name);

            await _dashboardAlert.LogAsync(
                $"Item deleted: {item.Name}",
                "MultifyItemDeleted").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling item removed event");
        }
    }

    private async Task SendItemAddedNotificationAsync(BaseItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.ItemAdded);
        data.AddItemData(item);

        await _webhookSender.SendNotification(
            NotificationType.ItemAdded,
            data,
            item.GetType()).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Item added notification sent for {ItemName}", item.Name);

        await _dashboardAlert.LogAsync(
            $"Item added: {item.Name}",
            "MultifyItemAdded").ConfigureAwait(false);
    }

    private static bool HasRequiredMetadata(BaseItem item)
    {
        // Send once any provider ID is present; Jellyfin may have parsed it from
        // the folder/file name before remote metadata is fully downloaded.
        return item.ProviderIds.Keys.Count > 0;
    }

    private sealed record QueuedItem(Guid ItemId, NotificationType NotificationType, int RetryCount = 0);
}
