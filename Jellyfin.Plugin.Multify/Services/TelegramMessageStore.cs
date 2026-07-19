using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for storing Telegram message IDs for editing existing notifications.
/// </summary>
public class TelegramMessageStore
{
    private readonly ILogger<TelegramMessageStore> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly string _storePath;
    private readonly ConcurrentDictionary<string, long> _messageStore = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramMessageStore"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TelegramMessageStore}"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    public TelegramMessageStore(ILogger<TelegramMessageStore> logger, IFileSystem fileSystem)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _storePath = _fileSystem.GetInternalDataPath("multify-telegram-messages.json");
        LoadStore();
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
        SaveStore();
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
        SaveStore();
    }

    private static string GetKey(string chatId, string itemId)
    {
        return $"{chatId}:{itemId}";
    }

    private void LoadStore()
    {
        try
        {
            if (_fileSystem.FileExists(_storePath))
            {
                var json = _fileSystem.ReadAllText(_storePath);
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
    }

    private void SaveStore()
    {
        try
        {
            var json = JsonSerializer.Serialize(_messageStore);
            _fileSystem.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Telegram message store");
        }
    }
}
