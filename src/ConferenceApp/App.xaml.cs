using ConferenceApp.AI;

namespace ConferenceApp;

public partial class App : Application
{
    public App(
        SessionIndexingService indexing,
        SessionSummaryEnrichmentService summaryEnrichment,
        SessionTopicEnrichmentService topicEnrichment)
    {
        InitializeComponent();
        
        _ = indexing.StartAsync();
        _ = summaryEnrichment.StartAsync();
        _ = topicEnrichment.StartAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
