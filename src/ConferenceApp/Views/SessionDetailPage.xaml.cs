using ConferenceApp.ViewModels;
namespace ConferenceApp.Views;
public partial class SessionDetailPage : ContentPage, IQueryAttributable
{
    private readonly SessionDetailViewModel viewModel;
    public SessionDetailPage(SessionDetailViewModel viewModel) { InitializeComponent(); BindingContext = this.viewModel = viewModel; }
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && value is string id)
            await viewModel.LoadAsync(Uri.UnescapeDataString(id));
        else
            viewModel.Error = "Session not found.";
    }
}
