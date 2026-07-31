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

namespace Jellyfin.Plugin.Multify.Destinations.Ntfy;

/// <summary>
/// ntfy destination option.
/// </summary>
public class NtfyOption : BaseOption
{
    /// <summary>Gets or sets the ntfy topic.</summary>
    [XmlElement("Topic")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>Gets or sets the priority (1-5).</summary>
    [XmlElement("Priority")]
    public int Priority { get; set; } = 3;

    /// <summary>Gets or sets whether to enable markdown.</summary>
    [XmlElement("EnableMarkdown")]
    public bool EnableMarkdown { get; set; } = true;

    /// <summary>Gets or sets the access token.</summary>
    [XmlElement("AccessToken")]
    public string? AccessToken { get; set; }

    /// <summary>Gets or sets the notification title.</summary>
    [XmlElement("Title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets comma-separated tags (first tag used as emoji icon).</summary>
    [XmlElement("Tags")]
    public string? Tags { get; set; }

    /// <summary>
    /// Gets or sets the photo URL template for attaching images.
    /// Supports template variables like <c>{{TmdbPosterUrl}}</c>, <c>{{PrimaryImage}}</c>, etc.
    /// When set, this URL is attached to the ntfy notification.
    /// When empty, no image is attached (avoids duplication with inline images in the body).
    /// </summary>
    [XmlElement("PhotoUrlTemplate")]
    public string? PhotoUrlTemplate { get; set; }
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
    /// <param name="filterService">Instance of the <see cref="FilterService"/>.</param>
    public NtfyClient(ILogger<NtfyClient> logger, IHttpClientFactory httpClientFactory, FilterService filterService)
        : base(filterService)
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

            _logger.LogDebug("Ntfy sending {BodyLength} bytes to {WebhookName}: {Body}", body.Length, option.WebhookName, body);

            // Join the base webhook URI and topic, tolerating trailing/leading slashes.
            var uriString = $"{option.WebhookUri.Trim().TrimEnd('/')}/{option.Topic.TrimStart('/')}";
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Invalid ntfy webhook URI: {option.WebhookUri}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Text.Plain)
            };

            // Use custom title if provided, otherwise default - with placeholder replacement
            var title = !string.IsNullOrEmpty(option.Title)
                ? BaseOption.ReplacePlaceholders(option.Title, data)
                : "Jellyfin Notification";

            // ntfy only accepts priorities 1-5; clamp out-of-range values.
            var priority = Math.Clamp(option.Priority, 1, 5);

            // Header values must be ISO-8859-1 and free of CR/LF — sanitize template
            // output and use TryAddWithoutValidation so one bad value can't kill the request.
            request.Headers.TryAddWithoutValidation("Title", EncodeHeaderValue(title));
            request.Headers.TryAddWithoutValidation("Priority", priority.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Add tags if provided (comma-separated, first tag used as emoji icon) - with placeholder replacement
            if (!string.IsNullOrEmpty(option.Tags))
            {
                var tags = BaseOption.ReplacePlaceholders(option.Tags, data);
                request.Headers.TryAddWithoutValidation("Tags", EncodeHeaderValue(tags));
            }

            if (option.EnableMarkdown)
            {
                request.Headers.TryAddWithoutValidation("Markdown", "yes");
            }

            if (!string.IsNullOrEmpty(option.AccessToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {EncodeHeaderValue(option.AccessToken)}");
            }

            // Attach image if PhotoUrlTemplate is configured (resolved with template variables).
            // When no template is set, no image is attached — this lets users use inline
            // images in the body via ![](url) without duplicating them as attachments.
            if (!string.IsNullOrEmpty(option.PhotoUrlTemplate))
            {
                var attachUrl = BaseOption.ReplacePlaceholders(option.PhotoUrlTemplate, data);
                if (!string.IsNullOrEmpty(attachUrl))
                {
                    // Append resize parameters only to local Jellyfin image URLs
                    if (attachUrl.Contains("/Items/", StringComparison.Ordinal) && !attachUrl.Contains('?', StringComparison.Ordinal))
                    {
                        attachUrl += "?maxWidth=800&maxHeight=800";
                    }
                    else if (attachUrl.Contains("/Items/", StringComparison.Ordinal) && attachUrl.Contains('?', StringComparison.Ordinal))
                    {
                        attachUrl += "&maxWidth=800&maxHeight=800";
                    }

                    // Resize TMDB CDN URLs to w500 (~100-300KB) to stay under ntfy.sh's 2MB attachment limit
                    if (attachUrl.Contains("image.tmdb.org", StringComparison.OrdinalIgnoreCase))
                    {
                        attachUrl = attachUrl.Replace("/original/", "/w500/", StringComparison.OrdinalIgnoreCase);
                    }

                    // The Attach header carries the raw URL and is not run through
                    // EncodeHeaderValue, so only accept absolute HTTP(S) URIs here;
                    // otherwise ntfy would reject the header (or misinterpret it).
                    if (Uri.TryCreate(attachUrl, UriKind.Absolute, out var attachUri)
                        && (attachUri.Scheme == Uri.UriSchemeHttp || attachUri.Scheme == Uri.UriSchemeHttps))
                    {
                        request.Headers.TryAddWithoutValidation("Attach", attachUri.AbsoluteUri);
                        request.Headers.TryAddWithoutValidation("Filename", "poster.jpg");
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ntfy: PhotoUrlTemplate resolved to a non-HTTP(S) value '{Url}', skipping attachment",
                            attachUrl);
                    }
                }
            }

            using var response = await _httpClientFactory
                .CreateClient(NamedClient.Default)
                .SendAsync(request)
                .ConfigureAwait(false);

            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogDebug("ntfy response: {Response}", responseJson);

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("ntfy notification sent successfully");
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Error sending ntfy notification");
            throw;
        }
    }

    /// <summary>
    /// Sanitizes a string for use as an HTTP header value: collapses CR/LF into a space,
    /// strips remaining control characters, and RFC 2047-encodes non-ASCII text
    /// (header values must be ISO-8859-1; ntfy expects encoded words for other encodings).
    /// </summary>
    private static string EncodeHeaderValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '\r' or '\n')
            {
                sb.Append(' ');
            }
            else if (!char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        var single = sb.ToString();
        foreach (var c in single)
        {
            if (c > 127)
            {
                return "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(single)) + "?=";
            }
        }

        return single;
    }
}
