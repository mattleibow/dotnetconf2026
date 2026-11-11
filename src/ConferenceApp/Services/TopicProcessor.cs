using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.Services;

internal sealed class TopicProcessor(IEmbeddingGenerator<string, Embedding<float>> generator) : IngestionChunkProcessor<string>
{
    private static readonly (string Name, string Description)[] Topics =
    [
        ("AI", "Artificial intelligence, machine learning, generative AI, agents and large language models."),
        ("Mobile", "Mobile app development for Android and iOS, .NET MAUI and user interfaces."),
        ("Web", "Web development, websites, ASP.NET, Blazor, frontend and backend APIs."),
        ("Data", "Databases, data engineering, analytics, SQL and data management."),
        ("Cloud", "Cloud computing, Azure, distributed applications, containers, deployment and infrastructure."),
        ("Other", "A general keynote about .NET, software development, programming practices, developer careers and community.")
    ];
    private readonly Lazy<Task<GeneratedEmbeddings<Embedding<float>>>> topicVectors =
        new(() => generator.GenerateAsync(Topics.Select(t => t.Description)));

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var topics = await topicVectors.Value.WaitAsync(cancellationToken);
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            var vector = (await generator.GenerateAsync([chunk.Content], cancellationToken: cancellationToken))[0].Vector.ToArray();
            var best = Enumerable.Range(0, Topics.Length).MaxBy(i => Cosine(vector, topics[i].Vector.ToArray()));
            chunk.Metadata["topic"] = Topics[best].Name;
            yield return chunk;
        }
    }

    private static float Cosine(float[] left, float[] right)
    {
        if (left.Length != right.Length)
            throw new InvalidOperationException("Topic and session embeddings have different dimensions.");
        float dot = 0, leftLength = 0, rightLength = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftLength += left[i] * left[i];
            rightLength += right[i] * right[i];
        }
        return leftLength == 0 || rightLength == 0 ? 0 : dot / MathF.Sqrt(leftLength * rightLength);
    }
}
