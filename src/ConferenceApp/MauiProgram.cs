using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using ConferenceApp.Views;
using Microsoft.Extensions.AI;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp;

public static class MauiProgram
{
	// Set to false to compare Apple's sentence embeddings with the bundled model.
	public const bool UseOnnxOnApple = true;

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
#if IOS || MACCATALYST
#pragma warning disable MAUIAI0001
		if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
		{
			builder.Services.AddSingleton<AppleIntelligenceChatClient>();
			builder.Services.AddSingleton<IChatClient>(services =>
#if IOS
				OperatingSystem.IsIOSVersionAtLeast(26)
#else
				OperatingSystem.IsMacCatalystVersionAtLeast(26)
#endif
					? AssistantService.AddSessionContext(services.GetRequiredService<AppleIntelligenceChatClient>(), services)
					: throw new PlatformNotSupportedException("Apple Intelligence requires iOS or Mac Catalyst 26 or later."));
		}
#pragma warning disable CS0162
		if (!UseOnnxOnApple)
			builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, NLEmbeddingGenerator>();
		else
			builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OnnxEmbeddingGenerator>();
#pragma warning restore CS0162
#pragma warning restore MAUIAI0001
#else
		builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OnnxEmbeddingGenerator>();
#endif
		builder.Services.AddSingleton<SemanticSearchService>();
		builder.Services.AddSingleton<SessionEnrichmentService>();
		builder.Services.AddSingleton<SessionSearchService>();
		builder.Services.AddSingleton<FavoriteApproval>();
		builder.Services.AddScoped<ConferenceTools>();
		builder.Services.AddSingleton<AssistantService>();
		builder.Services.AddSingleton<DayPlannerService>();
		builder.Services.AddSingleton<AssistantViewModel>();
		builder.Services.AddTransient<AssistantPage>();
		builder.Services.AddTransient<AgendaViewModel>();
		builder.Services.AddTransient<AgendaPage>();
		builder.Services.AddTransient<SessionDetailPage>();
		builder.Services.AddTransient<SessionDetailViewModel>();
		builder.Services.AddTransient<SpeakerDetailPage>();
		builder.Services.AddTransient<SpeakerDetailViewModel>();
		builder.Services.AddTransient<SponsorDetailPage>();
		builder.Services.AddTransient<SponsorDetailViewModel>();
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
