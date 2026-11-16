namespace ConferenceApp.AI;

public static class ToolExtensions
{
    public static MauiAppBuilder AddAITools(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<ConferenceDataTools>();
        builder.Services.AddSingleton<FavoritesTools>();

        return builder;
    }
}
