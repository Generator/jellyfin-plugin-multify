using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Tasks;

/// <summary>
/// Scheduled task to clean up stale Telegram message store entries.
/// Telegram message edits expire after 48 hours, so old entries are useless.
/// </summary>
public class CleanupTelegramStoreTask : IScheduledTask
{
    private readonly ILogger<CleanupTelegramStoreTask> _logger;
    private readonly TelegramMessageStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupTelegramStoreTask"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{CleanupTelegramStoreTask}"/> interface.</param>
    /// <param name="store">Instance of the <see cref="TelegramMessageStore"/> interface.</param>
    public CleanupTelegramStoreTask(ILogger<CleanupTelegramStoreTask> logger, TelegramMessageStore store)
    {
        _logger = logger;
        _store = store;
    }

    /// <inheritdoc />
    public string Name => "Multify Cleanup Telegram Store";

    /// <inheritdoc />
    public string Key => "MultifyCleanupTelegramStore";

    /// <inheritdoc />
    public string Description => "Removes stale Telegram message entries older than 7 days.";

    /// <inheritdoc />
    public string Category => "Multify";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Weekly on Sunday at 3 AM
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.WeeklyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
            DayOfWeek = DayOfWeek.Sunday
        };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up stale Telegram message store entries");
        _store.CleanupStaleEntries();
        _logger.LogInformation("Telegram message store cleanup complete");
        return Task.CompletedTask;
    }
}
