using ConferenceApp.Services;

namespace ConferenceApp;

public partial class App : Application
{
    private readonly CancellationTokenSource shutdown = new();

    public App(IEnumerable<BaseBackgroundService> backgroundServices)
    {
        InitializeComponent();

        foreach (var service in backgroundServices)
            _ = service.StartAsync(shutdown.Token);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
#if !ANDROID
        window.Destroying += (_, _) => StopBackgroundServices();
#endif
        return window;
    }

    internal void StopBackgroundServices() => shutdown.Cancel();
}
