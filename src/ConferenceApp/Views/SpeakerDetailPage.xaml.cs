using ConferenceApp.ViewModels;
namespace ConferenceApp.Views;
public partial class SpeakerDetailPage : ContentPage, IQueryAttributable
{
    private readonly SpeakerDetailViewModel viewModel;
    public SpeakerDetailPage(SpeakerDetailViewModel viewModel) { InitializeComponent(); BindingContext = this.viewModel = viewModel; }
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && value is string id)
            await viewModel.LoadAsync(Uri.UnescapeDataString(id));
        else
            viewModel.Error = "Speaker not found.";
    }
}
