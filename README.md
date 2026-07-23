<p align="center">
  <img src="https://github.com/Generator/jellyfin-plugin-multify/raw/main/logo.png" alt="Multify Logo" width="300"/>
</p>

# Jellyfin Plugin Multify

A unified notification plugin for [Jellyfin](https://jellyfin.org/) that sends notifications across multiple systems seamlessly.

> [!WARNING]
> This project was made with AI for personal use, use it at your own risk.

## Features

- **Multiple Destinations** — Send notifications to Telegram, Gotify, ntfy, and any generic webhook simultaneously
- **Configurable UI** — Intuitive plugin configuration page in the Jellyfin admin dashboard
- **Event Filtering** — Choose which event types trigger notifications per destination
- **Media Type Filtering** — Enable or disable notifications for Movies, Episodes, Series, Seasons, Albums, Songs, and Videos
- **User Filtering** — Restrict notifications to specific Jellyfin users
- **Customizable Messages** — Use base64-encoded templates with placeholder variables
- **Markdown Support** — Render rich messages on Telegram (HTML), Gotify (Markdown), and ntfy (Markdown)
- **Generic Webhook** — Send to any HTTP endpoint with custom headers and fields

## Supported Notification Events

See [Supported Notification Events](docs/supported-events.md) for the complete list of all notification events and when they trigger.

## Destinations

### Telegram

| Setting | Description |
|---------|-------------|
| `BotToken` | Telegram bot token from [@BotFather](https://t.me/BotFather) |
| `ChatId` | Target chat or channel ID |
| `ParseMode` | Message format: `HTML`, `Markdown`, or `MarkdownV2` |

**HTML Parse Mode** supports: `<b>`, `<i>`, `<a href="">`, `<code>`, `<pre>`, `<blockquote>`

### Gotify

| Setting | Description |
|---------|-------------|
| `WebhookUri` | Gotify server URL (e.g. `https://gotify.example.com`) |
| `Token` | Application token with message send permission |
| `Priority` | Message priority (0–10, higher = more urgent) |

**Markdown** is enabled via `extras.client::display.contentType = text/markdown`

### ntfy

| Setting | Description |
|---------|-------------|
| `WebhookUri` | ntfy server URL (e.g. `https://ntfy.sh`) |
| `Topic` | Target topic name (acts as a password) |
| `Priority` | 1 = min, 2 = low, 3 = default, 4 = high, 5 = max/urgent |
| `EnableMarkdown` | Enable Markdown formatting |
| `AccessToken` | Optional Bearer token for authentication |

### Generic Webhook

| Setting | Description |
|---------|-------------|
| `WebhookUri` | Any HTTP/HTTPS endpoint URL |
| `Headers` | Custom HTTP headers (key-value pairs) |
| `Fields` | Custom fields merged into notification data (key-value pairs) |

Supports `Content-Type` override via headers. Use `SendAllProperties` to send raw JSON, or define a custom template.

## Configuration

### Per-Destination Options

Each destination supports the following shared options:

| Option | Description |
|--------|-------------|
| `WebhookName` | Display name for the destination |
| `EnableWebhook` | Enable or disable the destination |
| `NotificationTypes` | Array of event types to listen for |
| `UserFilter` | Array of user IDs to include (empty = all users) |
| `EnableMovies` | Send notifications for movies |
| `EnableEpisodes` | Send notifications for episodes |
| `EnableSeries` | Send notifications for series |
| `EnableSeasons` | Send notifications for seasons |
| `EnableAlbums` | Send notifications for music albums |
| `EnableSongs` | Send notifications for individual songs |
| `EnableVideos` | Send notifications for videos |
| `SendAllProperties` | Send raw JSON data (ignores template) |
| `TrimWhitespace` | Trim whitespace from message body |
| `SkipEmptyMessageBody` | Skip sending when message is empty |
| `Template` | Base64-encoded message template |

### Available Template Variables

See [Template Variables](docs/template-variables.md) for the complete reference of all available template variables with examples.

## Installation

### From Release

1. Download the latest `.dll` from [GitHub Releases](../../releases)
2. Place it in your Jellyfin `plugins/Multify` directory
3. Restart Jellyfin
4. Navigate to **Dashboard > Plugins > Multify** and configure

### Build from Source

```bash
dotnet build Jellyfin.Plugin.Multify.csproj -c Release
```

The compiled DLL will be in `bin/Release/net9.0/`.

> **Note:** This project uses GitHub Actions for CI/CD. Pushes to `master` trigger build and test workflows. See `.github/workflows/` for details.

## Documentation

- **[Supported Notification Events](docs/supported-events.md)** — Complete list of all notification events and when they trigger
- **[Template Variables](docs/template-variables.md)** — Full reference of all available template variables with examples
- **[API Documentation](docs/api.md)** — Test notification endpoint and destination configuration schemas

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

## Acknowledgments

Built with patterns from [jellyfin-plugin-webhook](https://github.com/jellyfin/jellyfin-plugin-webhook) and the Jellyfin plugin ecosystem.
