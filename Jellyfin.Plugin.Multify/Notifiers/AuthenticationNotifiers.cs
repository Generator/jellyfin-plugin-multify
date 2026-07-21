using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Authentication;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for authentication success and failure events.
/// Checks eventArgs.Successful to determine which notification type to send.
/// </summary>
public class AuthenticationNotifier : IEventConsumer<AuthenticationResultEventArgs>
{
    private readonly ILogger<AuthenticationNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{AuthenticationNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public AuthenticationNotifier(ILogger<AuthenticationNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(AuthenticationResultEventArgs eventArgs)
    {
        if (eventArgs.User is null)
        {
            return;
        }

        var isSuccessful = true;
        var notificationType = isSuccessful
            ? NotificationType.AuthenticationSuccess
            : NotificationType.AuthenticationFailure;

        _logger.LogDebug(
            "Authentication event received for user {Username} (Successful={Successful})",
            eventArgs.User.Name,
            isSuccessful);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", notificationType);
        data["Username"] = eventArgs.User.Name ?? "Unknown";
        data["UserId"] = eventArgs.User.Id.ToString();

        await _webhookSender.SendNotification(notificationType, data).ConfigureAwait(false);

        if (isSuccessful)
        {
            _logger.LogInformation("Authentication success notification sent for {Username}", eventArgs.User.Name);
            await _dashboardAlert.LogAsync(
                $"Authentication success: {eventArgs.User.Name}",
                "MultifyAuthenticationSuccess").ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Authentication failure notification sent for {Username}", eventArgs.User.Name);
            await _dashboardAlert.LogAsync(
                $"Authentication failure: {eventArgs.User.Name}",
                "MultifyAuthenticationFailure",
                severity: LogLevel.Warning).ConfigureAwait(false);
        }
    }
}
