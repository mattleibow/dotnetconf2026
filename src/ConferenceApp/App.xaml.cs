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
        window.Destroying += (_, _) =>
        {
#if ANDROID
            // Activity recreation destroys the native window without ending the app.
            if (window.Handler?.PlatformView is not Android.App.Activity { IsFinishing: true })
                return;
#endif
            shutdown.Cancel();
        };
        return window;
    }
}
