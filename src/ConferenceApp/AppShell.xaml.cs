using ConferenceApp.Views;

namespace ConferenceApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("session-detail", typeof(SessionDetailPage));
	}
}
