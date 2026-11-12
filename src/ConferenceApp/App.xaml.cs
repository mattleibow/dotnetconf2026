using ConferenceApp.AI;

namespace ConferenceApp;

public partial class App : Application
{
	public App(SessionEnrichmentService enrichment)
	{
		InitializeComponent();
		_ = enrichment.StartAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}