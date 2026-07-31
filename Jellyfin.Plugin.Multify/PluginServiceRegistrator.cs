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
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
        // Resolve at runtime to avoid stale config after save — Jellyfin replaces the Configuration instance on save
        serviceCollection.AddScoped<PluginConfiguration>(sp =>
        {
            var config = MultifyPlugin.Instance?.Configuration;
            if (config == null)
            {
                config = new PluginConfiguration();
            }
            return config;
        });

        // Register AdvancedOption as IOptions for MdblistService
        serviceCollection.AddScoped<IOptions<AdvancedOption>>(sp =>
        {
            var config = sp.GetRequiredService<PluginConfiguration>();
            return Options.Create(config.AdvancedSettings);
        });

        // Register dashboard alert service
        serviceCollection.AddScoped<DashboardAlertService>();

        // Register filter service
        serviceCollection.AddSingleton<FilterService>();

        // Register LibraryCache as hosted service (with periodic cleanup)
        serviceCollection.AddHostedService<LibraryCache>();

        // Register destination clients
        serviceCollection.AddScoped<IWebhookClient<TelegramOption>, TelegramClient>();
        serviceCollection.AddScoped<IWebhookClient<GotifyOption>, GotifyClient>();
        serviceCollection.AddScoped<IWebhookClient<NtfyOption>, NtfyClient>();
        serviceCollection.AddScoped<IWebhookClient<GenericWebhookOption>, GenericWebhookClient>();

        // Register sender
        serviceCollection.AddScoped<IWebhookSender, MultifySender>();

        // Register playback bitrate service (shared across playback notifiers)
        serviceCollection.AddScoped<PlaybackBitrateService>();

        // Register per-user data service (shared across playback notifiers)
        serviceCollection.AddScoped<UserDataService>();

        // Register image enrichment service (shared across MultifySender and MultifyTestService)
        serviceCollection.AddScoped<ImageEnrichmentService>();

        // Register MDBList service
        serviceCollection.AddScoped<MdblistService>();

        // Register Telegram message store
        serviceCollection.AddSingleton<TelegramMessageStore>();

        // Register test notification service
        serviceCollection.AddScoped<IMultifyTestService, MultifyTestService>();

        // Register library event hosted service (subscribes to ILibraryManager events)
        serviceCollection.AddHostedService<LibraryEventHostedService>();

        // Register event consumers
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackProgressEventArgs>, PlaybackProgressNotifier>();
        serviceCollection.AddScoped<IEventConsumer<AuthenticationResultEventArgs>, AuthenticationSuccessNotifier>();
        serviceCollection.AddScoped<IEventConsumer<AuthenticationRequestEventArgs>, AuthenticationFailureNotifier>();

        // User events
        serviceCollection.AddScoped<IEventConsumer<UserCreatedEventArgs>, UserCreatedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<UserDeletedEventArgs>, UserDeletedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<UserUpdatedEventArgs>, UserUpdatedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<UserLockedOutEventArgs>, UserLockedOutNotifier>();
        serviceCollection.AddScoped<IEventConsumer<UserPasswordChangedEventArgs>, UserPasswordChangedNotifier>();

        // Task events
        serviceCollection.AddScoped<IEventConsumer<TaskCompletionEventArgs>, TaskCompletedNotifier>();

        // Plugin events
        serviceCollection.AddScoped<IEventConsumer<PluginUpdatedEventArgs>, PluginUpdatedNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PluginInstalledEventArgs>, PluginInstalledNotifier>();
        serviceCollection.AddScoped<IEventConsumer<PluginUninstalledEventArgs>, PluginUninstalledNotifier>();

        // Session events
        serviceCollection.AddScoped<IEventConsumer<SessionStartedEventArgs>, SessionStartedNotifier>();

        // Subtitle events
        serviceCollection.AddScoped<IEventConsumer<SubtitleDownloadFailureEventArgs>, SubtitleDownloadFailureNotifier>();
    }
}
