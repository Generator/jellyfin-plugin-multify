using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Events;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Notifier for task completed events.
/// </summary>
public class TaskCompletedNotifier : IEventConsumer<TaskCompletionEventArgs>
{
    private readonly ILogger<TaskCompletedNotifier> _logger;
    private readonly IWebhookSender _webhookSender;
    private readonly DashboardAlertService _dashboardAlert;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskCompletedNotifier"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TaskCompletedNotifier}"/> interface.</param>
    /// <param name="webhookSender">Instance of the <see cref="IWebhookSender"/> interface.</param>
    /// <param name="dashboardAlert">Instance of the <see cref="DashboardAlertService"/>.</param>
    public TaskCompletedNotifier(ILogger<TaskCompletedNotifier> logger, IWebhookSender webhookSender, DashboardAlertService dashboardAlert)
    {
        _logger = logger;
        _webhookSender = webhookSender;
        _dashboardAlert = dashboardAlert;
    }

    /// <inheritdoc />
    public async Task OnEvent(TaskCompletionEventArgs eventArgs)
    {
        if (eventArgs.Task is null)
        {
            return;
        }

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.TaskCompleted);
        data["TaskName"] = eventArgs.Task.Name ?? "Unknown";
        data["TaskId"] = eventArgs.Task.Id.ToString();
        data["Status"] = eventArgs.Status?.Status.ToString() ?? "Unknown";
        data["StartTime"] = eventArgs.Status?.StartTimeUtc.ToString("O") ?? string.Empty;
        data["EndTime"] = eventArgs.Status?.EndTimeUtc.ToString("O") ?? string.Empty;
        data["Duration"] = eventArgs.Status?.Duration.ToString() ?? string.Empty;

        await _webhookSender.SendNotification(
            NotificationType.TaskCompleted,
            data).ConfigureAwait(false);

        await _dashboardAlert.LogAsync(
            $"Task completed: {eventArgs.Task.Name}",
            "MultifyTaskCompleted").ConfigureAwait(false);
    }
}
