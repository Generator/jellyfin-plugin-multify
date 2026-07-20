using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Request model for test notification.
/// </summary>
public class TestNotificationRequest
{
    /// <summary>Gets or sets the destination type (telegram, gotify, ntfy, generic).</summary>
    [JsonPropertyName("destinationType")]
    public string DestinationType { get; set; } = string.Empty;

    /// <summary>Gets or sets the destination configuration as JSON.</summary>
    [JsonPropertyName("config")]
    public JsonElement Config { get; set; }
}

/// <summary>
/// Response model for test notification.
/// </summary>
public class TestNotificationResponse
{
    /// <summary>Gets or sets whether the test was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Gets or sets the error message if failed.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for the test notification service.
/// </summary>
public interface IMultifyTestService
{
    /// <summary>
    /// Sends a test notification to the specified destination.
    /// </summary>
    /// <param name="request">The test notification request.</param>
    /// <returns>A task representing the async operation.</returns>
    Task<TestNotificationResponse> SendTestNotificationAsync(TestNotificationRequest request);
}

/// <summary>
/// Service for sending test notifications.
/// </summary>
public class MultifyTestService : IMultifyTestService
{
    private readonly ILogger<MultifyTestService> _logger;
    private readonly IWebhookClient<TelegramOption> _telegramClient;
    private readonly IWebhookClient<GotifyOption> _gotifyClient;
    private readonly IWebhookClient<NtfyOption> _ntfyClient;
    private readonly IWebhookClient<GenericWebhookOption> _genericClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifyTestService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MultifyTestService}"/> interface.</param>
    /// <param name="telegramClient">Instance of the <see cref="IWebhookClient{TelegramOption}"/>.</param>
    /// <param name="gotifyClient">Instance of the <see cref="IWebhookClient{GotifyOption}"/>.</param>
    /// <param name="ntfyClient">Instance of the <see cref="IWebhookClient{NtfyOption}"/>.</param>
    /// <param name="genericClient">Instance of the <see cref="IWebhookClient{GenericWebhookOption}"/>.</param>
    public MultifyTestService(
        ILogger<MultifyTestService> logger,
        IWebhookClient<TelegramOption> telegramClient,
        IWebhookClient<GotifyOption> gotifyClient,
        IWebhookClient<NtfyOption> ntfyClient,
        IWebhookClient<GenericWebhookOption> genericClient)
    {
        _logger = logger;
        _telegramClient = telegramClient;
        _gotifyClient = gotifyClient;
        _ntfyClient = ntfyClient;
        _genericClient = genericClient;
    }

    /// <inheritdoc />
    public async Task<TestNotificationResponse> SendTestNotificationAsync(TestNotificationRequest request)
    {
        try
        {
            var data = CreateTestData();
            var option = ParseOption(request);

            if (option == null)
            {
                return new TestNotificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Unsupported destination type: {request.DestinationType}"
                };
            }

            // Ensure webhook is enabled for test
            option.EnableWebhook = true;

            await SendAsync(request.DestinationType, option, data).ConfigureAwait(false);

            _logger.LogDebug("Test notification sent successfully to {DestinationType}", request.DestinationType);

            return new TestNotificationResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending test notification to {DestinationType}", request.DestinationType);

            return new TestNotificationResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static Dictionary<string, object> CreateTestData()
    {
        return new Dictionary<string, object>
        {
            ["Title"] = "Test Notification",
            ["Body"] = "This is a test message to verify your notification configuration is working correctly.",
            ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
            ["ItemType"] = "Movie",
            ["ItemName"] = "Test Movie (2024)",
            ["ServerName"] = "Jellyfin Server"
        };
    }

    private BaseOption? ParseOption(TestNotificationRequest request)
    {
        return request.DestinationType.ToLowerInvariant() switch
        {
            "telegram" => request.Config.Deserialize<TelegramOption>(),
            "gotify" => request.Config.Deserialize<GotifyOption>(),
            "ntfy" => request.Config.Deserialize<NtfyOption>(),
            "generic" => request.Config.Deserialize<GenericWebhookOption>(),
            _ => null
        };
    }

    private async Task SendAsync(string destinationType, BaseOption option, Dictionary<string, object> data)
    {
        switch (destinationType.ToLowerInvariant())
        {
            case "telegram":
                await _telegramClient.SendAsync((TelegramOption)option, data).ConfigureAwait(false);
                break;
            case "gotify":
                await _gotifyClient.SendAsync((GotifyOption)option, data).ConfigureAwait(false);
                break;
            case "ntfy":
                await _ntfyClient.SendAsync((NtfyOption)option, data).ConfigureAwait(false);
                break;
            case "generic":
                await _genericClient.SendAsync((GenericWebhookOption)option, data).ConfigureAwait(false);
                break;
        }
    }
}
