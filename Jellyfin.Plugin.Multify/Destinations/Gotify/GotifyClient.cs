using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Destinations;
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
    public GotifyClient(ILogger<GotifyClient> logger, IHttpClientFactory httpClientFactory)
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

            var uri = new Uri(option.WebhookUri.TrimEnd() + $"/message?token={option.Token}");
            using var content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);
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
