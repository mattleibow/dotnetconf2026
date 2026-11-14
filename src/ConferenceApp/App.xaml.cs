using ConferenceApp.AI;
using ConferenceApp.Data;

namespace ConferenceApp;

public partial class App : Application
{
    public App(
        DatabasePreloadService dbPreload,
        SessionIndexingService indexing,
        SessionSummaryEnrichmentService summaryEnrichment,
        SessionTopicEnrichmentService topicEnrichment)
    {
        InitializeComponent();

        _ = dbPreload.StartAsync();
        _ = indexing.StartAsync();
        _ = summaryEnrichment.StartAsync();
        _ = topicEnrichment.StartAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
