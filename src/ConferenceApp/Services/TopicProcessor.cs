using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.Services;

internal sealed class TopicProcessor(IEmbeddingGenerator<string, Embedding<float>> generator) : IngestionChunkProcessor<string>
{
    private static readonly (string Name, string Description)[] Topics =
    [
        ("AI", "This session explores artificial intelligence, AI agents, large language models, generative AI, and machine learning."),
        ("Mobile", "This session explores building mobile applications for Android and iOS with .NET MAUI."),
        ("Web", "This session explores web development, websites, ASP.NET Core, Blazor, and web application APIs."),
        ("Data", "This session explores data, databases, SQL, data analytics, data engineering, and real-time data processing."),
        ("Cloud", "This session explores cloud computing, Azure, cloud infrastructure, containers, and deploying distributed applications."),
        ("Other", "This session explores software development practices, programming, developer productivity, and the .NET community.")
    ];

    private readonly Dictionary<string, (double[] Scores, int Count)> scores = [];
    private readonly Lock sync = new();
    private Task<GeneratedEmbeddings<Embedding<float>>>? anchorTask;

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Task<GeneratedEmbeddings<Embedding<float>>> anchorsTask;
        lock (sync)
            anchorsTask = anchorTask ??= generator.GenerateAsync(Topics.Select(t => t.Description), cancellationToken: cancellationToken);
        var anchors = await anchorsTask;
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            var vector = (await generator.GenerateAsync([chunk.Content], cancellationToken: cancellationToken))[0].Vector.ToArray();
            lock (sync)
            {
                if (!scores.TryGetValue(chunk.Document.Identifier, out var aggregate))
                    aggregate = (new double[Topics.Length], 0);
                for (var i = 0; i < Topics.Length; i++)
                    aggregate.Scores[i] += Cosine(vector, anchors[i].Vector.ToArray());
                aggregate.Count++;
                scores[chunk.Document.Identifier] = aggregate;
                chunk.Metadata["topic"] = Topics[Array.IndexOf(aggregate.Scores, aggregate.Scores.Max())].Name;
            }
            yield return chunk;
        }
    }

    public string TopicFor(string documentId)
    {
        lock (sync)
            return scores.TryGetValue(documentId, out var value)
                ? Topics[Array.IndexOf(value.Scores, value.Scores.Max())].Name
                : "Other";
    }

    private static double Cosine(float[] left, float[] right)
    {
        double dot = 0, leftLength = 0, rightLength = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftLength += left[i] * left[i];
            rightLength += right[i] * right[i];
        }
        return leftLength == 0 || rightLength == 0 ? 0 : dot / Math.Sqrt(leftLength * rightLength);
    }
}
