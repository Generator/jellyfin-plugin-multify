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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private static readonly JsonSerializerOptions StoreJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly TimeSpan _saveDebounceDelay = TimeSpan.FromSeconds(2);
    private long _storeVersion;
    private long _savedVersion;
    private int _saveRunning;
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
    /// Gets a per-key semaphore for serializing Get→Edit/Send→Store sequences.
    /// Caller must await WaitAsync and Release.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="messageThreadId">The optional forum topic thread ID.</param>
    /// <param name="itemKey">The item key (TMDB id when available, otherwise the Jellyfin ItemId).</param>
    /// <returns>The per-key semaphore.</returns>
    public SemaphoreSlim GetKeyLock(string chatId, int? messageThreadId, string itemKey)
    {
        var key = GetKey(chatId, messageThreadId, itemKey);
        return _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
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
        Interlocked.Increment(ref _storeVersion);
        await DebouncedSaveAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Persists the store with debounce: bursts of <see cref="StoreMessageIdAsync"/>
    /// calls (e.g. a library scan adding many episodes) collapse into a single delayed
    /// write instead of one full serialization per entry. Version tracking guarantees
    /// a trailing save so no entry is lost even if it arrives while a save is already
    /// scheduled or running.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task DebouncedSaveAsync()
    {
        if (Interlocked.CompareExchange(ref _saveRunning, 1, 0) == 1)
        {
            // A save loop is already scheduled or running; our entry is in the
            // dictionary and the loop's version check will pick it up.
            return;
        }

        try
        {
            while (true)
            {
                await Task.Delay(_saveDebounceDelay).ConfigureAwait(false);
                await SaveStoreAsync().ConfigureAwait(false);
                Interlocked.Exchange(ref _savedVersion, Volatile.Read(ref _storeVersion));
                if (Volatile.Read(ref _savedVersion) >= Volatile.Read(ref _storeVersion))
                {
                    return;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _saveRunning, 0);

            // An entry may have arrived after the final version check above but
            // before the flag was cleared; reschedule once if so (bounded: the
            // rescheduled call either saves or exits immediately).
            if (Volatile.Read(ref _savedVersion) < Volatile.Read(ref _storeVersion))
            {
                await DebouncedSaveAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Clears all entries from the store. Useful for periodic cleanup since
    /// Telegram message edits expire after 48 hours anyway.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task CleanupStaleEntriesAsync()
    {
        try
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown in progress — the store is being disposed.
            return;
        }

        try
        {
            var count = _messageStore.Count;
            if (count == 0)
            {
                _logger.LogDebug("Telegram message store is empty, nothing to clean up");
                return;
            }

            _messageStore.Clear();
            // Drop per-key locks to avoid unbounded growth, but do NOT dispose
            // them: a lock held by an active send/edit sequence stays valid via
            // the holder's reference, and disposing it here would race the
            // holder's Release() with ObjectDisposedException. Abandoned locks
            // are reclaimed by the garbage collector once released.
            _keyLocks.Clear();
            var json = JsonSerializer.Serialize(_messageStore, StoreJsonOptions);
            await File.WriteAllTextAsync(_storePath, json).ConfigureAwait(false);
            _logger.LogInformation("Cleared {Count} entries from Telegram message store", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear Telegram message store");
        }
        finally
        {
            try
            {
                _fileLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Shutdown in progress — the store is being disposed.
            }
        }
    }

    private static string GetKey(string chatId, int? messageThreadId, string itemKey)
    {
        var threadId = messageThreadId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        return $"{chatId}:{threadId}:{itemKey}";
    }

    private async Task SaveStoreAsync()
    {
        try
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Shutdown in progress — the store is being disposed.
            return;
        }

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
            try
            {
                _fileLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Shutdown in progress — the store is being disposed.
            }
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

                // Drop per-key locks without disposing them: a lock held by an
                // active send/edit sequence stays usable via the holder's own
                // reference instead of hitting ObjectDisposedException on Release().
                _keyLocks.Clear();
            }

            _disposed = true;
        }
    }
}
