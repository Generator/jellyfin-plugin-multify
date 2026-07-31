using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Cache for library ID to name mappings with periodic cleanup.
/// </summary>
public sealed class LibraryCache : IHostedService, IDisposable, IAsyncDisposable
{
    private readonly ILogger<LibraryCache> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);
    private Timer? _cleanupTimer;
    private bool _disposed;

    private long _hitCount;
    private long _missCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCache"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{LibraryCache}"/> interface.</param>
    public LibraryCache(ILogger<LibraryCache> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Library cache started with periodic cleanup every {Interval} minutes", _cleanupInterval.TotalMinutes);
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, _cleanupInterval, _cleanupInterval);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
            _cleanupTimer = null;
        }
        _logger.LogInformation("Library cache stopped");
    }

    /// <summary>
    /// Gets or adds a library name to the cache.
    /// </summary>
    /// <param name="libraryId">The library ID.</param>
    /// <param name="name">The library name to cache.</param>
    /// <param name="ttl">Optional TTL override.</param>
    public void Set(Guid libraryId, string name, TimeSpan? ttl = null)
    {
        var entry = new CacheEntry
        {
            Name = name,
            Expiry = DateTime.UtcNow.Add(ttl ?? _defaultTtl)
        };

        _cache[libraryId] = entry;
        _logger.LogDebug("Cached library {LibraryId} -> {Name}", libraryId, name);
    }

    /// <summary>
    /// Tries to get a library name from the cache.
    /// </summary>
    /// <param name="libraryId">The library ID.</param>
    /// <param name="name">The cached name if found and not expired.</param>
    /// <returns>True if found and not expired; false otherwise.</returns>
    public bool TryGetValue(Guid libraryId, out string? name)
    {
        if (_cache.TryGetValue(libraryId, out var entry))
        {
            if (DateTime.UtcNow < entry.Expiry)
            {
                Interlocked.Increment(ref _hitCount);
                name = entry.Name;
                _logger.LogDebug("Cache hit for library {LibraryId}", libraryId);
                return true;
            }

            // Entry expired, remove it
            _cache.TryRemove(libraryId, out _);
        }

        Interlocked.Increment(ref _missCount);
        name = null;
        _logger.LogDebug("Cache miss for library {LibraryId}", libraryId);
        return false;
    }

    /// <summary>
    /// Invalidates all cache entries.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.LogDebug("Library cache invalidated");
    }

    /// <summary>
    /// Invalidates a specific cache entry.
    /// </summary>
    /// <param name="libraryId">The library ID to invalidate.</param>
    public void Invalidate(Guid libraryId)
    {
        _cache.TryRemove(libraryId, out _);
        _logger.LogDebug("Invalidated cache for library {LibraryId}", libraryId);
    }

    /// <summary>
    /// Periodically cleans up expired cache entries.
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var removedCount = 0;

        foreach (var kvp in _cache)
        {
            if (now >= kvp.Value.Expiry)
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    removedCount++;
                }
            }
        }

        if (removedCount > 0)
        {
            _logger.LogDebug("Library cache cleanup: removed {Count} expired entries", removedCount);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the LibraryCache.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
            _cleanupTimer = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cache entry with expiry.
    /// </summary>
    private sealed class CacheEntry
    {
        public string Name { get; set; } = string.Empty;

        public DateTime Expiry { get; set; }
    }
}
