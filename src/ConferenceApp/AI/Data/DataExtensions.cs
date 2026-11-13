using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class DataExtensions
{
    public static readonly string[] Topics = ["Mobile", "Web", "Cloud", "Data", "IoT", "AI"];

    public static MauiAppBuilder AddDataEnrichment(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton(services =>
        {
            // BatchSize is 1, so SummaryEnricher and ClassificationEnricher expect {"data": ["..."]} with 1 item.
            // If the small on-device model returns multiple tags for a talk (e.g. {"data": ["AI", "Cloud"]}),
            // keep the primary (first) item so ClassificationEnricher doesn't reject the batch.
            var client = services.GetRequiredService<IChatClient>()
                .AsBuilder()
                .Use(
                    getResponseFunc: async (messages, options, innerClient, cancellationToken) =>
                    {
                        var isNestedArraySchema = IsNestedArraySchema(options);
                        try
                        {
                            var response = await innerClient.GetResponseAsync(messages, options, cancellationToken);
                            return NormalizeSingleItemBatch(response, isNestedArraySchema);
                        }
                        catch
                        {
                            // If a full abstract triggers the on-device safety guardrail (e.g. security/attack terms),
                            // retry with just the session title line so the talk still gets a summary and topics.
                            try
                            {
                                var titleOnly = messages.Select(m => m.Role == ChatRole.User
                                    ? new ChatMessage(ChatRole.User, m.Text.Split('\n')[0])
                                    : m);
                                var response = await innerClient.GetResponseAsync(titleOnly, options, cancellationToken);
                                return NormalizeSingleItemBatch(response, isNestedArraySchema);
                            }
                            catch
                            {
                                var emptyBatch = isNestedArraySchema ? """{"data":[[]]}""" : """{"data":[""]}""";
                                return new ChatResponse(new ChatMessage(ChatRole.Assistant, emptyBatch));
                            }
                        }
                    },
                    getStreamingResponseFunc: null)
                .Build();

            return new EnricherOptions(client)
            {
                BatchSize = 1,
                ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
                LoggerFactory = services.GetRequiredService<ILoggerFactory>()
            };
        });

        builder.Services.AddSingleton(services =>
            new SummaryEnricher(services.GetRequiredService<EnricherOptions>(), maxWordCount: 24));

        builder.Services.AddSingleton(services =>
            new KeywordEnricher(
                services.GetRequiredService<EnricherOptions>(),
                predefinedKeywords: Topics,
                maxKeywords: 3,
                confidenceThreshold: 0.75));

        builder.Services.AddSingleton<SessionEnrichmentService>();

        return builder;
    }

    private static bool IsNestedArraySchema(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson jsonFormat &&
        jsonFormat.Schema?.ToString() is { } schema &&
        schema.IndexOf("\"type\":\"array\"", StringComparison.Ordinal) !=
        schema.LastIndexOf("\"type\":\"array\"", StringComparison.Ordinal);

    private static ChatResponse NormalizeSingleItemBatch(ChatResponse response, bool isNestedArraySchema)
    {
        if (JsonNode.Parse(response.Text) is not JsonObject obj ||
            obj["data"] is not JsonArray items)
        {
            return response;
        }

        if (isNestedArraySchema)
        {
            // Collect all extracted keywords across the response into a single inner array for BatchSize = 1.
            var keywords = new JsonArray();
            foreach (var item in items)
            {
                if (item is JsonArray inner)
                {
                    foreach (var sub in inner)
                        keywords.Add(sub?.DeepClone());
                }
                else if (item is JsonValue val)
                {
                    keywords.Add(val.DeepClone());
                }
            }

            obj["data"] = new JsonArray(keywords);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, obj.ToJsonString()));
        }

        if (items.Count > 1)
        {
            obj["data"] = new JsonArray(items[0]?.DeepClone());
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, obj.ToJsonString()));
        }

        return response;
    }
}
