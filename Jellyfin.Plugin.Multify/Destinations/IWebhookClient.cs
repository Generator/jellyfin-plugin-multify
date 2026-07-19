using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Multify.Destinations;

/// <summary>
/// Interface for webhook clients.
/// </summary>
/// <typeparam name="TDestinationOption">The destination option type.</typeparam>
public interface IWebhookClient<in TDestinationOption> where TDestinationOption : BaseOption
{
    /// <summary>
    /// Sends a notification to the destination.
    /// </summary>
    /// <param name="option">The destination option.</param>
    /// <param name="data">The data dictionary.</param>
    /// <returns>A task representing the async operation.</returns>
    Task SendAsync(TDestinationOption option, Dictionary<string, object> data);
}
