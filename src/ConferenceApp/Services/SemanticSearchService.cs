using CommunityToolkit.VectorData.InMemory;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public sealed class SemanticSearchService(
    ConferenceService conferenceService,
    IServiceProvider services,
    ILogger<SemanticSearchService> logger)
{
    private Task<Index>? initialization;

    public void Start() => initialization ??= Task.Run(BuildIndexAsync);

    public async Task<SessionSearchResponse> SearchAsync(string query, string cityId)
    {
        Start();
        try
        {
            var index = await initialization!;
            var results = new List<SessionSearchResult>();
            var seen = new HashSet<string>();
            if (index.Count == 0)
                return new(results, "");
            await foreach (var hit in index.Writer.VectorStoreCollection.SearchAsync(query, index.Count))
            {
                var session = index.Sessions[(string)hit.Record["documentid"]!];
                if (session.CityId == cityId && seen.Add(session.Id))
                    results.Add(new SessionSearchResult(session, (string)hit.Record["excerpt"]!, true));
            }
            return new(results, "");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic search unavailable; using keyword search.");
            return new([], "Semantic search unavailable. Showing keyword matches only.");
        }
    }

    private async Task<Index> BuildIndexAsync()
    {
        var generator = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        logger.LogInformation("Building semantic search index with {Generator}", generator.GetType().FullName);
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var dimensions = generator is OnnxEmbeddingGenerator ? 384 : 512;
        var writer = new VectorStoreWriter<string>(store, dimensions);
        var data = await conferenceService.GetConferenceAsync();
        var sessions = new Dictionary<string, Session>();
        var directory = Path.Combine(FileSystem.AppDataDirectory, "conference", "sessions");
        Directory.CreateDirectory(directory);

        try
        {
            foreach (var session in data.Sessions.Where(s => s.Kind == "session"))
            {
                var path = Path.Combine(directory, Path.GetFileName(session.Content));
                using var source = await FileSystem.OpenAppPackageFileAsync($"conference/{session.Content}");
                using var destination = File.Create(path);
                await source.CopyToAsync(destination);
                sessions.Add(path, session);
            }

            var chunker = new SessionChunker(sessions);
            var pipeline = new IngestionPipeline<string>(new MarkdownReader(), chunker, writer);
            await foreach (var result in pipeline.ProcessAsync(sessions.Keys.Select(path => new FileInfo(path))))
            {
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not index session document '{result.DocumentId}'.", result.Exception);
            }
            return new(writer, pipeline, sessions, chunker.Count);
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    private sealed record Index(
        VectorStoreWriter<string> Writer,
        IngestionPipeline<string> Pipeline,
        Dictionary<string, Session> Sessions,
        int Count);
}
