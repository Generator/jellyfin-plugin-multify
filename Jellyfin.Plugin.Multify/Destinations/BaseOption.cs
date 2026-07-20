using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Base configuration option for all destinations.
/// </summary>
public class BaseOption
{
    /// <summary>Gets or sets the webhook name.</summary>
    public string WebhookName { get; set; } = string.Empty;

    /// <summary>Gets or sets the webhook URI.</summary>
    public string? WebhookUri { get; set; }

    /// <summary>Gets or sets whether the webhook is enabled.</summary>
    public bool EnableWebhook { get; set; } = true;

    /// <summary>Gets or sets the notification types.</summary>
    public NotificationType[] NotificationTypes { get; set; } = Array.Empty<NotificationType>();

    /// <summary>Gets or sets the template (base64 encoded).</summary>
    public string? Template { get; set; }

    /// <summary>Gets or sets whether movies are enabled.</summary>
    public bool EnableMovies { get; set; } = true;

    /// <summary>Gets or sets whether episodes are enabled.</summary>
    public bool EnableEpisodes { get; set; } = true;

    /// <summary>Gets or sets whether series are enabled.</summary>
    public bool EnableSeries { get; set; } = true;

    /// <summary>Gets or sets whether seasons are enabled.</summary>
    public bool EnableSeasons { get; set; } = true;

    /// <summary>Gets or sets whether albums are enabled.</summary>
    public bool EnableAlbums { get; set; } = true;

    /// <summary>Gets or sets whether songs are enabled.</summary>
    public bool EnableSongs { get; set; } = true;

    /// <summary>Gets or sets whether videos are enabled.</summary>
    public bool EnableVideos { get; set; } = true;

    /// <summary>Gets or sets whether to send all properties.</summary>
    public bool SendAllProperties { get; set; }

    /// <summary>Gets or sets whether to trim whitespace.</summary>
    public bool TrimWhitespace { get; set; }

    /// <summary>Gets or sets whether to skip empty message body.</summary>
    public bool SkipEmptyMessageBody { get; set; }

    /// <summary>Gets or sets the user filter.</summary>
    public Guid[] UserFilter { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Compiles the template with the given data.
    /// </summary>
    /// <param name="data">The data dictionary.</param>
    /// <returns>The compiled message body.</returns>
    public string GetMessageBody(Dictionary<string, object> data)
    {
        if (SendAllProperties)
        {
            return System.Text.Json.JsonSerializer.Serialize(data);
        }

        if (string.IsNullOrEmpty(Template))
        {
            // Fallback: serialize data as JSON when no template is configured
            return System.Text.Json.JsonSerializer.Serialize(data);
        }

        try
        {
            var templateBytes = Convert.FromBase64String(Template);
            var template = Encoding.UTF8.GetString(templateBytes);
            return ReplacePlaceholders(template, data);
        }
        catch (FormatException)
        {
            return System.Text.Json.JsonSerializer.Serialize(data);
        }
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, object> data)
    {
        var result = template;
        foreach (var kvp in data)
        {
            var placeholder = "{{" + kvp.Key + "}}";
            result = result.Replace(placeholder, kvp.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
