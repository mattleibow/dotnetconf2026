using Microsoft.Extensions.DependencyInjection;
using ConferenceApp.Services;

namespace ConferenceApp;

public partial class App : Application
{
	private readonly AppShell shell;
	public App(AppShell shell
#if IOS || MACCATALYST
		, SemanticSearchService semanticSearch
#endif
		)
	{
		InitializeComponent();
		this.shell = shell;
#if IOS || MACCATALYST
		semanticSearch.Start();
#endif
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(shell);
	}
}