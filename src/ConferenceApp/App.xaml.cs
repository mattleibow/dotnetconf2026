using ConferenceApp.AI;

namespace ConferenceApp;

public partial class App : Application
{
    public App(SessionIndexingService indexing, SessionEnrichmentService enrichment)
    {
        InitializeComponent();
        _ = InitializeAIAsync(indexing, enrichment);
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());

    private static async Task InitializeAIAsync(
        SessionIndexingService indexing,
        SessionEnrichmentService enrichment)
    {
        await indexing.StartAsync();
        await enrichment.StartAsync();
    }
}
