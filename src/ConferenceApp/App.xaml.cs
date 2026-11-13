using ConferenceApp.AI;

namespace ConferenceApp;

public partial class App : Application
{
	public App(SessionIndexingService indexing, SessionEnrichmentService enrichment)
	{
		InitializeComponent();
		_ = indexing.StartAsync();
		_ = enrichment.StartAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}