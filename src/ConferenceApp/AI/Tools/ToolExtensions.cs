namespace ConferenceApp.AI;

// Registers the conference data and favourites tool services used by [AIToolSource] contexts.
public static class ToolExtensions
{
    public static MauiAppBuilder AddAITools(this MauiAppBuilder builder)
    {
        // Register read-only session/speaker lookup tools and favourites management tools.
        builder.Services.AddSingleton<ConferenceDataTools>();
        builder.Services.AddSingleton<FavoritesTools>();

        return builder;
    }
}
