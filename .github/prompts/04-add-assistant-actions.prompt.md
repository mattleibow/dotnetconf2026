---
description: Let the assistant navigate and update favourites, with user approval
---

Let the assistant do more than chat: open a session, speaker, sponsor, or venue page, and add or remove favourites when asked. Keep the existing design and features, and keep the code small enough to explain during a live demo.

## Implementation guidance

- Expose navigation and favourites from the existing app services as tools via `Microsoft.Maui.AI.Attributes`. Keep tools descriptive and narrowly scoped. Prefer an explicit `set favourite` action over a toggle, so retries can't reverse the user's intent.
- Mark state-changing tools with `[ExportAIFunction(ApprovalRequired = true)]`. Where `FunctionInvokingChatClient` executes tools, handle its `ToolApprovalRequestContent` and resume with the matching `ToolApprovalResponseContent`, preserving the request ID and exact tool call. Show the proposed session and change in an approve/deny card before anything changes. Navigation doesn't need approval.
- `ApprovalRequiredAIFunction` is a marker, not an authorization check. Apple executes tools inside its native provider and can invoke that wrapper directly, bypassing middleware approval messages. Add an approval-enforcing tool wrapper or guard in the app action service that awaits the same user confirmation before native invocation can mutate state. Pass the guarded tools to Apple, not the unguarded originals; don't rely on the model to request consent.
- Show only one confirmation per invocation, validate IDs/arguments before applying the approved change, and prevent duplicate execution when messages are resumed or retried. Denial, cancellation, or leaving the conversation must leave state unchanged and release any pending approval.
- Dispatch navigation, approval UI, and bound-state updates to the MAUI UI thread; don't block it while waiting for the user. Report the actual action result to the assistant, not a success-shaped placeholder.
- Respect provider differences: Android native tools need a capable Nano V4 model, and Windows currently has no native managed-tool registration. If tools aren't supported, disable assistant actions clearly rather than simulating success or parsing model text into commands.

Use the `maui-ai-tool-bindings` skill when available, but verify its guidance against the current upstream docs.

Use DevFlow to exercise the real app: navigate from chat, approve a favourite change, deny one, and confirm favourites survive a restart. If the hardware can't run the model, report that rather than claiming it was verified.

## Current references

- [Source-generated app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md)
- [Provider capabilities and native tool behavior](https://github.com/dotnet/maui-labs/blob/main/docs/ai/local-chat-provider.md)
- [Microsoft.Extensions.AI tool approval](https://learn.microsoft.com/dotnet/ai/ai-extensions)
- [FunctionInvokingChatClient approval contract](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/FunctionInvokingChatClient.cs)
