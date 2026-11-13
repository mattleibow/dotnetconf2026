using System.Runtime.Versioning;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public static class ChatExtensions
{
    public static MauiAppBuilder AddChatClients(this MauiAppBuilder builder)
    {
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
            builder.Services.AddTransient<IChatClient>(CreateAppleChatClient);
#elif ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            builder.Services.AddTransient<IChatClient>(CreateAndroidChatClient);
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            builder.Services.AddTransient<IChatClient>(CreateWindowsChatClient);
#endif

        builder.Services.AddSingleton<SessionEnrichmentService>();
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

#if IOS || MACCATALYST
    [SupportedOSPlatform("ios26.0")]
    [SupportedOSPlatform("maccatalyst26.0")]
    private static IChatClient CreateAppleChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        return new AppleIntelligenceChatClient(loggerFactory: loggerFactory, functionInvocationServices: services)
            .AsBuilder()
            .UseLogging(loggerFactory)
            .Build();
    }
#elif ANDROID
    [SupportedOSPlatform("android26.0")]
    private static IChatClient CreateAndroidChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        return new GeminiNanoChatClient()
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .UseLogging(loggerFactory)
            .Build();
    }
#elif WINDOWS
    [SupportedOSPlatform("windows10.0.26100.0")]
    private static IChatClient CreateWindowsChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        return new WindowsAIChatClient()
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .UseLogging(loggerFactory)
            .Build();
    }
#endif
}
