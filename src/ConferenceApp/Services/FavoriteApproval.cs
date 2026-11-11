using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConferenceApp.Services;

public sealed partial class FavoriteApproval : ObservableObject
{
    private TaskCompletionSource<bool>? pending;
    private readonly List<string> results = [];
    private readonly Dictionary<(string Id, bool IsFavorite), string> decisions = [];

    [ObservableProperty] public partial bool IsPending { get; set; }
    [ObservableProperty] public partial string Description { get; set; } = "";

    public void BeginRequest()
    {
        results.Clear();
        decisions.Clear();
    }

    public void RecordResult(string result) => results.Add(result);

    public bool TryGetDecision(string id, bool isFavorite, out string result) =>
        decisions.TryGetValue((id, isFavorite), out result!);

    public string RecordDecision(string id, bool isFavorite, string result)
    {
        decisions[(id, isFavorite)] = result;
        RecordResult(result);
        return result;
    }

    public string? GetResults() => results.Count == 0 ? null : string.Join("\n", results);

    public async Task<bool> RequestAsync(string description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pending is not null)
            throw new InvalidOperationException("Another favourite request is already awaiting approval.");

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = completion;
        Description = description;
        IsPending = true;
        SemanticScreenReader.Default.Announce($"Approval required. {description}");
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try
        {
            return await completion.Task;
        }
        finally
        {
            IsPending = false;
            pending = null;
        }
    }

    [RelayCommand]
    private void Approve() => pending?.TrySetResult(true);

    [RelayCommand]
    private void Deny() => pending?.TrySetResult(false);
}
