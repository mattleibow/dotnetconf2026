namespace ConferenceApp.AI;

public static class ConferenceAIExtensions
{
    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        builder.AddChatClients();
        builder.AddEmbeddings();
        builder.AddDataEnrichment();
        builder.AddAITools();
        builder.AddAgents();

        return builder;
    }
}
