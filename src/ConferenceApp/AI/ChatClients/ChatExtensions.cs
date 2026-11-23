using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;
using OpenAI;

namespace ConferenceApp.AI;

// Registers the cloud gateway and native on-device clients, with the default IChatClient failing over from cloud to local.
public static class ChatExtensions
{
    public const string Cloud = "cloud";
    public const string Local = "local";

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
        // Aspire supplies the gateway (direct on Mac, a dev tunnel on simulators) and its key as configuration.
        var hasGateway = builder.Configuration.GetSection("services:ai-gateway").Exists();

        builder.Services.AddKeyedTransient<IChatClient>(Local, (services, _) => CreateLocalChatClient(services));

        builder.Services.AddKeyedTransient<IChatClient>(Cloud, (services, _) =>
            new OpenAIClient(new ApiKeyCredential(builder.Configuration["AI_GATEWAY_KEY"] ?? ""), new OpenAIClientOptions
            {
                Endpoint = new Uri("https+http://ai-gateway/v1"),
                Transport = new HttpClientPipelineTransport(services.GetRequiredService<IHttpClientFactory>().CreateClient()),
                NetworkTimeout = TimeSpan.FromSeconds(30) // Fall back to on-device AI quickly if the gateway is unreachable.
            })
                .GetChatClient("azure") // The gateway endpoint to use; LiteLLM falls back to "ollama" if Azure fails.
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(services.GetRequiredService<ILoggerFactory>())
                .UseLogging(services.GetRequiredService<ILoggerFactory>())
                .Build(services));

        // Each agent gets its own client: try the gateway first, and use on-device AI if it fails.
        builder.Services.AddTransient<IChatClient>(services => !hasGateway
            ? services.GetRequiredKeyedService<IChatClient>(Local)
            : new OrderedFailoverChatClient(
            [
                services.GetRequiredKeyedService<IChatClient>(Cloud),
                services.GetRequiredKeyedService<IChatClient>(Local)
            ]));
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
