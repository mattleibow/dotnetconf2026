#if WINDOWS
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.AI;

public sealed class SessionTopicKeywords
{
    [Description("Up to 3 relevant technical topics selected from the predefined list.")]
    public string[] Keywords { get; set; } = [];
}

// Phi Silica needs a flat single-item schema instead of KeywordEnricher's nested batch arrays.
public sealed class WindowsSessionTopicEnricher(EnricherOptions options) : IngestionChunkProcessor<string>
{
    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            ChatMessage[] messages =
                [
                    new(ChatRole.System,
                        $"Extract up to 3 relevant topics for this conference session from: {string.Join(", ", SessionTopicEnrichmentService.Topics)}. " +
                        "Exclude topics with confidence below 0.7."),
                    new(ChatRole.User, chunk.Content)
                ];
            var response = await options.ChatClient.GetResponseAsync<SessionTopicKeywords>(
                messages,
                options.ChatOptions,
                useJsonSchemaResponseFormat: true,
                cancellationToken: cancellationToken);

            chunk.Metadata[KeywordEnricher.MetadataKey] = response.Result.Keywords;
            yield return chunk;
        }
    }
}
#endif
