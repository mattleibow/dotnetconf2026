using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SponsorDetailPage : ContentPage, IQueryAttributable
{
    private readonly SponsorDetailViewModel viewModel;
    public SponsorDetailPage(SponsorDetailViewModel viewModel) { InitializeComponent(); BindingContext = this.viewModel = viewModel; }
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("name", out var value) && value is string name)
            await viewModel.LoadAsync(Uri.UnescapeDataString(name));
        else
            viewModel.Error = "Sponsor not found.";
    }
}
