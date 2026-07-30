using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Multify.Helpers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Service for querying source media bitrate via <see cref="IMediaSourceManager"/>.
/// Shared across playback notifiers to eliminate code duplication.
/// </summary>
public class PlaybackBitrateService
{
    private readonly ILogger<PlaybackBitrateService> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackBitrateService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaybackBitrateService}"/> interface.</param>
    /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> interface.</param>
    public PlaybackBitrateService(
        ILogger<PlaybackBitrateService> logger,
        IMediaSourceManager mediaSourceManager)
    {
        _logger = logger;
        _mediaSourceManager = mediaSourceManager;
    }

    /// <summary>
    /// Queries the source media bitrate via <see cref="IMediaSourceManager"/> for
    /// DirectPlay/DirectStream sessions. Transcode bitrate is already handled by
    /// <see cref="DataObjectHelpers.AddSessionInfo"/>.
    /// </summary>
    /// <param name="data">The data dictionary to populate with bitrate info.</param>
    /// <param name="item">The media item being played.</param>
    /// <param name="user">The user playing the item.</param>
    public async Task AddSourceBitrateAsync(Dictionary<string, object> data, BaseItem item, User user)
    {
        try
        {
            var sources = await _mediaSourceManager
                .GetPlaybackMediaSources(item, user, false, false, CancellationToken.None)
                .ConfigureAwait(false);

            var source = sources?.FirstOrDefault(s => s.Bitrate.HasValue && s.Bitrate.Value > 0);
            if (source?.Bitrate.HasValue == true)
            {
                var bitrate = source.Bitrate.Value;
                data["PlaybackBitrate"] = bitrate;
                data["PlaybackBitrateText"] = DataObjectHelpers.FormatBitrate(bitrate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying source bitrate for {ItemName}", item.Name);
        }
    }
}
