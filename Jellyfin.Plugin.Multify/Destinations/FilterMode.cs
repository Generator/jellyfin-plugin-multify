namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Filter mode for user and library filters.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// Only include selected items in the filter.
    /// </summary>
    OnlySelected,

    /// <summary>
    /// Include all items except those in the filter.
    /// </summary>
    AllExcept
}
