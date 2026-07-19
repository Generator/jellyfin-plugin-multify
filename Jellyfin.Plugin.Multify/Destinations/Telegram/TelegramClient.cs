using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Destinations.Telegram;

/// <summary>
/// Telegram message type.
/// </summary>
public enum TelegramMessageType
{
    /// <summary>Text-only message via sendMessage.</summary>
    SendText = 0,

    /// <summary>Photo with caption via sendPhoto.</summary>
    SendPhoto = 1,

    /// <summary>Rich formatted message via sendRichMessage.</summary>
    SendRichMessage = 2
}

/// <summary>
/// Telegram destination option.
/// </summary>
public class TelegramOption : BaseOption
{
    /// <summary>Gets or sets the bot token.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the chat ID.</summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parse mode.</summary>
    public string ParseMode { get; set; } = "HTML";

    /// <summary>Gets or sets the message type.</summary>
    public TelegramMessageType MessageType { get; set; }
}

/// <summary>
/// Client for the Telegram destination.
/// </summary>
public class TelegramClient : BaseClient, IWebhookClient<TelegramOption>
{
    private const string ApiBaseUrl = "https://api.telegram.org/bot";

    private readonly ILogger<TelegramClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramMessageStore? _messageStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramClient"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TelegramClient}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    /// <param name="messageStore">Instance of the <see cref="TelegramMessageStore"/>.</param>
    public TelegramClient(ILogger<TelegramClient> logger, IHttpClientFactory httpClientFactory, TelegramMessageStore? messageStore = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _messageStore = messageStore;
    }

    /// <inheritdoc />
    public async Task SendAsync(TelegramOption option, Dictionary<string, object> data)
    {
        try
        {
            if (string.IsNullOrEmpty(option.BotToken) || string.IsNullOrEmpty(option.ChatId))
            {
                throw new ArgumentException("BotToken and ChatId are required for Telegram");
            }

            if (!SendWebhook(_logger, option, data))
            {
                return;
            }

            var body = option.GetMessageBody(data);
            if (!SendMessageBody(_logger, option, body))
            {
                return;
            }

            // Check if we should edit an existing message
            if (_messageStore != null && data.TryGetValue("ItemId", out var itemIdObj) && itemIdObj is string itemId)
            {
                var existingMessageId = _messageStore.GetMessageId(option.ChatId, itemId);
                if (existingMessageId.HasValue)
                {
                    await EditMessageAsync(option, data, body, existingMessageId.Value).ConfigureAwait(false);
                    return;
                }
            }

            // Send new message
            long? newMessageId = null;
            switch (option.MessageType)
            {
                case TelegramMessageType.SendPhoto:
                    newMessageId = await SendPhotoAsync(option, data, body).ConfigureAwait(false);
                    break;
                case TelegramMessageType.SendRichMessage:
                    newMessageId = await SendRichMessageAsync(option, data, body).ConfigureAwait(false);
                    break;
                default:
                    newMessageId = await SendTextAsync(option, body).ConfigureAwait(false);
                    break;
            }

            // Store the message ID for future edits
            if (_messageStore != null && newMessageId.HasValue && data.TryGetValue("ItemId", out var itemIdObj2) && itemIdObj2 is string itemId2)
            {
                _messageStore.StoreMessageId(option.ChatId, itemId2, newMessageId.Value);
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(e, "Error sending Telegram notification");
        }
    }

    private async Task EditMessageAsync(TelegramOption option, Dictionary<string, object> data, string body, long messageId)
    {
        try
        {
            switch (option.MessageType)
            {
                case TelegramMessageType.SendPhoto:
                    await EditPhotoMessageAsync(option, data, body, messageId).ConfigureAwait(false);
                    break;
                case TelegramMessageType.SendRichMessage:
                    await EditRichMessageAsync(option, body, messageId).ConfigureAwait(false);
                    break;
                default:
                    await EditTextMessageAsync(option, body, messageId).ConfigureAwait(false);
                    break;
            }

            _logger.LogDebug("Telegram message {MessageId} edited successfully", messageId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error editing Telegram message {MessageId}, sending new message", messageId);
            // Fall back to sending a new message
            switch (option.MessageType)
            {
                case TelegramMessageType.SendPhoto:
                    await SendPhotoAsync(option, data, body).ConfigureAwait(false);
                    break;
                case TelegramMessageType.SendRichMessage:
                    await SendRichMessageAsync(option, data, body).ConfigureAwait(false);
                    break;
                default:
                    await SendTextAsync(option, body).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task<long?> EditTextMessageAsync(TelegramOption option, string body, long messageId)
    {
        var payload = new
        {
            chat_id = option.ChatId,
            message_id = messageId,
            text = body,
            parse_mode = option.ParseMode
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editMessageText");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return messageId;
    }

    private async Task EditPhotoMessageAsync(TelegramOption option, Dictionary<string, object> data, string body, long messageId)
    {
        if (!data.TryGetValue("PhotoUrl", out var photoUrlObj) || string.IsNullOrEmpty(photoUrlObj?.ToString()))
        {
            _logger.LogWarning("Photo message type selected but no PhotoUrl in data, falling back to text");
            await EditTextMessageAsync(option, body, messageId).ConfigureAwait(false);
            return;
        }

        var payload = new
        {
            chat_id = option.ChatId,
            message_id = messageId,
            caption = body,
            parse_mode = option.ParseMode
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editMessageCaption");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private async Task EditRichMessageAsync(TelegramOption option, string body, long messageId)
    {
        var richMessage = new
        {
            content = new
            {
                blocks = new object[]
                {
                    new
                    {
                        type = "paragraph",
                        text = body
                    }
                }
            }
        };

        var payload = new
        {
            chat_id = option.ChatId,
            message_id = messageId,
            rich_message = richMessage
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editRichMessage");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    private async Task<long?> SendTextAsync(TelegramOption option, string body)
    {
        var payload = new
        {
            chat_id = option.ChatId,
            text = body,
            parse_mode = option.ParseMode
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendMessage");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (result.TryGetProperty("result", out var resultElement) && resultElement.TryGetProperty("message_id", out var messageIdElement))
        {
            return messageIdElement.GetInt64();
        }

        return null;
    }

    private async Task<long?> SendPhotoAsync(TelegramOption option, Dictionary<string, object> data, string body)
    {
        if (!data.TryGetValue("PhotoUrl", out var photoUrlObj) || string.IsNullOrEmpty(photoUrlObj?.ToString()))
        {
            _logger.LogWarning("Photo message type selected but no PhotoUrl in data, falling back to text");
            return await SendTextAsync(option, body).ConfigureAwait(false);
        }

        var photoUrl = photoUrlObj.ToString()!;

        var payload = new
        {
            chat_id = option.ChatId,
            photo = photoUrl,
            caption = body,
            parse_mode = option.ParseMode
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendPhoto");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (result.TryGetProperty("result", out var resultElement) && resultElement.TryGetProperty("message_id", out var messageIdElement))
        {
            return messageIdElement.GetInt64();
        }

        return null;
    }

    private async Task<long?> SendRichMessageAsync(TelegramOption option, Dictionary<string, object> data, string body)
    {
        var richMessage = new
        {
            content = new
            {
                blocks = new object[]
                {
                    new
                    {
                        type = "paragraph",
                        text = body
                    }
                }
            }
        };

        var payload = new
        {
            chat_id = option.ChatId,
            rich_message = richMessage
        };

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendRichMessage");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (result.TryGetProperty("result", out var resultElement) && resultElement.TryGetProperty("message_id", out var messageIdElement))
        {
            return messageIdElement.GetInt64();
        }

        return null;
    }
}
