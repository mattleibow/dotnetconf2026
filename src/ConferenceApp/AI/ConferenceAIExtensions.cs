namespace ConferenceApp.AI;

// Central entry point for registering the app's AI stack.
public static class ConferenceAIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        // Register remote-first chat with native on-device fallback.
        builder.AddChatClients();

        // Register semantic search embeddings, in-memory vector store, and session indexing.
        builder.AddEmbeddings();

        // Register independent session and speaker enrichment services that run in parallel.
        builder.AddDataEnrichment();

        // Register source-generated AI tool contexts for conference data and favourites.
        builder.AddAITools();

        // Register AI context providers, agents, multi-agent workflows, and semantic routing.
        builder.AddAgents();

        return builder;
    }
}
