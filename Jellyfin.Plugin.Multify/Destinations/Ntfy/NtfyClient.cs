using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Destinations;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Destinations.Ntfy;

/// <summary>
/// ntfy destination option.
/// </summary>
public class NtfyOption : BaseOption
{
    /// <summary>Gets or sets the ntfy topic.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Gets or sets the priority (1-5).</summary>
    public int Priority { get; set; } = 3;

    /// <summary>Gets or sets whether to enable markdown.</summary>
    public bool EnableMarkdown { get; set; } = true;

    /// <summary>Gets or sets the access token.</summary>
    public string? AccessToken { get; set; }
}

/// <summary>
/// Client for the ntfy destination.
/// </summary>
public class NtfyClient : BaseClient, IWebhookClient<NtfyOption>
{
    private readonly ILogger<NtfyClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NtfyClient"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{NtfyClient}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    public NtfyClient(ILogger<NtfyClient> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task SendAsync(NtfyOption option, Dictionary<string, object> data)
    {
        try
        {
            if (string.IsNullOrEmpty(option.WebhookUri) || string.IsNullOrEmpty(option.Topic))
            {
                throw new ArgumentException("WebhookUri and Topic are required for ntfy");
            }

            if (!SendWebhook(_logger, option, data))
            {
                return;
            }

            var body = option.GetMessageBody(data);
            if (!SendMessageBody(_logger, option, ref body))
            {
                return;
            }

            var uri = new Uri(option.WebhookUri.TrimEnd() + $"/{option.Topic}");

            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Text.Plain)
            };

            request.Headers.Add("Title", "Jellyfin Notification");
            request.Headers.Add("Priority", option.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (option.EnableMarkdown)
            {
                request.Headers.Add("Markdown", "yes");
            }

            if (!string.IsNullOrEmpty(option.AccessToken))
            {
                request.Headers.Add("Authorization", $"Bearer {option.AccessToken}");
            }

            using var response = await _httpClientFactory
                .CreateClient(NamedClient.Default)
                .SendAsync(request)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("ntfy notification sent successfully");
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(e, "Error sending ntfy notification");
        }
    }
}
