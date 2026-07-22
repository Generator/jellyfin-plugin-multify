using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Multify.Services;

/// <summary>
/// Cache for library ID to name mappings.
/// </summary>
public class LibraryCache
{
    private readonly ILogger<LibraryCache> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

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
    /// Gets cache statistics.
    /// </summary>
    /// <returns>A tuple with hit count, miss count, and total entries.</returns>
    public (long Hits, long Misses, int EntryCount) GetStats()
    {
        return (Interlocked.Read(ref _hitCount), Interlocked.Read(ref _missCount), _cache.Count);
    }

    /// <summary>
    /// Logs cache statistics.
    /// </summary>
    public void LogStats()
    {
        var (hits, misses, count) = GetStats();
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total * 100 : 0;

        _logger.LogInformation(
            "Library cache stats: {Hits} hits, {Misses} misses ({HitRate:F1}% hit rate), {Count} entries",
            hits,
            misses,
            hitRate,
            count);
    }

    /// <summary>
    /// Cache entry with expiry.
    /// </summary>
    private class CacheEntry
    {
        public string Name { get; set; } = string.Empty;

        public DateTime Expiry { get; set; }
    }
}
