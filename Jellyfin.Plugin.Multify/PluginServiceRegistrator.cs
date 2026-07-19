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
        var configuration = new PluginConfiguration();
        serviceCollection.AddSingleton(configuration);

        // Register advanced settings (derived from configuration)
        serviceCollection.AddSingleton(configuration.AdvancedSettings);

        // Register dashboard alert service
        serviceCollection.AddScoped<DashboardAlertService>();

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

        // Register event consumers
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopNotifier>();
        serviceCollection.AddScoped<IEventConsumer<AuthenticationResultEventArgs>, AuthenticationSuccessNotifier>();
        serviceCollection.AddScoped<IEventConsumer<AuthenticationResultEventArgs>, AuthenticationFailureNotifier>();

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
