using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.Multify.Configuration;
using Jellyfin.Plugin.Multify.Destinations;
using Jellyfin.Plugin.Multify.Destinations.Generic;
using Jellyfin.Plugin.Multify.Destinations.Gotify;
using Jellyfin.Plugin.Multify.Destinations.Ntfy;
using Jellyfin.Plugin.Multify.Destinations.Telegram;
using Jellyfin.Plugin.Multify.Notifiers;
using Jellyfin.Plugin.Multify.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Authentication;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Events.Updates;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Multify;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Controller.IServerApplicationHost applicationHost)
    {
        // Use the plugin instance's configuration (loaded from disk with user settings)
        // Resolve at runtime to avoid race condition if plugin hasn't been instantiated yet
        serviceCollection.AddSingleton(sp =>
        {
            var config = MultifyPlugin.Instance?.Configuration;
            if (config == null)
            {
                config = new PluginConfiguration();
            }
            return config;
        });

        // Register dashboard alert service
        serviceCollection.AddScoped<DashboardAlertService>();

        // Register filter services
        serviceCollection.AddSingleton<FilterService>();
        serviceCollection.AddSingleton<FilterValidator>();
        serviceCollection.AddSingleton<LibraryCache>();

        // Register destination clients
        serviceCollection.AddScoped<IWebhookClient<TelegramOption>, TelegramClient>();
        serviceCollection.AddScoped<IWebhookClient<GotifyOption>, GotifyClient>();
        serviceCollection.AddScoped<IWebhookClient<NtfyOption>, NtfyClient>();
        serviceCollection.AddScoped<IWebhookClient<GenericWebhookOption>, GenericWebhookClient>();

        // Register sender
        serviceCollection.AddScoped<IWebhookSender, MultifySender>();

        // Register MDBList service
        serviceCollection.AddScoped<MdblistService>();

        // Register Telegram message store
        serviceCollection.AddSingleton<TelegramMessageStore>();

        // Register test notification service
        serviceCollection.AddScoped<IMultifyTestService, MultifyTestService>();

        // Register event consumers
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopNotifier>();
        serviceCollection.AddScoped<IEventConsumer<AuthenticationResultEventArgs>, AuthenticationNotifier>();

        // Library events (not IEventConsumer — ItemChangeEventArgs doesn't inherit EventArgs)
        serviceCollection.AddScoped<ItemAddedNotifier>();
        serviceCollection.AddScoped<ItemDeletedNotifier>();

        // User events
        serviceCollection.AddScoped<IEventConsumer<UserCreatedEventArgs>, UserCreatedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<UserDeletedEventArgs>, UserDeletedNotifier>();

        // Task events
        serviceCollection.AddScoped<IEventConsumer<TaskCompletionEventArgs>, TaskCompletedNotifier>();

        // Plugin events
        serviceCollection.AddScoped<IEventConsumer<PluginUpdatedEventArgs>, PluginUpdatedNotifier>();

        // Session events
        serviceCollection.AddScoped<IEventConsumer<SessionStartedEventArgs>, SessionStartedNotifier>();
    }
}
