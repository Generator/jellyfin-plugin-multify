using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for session started events.
/// </summary>
public class SessionStartedNotifier : IEventConsumer<SessionStartedEventArgs>
{
    private readonly ILogger<SessionStartedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStartedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{SessionStartedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public SessionStartedNotifier(ILogger<SessionStartedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(SessionStartedEventArgs eventArgs)
    {
        var session = eventArgs.Argument;
        if (session is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.SessionStart);
        data.AddSessionInfo(session);

        await _webhookSender.SendNotification(
            NotificationType.SessionStart,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Session started: {session.Client}",
            "MultifySessionStarted").ConfigureAwait(false);
    }
}
