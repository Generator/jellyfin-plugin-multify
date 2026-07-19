using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Multify;

/// <summary>
/// The Multify plugin.
/// </summary>
public class MultifyPlugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    private readonly Guid _id = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static MultifyPlugin? Instance { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultifyPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public MultifyPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Name = "Multify";
        Description = "A unified notification plugin for Jellyfin that sends notifications to multiple systems.";
    }

    /// <inheritdoc />
    public override Guid Id => _id;

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "config",
                EmbeddedResourcePath = "Jellyfin.Plugin.Multify.Configuration.Web.config.html"
            },
            new PluginPageInfo
            {
                Name = "config.js",
                EmbeddedResourcePath = "Jellyfin.Plugin.Multify.Configuration.Web.config.js"
            }
        };
    }
}
