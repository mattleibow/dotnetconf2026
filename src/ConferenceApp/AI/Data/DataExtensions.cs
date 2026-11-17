using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// Registers Microsoft.Extensions.DataIngestion options and background session and speaker enrichment services.
public static class DataExtensions
{
    public static MauiAppBuilder AddDataEnrichment(this MauiAppBuilder builder)
    {
        // Configure DataIngestion enrichers to process one item at a time with deterministic output.
        builder.Services.AddTransient(services => new EnricherOptions(services.GetRequiredService<IChatClient>())
        {
            BatchSize = 1,
            ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 256 },
            LoggerFactory = services.GetRequiredService<ILoggerFactory>()
        });

        // Register background services that populate Session.Summary, Session.Topics, and Speaker profiles at startup.
        builder.Services.AddSingleton<BaseBackgroundService, SessionSummaryEnrichmentService>();
        builder.Services.AddSingleton<BaseBackgroundService, SessionTopicEnrichmentService>();
        builder.Services.AddSingleton<BaseBackgroundService, SpeakerProfileEnrichmentService>();

        return builder;
    }
}
