using ConferenceApp.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceApp.AI;

public static class EmbeddingExtensions
{
    public static MauiAppBuilder AddEmbeddings(this MauiAppBuilder builder)
    {
        // Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform, wrapped with Microsoft.Extensions.AI logging.
        builder.Services
            .AddEmbeddingGenerator(sp => ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp))
            .UseLogging();

        // Register the in-memory VectorStore and strongly-typed Session vector collection.
        builder.Services.AddInMemoryVectorStore();
        builder.Services.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions");

        builder.Services.AddSingleton<SessionIndexingService>();

        return builder;
    }
}
