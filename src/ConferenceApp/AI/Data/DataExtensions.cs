using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class DataExtensions
{
    public static readonly string[] Topics = ["AI", "Mobile", "Web", "Cloud", "Data", "IoT"];

    public static MauiAppBuilder AddDataEnrichment(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton(services => new EnricherOptions(services.GetRequiredService<IChatClient>())
        {
            BatchSize = 1,
            ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
            LoggerFactory = services.GetRequiredService<ILoggerFactory>()
        });

        builder.Services.AddSingleton(services =>
            new SummaryEnricher(services.GetRequiredService<EnricherOptions>(), maxWordCount: 24));

        builder.Services.AddSingleton(services =>
            new ClassificationEnricher(services.GetRequiredService<EnricherOptions>(), Topics, fallbackClass: "None"));

        builder.Services.AddSingleton<SessionEnrichmentService>();

        return builder;
    }
}
