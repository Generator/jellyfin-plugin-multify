using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Events;
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
        if (eventArgs.Result?.User is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.AuthenticationSuccess);
        data.AddUserData(eventArgs.Result.User);

        await _webhookSender.SendNotification(
            NotificationType.AuthenticationSuccess,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Authentication success: {eventArgs.Result.User.Username}",
            "MultifyAuthenticationSuccess").ConfigureAwait(false);
    }
}

/// <summary>
/// Notifier for authentication failure events.
/// </summary>
public class AuthenticationFailureNotifier : IEventConsumer<AuthenticationResultEventArgs>
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
    public async Task OnEvent(AuthenticationResultEventArgs eventArgs)
    {
        if (eventArgs.Result?.User is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.AuthenticationFailure);
        data.AddUserData(eventArgs.Result.User);

        await _webhookSender.SendNotification(
            NotificationType.AuthenticationFailure,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Authentication failure: {eventArgs.Result.User.Username}",
            "MultifyAuthenticationFailure",
            severity: MediaBrowser.Model.Activity.LogSeverity.Warn).ConfigureAwait(false);
    }
}
