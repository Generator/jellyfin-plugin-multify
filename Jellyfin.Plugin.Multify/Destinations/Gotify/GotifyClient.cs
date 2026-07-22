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

namespace Jellyfin.Plugin.Multify.Destinations.Gotify;

/// <summary>
/// Gotify destination option.
/// </summary>
public class GotifyOption : BaseOption
{
    /// <summary>Gets or sets the Gotify application token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the message priority.</summary>
    public int Priority { get; set; }

    /// <summary>Gets or sets the notification title.</summary>
    public string? Title { get; set; }
}

/// <summary>
/// Client for the Gotify destination.
/// </summary>
public class GotifyClient : BaseClient, IWebhookClient<GotifyOption>
{
    private readonly ILogger<GotifyClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GotifyClient"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{GotifyClient}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    /// <param name="filterService">Instance of the <see cref="FilterService"/>.</param>
    public GotifyClient(ILogger<GotifyClient> logger, IHttpClientFactory httpClientFactory, FilterService filterService)
        : base(filterService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task SendAsync(GotifyOption option, Dictionary<string, object> data)
    {
        try
        {
            if (string.IsNullOrEmpty(option.WebhookUri) || string.IsNullOrEmpty(option.Token))
            {
                throw new ArgumentException("WebhookUri and Token are required for Gotify");
            }

            if (!SendWebhook(_logger, option, data))
            {
                return;
            }

            data["Priority"] = option.Priority;

            var body = option.GetMessageBody(data);
            if (!SendMessageBody(_logger, option, ref body))
            {
                return;
            }

            // Build JSON payload with extras
            var extras = new Dictionary<string, object>
            {
                ["client::display"] = new { contentType = "text/markdown" }
            };

            // Add image URL to extras if available (correct format: bigImageUrl)
            if (data.TryGetValue("PrimaryImageUrl", out var imageObj) && imageObj is string imageUrl && !string.IsNullOrEmpty(imageUrl))
            {
                extras["client::notification"] = new { bigImageUrl = imageUrl };
            }

            var payload = new Dictionary<string, object>
            {
                ["message"] = body,
                ["title"] = option.WebhookName,
                ["priority"] = option.Priority,
                ["extras"] = extras
            };

            var json = JsonSerializer.Serialize(payload);
            var uri = new Uri(option.WebhookUri.TrimEnd() + $"/message?token={option.Token}");
            using var content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            using var response = await _httpClientFactory
                .CreateClient(NamedClient.Default)
                .PostAsync(uri, content)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Gotify notification sent successfully");
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(e, "Error sending Gotify notification");
        }
    }
}
