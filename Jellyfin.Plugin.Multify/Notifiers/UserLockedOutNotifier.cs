using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for user locked out events.
/// </summary>
public class UserLockedOutNotifier : IEventConsumer<UserLockedOutEventArgs>
{
    private readonly ILogger<UserLockedOutNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLockedOutNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserLockedOutNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public UserLockedOutNotifier(ILogger<UserLockedOutNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserLockedOutEventArgs eventArgs)
    {
        var user = eventArgs.Argument;
        if (user is null)
        {
            return;
        }

        _logger.LogDebug("User locked out event received: {Username}", user.Username);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.UserLockedOut);
        data.AddUserData(user);

        await _webhookSender.SendNotification(
            NotificationType.UserLockedOut,
            data).ConfigureAwait(false);

        _logger.LogInformation("User locked out notification sent for {Username}", user.Username);

        await _dashboardAlert.LogAsync(
            $"User locked out: {user.Username}",
            "MultifyUserLockedOut").ConfigureAwait(false);
    }
}