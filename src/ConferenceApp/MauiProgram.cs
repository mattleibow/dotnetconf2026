using Microsoft.Extensions.Logging;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using ConferenceApp.Views;
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
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<ConferenceService>();
		builder.Services.AddSingleton<CityService>();
		builder.Services.AddSingleton<NearbyCityService>();
		builder.Services.AddSingleton<FavoritesService>();
		builder.Services.AddSingleton<AgendaViewModel>();
		builder.Services.AddTransient<SessionViewModel>();
		builder.Services.AddTransient<SpeakerViewModel>();
		builder.Services.AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<ExploreViewModel>();
		builder.Services.AddSingleton<AgendaPage>();
		builder.Services.AddTransient<SessionPage>();
		builder.Services.AddTransient<SpeakerPage>();
		builder.Services.AddSingleton<FavoritesPage>();
		builder.Services.AddSingleton<ExplorePage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent();
#endif

		return builder.Build();
	}
}
