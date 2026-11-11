using ConferenceApp.Services;
using Microsoft.Extensions.AI;

using var source = new CancellationTokenSource();
var guard = new ToolErrorGuard(source);
var calls = 0;
var failing = AIFunctionFactory.Create(() => { calls++; throw new InvalidOperationException("test failure"); });
var action = AIFunctionFactory.Create(() => { calls++; return "changed"; });
var guardedFailure = (AIFunction)guard.Wrap(failing);
var guardedAction = (AIFunction)guard.Wrap(action);
try { await guardedFailure.InvokeAsync(); throw new Exception("Failure was swallowed."); }
catch (InvalidOperationException) { }
if (!guard.Failed || !source.IsCancellationRequested || calls != 1) throw new Exception("Failure did not stop the request.");
try { await guardedAction.InvokeAsync(); throw new Exception("Action executed after failure."); }
catch (InvalidOperationException) { }
if (calls != 1) throw new Exception("Action executed after failure.");

foreach (var approve in new[] { false, true })
{
    using var request = new CancellationTokenSource();
    var invokingGuard = new ToolErrorGuard(request);
    var approvalCalls = 0;
    var changes = 0;
    var function = AIFunctionFactory.Create(() =>
    {
        approvalCalls++;
        if (approve) changes++;
        return approve ? "approved" : "denied";
    }, "add_favourite");
    var tool = invokingGuard.Wrap(new ApprovalRequiredAIFunction(function));
    var backend = new FakeBackend();
    using var client = new FunctionInvokingChatClient(backend)
    {
        MaximumConsecutiveErrorsPerRequest = 0,
        AllowConcurrentInvocation = false,
        TerminateOnUnknownCalls = true
    };
    var response = await client.GetResponseAsync(
        [new ChatMessage(ChatRole.User, "Change a favourite")],
        new ChatOptions { Tools = [tool], AllowMultipleToolCalls = false }, request.Token);
    if (approvalCalls != 1 || changes != (approve ? 1 : 0) || response.Text != "done" || backend.Requests != 2)
        throw new Exception("Simulated favourite approval tool call failed.");
}

using (var request = new CancellationTokenSource())
{
    var invokingGuard = new ToolErrorGuard(request);
    var unknownAction = AIFunctionFactory.Create(() => "should not run", "other_action");
    var backend = new FakeBackend("other_action");
    using var client = new FunctionInvokingChatClient(backend);
    var response = await client.GetResponseAsync(
        [new ChatMessage(ChatRole.User, "Other action")],
        new ChatOptions { Tools = [invokingGuard.Wrap(new ApprovalRequiredAIFunction(unknownAction))] });
    if (!response.Messages.SelectMany(m => m.Contents).Any(c => c is ToolApprovalRequestContent) || backend.Requests != 1)
        throw new Exception("Other approval-required tools must preserve framework approval.");
}
Console.WriteLine("Tool error and simulated approval checks passed.");

sealed class FakeBackend(string toolName = "add_favourite") : IChatClient
{
    public int Requests { get; private set; }
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests++;
        var response = Requests == 1
            ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", toolName, new Dictionary<string, object?>())])
            : new ChatMessage(ChatRole.Assistant, "done");
        return Task.FromResult(new ChatResponse(response) { FinishReason = Requests == 1 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
