# Template Variables

Multify supports template variables that can be used in notification templates. Use `{{variable}}` syntax in your templates.

Every variable returns an empty string when its data is unavailable for the current event, so templates won't break — they'll just show blanks.

---

## Base Variables

Always present in every notification.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ServerName}}` | Server identifier (hardcoded `Jellyfin`) | `Jellyfin` |
| `{{NotificationType}}` | Event type that triggered the notification | `PlaybackStart` |
| `{{Timestamp}}` | UTC timestamp (ISO 8601) | `2024-01-15T10:30:00.000Z` |

---

## Item Variables

Available in **library events** (`ItemAdded`, `ItemDeleted`) and **playback events** (`PlaybackStart`, `PlaybackStop`, `PlaybackProgress`).

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ItemId}}` | Item GUID | `3c9cf20670bedf5866ff224850824948` |
| `{{ItemName}}` | Item display name | `Inception` |
| `{{ItemType}}` | Jellyfin item type class name | `Movie`, `Episode`, `Series`, `Season` |
| `{{LibraryName}}` | Library containing the item | `Movies` |
| `{{LibraryId}}` | Library GUID | `a1b2c3d4-...` |
| `{{ItemUrl}}` | Deep link to item in Jellyfin web UI | `https://jellyfin.example.com/web/#/details?id=...` |
| `{{ItemShortId}}` | First 10 hex chars of ItemId | `3c9cf20670` |
| `{{ProductionYear}}` | Production year | `2010` |
| `{{Overview}}` | Plot summary | `A thief who steals corporate secrets...` |
| `{{Genres}}` | Comma-separated genres | `Action, Sci-Fi, Thriller` |
| `{{PremiereDate}}` | Release date (YYYY-MM-DD) | `2010-07-16` |
| `{{Runtime}}` | Formatted duration | `2h 28m` |
| `{{OfficialRating}}` | Content rating (MPAA, TV rating) | `PG-13` |
| `{{CommunityRating}}` | Community rating | `8.8` |
| `{{CriticRating}}` | Critic rating | `74` |
| `{{Tagline}}` | Item tagline | `Your mind is the scene of the crime` |
| `{{OriginalTitle}}` | Original language title | `Inception` |
| `{{Studios}}` | Comma-separated studios | `Warner Bros., Legendary` |
| `{{ProductionLocations}}` | Comma-separated locations | `USA, UK` |
| `{{Tags}}` | Comma-separated user tags | `favorite, sci-fi` |
| `{{Path}}` | File path on server | `/media/movies/Inception.mkv` |
| `{{Container}}` | File container format | `mkv` |
| `{{DateCreated}}` | Date added to library (ISO 8601) | `2024-01-15T10:30:00.000Z` |

### Movie-only

Only populated when `{{ItemType}}` is `Movie`.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{Year}}` | Production year (copy of `ProductionYear` for Movies) | `2010` |

> **Note**: For non-Movie items use `{{ProductionYear}}` instead — `{{Year}}` will be empty.

### TV Episode-only

Only populated when `{{ItemType}}` is `Episode`.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{SeriesName}}` | Parent series name | `Breaking Bad` |
| `{{SeasonName}}` | Season display name | `Season 1` |
| `{{SeasonNumber}}` | Season number (raw) | `1` |
| `{{SeasonNumber00}}` | Season number (zero-padded to 2) | `01` |
| `{{SeasonNumber000}}` | Season number (zero-padded to 3) | `001` |
| `{{EpisodeNumber}}` | Episode number (raw) | `1` |
| `{{EpisodeNumber00}}` | Episode number (zero-padded to 2) | `01` |
| `{{EpisodeNumber000}}` | Episode number (zero-padded to 3) | `001` |

### TV Series-only

Only populated when `{{ItemType}}` is `Series`.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{SeriesStatus}}` | Series release status | `Ended`, `Continuing` |

---

## Provider IDs

Populated when the item has the corresponding external ID.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ImdbId}}` | IMDb identifier | `tt1375666` |
| `{{TmdbId}}` | TMDb identifier | `27205` |
| `{{TvdbId}}` | TVDb identifier | `12345` |

---

## User Info

Available in **playback events** (per-user) and **user events** (UserCreated, UserDeleted, UserLockedOut, UserPasswordChanged, UserUpdated).

| Variable | Description | Example |
|----------|-------------|---------|
| `{{Username}}` | Username | `john_doe` |
| `{{UserId}}` | User GUID | `abc123` |
| `{{NotificationUsername}}` | Username (playback events only) | `john_doe` |

