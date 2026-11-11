using Microsoft.Extensions.Logging;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using ConferenceApp.Views;
using Microsoft.Extensions.AI;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace ConferenceApp;

public static class MauiProgram
{
	// Set to true to compare the bundled model with Apple's sentence embeddings.
	public static readonly bool UseOnnxOnApple = false;

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
#if IOS || MACCATALYST
#pragma warning disable MAUIAI0001
		if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
			builder.Services.AddSingleton<IChatClient>(new AppleIntelligenceChatClient());
		builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(services =>
		{
			if (UseOnnxOnApple)
				return new OnnxEmbeddingGenerator();
			try
			{
				return new NLEmbeddingGenerator();
			}
			catch (NotSupportedException ex)
			{
				services.GetRequiredService<ILoggerFactory>().CreateLogger("Embeddings")
					.LogWarning(ex, "Apple sentence embeddings unavailable; using the bundled ONNX model.");
				return new OnnxEmbeddingGenerator();
			}
		});
#pragma warning restore MAUIAI0001
#else
		builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OnnxEmbeddingGenerator>();
#endif
		builder.Services.AddSingleton<SemanticSessionSearch>();
		builder.Services.AddSingleton<SessionSummaryService>();
		builder.Services.AddSingleton<AssistantService>();
		builder.Services.AddSingleton<SessionSearchService>();
		builder.Services.AddSingleton<NearbyCityService>();
		builder.Services.AddSingleton<FavoritesService>();
		builder.Services.AddSingleton<AgendaViewModel>();
		builder.Services.AddTransient<SessionViewModel>();
		builder.Services.AddTransient<SpeakerViewModel>();
		builder.Services.AddTransient<FavoritesViewModel>();
		builder.Services.AddTransient<ExploreViewModel>();
		builder.Services.AddSingleton<AssistantViewModel>();
		builder.Services.AddSingleton<AgendaPage>();
		builder.Services.AddTransient<SessionPage>();
		builder.Services.AddTransient<SpeakerPage>();
		builder.Services.AddSingleton<FavoritesPage>();
		builder.Services.AddSingleton<ExplorePage>();
		builder.Services.AddSingleton<AssistantPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent();
#endif

		return builder.Build();
	}
}
