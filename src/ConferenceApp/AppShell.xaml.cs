using ConferenceApp.Views;

namespace ConferenceApp;

public partial class AppShell : Shell
{
	public AppShell(AgendaPage agenda, FavoritesPage favorites, ExplorePage explore, AssistantPage assistant)
	{
		InitializeComponent();
		Routing.RegisterRoute("session", typeof(SessionPage));
		Routing.RegisterRoute("speaker", typeof(SpeakerPage));
		Routing.RegisterRoute("sponsor", typeof(SponsorPage));
		var tabs = new TabBar();
		tabs.Items.Add(new ShellContent { Title = "Agenda", Icon = "tab_agenda.png", Route = "agenda", Content = agenda, AutomationId = "agenda-tab" });
		tabs.Items.Add(new ShellContent { Title = "Favourites", Icon = "tab_favorites.png", Route = "favorites", Content = favorites, AutomationId = "favorites-tab" });
		tabs.Items.Add(new ShellContent { Title = "Venue", Icon = "tab_venue.png", Route = "explore", Content = explore, AutomationId = "venue-tab" });
		tabs.Items.Add(new ShellContent { Title = "Assistant", Icon = "tab_assistant.png", Route = "assistant", Content = assistant, AutomationId = "assistant-tab" });
		Items.Add(tabs);
	}
}
