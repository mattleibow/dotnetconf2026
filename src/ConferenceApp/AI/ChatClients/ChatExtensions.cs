using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                .Use(UseAndroidCompatibilityAsync, UseAndroidStreamingCompatibilityAsync)
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

#if ANDROID
    // Adapts tool schemas (removing 'additionalProperties' which ML Kit rejects) and
    // converts JSON-schema structured output requests (SummaryEnricher / KeywordEnricher)
    // into prompt instructions for GeminiNanoChatClient.
    private static async Task<ChatResponse> UseAndroidCompatibilityAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        options = SanitizeAndroidTools(options);

        if (options?.ResponseFormat is not ChatResponseFormatJson jsonFormat)
            return await innerClient.GetResponseAsync(messages, options, cancellationToken);

        var clonedOptions = options.Clone();
        clonedOptions.ResponseFormat = null;
        var schemaText = jsonFormat.Schema?.GetRawText() ?? "{}";
        var isNestedArray = schemaText.IndexOf("\"items\"", StringComparison.Ordinal) !=
                            schemaText.LastIndexOf("\"items\"", StringComparison.Ordinal);

        var formatInstruction = isNestedArray
            ? """Respond ONLY with raw valid JSON in the exact format {"data":[["Keyword1","Keyword2"]]} with no markdown fences or extra text."""
            : """Respond ONLY with raw valid JSON in the exact format {"data":["Summary text here"]} with no markdown fences or extra text.""";

        var promptMessages = messages.Append(new ChatMessage(ChatRole.User, formatInstruction));

        var response = await innerClient.GetResponseAsync(promptMessages, clonedOptions, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, NormalizeAndroidJson(response.Text, isNestedArray)));
    }

    private static IAsyncEnumerable<ChatResponseUpdate> UseAndroidStreamingCompatibilityAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken) =>
        innerClient.GetStreamingResponseAsync(messages, SanitizeAndroidTools(options), cancellationToken);

    private static ChatOptions? SanitizeAndroidTools(ChatOptions? options)
    {
        if (options?.Tools is not { Count: > 0 } tools)
            return options;

        var cloned = options.Clone();
        cloned.Tools = [.. tools.Select(t => t is AIFunction fn ? new AndroidSanitizedAIFunction(fn) : t)];
        return cloned;
    }

    private static string NormalizeAndroidJson(string rawText, bool isNestedArray)
    {
        var text = rawText.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var node = JsonNode.Parse(text);
            if (node is JsonArray rootArray)
                node = new JsonObject { ["data"] = rootArray };

            if (node is JsonObject obj && obj["data"] is JsonArray dataArray)
            {
                if (isNestedArray)
                {
                    var flatStrings = new JsonArray();
                    foreach (var item in dataArray)
                    {
                        if (item is JsonArray inner)
                        {
                            foreach (var sub in inner)
                                flatStrings.Add(sub?.DeepClone());
                        }
                        else if (item is JsonValue val)
                        {
                            flatStrings.Add(val.DeepClone());
                        }
                    }

                    var outer = new JsonArray();
                    outer.Add(flatStrings);
                    obj["data"] = outer;
                }
                else if (dataArray.Count > 1)
                {
                    var single = new JsonArray();
                    single.Add(dataArray[0]?.DeepClone());
                    obj["data"] = single;
                }

                return obj.ToJsonString();
            }
        }
        catch
        {
            // If Gemini Nano returned plain text or truncated JSON, wrap it into the expected batch envelope.
        }

        if (isNestedArray)
        {
            var matched = new JsonArray();
            foreach (var topic in SessionTopicEnrichmentService.Topics)
            {
                if (text.Contains(topic, StringComparison.OrdinalIgnoreCase))
                    matched.Add(topic);
            }

            var outer = new JsonArray();
            outer.Add(matched);
            return new JsonObject { ["data"] = outer }.ToJsonString();
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var clean = text;
            var prefixIndex = clean.IndexOf("[\"\"", StringComparison.Ordinal);
            if (clean.StartsWith('{') && clean.IndexOf("[\"", StringComparison.Ordinal) is > 0 and var start)
                clean = clean[(start + 2)..];
            clean = clean.TrimEnd('}', ']', '"', ' ');

            var fallbackObj = new JsonObject { ["data"] = new JsonArray(JsonValue.Create(clean)) };
            return fallbackObj.ToJsonString();
        }

        return text;
    }

    private sealed class AndroidSanitizedAIFunction(AIFunction inner) : DelegatingAIFunction(inner)
    {
        private JsonElement? sanitizedSchema;

        public override JsonElement JsonSchema => sanitizedSchema ??= StripAdditionalProperties(base.JsonSchema);

        private static JsonElement StripAdditionalProperties(JsonElement schema)
        {
            if (JsonNode.Parse(schema.GetRawText()) is not JsonObject root)
                return schema;

            RemoveAdditionalProperties(root);
            return JsonSerializer.Deserialize<JsonElement>(root.ToJsonString());
        }

        private static void RemoveAdditionalProperties(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                obj.Remove("additionalProperties");
                foreach (var kvp in obj.ToList())
                    RemoveAdditionalProperties(kvp.Value);
            }
            else if (node is JsonArray arr)
            {
                foreach (var item in arr)
                    RemoveAdditionalProperties(item);
            }
        }
    }
#endif
}
