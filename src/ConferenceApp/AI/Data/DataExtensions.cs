using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// Registers Microsoft.Extensions.DataIngestion options and background session enrichment services.
public static class DataExtensions
{
    public static MauiAppBuilder AddDataEnrichment(this MauiAppBuilder builder)
    {
        // Configure DataIngestion enrichers to process one session at a time with deterministic output.
        builder.Services.AddSingleton(services => new EnricherOptions(services.GetRequiredService<IChatClient>())
        {
            BatchSize = 1,
            ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
            LoggerFactory = services.GetRequiredService<ILoggerFactory>()
        });

        // Register background services that populate Session.Summary and Session.Topics at startup.
        builder.Services.AddSingleton<BaseBackgroundService, SessionSummaryEnrichmentService>();
        builder.Services.AddSingleton<BaseBackgroundService, SessionTopicEnrichmentService>();

        return builder;
    }
}
