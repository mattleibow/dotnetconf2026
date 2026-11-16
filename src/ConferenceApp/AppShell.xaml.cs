using ConferenceApp.Views;

namespace ConferenceApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("session", typeof(SessionPage));
        Routing.RegisterRoute("speaker", typeof(SpeakerPage));
    }
}
