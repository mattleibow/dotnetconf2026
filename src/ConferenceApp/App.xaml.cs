using ConferenceApp.Services;

namespace ConferenceApp;

public partial class App : Application
{
    public App(IEnumerable<BaseBackgroundService> backgroundServices)
    {
        InitializeComponent();

        foreach (var service in backgroundServices)
            _ = service.StartAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
