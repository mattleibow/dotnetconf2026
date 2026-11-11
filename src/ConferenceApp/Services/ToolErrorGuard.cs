using Microsoft.Extensions.AI;

namespace ConferenceApp.Services;

public sealed class ToolErrorGuard(CancellationTokenSource request)
{
    private readonly CancellationTokenSource request = request;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int failed;
    public bool Failed => Volatile.Read(ref failed) != 0;

    public AITool Wrap(AITool tool)
    {
        // Favourite methods request approval themselves, rather than via the framework's approval protocol.
        if (tool is ApprovalRequiredAIFunction required && required.Name is "add_favourite" or "remove_favourite")
            return new GuardedFunction(required, this, true);
        return tool is AIFunction function ? new GuardedFunction(function, this, false) : tool;
    }

    private sealed class GuardedFunction(AIFunction inner, ToolErrorGuard guard, bool approvalHandledByTool) : DelegatingAIFunction(inner)
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) =>
            approvalHandledByTool && serviceType == typeof(ApprovalRequiredAIFunction) ? null : base.GetService(serviceType, serviceKey);

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await guard.gate.WaitAsync(cancellationToken);
            try
            {
                if (guard.Failed)
                    throw new InvalidOperationException("A previous tool failed.");
                try
                {
                    return await base.InvokeCoreAsync(arguments, cancellationToken);
                }
                catch
                {
                    Interlocked.Exchange(ref guard.failed, 1);
                    guard.request.Cancel();
                    throw;
                }
            }
            finally
            {
                guard.gate.Release();
            }
        }
    }
}
