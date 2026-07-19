using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for user created events.
/// </summary>
public class UserCreatedNotifier : IEventConsumer<UserCreatedEventArgs>
{
    private readonly ILogger<UserCreatedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserCreatedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserCreatedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public UserCreatedNotifier(ILogger<UserCreatedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        var user = eventArgs.Argument;
        if (user is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.UserCreated);
        data.AddUserData(user);

        await _webhookSender.SendNotification(
            NotificationType.UserCreated,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"User created: {user.Username}",
            "MultifyUserCreated").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for user deleted events.
/// </summary>
public class UserDeletedNotifier : IEventConsumer<UserDeletedEventArgs>
{
    private readonly ILogger<UserDeletedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserDeletedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public UserDeletedNotifier(ILogger<UserDeletedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserDeletedEventArgs eventArgs)
    {
        var user = eventArgs.Argument;
        if (user is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.UserDeleted);
        data.AddUserData(user);

        await _webhookSender.SendNotification(
            NotificationType.UserDeleted,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"User deleted: {user.Username}",
            "MultifyUserDeleted").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for user updated events.
/// TODO: User entity doesn't inherit from EventArgs in Jellyfin 10.11,
/// so it cannot be used with IEventConsumer&lt;T&gt;. This notifier is
/// registered as a regular service and called manually when needed.
/// </summary>
public class UserUpdatedNotifier
{
    private readonly ILogger<UserUpdatedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserUpdatedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserUpdatedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public UserUpdatedNotifier(ILogger<UserUpdatedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <summary>
    /// Processes a user updated event.
    /// </summary>
    /// <param name="user">The updated user.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task OnUserUpdated(User user)
    {
        if (user is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.UserUpdated);
        data.AddUserData(user);

        await _webhookSender.SendNotification(
            NotificationType.UserUpdated,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"User updated: {user.Username}",
            "MultifyUserUpdated").ConfigureAwait(false);
    }
}
