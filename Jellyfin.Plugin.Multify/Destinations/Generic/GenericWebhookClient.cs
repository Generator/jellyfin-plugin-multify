using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Destinations.Generic;

/// <summary>
/// Client for the <see cref="GenericWebhookOption"/>.
/// </summary>
public class GenericWebhookClient : BaseClient, IWebhookClient<GenericWebhookOption>
{
    private readonly ILogger<GenericWebhookClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericWebhookClient"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{GenericWebhookClient}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/>.</param>
    /// <param name="filterService">Instance of the <see cref="FilterService"/>.</param>
    public GenericWebhookClient(ILogger<GenericWebhookClient> logger, IHttpClientFactory httpClientFactory, FilterService filterService)
        : base(filterService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task SendAsync(GenericWebhookOption option, Dictionary<string, object> data)
    {
        try
        {
            if (string.IsNullOrEmpty(option.WebhookUri))
            {
                throw new ArgumentException("WebhookUri is required for generic webhook");
            }

            if (!SendWebhook(_logger, option, data))
            {
                return;
            }

            // Merge custom fields into data
            foreach (var field in option.Fields)
            {
                if (string.IsNullOrEmpty(field.Key))
                {
                    continue;
                }

                data[field.Key] = field.Value;
            }

            var body = option.GetMessageBody(data);
            if (!SendMessageBody(_logger, option, ref body))
            {
                return;
            }

            _logger.LogDebug("GenericWebhook sending {BodyLength} bytes to {WebhookName}", body.Length, option.WebhookName);

            using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, option.WebhookUri);
            var contentType = MediaTypeNames.Application.Json;

            foreach (var header in option.Headers)
            {
                if (string.IsNullOrEmpty(header.Key) || string.IsNullOrEmpty(header.Value))
                {
                    continue;
                }

                // Content-Type must be set on the content, not the request headers
                if (string.Equals("Content-Type", header.Key, StringComparison.OrdinalIgnoreCase))
                {
                    contentType = header.Value;
                }
                else
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            httpRequestMessage.Content = new StringContent(body, Encoding.UTF8, contentType);
            using var response = await _httpClientFactory
                .CreateClient(NamedClient.Default)
                .SendAsync(httpRequestMessage)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Generic webhook notification sent successfully");
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Error sending generic webhook notification");
            throw;
        }
    }
}
