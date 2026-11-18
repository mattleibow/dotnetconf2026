#if WINDOWS
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.AI;

public sealed class SessionSummaryText
{
    [Description("A concise summary of the conference session in no more than 24 words.")]
    public string Summary { get; set; } = "";
}

// A single summary property avoids Phi Silica returning multiple entries for one batch item.
public sealed class WindowsSessionSummaryEnricher(EnricherOptions options) : IngestionChunkProcessor<string>
{
    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            ChatMessage[] messages =
                [
                    new(ChatRole.System, "Write a summary of this conference session in no more than 24 words."),
                    new(ChatRole.User, chunk.Content)
                ];
            var response = await options.ChatClient.GetResponseAsync<SessionSummaryText>(
                messages,
                options.ChatOptions,
                useJsonSchemaResponseFormat: true,
                cancellationToken: cancellationToken);

            chunk.Metadata[SummaryEnricher.MetadataKey] = response.Result.Summary;
            yield return chunk;
        }
    }
}
#endif
