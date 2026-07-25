using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for storing Telegram message IDs for editing existing notifications.
/// </summary>
public sealed class TelegramMessageStore : IDisposable
{
    private readonly ILogger<TelegramMessageStore> _logger;
    private readonly string _storePath;
    private readonly ConcurrentDictionary<string, long> _messageStore = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramMessageStore"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TelegramMessageStore}"/> interface.</param>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    public TelegramMessageStore(ILogger<TelegramMessageStore> logger, IApplicationPaths applicationPaths)
    {
        _logger = logger;
        _storePath = Path.Combine(applicationPaths.DataPath, "multify-telegram-messages.json");
        _ = LoadStoreAsync();
    }

    /// <summary>
    /// Gets the message ID for a chat and item.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="itemId">The item ID.</param>
    /// <returns>The message ID, or null if not found.</returns>
    public long? GetMessageId(string chatId, string itemId)
    {
        var key = GetKey(chatId, itemId);
        return _messageStore.TryGetValue(key, out var messageId) ? messageId : null;
    }

    /// <summary>
    /// Stores the message ID for a chat and item.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="itemId">The item ID.</param>
    /// <param name="messageId">The message ID.</param>
    public void StoreMessageId(string chatId, string itemId, long messageId)
    {
        var key = GetKey(chatId, itemId);
        _messageStore[key] = messageId;
        _ = SaveStoreAsync();
    }

    /// <summary>
    /// Removes the message ID for a chat and item.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="itemId">The item ID.</param>
    public void RemoveMessageId(string chatId, string itemId)
    {
        var key = GetKey(chatId, itemId);
        _messageStore.TryRemove(key, out _);
        _ = SaveStoreAsync();
    }

    /// <summary>
    /// Clears all entries from the store. Useful for periodic cleanup since
    /// Telegram message edits expire after 48 hours anyway.
    /// </summary>
    public void CleanupStaleEntries()
    {
        var count = _messageStore.Count;
        if (count == 0)
        {
            _logger.LogDebug("Telegram message store is empty, nothing to clean up");
            return;
        }

        _messageStore.Clear();
        _ = SaveStoreAsync();
        _logger.LogInformation("Cleared {Count} entries from Telegram message store", count);
    }

    private static string GetKey(string chatId, string itemId)
    {
        return $"{chatId}:{itemId}";
    }

    private async Task LoadStoreAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(_storePath))
            {
                var json = await File.ReadAllTextAsync(_storePath).ConfigureAwait(false);
                var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, long>>(json);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        _messageStore[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Telegram message store");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveStoreAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_messageStore);
            await File.WriteAllTextAsync(_storePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Telegram message store");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the TelegramMessageStore.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _fileLock?.Dispose();
            }

            _disposed = true;
        }
    }
}
