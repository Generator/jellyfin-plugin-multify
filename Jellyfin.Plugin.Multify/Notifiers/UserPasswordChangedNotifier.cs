using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for user password changed events.
/// </summary>
public class UserPasswordChangedNotifier : IEventConsumer<UserPasswordChangedEventArgs>
{
    private readonly ILogger<UserPasswordChangedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPasswordChangedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{UserPasswordChangedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public UserPasswordChangedNotifier(ILogger<UserPasswordChangedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserPasswordChangedEventArgs eventArgs)
    {
        var user = eventArgs.Argument;
        if (user is null)
        {
            return;
        }

        _logger.LogDebug("User password changed event received: {Username}", user.Username);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.UserPasswordChanged);
        data.AddUserData(user);

        await _webhookSender.SendNotification(
            NotificationType.UserPasswordChanged,
            data).ConfigureAwait(false);

        _logger.LogInformation("User password changed notification sent for {Username}", user.Username);

        await _dashboardAlert.LogAsync(
            $"User password changed: {user.Username}",
            "MultifyUserPasswordChanged").ConfigureAwait(false);
    }
}