> `{{Username}}` and `{{NotificationUsername}}` are identical in playback events. `{{NotificationUsername}}` is only set during playback notifications.

---

## Client Info

Available in **playback events** and **session events** (SessionStart). Identifies the device/app that initiated the action.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{Client}}` | Client application name | `Jellyfin Web` |
| `{{DeviceName}}` | Device name | `Chrome on Windows` |
| `{{RemoteEndPoint}}` | Client IP address | `192.168.1.100` |
| `{{SessionId}}` | Session GUID | `abc123def456` |

---

## Playback State

Available in **playback events** (`PlaybackStart`, `PlaybackStop`, `PlaybackProgress`). Reflects the current player state at the time of the event.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PlayMethod}}` | How media is being delivered | `DirectPlay`, `DirectStream`, `Transcode` |
| `{{IsPaused}}` | Whether playback is currently paused | `True`, `False` |
| `{{VolumeLevel}}` | Client volume (0–100) | `80` |
| `{{IsMuted}}` | Whether audio is muted | `True`, `False` |
| `{{CanSeek}}` | Whether seeking is permitted | `True`, `False` |
| `{{AudioStreamIndex}}` | Active audio track | `1` |
| `{{SubtitleStreamIndex}}` | Active subtitle track | `0` |
| `{{RepeatMode}}` | Repeat setting | `Off`, `RepeatOne`, `RepeatAll` |
| `{{PlaybackOrder}}` | Queue playback order | `Default`, `Shuffle` |
| `{{MediaSourceId}}` | Media source identifier | `source123` |
| `{{LiveStreamId}}` | Live stream identifier (live TV only) | `live123` |

---

## Playback Position

Available in **playback events**. Captures the moment-in-time data of the event.

| Variable | Description | Availability | Example |
|----------|-------------|--------------|---------|
| `{{PlaybackPositionTicks}}` | Current position in 10 MHz ticks | Start / Progress / Stop | `1234567890` |
| `{{PlaybackPosition}}` | Formatted position (HH:MM:SS) | Start / Progress / Stop | `00:15:30` |
| `{{PlaySessionId}}` | Play session GUID | Start / Progress / Stop | `session456` |
| `{{IsAutomated}}` | Whether this is an automated progress report | Progress only | `True`, `False` |
| `{{PlayedToCompletion}}` | Whether item played to end | Stop only | `True`, `False` |

---

## Jellyfin Images

URLs to item images served by the Jellyfin server.

**Availability**: Library events and playback events, **only when `ServerUrl` is configured** in plugin General settings. Without it, all image variables will be empty.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PrimaryImageUrl}}` | Primary poster image | `https://jellyfin.example.com/Items/abc/Images/Primary` |
| `{{BackdropImageUrl}}` | Backdrop image | `https://jellyfin.example.com/Items/abc/Images/Backdrop` |
| `{{ThumbImageUrl}}` | Thumbnail image | `https://jellyfin.example.com/Items/abc/Images/Thumbnail` |
| `{{LogoImageUrl}}` | Logo image | `https://jellyfin.example.com/Items/abc/Images/Logo` |
| `{{BannerImageUrl}}` | Banner image | `https://jellyfin.example.com/Items/abc/Images/Banner` |

### Per-destination behaviour

- **Telegram**: With `MessageType = Photo`, the photo URL is taken from the data dictionary (`PhotoUrlTemplate` → `PrimaryImageUrl` → `TmdbPosterUrl`), **not** from the template body. The template body becomes the caption only — do not include image URLs in the caption.
- **Gotify**: Image URL is automatically set as `extras.client::notification.bigImageUrl`.
- **ntfy**: Image URL is automatically attached via the `Attach` header.
- **Generic Webhook**: Include image URLs in the JSON payload for custom processing.

---

## Trailers

Available when the item has remote trailers.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{TrailerUrl}}` | First remote trailer URL | `https://www.youtube.com/watch?v=dQw4w9WgXcQ` |
| `{{TrailerYtId}}` | Extracted YouTube video ID | `dQw4w9WgXcQ` |

### Template usage

```
{{ItemName}}
Trailer: {{TrailerUrl}}
YouTube: https://youtu.be/{{TrailerYtId}}
```

---

## TMDb Images

