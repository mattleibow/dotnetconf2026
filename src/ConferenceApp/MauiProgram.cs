using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using ConferenceApp.Views;

namespace ConferenceApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent();
#endif

		builder.Services.AddSingleton<ConferenceService>();
		builder.Services.AddSingleton<SessionSearchService>();
		builder.Services.AddTransient<AgendaViewModel>();
		builder.Services.AddTransient<AgendaPage>();
		builder.Services.AddTransient<SessionDetailPage>();
		builder.Services.AddTransient<SessionDetailViewModel>();
		builder.Services.AddTransient<SpeakerDetailPage>();
		builder.Services.AddTransient<SpeakerDetailViewModel>();
		builder.Services.AddTransient<SpeakersPage>();
		builder.Services.AddTransient<SpeakersViewModel>();
		builder.Services.AddTransient<FavoritesPage>();
		builder.Services.AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<VenuePage>();
		builder.Services.AddTransient<VenueViewModel>();
		builder.Services.AddSingleton<AppShell>();
		return builder.Build();
	}
}
