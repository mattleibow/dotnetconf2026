using System.Runtime.Versioning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public static class ChatExtensions
{
#if ANDROID
    private static readonly string[] AndroidCandidateModels =
    [
        "gemini-nano-preview-fast",
        "gemini-nano-preview-full",
        "gemini-nano-full",
        "gemini-nano-fast"
    ];
#endif

    public static MauiAppBuilder AddChatClients(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<IChatClient>(CreateLocalChatClient);
        return builder;
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
            return new OrderedFailoverChatClient(
                [
                    .. AndroidCandidateModels.Select(modelId =>
                        new GeminiNanoChatClient()
                            .AsBuilder()
                            .ConfigureOptions(options => options.ModelId = modelId)
                            .Build())
                ])
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build(services);
        }
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return new WindowsAIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build(services);
        }
#endif
        return new UnavailableChatClient();
    }

    public static async Task<ChatModelAvailability> GetModelAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
#if IOS || MACCATALYST
            if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
                return (await AppleIntelligenceChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#elif ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                ChatModelAvailability fallback = ChatModelAvailability.NotSupported;
                foreach (var modelId in AndroidCandidateModels)
                {
                    try
                    {
                        var status = await GeminiNanoChatClient.GetModelStatusAsync(modelId, cancellationToken);
                        if (status.Availability == ChatModelAvailability.Available)
                            return ChatModelAvailability.Available;

                        if (fallback == ChatModelAvailability.NotSupported && status.Availability != ChatModelAvailability.Unavailable)
                            fallback = status.Availability;
                    }
                    catch
                    {
                        // Ignore unsupported model profile IDs and try the next candidate.
                    }
                }

                return fallback;
            }
#elif WINDOWS
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                return (await WindowsAIChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#endif
        }
        catch
        {
            return ChatModelAvailability.NotSupported;
        }

        await Task.CompletedTask;
        return ChatModelAvailability.NotSupported;
    }
}
