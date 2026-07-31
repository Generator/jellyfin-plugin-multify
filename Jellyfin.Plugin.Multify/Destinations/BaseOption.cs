using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

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
    [JsonPropertyName("NotificationTypes")]
    [XmlArray("NotificationTypes")]
    [XmlArrayItem("NotificationType")]
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

    /// <summary>Gets or sets the user filter (user IDs as strings, typically GUIDs).</summary>
    [JsonPropertyName("UserFilter")]
    [XmlArray("UserFilter")]
    [XmlArrayItem("Guid")]
    public string[] UserFilter { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the user filter mode (OnlySelected or AllExcept).</summary>
    [JsonPropertyName("UserFilterMode")]
    public FilterMode UserFilterMode { get; set; } = FilterMode.OnlySelected;

    /// <summary>Gets or sets the library filter (library IDs as strings, typically GUIDs).</summary>
    [JsonPropertyName("LibraryFilter")]
    [XmlArray("LibraryFilter")]
    [XmlArrayItem("Guid")]
    public string[] LibraryFilter { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the library filter mode (OnlySelected or AllExcept).</summary>
    [JsonPropertyName("LibraryFilterMode")]
    public FilterMode LibraryFilterMode { get; set; } = FilterMode.OnlySelected;

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
            throw new InvalidOperationException("Template is required but was empty or not configured.");
        }

        try
        {
            var templateBytes = Convert.FromBase64String(Template);
            var template = Encoding.UTF8.GetString(templateBytes);
            return ReplacePlaceholders(template, data);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Template is not valid base64.");
        }
    }

    internal static string ReplacePlaceholders(string template, Dictionary<string, object> data)
    {
        var result = template;
        foreach (var kvp in data)
        {
            var placeholder = "{{" + kvp.Key + "}}";
            // Invariant formatting keeps numbers/dates/bools stable regardless of the
            // server's culture (e.g. decimal point vs comma).
            var valueText = kvp.Value switch
            {
                null => string.Empty,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => kvp.Value.ToString() ?? string.Empty
            };
            result = result.Replace(placeholder, valueText, StringComparison.Ordinal);
        }

        // Safety net: strip markdown link/image syntax with empty/blank URLs
        // (![alt]() or [text]()) to prevent MarkdownV2 parse errors
        result = Regex.Replace(result, @"!?\[.*?\]\(\s*\)", string.Empty);

        return result;
    }
}
