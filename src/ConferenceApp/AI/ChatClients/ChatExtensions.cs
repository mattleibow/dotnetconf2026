using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

// Registers the platform's local on-device IChatClient and model availability checks.
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
        // Register a transient local IChatClient so concurrent agents don't contend on one session.
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
                            .ConfigureOptions(options =>
                            {
                                options.ModelId = modelId;
                                options.MaxOutputTokens ??= 1024;
                            })
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
            IChatClient platformClient = new WindowsAIChatClient();
            IChatClient toolClient = new QwenChatClient(loggerFactory.CreateLogger<QwenChatClient>())
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory, invocation =>
                {
                    invocation.MaximumIterationsPerRequest = 6;
                    invocation.MaximumConsecutiveErrorsPerRequest = 1;
                })
                .Build(services);

            var logger = loggerFactory.CreateLogger(typeof(ChatExtensions));
            return RoutingChatClient.Create((context, _) =>
            {
                var toolCount = context.ChatOptions?.Tools?.Count ?? 0;
                logger.LogInformation("[AI Chat] Routing to {Provider} ({ToolCount} tools).",
                    toolCount > 0 ? "ONNX Qwen3-0.6B" : "Windows AI", toolCount);
                return ValueTask.FromResult(toolCount > 0 ? toolClient : platformClient);
            })
                .AsBuilder()
                .UseLogging(loggerFactory)
                .Build();
        }
#endif
        return new UnavailableChatClient();
    }

#if WINDOWS
    // Adds the copied MAUI Labs tool-calling adapter between function invocation and the native client.
    public static ChatClientBuilder UseWindowsAIToolCalling(this ChatClientBuilder builder, ILoggerFactory? loggerFactory = null) =>
        builder.Use((inner, services) => new WindowsAIToolCallingClient(inner,
            (loggerFactory ?? services.GetRequiredService<ILoggerFactory>()).CreateLogger<WindowsAIToolCallingClient>()));
#endif

    public static async Task<ChatModelAvailability> GetModelAvailabilityAsync(
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger?.LogWarning(ex, "[AI Chat] Could not query Android model {ModelId}; trying the next profile.", modelId);
                }
            }

            return fallback;
        }
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            var status = await WindowsAIChatClient.GetModelStatusAsync(cancellationToken);
            logger?.LogInformation("[AI Chat] Windows model availability: {Availability}; capabilities: {Capabilities}.",
                status.Availability, string.Join(", ", status.Capabilities ?? []));
            return status.Availability;
        }
#endif

        await Task.CompletedTask;
        return ChatModelAvailability.NotSupported;
    }
}
