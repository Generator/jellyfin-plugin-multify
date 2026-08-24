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
/// A single stored Telegram notification, carrying enough metadata to identify
/// the originating item in the history file. Null fields are omitted on serialize.
/// </summary>
/// <param name="MessageId">The Telegram message ID, used to edit the message later.</param>
/// <param name="ItemType">The Jellyfin item type (Movie, Episode, Audio, ...).</param>
/// <param name="ItemName">The item display name.</param>
/// <param name="TmdbId">The TMDB id when available, otherwise null.</param>
/// <param name="Show">Series name for Series/Season/Episode items.</param>
/// <param name="Season">Zero-padded season number for Season/Episode items.</param>
/// <param name="Episode">Zero-padded episode number for Episode items.</param>
/// <param name="Artist">Primary artist for music items.</param>
/// <param name="Album">Album name for music items.</param>
/// <param name="Song">Track name for Audio items.</param>
/// <param name="Movie">Movie name for Movie items.</param>
public sealed record TelegramMessageEntry(
    long MessageId,
    string ItemType,
    string ItemName,
    string? TmdbId,
    string? Show,
    string? Season,
    string? Episode,
    string? Artist,
    string? Album,
    string? Song,
    string? Movie);

/// <summary>
/// Service for storing Telegram message IDs for editing existing notifications.
/// </summary>
public sealed class TelegramMessageStore : IDisposable
{
    private readonly ILogger<TelegramMessageStore> _logger;
    private readonly string _storePath;
    private readonly ConcurrentDictionary<string, TelegramMessageEntry> _messageStore = new();
    private static readonly JsonSerializerOptions StoreJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
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
        LoadStoreSync();
    }

    private void LoadStoreSync()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                var json = File.ReadAllText(_storePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        TelegramMessageEntry entry = prop.Value.ValueKind == JsonValueKind.Number
                            ? new TelegramMessageEntry(prop.Value.GetInt64(), "Unknown", "Unknown", null, null, null, null, null, null, null, null)
                            : prop.Value.Deserialize<TelegramMessageEntry>() ?? new TelegramMessageEntry(0, "Unknown", "Unknown", null, null, null, null, null, null, null, null);

                        if (entry.MessageId != 0)
                        {
                            _messageStore[prop.Name] = entry;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Telegram message store");
        }
    }

    /// <summary>
    /// Gets the message ID for a chat, thread, and item key.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="messageThreadId">The optional forum topic thread ID.</param>
    /// <param name="itemKey">The item key (TMDB id when available, otherwise the Jellyfin ItemId).</param>
    /// <returns>The message ID, or null if not found.</returns>
    public long? GetMessageId(string chatId, int? messageThreadId, string itemKey)
    {
        var key = GetKey(chatId, messageThreadId, itemKey);
        return _messageStore.TryGetValue(key, out var entry) ? entry.MessageId : null;
    }

    /// <summary>
    /// Stores the message entry for a chat, thread, and item key.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="messageThreadId">The forum topic thread ID.</param>
    /// <param name="itemKey">The item key (TMDB id when available, otherwise the Jellyfin ItemId).</param>
    /// <param name="entry">The message entry carrying metadata for the history file.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StoreMessageIdAsync(string chatId, int? messageThreadId, string itemKey, TelegramMessageEntry entry)
    {
        var key = GetKey(chatId, messageThreadId, itemKey);
        _messageStore[key] = entry;
        await SaveStoreAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all entries from the store. Useful for periodic cleanup since
    /// Telegram message edits expire after 48 hours anyway.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CleanupStaleEntriesAsync()
    {
        var count = _messageStore.Count;
        if (count == 0)
        {
            _logger.LogDebug("Telegram message store is empty, nothing to clean up");
            return;
        }

        _messageStore.Clear();
        await SaveStoreAsync().ConfigureAwait(false);
        _logger.LogInformation("Cleared {Count} entries from Telegram message store", count);
    }

    private static string GetKey(string chatId, int? messageThreadId, string itemKey)
    {
        var threadId = messageThreadId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        return $"{chatId}:{threadId}:{itemKey}";
    }

    private async Task SaveStoreAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_messageStore, StoreJsonOptions);
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