URLs to TMDb-hosted images. Requires the item to have a `TmdbId` provider ID.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{TmdbPosterUrl}}` | TMDb poster (w500) | `https://image.tmdb.org/t/p/w500/9gk7adSYeDvHkCSEhniJIsaVti8.jpg` |
| `{{TmdbBackdropUrl}}` | TMDb backdrop (w1280) | `https://image.tmdb.org/t/p/w1280/8ZTVqvKDQ8emSGUEMjsS4yHAwrp.jpg` |
| `{{TmdbProfileUrl}}` | TMDb profile (w185) | `https://image.tmdb.org/t/p/w185/9gk7adSYeDvHkCSEhniJIsaVti8.jpg` |
| `{{TmdbStillUrl}}` | TMDb still (w300) | `https://image.tmdb.org/t/p/w300/9gk7adSYeDvHkCSEhniJIsaVti8.jpg` |
| `{{TmdbLogoUrl}}` | TMDb logo (w154) | `https://image.tmdb.org/t/p/w154/9gk7adSYeDvHkCSEhniJIsaVti8.jpg` |

---

## TVDB Images

URLs to TVDB-hosted images. Requires the item to have a `TvdbId` provider ID.

| Variable | Description | Example |
|----------|-------------|---------|
---



## Ratings

Available when an **MDBList API key** is configured in plugin General settings. Populated only for items that have an `ImdbId` or `TmdbId`.

| Variable | Description | Example |
|----------|-------------|---------|
| `{{MdblistScore}}` | MDBList aggregate score | `8.5` |
| `{{ImdbRating}}` | IMDb rating | `8.8` |
| `{{TmdbRating}}` | TMDb rating | `8.4` |
| `{{RottenTomatoesRating}}` | Rotten Tomatoes score | `87` |
| `{{MetacriticRating}}` | Metacritic score | `74` |
| `{{LetterboxdRating}}` | Letterboxd rating | `4.2` |
| `{{PopcornRating}}` | Popcorn Time rating | `8.5` |
| `{{TraktRating}}` | Trakt rating | `8.6` |
| `{{MyAnimeListRating}}` | MyAnimeList rating | `9.0` |
| `{{AnilistRating}}` | AniList rating | `8.7` |
| `{{RogerEbertRating}}` | Roger Ebert rating | `4.0` |

---

## Task Variables

Available for **task completion events** (`TaskCompleted`).

| Variable | Description | Example |
|----------|-------------|---------|
| `{{TaskName}}` | Scheduled task name | `Refresh Library` |
| `{{TaskId}}` | Task identifier | `task123` |
| `{{Status}}` | Completion status | `Completed` |
| `{{StartTime}}` | Task start time (ISO 8601) | `2024-01-15T10:00:00.000Z` |
| `{{EndTime}}` | Task end time (ISO 8601) | `2024-01-15T10:05:00.000Z` |
| `{{Duration}}` | Formatted duration | `00:05:00` |

---

## Plugin Variables

Available for **plugin events** (`PluginInstalled`, `PluginUpdated`, `PluginUninstalled`).

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PluginName}}` | Plugin display name | `Intro Skipper` |
| `{{PluginId}}` | Plugin identifier | `plugin123` |
| `{{NewVersion}}` | Version string | `1.2.3` |

---

## Template Examples

### Movie Added

```
🎬 {{ItemName}} ({{Year}})
📂 {{LibraryName}} · ⏱️ {{Runtime}}
🎭 {{Genres}}
⭐ {{ImdbRating}}/10 (IMDb) | {{MdblistScore}} (MDBList)
```

### TV Episode Added

```
📺 {{SeriesName}} - S{{SeasonNumber00}}E{{EpisodeNumber00}}
📝 {{ItemName}} ({{ProductionYear}})
📂 {{LibraryName}}
{{Overview}}
```

### Library Update

```
🆕 New {{ItemType}} added to {{LibraryName}}!
🎬 {{ItemName}} ({{ProductionYear}})
🎭 {{Genres}}
⭐ {{ImdbRating}}/10
```

### Playback Started

```
▶️ {{Client}} · {{DeviceName}}
🎬 {{ItemName}} ({{ProductionYear}})
👤 {{Username}}
⏱️ {{PlaybackPosition}} / {{Runtime}}
📡 {{PlayMethod}}
```

### Generic Item Deleted

```
🗑️ {{ItemType}} removed: {{ItemName}}
📂 {{LibraryName}}
```

---

## Quick Reference: What's Available in Each Event Type

| Variables | ItemAdded / ItemDeleted | PlaybackStart / Stop / Progress | User events | TaskCompleted | Plugin events |
|-----------|------------------------|--------------------------------|-------------|---------------|---------------|
| Base | ✓ | ✓ | ✓ | ✓ | ✓ |
| Item | ✓ | ✓ | — | — | — |
| Episode vars | ✓ | ✓ | — | — | — |
| Series vars | ✓ | ✓ | — | — | — |
| Movie vars | ✓ | ✓ | — | — | — |
| Provider IDs | ✓ | ✓ | — | — | — |
| User | — | ✓ | ✓ | — | — |
| Client | — | ✓ | — | — | — |
| Playback State | — | ✓ | — | — | — |
| Playback Position | — | ✓ | — | — | — |
| Jellyfin Images | ✓ | ✓ | — | — | — |
| TMDb/TVDB Images | ✓ | ✓ | — | — | — |
| Trailers | ✓ | ✓ | — | — | — |
| Ratings | ✓ | ✓ | — | — | — |
| Task | — | — | — | ✓ | — |
| Plugin | — | — | — | — | ✓ |

---

## Message Type Examples

Templates differ depending on the destination's message type. Below are examples for each supported format.

### Telegram — SendText (MarkdownV2)

Uses Telegram's MarkdownV2 parse mode. Characters `_ * [ ] ( ) ~ ` > # + - = | { } . !` must be escaped with `\` in the template body. Variable values are auto-escaped.

```
**New Season Added**

