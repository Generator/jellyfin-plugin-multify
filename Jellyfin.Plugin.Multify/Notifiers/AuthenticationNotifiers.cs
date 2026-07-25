using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Authentication;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for authentication success events.
/// </summary>
public class AuthenticationSuccessNotifier : IEventConsumer<AuthenticationResultEventArgs>
{
    private readonly ILogger<AuthenticationSuccessNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationSuccessNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{AuthenticationSuccessNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public AuthenticationSuccessNotifier(ILogger<AuthenticationSuccessNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
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

        _logger.LogDebug(
            "Authentication success event received for user {Username}",
            eventArgs.User.Name);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.AuthenticationSuccess);
        data["Username"] = eventArgs.User.Name ?? "Unknown";
        data["UserId"] = eventArgs.User.Id.ToString();

        await _webhookSender.SendNotification(NotificationType.AuthenticationSuccess, data).ConfigureAwait(false);

        _logger.LogInformation("Authentication success notification sent for {Username}", eventArgs.User.Name);
        await _dashboardAlert.LogAsync(
            $"Authentication success: {eventArgs.User.Name}",
            "MultifyAuthenticationSuccess").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for authentication failure events.
/// </summary>
public class AuthenticationFailureNotifier : IEventConsumer<AuthenticationRequestEventArgs>
{
    private readonly ILogger<AuthenticationFailureNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationFailureNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{AuthenticationFailureNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public AuthenticationFailureNotifier(ILogger<AuthenticationFailureNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(AuthenticationRequestEventArgs eventArgs)
    {
        if (string.IsNullOrEmpty(eventArgs.Username))
        {
            return;
        }

        _logger.LogDebug(
            "Authentication failure event received for user {Username} from {RemoteEndPoint}",
            eventArgs.Username,
            eventArgs.RemoteEndPoint);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.AuthenticationFailure);
        data["Username"] = eventArgs.Username;
        data["RemoteEndPoint"] = eventArgs.RemoteEndPoint ?? "Unknown";

        await _webhookSender.SendNotification(NotificationType.AuthenticationFailure, data).ConfigureAwait(false);

        _logger.LogInformation("Authentication failure notification sent for {Username}", eventArgs.Username);
        await _dashboardAlert.LogAsync(
            $"Authentication failure: {eventArgs.Username} from {eventArgs.RemoteEndPoint}",
            "MultifyAuthenticationFailure").ConfigureAwait(false);
    }
}
