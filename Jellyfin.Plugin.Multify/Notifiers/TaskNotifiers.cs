using System;
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

        _logger.LogDebug("Task completed event received: {TaskName} ({Status})", eventArgs.Task.Name, eventArgs.Result?.Status);

        var data = DataObjectHelpers.GetBaseDataObject("Jellyfin", NotificationType.TaskCompleted);
        data["TaskName"] = eventArgs.Task.Name ?? "Unknown";
        data["TaskId"] = eventArgs.Task.Id.ToString();
        data["Status"] = eventArgs.Result?.Status.ToString() ?? "Unknown";
        data["StartTime"] = eventArgs.Result?.StartTimeUtc.ToString("O") ?? string.Empty;
        data["EndTime"] = eventArgs.Result?.EndTimeUtc.ToString("O") ?? string.Empty;
        var duration = eventArgs.Result != null
            ? eventArgs.Result.EndTimeUtc - eventArgs.Result.StartTimeUtc
            : TimeSpan.Zero;
        data["Duration"] = duration.ToString();

        await _webhookSender.SendNotification(
            NotificationType.TaskCompleted,
            data).ConfigureAwait(false);

        _logger.LogInformation("Task completed notification sent for {TaskName}", eventArgs.Task.Name);

        await _dashboardAlert.LogAsync(
            $"Task completed: {eventArgs.Task.Name}",
            "MultifyTaskCompleted").ConfigureAwait(false);
    }
}
