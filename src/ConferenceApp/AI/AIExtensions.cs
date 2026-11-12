using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.AI;

public static class AIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        // Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform.
        // On Apple, you can also compare against the built-in NLEmbeddingGenerator:
        // builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
        //     sp => new NLEmbeddingGenerator(sp.GetRequiredService<ILoggerFactory>()));
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, MiniLmEmbeddingGenerator>();

#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
        {
            builder.Services.AddTransient<IChatClient>(CreateAppleChatClient);
        }
#endif

        builder.Services.AddSingleton<SessionEnrichmentService>();
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddScoped<ConferenceAssistantTools>();
        builder.Services.AddTransient<DayPlanService>();
        builder.Services.AddSingleton<ChatService>();

        return builder;
    }

#if IOS || MACCATALYST
    [System.Runtime.Versioning.SupportedOSPlatform("ios26.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("maccatalyst26.0")]
    private static IChatClient CreateAppleChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        return new AppleIntelligenceChatClient(loggerFactory: loggerFactory, functionInvocationServices: services)
            .AsBuilder()
            .UseLogging(loggerFactory)
            .Build();
    }
#endif
}
