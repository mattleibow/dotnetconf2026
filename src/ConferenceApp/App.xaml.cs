using Microsoft.Extensions.DependencyInjection;
using ConferenceApp.Services;

namespace ConferenceApp;

public partial class App : Application
{
	private readonly AppShell shell;
	public App(AppShell shell
		, SemanticSearchService semanticSearch
		, SessionEnrichmentService enrichment
		)
	{
		InitializeComponent();
		this.shell = shell;
		semanticSearch.Start();
		enrichment.Start();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(shell);
	}
}