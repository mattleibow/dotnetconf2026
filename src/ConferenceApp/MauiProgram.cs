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

		builder.Services.AddSingleton<ConferenceDatabase>();
		builder.Services.AddSingleton<UserPreferencesService>();
		builder.Services.AddSingleton<ChatService>();

		builder.Services.AddTransient<AgendaPage>().AddTransient<AgendaViewModel>();
		builder.Services.AddTransient<SpeakersPage>().AddTransient<SpeakersViewModel>();
		builder.Services.AddTransient<FavoritesPage>().AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<VenuePage>().AddTransient<VenueViewModel>();
		builder.Services.AddTransient<SponsorsPage>().AddTransient<SponsorsViewModel>();
		builder.Services.AddTransient<ChatPage>().AddTransient<ChatViewModel>();
		builder.Services.AddTransient<SessionPage>().AddTransient<SessionViewModel>();
		builder.Services.AddTransient<SpeakerPage>().AddTransient<SpeakerViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent();
#endif

		return builder.Build();
	}
}
