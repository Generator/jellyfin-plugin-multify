# Supported Notification Events

Multify supports the following notification events from Jellyfin. Each event can be configured to trigger notifications to your configured destinations.

## Playback Events

### Playback Start
- **Trigger**: When a user starts playing media
- **Event Type**: `PlaybackStart`
- **Available Data**: Item details, user info, session info (client, device, IP)

### Playback Stop
- **Trigger**: When a user stops playing media
- **Event Type**: `PlaybackStop`
- **Available Data**: Item details, user info, session info, play duration

### Playback Progress
- **Trigger**: Periodic progress updates during playback
- **Event Type**: `PlaybackProgress`
- **Available Data**: Item details, user info, session info, progress percentage

## Session Events

### Session Started
- **Trigger**: When a user session begins (login)
- **Event Type**: `SessionStarted`
- **Available Data**: User info, session info (client, device, IP)

### Session Ended
- **Trigger**: When a user session ends (logout)
- **Event Type**: `SessionEnded`
- **Available Data**: User info, session info

### Session Activity
- **Trigger**: When a user performs an activity in their session
- **Event Type**: `SessionActivity`
- **Available Data**: User info, session info

## User Events

### User Authentication
- **Trigger**: When a user successfully authenticates
- **Event Type**: `UserAuthentication`
- **Available Data**: User info, session info

### User Created
- **Trigger**: When a new user account is created
- **Event Type**: `UserCreated`
- **Available Data**: User info

### User Deleted
- **Trigger**: When a user account is deleted
- **Event Type**: `UserDeleted`
- **Available Data**: User info

### User Updated
- **Trigger**: When a user account is modified
- **Event Type**: `UserUpdated`
- **Available Data**: User info

### User Password Changed
- **Trigger**: When a user changes their password
- **Event Type**: `UserPasswordChanged`
- **Available Data**: User info

### User Locked Out
- **Trigger**: When a user is locked out due to failed login attempts
- **Event Type**: `UserLockedOut`
- **Available Data**: User info

## Library Events

### Item Added
- **Trigger**: When a new item is added to the library
- **Event Type**: `ItemAdded`
- **Available Data**: Full item details (name, type, library, providers, etc.)

### Item Deleted
- **Trigger**: When an item is removed from the library
- **Event Type**: `ItemDeleted`
- **Available Data**: Item details

### Item Updated
- **Trigger**: When an existing item is updated (metadata refresh, etc.)
- **Event Type**: `ItemUpdated`
- **Available Data**: Item details

## Plugin Events

### Plugin Installed
- **Trigger**: When a new plugin is installed
- **Event Type**: `PluginInstalled`
- **Available Data**: Plugin name, version, ID

### Plugin Updated
- **Trigger**: When a plugin is updated
- **Event Type**: `PluginUpdated`
- **Available Data**: Plugin name, version, ID

### Plugin Uninstalled
- **Trigger**: When a plugin is uninstalled
- **Event Type**: `PluginUninstalled`
- **Available Data**: Plugin name, ID

### Plugin Failed
- **Trigger**: When a plugin fails to load or initialize
- **Event Type**: `PluginFailed`
- **Available Data**: Plugin name, error message

## Task Events

### Task Completed
- **Trigger**: When a scheduled task completes
- **Event Type**: `TaskCompleted`
- **Available Data**: Task name, status, start/end time, duration

## Item Type Filters

Each destination can be configured to notify only for specific item types:

| Item Type | Description |
|-----------|-------------|
| `Movies` | Movie items |
| `Episodes` | TV show episodes |
| `Series` | TV show series |
| `Seasons` | TV show seasons |
| `Albums` | Music albums |
| `Songs` | Audio tracks |
| `Videos` | Video items |

## User Filtering

Notifications can be filtered to only trigger for specific users. Configure the allowed user IDs in the destination settings.

## Template Variables

All events include base template variables (see [Template Variables](template-variables.md) for complete reference). Item-specific events include additional item data variables.
