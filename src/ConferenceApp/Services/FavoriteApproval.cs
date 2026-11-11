using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ConferenceApp.Services;

public partial class FavoriteApproval : ObservableObject
{
    private TaskCompletionSource<bool>? pending;
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial bool IsPending { get; set; }

    public async Task<bool> RequestAsync(string description, CancellationToken cancellationToken)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (pending is not null)
                return false;
            cancellationToken.ThrowIfCancellationRequested();
            var request = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = request;
            Description = description;
            IsPending = true;
            SemanticScreenReader.Default.Announce($"Approval required. {description}");
            using var registration = cancellationToken.Register(() => request.TrySetResult(false));
            try { return await request.Task; }
            finally
            {
                if (pending == request)
                {
                    pending = null;
                    IsPending = false;
                }
            }
        });
    }

    [RelayCommand]
    private void Approve() => pending?.TrySetResult(true);

    [RelayCommand]
    private void Deny() => pending?.TrySetResult(false);
}
