using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;

namespace ConferenceApp.AI;

// Registers the local ONNX embedding generator, in-memory vector store, and background session indexer.
public static class EmbeddingExtensions
{
    public static MauiAppBuilder AddEmbeddings(this MauiAppBuilder builder)
    {
        // Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform, wrapped with Microsoft.Extensions.AI logging.
        builder.Services.AddSingleton<MiniLmEmbeddingGenerator>();
        builder.Services
            .AddEmbeddingGenerator(sp => sp.GetRequiredService<MiniLmEmbeddingGenerator>())
            .UseLogging();

        // Register the in-memory VectorStore and strongly-typed Session vector collection.
        builder.Services.AddInMemoryVectorStore();
        builder.Services.AddInMemoryVectorStoreRecordCollection<string, Session>("sessions");

        // Register the background service that embeds and indexes talk abstracts at startup.
        builder.Services.AddSingleton<BaseBackgroundService, SessionIndexingService>();

        return builder;
    }
}
