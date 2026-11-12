using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class EmbeddingExtensions
{
    public static MauiAppBuilder AddEmbeddings(this MauiAppBuilder builder)
    {
        // Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform, wrapped with Microsoft.Extensions.AI logging.
        // On Apple, you can also compare against the built-in NLEmbeddingGenerator:
        // builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
        //     sp => new NLEmbeddingGenerator(sp.GetRequiredService<ILoggerFactory>()).AsBuilder().UseLogging(sp.GetRequiredService<ILoggerFactory>()).Build());
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp)
                .AsBuilder()
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build());

        builder.Services.AddSingleton<SessionEnrichmentService>();
        builder.Services.AddSingleton<SearchService>();

        return builder;
    }
}
