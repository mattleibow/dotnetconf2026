using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public static class AIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        // 1. Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform, wrapped with Microsoft.Extensions.AI logging.
        // On Apple, you can also compare against the built-in NLEmbeddingGenerator:
        // builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
        //     sp => new NLEmbeddingGenerator(sp.GetRequiredService<ILoggerFactory>()).AsBuilder().UseLogging(sp.GetRequiredService<ILoggerFactory>()).Build());
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp)
                .AsBuilder()
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build());

        // 2. On-device chat client across Apple (AppleIntelligenceChatClient), Android (GeminiNanoChatClient),
        // and Windows (WindowsAIChatClient) from Microsoft.Maui.Essentials.AI, wrapped with logging.
        builder.Services.AddTransient<IChatClient>(CreateLocalChatClient);

        // 3. Data ingestion, semantic search, tools, and Agent Framework setup.
        builder.Services.AddSingleton<SessionEnrichmentService>();
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddScoped<ConferenceAssistantTools>();
        builder.AddConferenceAgents();
        builder.Services.AddSingleton<ChatService>();

        return builder;
    }

    public static async Task<ChatModelAvailability> GetModelAvailabilityAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
            return (await AppleIntelligenceChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#elif ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return (await GeminiNanoChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            return (await WindowsAIChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#endif
        await Task.CompletedTask;
        return ChatModelAvailability.NotSupported;
    }

    private static IChatClient CreateLocalChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
        {
            return new AppleIntelligenceChatClient(loggerFactory: loggerFactory, functionInvocationServices: services)
                .AsBuilder()
                .UseLogging(loggerFactory)
                .Build();
        }
#elif ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return new GeminiNanoChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build();
        }
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return new WindowsAIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build();
        }
#endif
        throw new PlatformNotSupportedException("On-device chat is not supported on this OS version.");
    }
}
