using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConferenceApp.Models;

public partial class ChatApproval : ObservableObject
{
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ChatApproval(Session session, bool add) : this([session], add) { }

    public ChatApproval(IReadOnlyList<Session> sessions, bool add)
    {
        ArgumentOutOfRangeException.ThrowIfZero(sessions.Count);
        Sessions = sessions.ToArray();
        Title = sessions.Count == 1
            ? add ? "Add to favourites?" : "Remove from favourites?"
            : add ? $"Add all {sessions.Count} talks to favourites?" : $"Remove all {sessions.Count} talks from favourites?";
        Description = sessions.Count == 1 ? sessions[0].Title : "The whole recommended plan. Your other favourites will stay as they are.";
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<Session> Sessions { get; }
    public string PrimarySessionId => Sessions[0].Id;
    public string Title { get; }
    public string Description { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResolved))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DenyCommand))]
    public partial bool IsPending { get; set; } = true;

    public bool IsResolved => !IsPending;

    [ObservableProperty]
    public partial string Status { get; set; } = "Only you can approve this change.";

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void Approve() => Decide(true);

    [RelayCommand(CanExecute = nameof(IsPending))]
    private void Deny() => Decide(false);

    public void Decide(bool approved)
    {
        if (!decision.TrySetResult(approved))
            return;
        IsPending = false;
        Status = approved ? "Approved" : "Denied — nothing changed.";
    }

    public void Cancel()
    {
        if (!decision.TrySetCanceled())
            return;
        IsPending = false;
        Status = "Cancelled — nothing changed.";
    }

    public async Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => decision.TrySetCanceled(cancellationToken));
        try
        {
            return await decision.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsPending = false;
                Status = "Cancelled — nothing changed.";
            });
            throw;
        }
    }
}
