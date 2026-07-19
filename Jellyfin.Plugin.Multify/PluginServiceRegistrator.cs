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
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Multify;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Common.System.IServerApplicationHost applicationHost)
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
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Session.PlaybackStartEventArgs>, PlaybackStartNotifier>();
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Session.PlaybackStopEventArgs>, PlaybackStopNotifier>();
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Authentication.AuthenticationResultEventArgs>, AuthenticationSuccessNotifier>();
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Authentication.AuthenticationResultEventArgs>, AuthenticationFailureNotifier>();

        // Library events
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Library.ItemChangeEventArgs>, ItemAddedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Library.ItemChangeEventArgs>, ItemDeletedNotifier>();

        // User events
        serviceCollection.AddScoped<IEventConsumer<Jellyfin.Data.Events.Users.UserCreatedEventArgs>, UserCreatedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<Jellyfin.Data.Events.Users.UserDeletedEventArgs>, UserDeletedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Entities.User>, UserUpdatedNotifier>();

        // Task events
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Model.Tasks.TaskCompletionEventArgs>, TaskCompletedNotifier>();

        // Plugin events
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Events.Updates.PluginUpdatedEventArgs>, PluginUpdatedNotifier>();

        // Session events
        serviceCollection.AddScoped<IEventConsumer<MediaBrowser.Controller.Events.Session.SessionStartedEventArgs>, SessionStartedNotifier>();
    }
}
