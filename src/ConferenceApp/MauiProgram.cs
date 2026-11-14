using ConferenceApp.AI;
using ConferenceApp.Data;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using ConferenceApp.Views;
using Microsoft.Extensions.Logging;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace ConferenceApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ConferenceDbProvider>();
        builder.Services.AddSingleton<DatabasePreloadService>();
        builder.Services.AddSingleton<UserPreferencesService>();
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddSingleton<ChatService>();

        builder.AddConferenceAI();

        builder.Services.AddTransient<EventPage>().AddTransient<EventViewModel>();
        builder.Services.AddTransient<AgendaPage>().AddTransient<AgendaViewModel>();
        builder.Services.AddTransient<SpeakersPage>().AddTransient<SpeakersViewModel>();
        builder.Services.AddTransient<ChatPage>().AddTransient<ChatViewModel>();
        builder.Services.AddTransient<SessionPage>().AddTransient<SessionViewModel>();
        builder.Services.AddTransient<SpeakerPage>().AddTransient<SpeakerViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.Extensions.AI", LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.Agents.AI", LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.Maui.Essentials.AI", LogLevel.Debug);
        builder.AddMauiDevFlowAgent(options =>
        {
            options.EnableLayoutDiagnostics = true;
            options.EnableProfiler = true;
            options.RequireMutationLease = false;
        });
#endif

        return builder.Build();
    }
}
