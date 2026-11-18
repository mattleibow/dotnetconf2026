using ConferenceApp.Services;

namespace ConferenceApp.AI;

public static class DataExtensions
{
    public static MauiAppBuilder AddDataEnrichment(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<BaseBackgroundService, SessionEnrichmentService>();
        builder.Services.AddSingleton<BaseBackgroundService, SpeakerEnrichmentService>();

        return builder;
    }
}
