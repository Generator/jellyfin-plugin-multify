using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
/// Note: Telegram uses Bot Token in the URL path (https://api.telegram.org/bot{token}/METHOD).
/// The WebhookUri field from BaseOption is ignored for Telegram destinations.
/// </summary>
public class TelegramOption : BaseOption
{
    /// <summary>Gets or sets the bot token.</summary>
    [XmlElement("BotToken")]
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the chat ID.</summary>
    [XmlElement("ChatId")]
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Gets or sets the parse mode.</summary>
    [XmlElement("ParseMode")]
    public string ParseMode { get; set; } = "HTML";

    /// <summary>Gets or sets the message type.</summary>
    [XmlElement("MessageType")]
    public TelegramMessageType MessageType { get; set; }

    /// <summary>Gets or sets the optional Telegram Forum Topic thread ID. When set, messages are sent to this specific topic.</summary>
    [XmlElement("MessageThreadId")]
    public int? MessageThreadId { get; set; }
}

/// <summary>
    /// Client for the Telegram destination.
    /// </summary>
    public class TelegramClient : BaseClient, IWebhookClient<TelegramOption>
    {
        // Telegram API uses Bot Token in the URL path (https://api.telegram.org/bot{token}/METHOD),
        // NOT the WebhookUri from BaseOption. The WebhookUri field is ignored for Telegram destinations.
        private const string ApiBaseUrl = "https://api.telegram.org/bot";

        private readonly ILogger<TelegramClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TelegramMessageStore? _messageStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelegramClient"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{TelegramClient}"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
        /// <param name="filterService">Instance of the <see cref="FilterService"/>.</param>
        /// <param name="messageStore">Instance of the <see cref="TelegramMessageStore"/>.</param>
        public TelegramClient(ILogger<TelegramClient> logger, IHttpClientFactory httpClientFactory, FilterService filterService, TelegramMessageStore? messageStore = null)
            : base(filterService)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _messageStore = messageStore;
        }

        /// <summary>
        /// Escapes text for Telegram MarkdownV2 parse mode.
        /// According to Telegram Bot API: characters _ * [ ] ( ) ~ ` > # + - = | { } . ! must be escaped with \.
        /// </summary>
        /// <param name="text">The text to escape.</param>
        /// <returns>Escaped text safe for MarkdownV2.</returns>
        public static string EscapeMarkdownV2(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Characters that must be escaped in MarkdownV2 (outside of code/pre entities)
            // _ * [ ] ( ) ~ ` > # + - = | { } . !
            var charsToEscape = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
            var sb = new StringBuilder(text.Length * 2); // Estimate capacity

            foreach (var c in text)
            {
                if (Array.IndexOf(charsToEscape, c) >= 0)
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes text for Telegram Markdown (legacy) parse mode.
        /// According to Telegram Bot API: characters _ * ` [ must be escaped with \.
        /// </summary>
        /// <param name="text">The text to escape.</param>
        /// <returns>Escaped text safe for Markdown (legacy).</returns>
        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Characters that must be escaped in legacy Markdown
            var charsToEscape = new[] { '_', '*', '`', '[' };
            var sb = new StringBuilder(text.Length * 2);

            foreach (var c in text)
            {
                if (Array.IndexOf(charsToEscape, c) >= 0)
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes text based on the Telegram parse mode.
        /// </summary>
        /// <param name="text">The text to escape.</param>
        /// <param name="parseMode">The parse mode (MarkdownV2, Markdown, HTML, or null/empty).</param>
        /// <returns>Escaped text appropriate for the parse mode.</returns>
        public static string EscapeForParseMode(string text, string? parseMode)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(parseMode))
            {
                return text;
            }

            return parseMode.ToLowerInvariant() switch
            {
                "markdownv2" => EscapeMarkdownV2(text),
                "markdown" => EscapeMarkdown(text),
                "html" => text, // HTML uses different escaping (handled by user in template)
                _ => text
            };
        }

    /// <summary>
    /// Creates a copy of the data dictionary with string values escaped for the given parse mode.
    /// Text values are escaped so special characters appear as raw text in the message body.
    /// URL values (starting with http:// or https://) are NOT escaped because MarkdownV2 does
    /// not allow backslash-escaped characters inside the URL portion of <c>[text](url)</c>
    /// or <c>![alt](url)</c> syntax.
    /// </summary>
    /// <param name="data">The original data dictionary.</param>
    /// <param name="parseMode">The Telegram parse mode (MarkdownV2, Markdown, HTML, or null).</param>
    /// <returns>A new dictionary with escaped string values.</returns>
    private static Dictionary<string, object> EscapeDataValues(Dictionary<string, object> data, string? parseMode)
    {
        if (string.IsNullOrEmpty(parseMode))
        {
            return data;
        }

        var lowerMode = parseMode.ToLowerInvariant();
        if (lowerMode == "html")
        {
            return data;
        }

        var escaped = new Dictionary<string, object>(data.Count);
        foreach (var kvp in data)
        {
            if (kvp.Value is string strValue)
            {
                escaped[kvp.Key] = EscapeForParseMode(strValue, parseMode);
            }
            else
            {
                escaped[kvp.Key] = kvp.Value;
            }
        }

        return escaped;
    }

    /// <summary>
    /// Un-escapes MarkdownV2 characters inside the URL portions of <c>[text](url)</c>
    /// and <c>![alt](url)</c> syntax. URLs must NOT have backslash-escaped characters,
    /// even though the rest of the message does.
    /// </summary>
    /// <param name="body">The fully rendered message body with all placeholders substituted.</param>
    /// <returns>The body with URL portions un-escaped.</returns>
    internal static string UnescapeMarkdownV2Urls(string body)
    {
        // Match [text](url) and ![alt](url) patterns and un-escape the URL portion
        var charsToUnescape = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        return Regex.Replace(body, @"!?\[.*?\]\(([^)]*)\)", match =>
        {
            var url = match.Groups[1].Value;
            foreach (var c in charsToUnescape)
            {
                url = url.Replace($"\\{c}", c.ToString(), StringComparison.Ordinal);
            }

            return match.Value.Replace(match.Groups[1].Value, url, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Creates a base payload dictionary with chat_id and optional message_thread_id.
    /// All Telegram API methods should use this to ensure consistent field inclusion.
    /// </summary>
    private Dictionary<string, object> CreatePayload(TelegramOption option)
    {
        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = option.ChatId
        };

        if (option.MessageThreadId.HasValue)
        {
            payload["message_thread_id"] = option.MessageThreadId.Value;
        }

        return payload;
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

            // Escape variable VALUES before substitution so template formatting markers stay intact
            var escapedData = EscapeDataValues(data, option.ParseMode);
            var body = option.GetMessageBody(escapedData);

            // Post-process: un-escape MarkdownV2 characters inside the URL portions of
            // [text](url) and ![alt](url) syntax. URLs must NOT have backslash-escaped
            // characters, even though plain text does need them escaped.
            if (string.Equals(option.ParseMode, "MarkdownV2", StringComparison.OrdinalIgnoreCase))
            {
                body = UnescapeMarkdownV2Urls(body);
            }

            if (!SendMessageBody(_logger, option, ref body))
            {
                return;
            }

            _logger.LogDebug("Telegram sending {BodyLength} bytes to {WebhookName}: {Body}", body.Length, option.WebhookName, body);

            // Only attempt edit for ItemAdded/ItemUpdated events with a TmdbId
            var notificationType = data.TryGetValue("NotificationType", out var typeObj) ? typeObj?.ToString() : null;
            var isEditEvent = notificationType is "ItemAdded" or "ItemUpdated";
            data.TryGetValue("TmdbId", out var tmdbIdObj);
            var tmdbId = tmdbIdObj as string;

            if (isEditEvent && !string.IsNullOrEmpty(tmdbId) && _messageStore != null)
            {
                var existingMessageId = _messageStore.GetMessageId(option.ChatId, option.MessageThreadId, tmdbId);
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

            // Store the message ID for future edits (only for ItemAdded/ItemUpdated with TmdbId)
            if (isEditEvent && !string.IsNullOrEmpty(tmdbId) && _messageStore != null && newMessageId.HasValue)
            {
                _messageStore.StoreMessageId(option.ChatId, option.MessageThreadId, tmdbId, newMessageId.Value);
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Error sending Telegram notification");
            throw;
        }
    }

    private async Task EditMessageAsync(TelegramOption option, Dictionary<string, object> data, string body, long messageId)
    {
        // Body is already escaped in SendAsync before being passed here
        // Do NOT re-escape, as it would double-escape MarkdownV2 characters
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

            // Store the new message ID so future edits use the correct message
            if (_messageStore != null && newMessageId.HasValue
                && data.TryGetValue("TmdbId", out var tmdbIdObj) && tmdbIdObj is string tmdbId && !string.IsNullOrEmpty(tmdbId))
            {
                _messageStore.StoreMessageId(option.ChatId, option.MessageThreadId, tmdbId, newMessageId.Value);
            }
        }
    }

    private async Task<long?> EditTextMessageAsync(TelegramOption option, string body, long messageId)
    {
        var payload = CreatePayload(option);
        payload["message_id"] = messageId;
        payload["text"] = body;
        payload["parse_mode"] = option.ParseMode;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editMessageText");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Telegram editMessageText failed ({StatusCode}): {ErrorBody}", (int)response.StatusCode, errorJson);
            ThrowWithDescription(response, errorJson);
        }

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

        var payload = CreatePayload(option);
        payload["message_id"] = messageId;
        payload["caption"] = body;
        payload["parse_mode"] = option.ParseMode;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editMessageCaption");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Telegram editMessageCaption failed ({StatusCode}): {ErrorBody}", (int)response.StatusCode, errorJson);
            ThrowWithDescription(response, errorJson);
        }
    }

    private async Task EditRichMessageAsync(TelegramOption option, string body, long messageId)
    {
        var richMessage = new
        {
            markdown = body
        };

        var payload = CreatePayload(option);
        payload["message_id"] = messageId;
        payload["rich_message"] = richMessage;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/editMessageText");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Telegram editMessageText (rich) failed ({StatusCode}): {ErrorBody}", (int)response.StatusCode, errorJson);
            ThrowWithDescription(response, errorJson);
        }
    }

    private async Task<long?> SendTextAsync(TelegramOption option, string body)
    {
        var payload = CreatePayload(option);
        payload["text"] = body;
        payload["parse_mode"] = option.ParseMode;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendMessage");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Telegram sendMessage failed ({StatusCode}): {ErrorBody}", (int)response.StatusCode, errorJson);
            ThrowWithDescription(response, errorJson);
        }

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
        // Try to get photo URL from data - check PrimaryImageUrl first, then PhotoUrl
        string? photoUrl = null;

        if (data.TryGetValue("PrimaryImageUrl", out var primaryObj) && primaryObj is string primaryUrl && !string.IsNullOrEmpty(primaryUrl))
        {
            photoUrl = primaryUrl;
        }
        else if (data.TryGetValue("PhotoUrl", out var photoUrlObj) && photoUrlObj is string photoUrlStr && !string.IsNullOrEmpty(photoUrlStr))
        {
            photoUrl = photoUrlStr;
        }

        if (string.IsNullOrEmpty(photoUrl))
        {
            _logger.LogWarning("Photo message type selected but no photo URL in data, falling back to text");
            return await SendTextAsync(option, body).ConfigureAwait(false);
        }

        var payload = CreatePayload(option);
        payload["photo"] = photoUrl;
        payload["caption"] = body;
        payload["parse_mode"] = option.ParseMode;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendPhoto");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogWarning("Telegram rejected photo URL ({StatusCode}): {ErrorBody}, falling back to text", (int)response.StatusCode, errorJson);
            return await SendTextAsync(option, body).ConfigureAwait(false);
        }

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
        // Use the markdown field so the body is parsed as Rich Markdown (GitHub Flavored Markdown-like),
        // which supports **bold**, *italic*, [links](url), ![](url) images, headings, lists, etc.
        var richMessage = new
        {
            markdown = body
        };

        var payload = CreatePayload(option);
        payload["rich_message"] = richMessage;

        var json = JsonSerializer.Serialize(payload);
        var uri = new Uri($"{ApiBaseUrl}{option.BotToken}/sendRichMessage");

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .PostAsync(uri, content)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Telegram sendRichMessage failed ({StatusCode}): {ErrorBody}", (int)response.StatusCode, errorJson);
            ThrowWithDescription(response, errorJson);
        }

        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (result.TryGetProperty("result", out var resultElement) && resultElement.TryGetProperty("message_id", out var messageIdElement))
        {
            return messageIdElement.GetInt64();
        }

        return null;
    }

    /// <summary>
    /// Throws an <see cref="HttpRequestException"/> with the Telegram error description
    /// as the message, instead of the generic "Response status code does not indicate success" text.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="errorJson">The error body JSON from Telegram.</param>
    private static void ThrowWithDescription(HttpResponseMessage response, string errorJson)
    {
        try
        {
            var errorDoc = JsonSerializer.Deserialize<JsonElement>(errorJson);
            if (errorDoc.TryGetProperty("description", out var desc))
            {
                var message = desc.GetString() ?? "Unknown Telegram error";
                throw new HttpRequestException(message);
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch
        {
            // Ignore parse errors on the error JSON and fall through to generic
        }

        response.EnsureSuccessStatusCode();
    }
}
