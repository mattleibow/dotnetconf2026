namespace ConferenceApp.AI;

// Central entry point for registering the app's on-device AI stack.
public static class ConferenceAIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        // Register platform on-device IChatClient (Apple Intelligence, Gemini Nano, or Windows AI).
        builder.AddChatClients();

        // Register semantic search embeddings, in-memory vector store, and session indexing.
        builder.AddEmbeddings();

        // Register sequential structured-output enrichment for sessions and speakers.
        builder.AddDataEnrichment();

        // Register source-generated AI tool contexts for conference data and favourites.
        builder.AddAITools();

        return builder;
    }
}
