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
		builder.Services.AddTransient<AgendaViewModel>();
		builder.Services.AddTransient<AgendaPage>();
		builder.Services.AddTransient<SessionDetailPage>();
		builder.Services.AddSingleton<AppShell>();
		return builder.Build();
	}
}
