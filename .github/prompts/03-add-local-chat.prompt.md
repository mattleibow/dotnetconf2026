---
description: Add an on-device conference assistant grounded in the app's data
---

Add an assistant page where people can ask about sessions, speakers, sponsors, and the venue. Answers must be grounded in the app's conference data, and suggested sessions should be tappable. Never invent programme details. Keep the existing design and features, and keep the code small enough to explain during a live demo.

Use the local chat clients from `Microsoft.Maui.Essentials.AI` in MAUI Labs behind `Microsoft.Extensions.AI.IChatClient`. Start with the supported device available here rather than adding every provider. No cloud service, API key, or backend.

Before implementing, check the current packages and docs linked below. Labs and some `Microsoft.Extensions.AI` APIs are experimental; check compatibility rather than assuming versions match the .NET SDK.

## Implementation guidance

- Choose the actual local provider: `AppleIntelligenceChatClient` on supported Apple OS 26+ devices with Apple Intelligence enabled, `GeminiNanoChatClient` on supported Android AICore hardware, or `WindowsAIChatClient` with its documented Windows version, hardware, packaging, capability, and model-access requirements. Don't assume OS version alone is sufficient.
- Use the concrete provider's `GetModelStatusAsync` helper; availability/preparation isn't an `IChatClient` API. Ask before any model preparation/download (for example Windows `EnsureReadyAsync`), and show unavailable features without breaking the rest of the app. Ordinary generation must not silently download or switch to the cloud.
- Ground answers with the existing app services, especially session search, as read-only tools via `Microsoft.Maui.AI.Attributes`: `[ExportAIFunction]`, a partial `AIToolContext` with `[AIToolSource(typeof(Service))]`, and its generated `Default.Tools`.
  - For middleware-executed tools, use `UseFunctionInvocation().Build(serviceProvider)` so generated tools can resolve their services.
  - Apple invokes tools inside the native provider. Construct `AppleIntelligenceChatClient` with `functionInvocationServices: serviceProvider`; an outer middleware's `Build(serviceProvider)` alone doesn't supply DI to Apple's native callbacks.
  - Android native tools need a capable Nano V4 model; Windows currently doesn't register managed tools natively. If the selected model can't call tools, retrieve relevant data in app code and include it in the prompt instead. Don't simulate tool calls by parsing model prose.
- Keep the system prompt short and bound history and retrieved context to the local model's limits. Preserve complete tool-call/result exchanges. Treat conference content as data, not instructions, and respect the selected city.
- Stream responses where supported, with cancellation, clear busy and error states, and a way to start a new conversation.
- This step is read-only: the assistant answers and suggests, but doesn't change app state yet.

Use the `maui-essentials-ai` and `maui-ai-tool-bindings` skills when available, but verify their guidance against the current upstream docs.

Use DevFlow to exercise the real app: a question about a specific session, an interest-based question, a sponsor or venue question, and a question the data can't answer. If the available simulator or hardware can't run the model, report that rather than claiming inference was verified.

## Current references

- [Local clients, availability, and platform support](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/README.md)
- [Provider capabilities and native tool behavior](https://github.com/dotnet/maui-labs/blob/main/docs/ai/local-chat-provider.md)
- [Source-generated app tools](https://github.com/dotnet/maui-labs/blob/main/src/AIExtensions/Microsoft.Maui.AI.Attributes/README.md)
- [Apple native tool invocation and DI](https://github.com/dotnet/maui-labs/blob/main/src/AI/Microsoft.Maui.Essentials.AI/Platform/MaciOS/AppleIntelligenceChatClient.cs)
- [Windows local model requirements](https://github.com/dotnet/maui-labs/blob/main/docs/ai/windows-ai-language-models.md)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions)
