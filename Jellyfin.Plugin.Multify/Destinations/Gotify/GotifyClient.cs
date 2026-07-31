using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
    [XmlElement("Token")]
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the message priority.</summary>
    [XmlElement("Priority")]
    public int Priority { get; set; }

    /// <summary>Gets or sets the notification title.</summary>
    [XmlElement("Title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the photo URL template for attaching images.
    /// Supports template variables like <c>{{TmdbPosterUrl}}</c>, <c>{{PrimaryImage}}</c>, etc.
    /// When set, the resolved URL is prepended as an inline markdown image <c>![]({url})</c>
    /// to the message body. When empty, no image is sent.
    /// </summary>
    [XmlElement("PhotoUrlTemplate")]
    public string? PhotoUrlTemplate { get; set; }
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

            // Copy the data dictionary so the shared per-notification data is never mutated
            // (Priority is written to the copy only; the caller's dictionary stays pristine).
            var dataCopy = new Dictionary<string, object>(data);
            dataCopy["Priority"] = option.Priority;

            var body = option.GetMessageBody(dataCopy);
            if (!SendMessageBody(_logger, option, ref body))
            {
                return;
            }

            _logger.LogDebug("Gotify sending {BodyLength} bytes to {WebhookName}: {Body}", body.Length, option.WebhookName, body);

            // Build JSON payload with extras
            var extras = new Dictionary<string, object>
            {
                ["client::display"] = new { contentType = "text/markdown" }
            };

            // If PhotoUrlTemplate is configured, prepend the image as inline markdown
            // to the message body (this works reliably across all Gotify clients).
            if (!string.IsNullOrEmpty(option.PhotoUrlTemplate))
            {
                var imageUrl = BaseOption.ReplacePlaceholders(option.PhotoUrlTemplate, dataCopy);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    // Resize TMDB CDN URLs to w500 to keep image sizes reasonable
                    if (imageUrl.Contains("image.tmdb.org", StringComparison.OrdinalIgnoreCase))
                    {
                        imageUrl = imageUrl.Replace("/original/", "/w500/", StringComparison.OrdinalIgnoreCase);
                    }

                    body = $"![]({imageUrl})\n{body}";
                }
            }

            // Use custom title if provided (with placeholder replacement), otherwise default to WebhookName
            var title = !string.IsNullOrEmpty(option.Title)
                ? BaseOption.ReplacePlaceholders(option.Title, dataCopy)
                : option.WebhookName;

            var payload = new Dictionary<string, object>
            {
                ["message"] = body,
                ["title"] = title,
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

            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogDebug("Gotify response: {Response}", responseJson);

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Gotify notification sent successfully");
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Error sending Gotify notification");
            throw;
        }
    }
}
