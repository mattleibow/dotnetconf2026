using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class ChatViewModel(ChatService chat, ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<ChatItem> Messages { get; } = [];
    public IReadOnlyList<string> Suggestions => chat.Suggestions;

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    public partial string Prompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task LoadAsync() =>
        SelectedCity = await preferences.GetSelectedCityAsync(database);

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync(database);

    [RelayCommand]
    private Task AskSuggestionAsync(string suggestion)
    {
        Prompt = suggestion;
        return SendAsync();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = Prompt.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy)
            return;

        Prompt = "";
        Messages.Add(new ChatItem(isUser: true, text));
        var reply = new ChatItem(isUser: false);
        Messages.Add(reply);

        IsBusy = true;
        try
        {
            await foreach (var chunk in chat.SendAsync(Messages))
                reply.Text += chunk;
        }
        catch (Exception ex)
        {
            reply.Text = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