**TV Series:** {{SeriesName}}
**Season:** {{SeasonNumber00}}
**Jellyfin:** [{{ItemShortId}}]({{ItemUrl}})
```

### Telegram — SendPhoto (MarkdownV2 caption)

Sends a photo with a caption. The photo URL is taken from the data dictionary (`PrimaryImageUrl` → `TmdbPosterUrl`), **not** from the template body. The template body becomes the caption text only — do not include image URLs in the caption.

```
**Movie:** {{ItemName}} \({{Year}}\)
{{Overview}}

**Genres:** {{Genres}}
**Library:** {{LibraryName}}
**IMDb:** [{{ImdbId}}](https://www.imdb.com/title/{{ImdbId}}) {{ImdbRating}} ⭐
**TMDB:** {{TmdbRating}} ⭐
```

### Telegram — SendRichMessage (Rich Markdown)

Uses Telegram's Rich Markdown (GitHub Flavored Markdown-like). Supports `**bold**`, `*italic*`, `[links](url)`, `![](url)` inline images, headings, lists, and tables. No character escaping needed.

```
**New Season Added**
![]({{TmdbPosterUrl}})

**TV Series:** {{SeriesName}}
**Season:** {{SeasonNumber00}}
**Jellyfin:** [{{ItemShortId}}]({{ItemUrl}})
```

### Gotify

Plain text with Markdown support. No character escaping needed.

```
**{{ItemName}}** ({{Year}})
{{Overview}}

**Genres:** {{Genres}}
**Rating:** {{ImdbRating}}/10
```

### ntfy

Plain text with Markdown support. Supports `**bold**`, `[links](url)`, and emoji. Tags can be set separately in the config.

```
{{ItemName}} ({{Year}})
{{Overview}}

**Jellyfin:** {{ItemUrl}}
```

### Generic Webhook

Sends a JSON payload. The template is rendered as a string and sent as the request body. Use `SendAllProperties` to send all variables as JSON instead.

```
{
  "event": "{{NotificationType}}",
  "title": "{{ItemName}}",
  "year": "{{Year}}",
  "url": "{{ItemUrl}}"
}
```

---

## Notes

- Variables return an empty string when their data is not available for the current event.
- `{{Year}}` is Movie-only; use `{{ProductionYear}}` for all item types.
- `{{SeriesStatus}}` is Series-only (populated when `ItemType` is `Series`), not available on Episodes.
- Episode variables (`SeasonNumber`, `EpisodeNumber`, etc.) are only populated when `ItemType` is `Episode`.
- Jellyfin image variables require the `ServerUrl` setting in plugin General settings.
- Rating variables require an MDBList API key configured in plugin General settings.
- Library name and library ID require the item to belong to a library.
- Runtime is formatted as hours and minutes (e.g., `1h 30m` or `45m`).
- `{{IsPaused}}` originates from the session's current player state, not from the event arguments.
- `{{MediaSourceId}}` appears in both playback state and event data; values may differ.
