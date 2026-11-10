---
description: Route complex questions from the local model to a cloud model
---

Keep simple questions on the device, but send harder ones, like "plan my day around AI and performance talks with no clashes", to a more capable cloud model. Keep the existing design and features, and keep the code small enough to explain during a live demo.

Use the routing clients introduced in `Microsoft.Extensions.AI` 10.9.0, currently experimental (diagnostic `MEAI001`). Check the selected package version and docs linked below first.

## Implementation guidance

- Add one cloud `IChatClient` from a standard `Microsoft.Extensions.AI` provider. Ask me which service and model to use. Never commit or bundle an API key: uncommitted files can still end up in the app package. For this demo, use a short-lived, restricted credential supplied at runtime; a shipped app needs a secure backend rather than a long-lived provider key. If cloud isn't configured, keep working locally.
- Where an on-device embedding generator is available, use `SemanticRoutingChatClient` with example utterances for each configured client and the local client as default. It compares the last user message with examples; it doesn't reason about complexity or assess the quality of a previous answer. Evaluate representative simple/complex questions and tune the score threshold rather than assuming a value works.
- Otherwise, use `RoutingChatClient.Create` with a simple, explainable rule. Routing chooses a model; it isn't session search. Don't silently download a model or send embeddings to the cloud just to choose a route.
- Make cloud use opt-in before transmitting anything. Show the selected route before sending and which provider/model answered. An on-device-only setting must override the router, including when a local model is unavailable.
- Route between already-configured provider pipelines, preserving Apple's native tool DI and approval enforcement. Keep routing outside each provider's function-invocation loop, and pin the selected route through all tool iterations and approval continuations for that turn. Share the curated app tools and confirmation policy, not an assumption that identical outer middleware works for every provider.
- Don't add automatic failover or retries around actions that change state. If cloud fails, show the error and let the user choose a new attempt.
- Send only necessary conversation/context. Don't replay one provider's opaque conversation IDs, continuation tokens, or reasoning artifacts to another; retain portable message history and complete tool-call/result pairs.
- Make ownership explicit: routing clients can own their clients and embedding generator. Use `leaveOpen: true` where supported if DI owns those shared instances, and dispose them once.

Use DevFlow to exercise the real app: a simple question that stays local, a complex one that goes to the cloud, and an approved action through the cloud route. Report what was verified.

## Current references

- [Routing and failover for Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/routing-and-failover-for-microsoft-extensions-ai/)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions) and [dotnet/extensions releases](https://github.com/dotnet/extensions/releases)
