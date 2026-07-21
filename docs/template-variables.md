# Template Variables

Multify supports template variables that can be used in notification templates. Use the `{{variable}}` syntax to include variables in your templates.

## Base Variables

These variables are available in all notification types:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ServerName}}` | Jellyfin server name | `My Jellyfin Server` |
| `{{NotificationType}}` | Event type that triggered the notification | `PlaybackStart` |
| `{{Timestamp}}` | UTC timestamp (ISO 8601) | `2024-01-15T10:30:00.000Z` |

## Item Variables

Available when notifications include item data (playback, library events):

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ItemId}}` | Unique item identifier | `3c9cf20670bedf5866ff224850824948` |
| `{{ItemName}}` | Item display name | `Inception` |
| `{{ItemType}}` | Item type class name | `Movie` |
| `{{LibraryName}}` | Library containing the item | `Movies` |
| `{{ItemUrl}}` | Full Jellyfin URL to the item | `https://jellyfin.example.com/web/#/details?id=3c9cf20670bedf5866ff224850824948` |
| `{{ItemShortId}}` | Short item ID (first 10 chars of GUID) | `3c9cf20670` |
| `{{ProductionYear}}` | Production year | `2010` |
| `{{Overview}}` | Item plot summary | `A thief who steals corporate secrets...` |
| `{{Genres}}` | Genres as comma-separated string | `Action, Sci-Fi, Thriller` |
| `{{PremiereDate}}` | Release date (YYYY-MM-DD) | `2010-07-16` |
| `{{Runtime}}` | Formatted runtime | `2h 28m` |
| `{{OfficialRating}}` | Content rating (MPAA) | `PG-13` |
| `{{CommunityRating}}` | Community rating | `8.8` |
| `{{CriticRating}}` | Critic rating | `74` |
| `{{Tagline}}` | Item tagline | `Your mind is the scene of the crime` |
| `{{OriginalTitle}}` | Original language title | `Inception` |
| `{{OriginalLanguage}}` | Original language code | `en` |
| `{{Studios}}` | Studios as comma-separated string | `Warner Bros., Legendary` |
| `{{ProductionLocations}}` | Production locations as comma-separated string | `USA, UK` |
| `{{Tags}}` | User-defined tags as comma-separated string | `favorite, sci-fi` |
| `{{Path}}` | File path | `/media/movies/Inception.mkv` |
| `{{Container}}` | File container format | `mkv` |
| `{{DateCreated}}` | Date added to library (ISO 8601) | `2024-01-15T10:30:00.000Z` |
| `{{HomePageUrl}}` | Official website URL | `https://www.inception.com` |

## TV Show Variables

Additional variables for TV show episodes and series:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{SeriesName}}` | Parent series name | `Breaking Bad` |
| `{{SeasonNumber}}` | Season number (raw) | `1` |
| `{{SeasonNumber00}}` | Season number (zero-padded to 2 digits) | `01` |
| `{{SeasonNumber000}}` | Season number (zero-padded to 3 digits) | `001` |
| `{{SeasonName}}` | Season name (if available) | `Season 1` |
| `{{EpisodeNumber}}` | Episode number (raw) | `1` |
| `{{EpisodeNumber00}}` | Episode number (zero-padded to 2 digits) | `01` |
| `{{EpisodeNumber000}}` | Episode number (zero-padded to 3 digits) | `001` |
| `{{SeriesStatus}}` | Series status (Series items only) | `Ended` |

## Provider IDs

Media provider identifiers for external services:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{ImdbId}}` | IMDb identifier | `tt1375666` |
| `{{TmdbId}}` | TMDb identifier | `27205` |
| `{{TvdbId}}` | TVDb identifier | `12345` |

## User Variables

Available for user-related events:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{Username}}` | Authenticated username | `john_doe` |
| `{{UserId}}` | Unique user identifier | `abc123` |

## Session Variables

Available for session and playback events:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{Client}}` | Client application name | `Jellyfin Web` |
| `{{DeviceName}}` | Device name | `Chrome on Windows` |
| `{{RemoteEndPoint}}` | Client IP address | `192.168.1.100` |
| `{{SessionId}}` | Unique session identifier | `session123` |
| `{{PlayMethod}}` | Playback method | `Transcode`, `DirectStream`, or `DirectPlay` |
| `{{IsPaused}}` | Whether playback is paused | `True` or `False` |
| `{{VolumeLevel}}` | Current volume level (0-100) | `80` |
| `{{IsMuted}}` | Whether audio is muted | `True` or `False` |
| `{{CanSeek}}` | Whether seeking is possible | `True` or `False` |
| `{{AudioStreamIndex}}` | Active audio stream index | `1` |
| `{{SubtitleStreamIndex}}` | Active subtitle stream index | `0` |
| `{{RepeatMode}}` | Repeat mode | `Off`, `RepeatOne`, or `RepeatAll` |
| `{{PlaybackOrder}}` | Playback order | `Default` or `Shuffle` |
| `{{MediaSourceId}}` | Media source identifier | `source123` |
| `{{LiveStreamId}}` | Live stream identifier | `live123` |

## Playback Variables

Available for playback start and stop events:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PlaybackPositionTicks}}` | Current position in ticks | `1234567890` |
| `{{PlaybackPosition}}` | Formatted position (HH:MM:SS) | `00:15:30` |
| `{{IsAutomated}}` | Whether progress is automated | `True` or `False` |
| `{{PlaySessionId}}` | Play session identifier | `session456` |
| `{{PlayedToCompletion}}` | Whether item played to end (stop only) | `True` or `False` |

## Rating Variables

Available when MDBList API key is configured:

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

## Task Variables

Available for task completion events:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{TaskName}}` | Scheduled task name | `Refresh Library` |
| `{{TaskId}}` | Task identifier | `task123` |
| `{{Status}}` | Task completion status | `Completed` |
| `{{StartTime}}` | Task start time (ISO 8601) | `2024-01-15T10:00:00.000Z` |
| `{{EndTime}}` | Task end time (ISO 8601) | `2024-01-15T10:05:00.000Z` |
| `{{Duration}}` | Task duration | `00:05:00` |

## Plugin Variables

Available for plugin events:

| Variable | Description | Example |
|----------|-------------|---------|
| `{{PluginName}}` | Plugin display name | `Intro Skipper` |
| `{{PluginId}}` | Plugin identifier | `plugin123` |
| `{{NewVersion}}` | New plugin version | `1.2.3` |

## Template Examples

### Playback Notification
```
🎬 {{ItemName}} ({{Year}})
📂 {{LibraryName}} • ⏱️ {{Runtime}}
🎭 {{Genres}}

⭐ {{ImdbRating}}/10 (IMDb) | {{MdblistScore}} (MDBList)
```

### TV Episode Notification
```
📺 {{SeriesName}} - S{{SeasonNumber00}}E{{EpisodeNumber00}}
📝 {{ItemName}}
📂 {{LibraryName}}

{{Overview}}
```

### Library Update Notification
```
🆕 New {{ItemType}} added to {{LibraryName}}!
🎬 {{ItemName}} ({{Year}})
🎭 {{Genres}}
⭐ {{ImdbRating}}/10
```

## Notes

- Variables return empty string if data is not available
- Rating variables only appear if MDBList API key is configured
- TV show variables only appear for episode items
- Library name requires item to be in a library
- Runtime is formatted as hours and minutes (e.g., "1h 30m" or "45m")
