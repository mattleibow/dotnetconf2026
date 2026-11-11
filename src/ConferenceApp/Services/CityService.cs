namespace ConferenceApp.Services;

public sealed class CityService
{
    public event EventHandler? Changed;

    public string SelectedId => Preferences.Default.Get("city", "durban");

    public void Select(string id)
    {
        Preferences.Default.Set("city", id);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
