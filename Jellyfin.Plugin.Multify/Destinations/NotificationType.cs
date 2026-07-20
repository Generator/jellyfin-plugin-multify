namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Notification type enum.
/// </summary>
public enum NotificationType
{
    /// <summary>No notification.</summary>
    None = 0,

    /// <summary>Item added to library.</summary>
    ItemAdded = 1,

    /// <summary>Generic webhook notification.</summary>
    Generic = 2,

    /// <summary>Playback started.</summary>
    PlaybackStart = 3,

    /// <summary>Playback progress.</summary>
    PlaybackProgress = 4,

    /// <summary>Playback stopped.</summary>
    PlaybackStop = 5,

    /// <summary>Subtitle download failure.</summary>
    SubtitleDownloadFailure = 6,

    /// <summary>Authentication failure.</summary>
    AuthenticationFailure = 7,

    /// <summary>Authentication success.</summary>
    AuthenticationSuccess = 8,

    /// <summary>Session start.</summary>
    SessionStart = 9,

    /// <summary>Pending restart.</summary>
    PendingRestart = 10,

    /// <summary>Task completed.</summary>
    TaskCompleted = 11,

    /// <summary>Plugin installed.</summary>
    PluginInstalled = 14,

    /// <summary>Plugin uninstalled.</summary>
    PluginUninstalled = 16,

    /// <summary>Plugin updated.</summary>
    PluginUpdated = 17,

    /// <summary>User created.</summary>
    UserCreated = 18,

    /// <summary>User deleted.</summary>
    UserDeleted = 19,

    /// <summary>User locked out.</summary>
    UserLockedOut = 20,

    /// <summary>User password changed.</summary>
    UserPasswordChanged = 21,

    /// <summary>User updated.</summary>
    UserUpdated = 22,

    /// <summary>User data saved.</summary>
    UserDataSaved = 23,

    /// <summary>Item deleted.</summary>
    ItemDeleted = 24
}
