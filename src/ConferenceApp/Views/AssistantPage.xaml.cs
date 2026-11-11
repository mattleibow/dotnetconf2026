using ConferenceApp.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ConferenceApp.Views;

public partial class AssistantPage : ContentPage
{
    private readonly AssistantViewModel viewModel;

    public AssistantPage(AssistantViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
#if DEBUG
        DebugClockButton.IsVisible = true;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Messages.CollectionChanged += MessagesChanged;
        foreach (var message in viewModel.Messages)
            message.PropertyChanged += MessageChanged;
        await viewModel.CheckAvailabilityAsync();
    }

    protected override void OnDisappearing()
    {
        viewModel.Messages.CollectionChanged -= MessagesChanged;
        foreach (var message in viewModel.Messages)
            message.PropertyChanged -= MessageChanged;
        base.OnDisappearing();
    }

    private void MessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (AssistantMessage message in e.NewItems)
                message.PropertyChanged += MessageChanged;
        if (e.OldItems is not null)
            foreach (AssistantMessage message in e.OldItems)
                message.PropertyChanged -= MessageChanged;
        ScrollToLatest();
    }

    private void MessageChanged(object? sender, PropertyChangedEventArgs e) => ScrollToLatest();

    private void ScrollToLatest() => Dispatcher.Dispatch(() =>
    {
        if (viewModel.Messages.Count > 0)
            MessageList.ScrollTo(viewModel.Messages.Count - 1, position: ScrollToPosition.End, animate: false);
    });
}
