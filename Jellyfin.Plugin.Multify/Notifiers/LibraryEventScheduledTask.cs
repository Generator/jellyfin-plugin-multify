using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Notifiers;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Notifiers;

/// <summary>
/// Scheduled task that processes the library event queue.
/// </summary>
public class LibraryEventScheduledTask : IScheduledTask, IConfigurableScheduledTask
{
    private const int RecheckIntervalSec = 30;
    private readonly LibraryEventHostedService _libraryEventHostedService;
    private readonly ILocalizationManager _localizationManager;
    private readonly ILogger<LibraryEventScheduledTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryEventScheduledTask"/> class.
    /// </summary>
    /// <param name="libraryEventHostedService">Instance of the <see cref="LibraryEventHostedService"/> class.</param>
    /// <param name="localizationManager">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{LibraryEventScheduledTask}"/> interface.</param>
    public LibraryEventScheduledTask(
        LibraryEventHostedService libraryEventHostedService,
        ILocalizationManager localizationManager,
        ILogger<LibraryEventScheduledTask> logger)
    {
        _libraryEventHostedService = libraryEventHostedService;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Multify Library Event Queue Processor";

    /// <inheritdoc />
    public string Key => "MultifyLibraryEventQueueProcessor";

    /// <inheritdoc />
    public string Description => "Processes queued library item added notifications";

    /// <inheritdoc />
    public string Category => _localizationManager.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => false;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Scheduled task: processing library event queue");
        await _libraryEventHostedService.ProcessQueueAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Scheduled task: queue processing complete");
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromSeconds(RecheckIntervalSec).Ticks
            }
        };
    }
}
