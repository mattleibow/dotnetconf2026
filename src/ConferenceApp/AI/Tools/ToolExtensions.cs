namespace ConferenceApp.AI;

// Registers the conference data, favourites, and navigation tool services used by [AIToolSource] contexts.
public static class ToolExtensions
{
    public static MauiAppBuilder AddAITools(this MauiAppBuilder builder)
    {
        // Register session/speaker lookup tools, favourites management tools, and app navigation tools.
        builder.Services.AddSingleton<ConferenceDataTools>();
        builder.Services.AddSingleton<FavoritesTools>();
        builder.Services.AddSingleton<NavigationTools>();

        return builder;
    }
}
