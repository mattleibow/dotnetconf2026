using ConferenceApp.Services;

namespace ConferenceApp.Views;

public sealed class SessionDetailPage : ContentPage, IQueryAttributable
{
    private readonly ConferenceService service;
    private readonly VerticalStackLayout content = new() { Padding = 24, Spacing = 16 };

    public SessionDetailPage(ConferenceService service)
    {
        this.service = service;
        Title = "Session";
        Content = new ScrollView { Content = content };
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            var id = Uri.UnescapeDataString((string)query["id"]);
            var data = await service.GetConferenceAsync();
            var session = data.Sessions.First(s => s.Id == id);
            content.Children.Clear();
            content.Children.Add(new Label { Text = session.Title, FontSize = 26, FontFamily = "OpenSansSemibold" });
            content.Children.Add(new Label { Text = $"{session.Start:HH:mm} – {session.End:HH:mm}  ·  {session.Track ?? "Main session"}" });
            content.Children.Add(new Label { Text = string.Join(", ", data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => s.Name)) });
            content.Children.Add(new Label { Text = await service.GetContentAsync(session.Content) });
        }
        catch (Exception ex)
        {
            content.Children.Clear();
            content.Children.Add(new Label { Text = $"Unable to load session: {ex.Message}" });
        }
    }
}
