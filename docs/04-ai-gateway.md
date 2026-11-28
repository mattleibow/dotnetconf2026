# AI gateway with Aspire

The app answers from a server first and falls back to its on-device model when the server can't help.

```
app ─► OrderedFailoverChatClient ─┬─► LiteLLM gateway ─┬─► Azure OpenAI (gpt-4.1-mini)
                                  │                    └─► local model           ← if Azure fails
                                  └─► on-device AI (Apple Intelligence / Gemini Nano / Windows AI)
```

Everything is orchestrated by the AppHost in [`src/ConferenceApp.AppHost`](../src/ConferenceApp.AppHost/AppHost.cs).

## What Aspire runs

| Resource | What it is |
|---|---|
| Local run mode: `enable-model-runner`, `pull-local-model`, `load-local-model`, `model-runner` | Enables Docker Model Runner on port 12434, pulls Qwen 2.5 3B GGUF on first run, starts it, and health-checks the API. Docker Desktop runs llama.cpp natively with Metal acceleration on Apple Silicon. The runner is managed by Docker Desktop and remains enabled when Aspire stops. |
| Local run mode: `open-webui` | Open WebUI container connected to Model Runner's Ollama-compatible API. |
| Publish mode, or `LocalModelRuntime=Ollama`: `ollama` + `ollama-chat` | Ollama container with `qwen2.5:3b` and a data volume; Model Runner is a local Docker Desktop service and is not deployed. On macOS the container runs on the CPU, so it is slower. |
| Publish mode, or `LocalModelRuntime=Ollama`: `ollama-openwebui` | Open WebUI, to chat with the Ollama container directly. |
| `foundry` + `azure-chat` | An Azure AI Foundry account and `gpt-4.1-mini` deployment, provisioned by Aspire on first run (as in eShop). API keys are enabled so the gateway container can call it. |
| `postgres` / `litellm` | LiteLLM's database: virtual keys, guardrails, spend logs. |
| `ai-gateway` | The [LiteLLM](https://docs.litellm.ai) proxy and Admin UI (`/ui`). |
| `gateway-tunnel` | A dev tunnel so simulators and emulators can reach the gateway over HTTPS. |
| `conference-app` / `conference-app-*` | The MAUI app on Mac Catalyst, the iOS Simulator and the Android emulator, nested under the `conference-app` parent in the dashboard (run mode only; start them individually from the dashboard). |

## Gateway configuration

[`litellm-config.yaml`](../src/ConferenceApp.AppHost/litellm-config.yaml) defines two endpoints. The app asks for `azure`; each endpoint decides which model it runs, and LiteLLM falls back to `ollama` if Azure fails. The config also has one demo guardrail that blocks "Mary Poppins".

The AppHost sets the `ollama` endpoint by execution mode. Local run mode uses Docker Model Runner's OpenAI-compatible API and its Qwen GGUF model; publish mode uses the Ollama container's OpenAI-compatible API.

To use the Ollama container locally (for example, where Docker Model Runner isn't available), start the AppHost with the `ollama` launch profile (`aspire start -lp ollama`), or pass `aspire start -- --LocalModelRuntime Ollama`. The default `http` profile sets `LocalModelRuntime=ModelRunner`.

Virtual keys, budgets, rate limits and any further guardrails are managed in the Admin UI and stored in Postgres. Items defined in the config file are read-only in the UI.

## First run

1. Set the Azure context for provisioning in the AppHost's user secrets (`Azure:SubscriptionId`, `Azure:Location`, `Azure:TenantId`, `Azure:CredentialSource=AzureCli`), then run `aspire start` from the repository root.
2. After `foundry` is provisioned, copy its key into the `azure-openai-key` parameter (dashboard or `aspire secret set Parameters:azure-openai-key …`) and restart `ai-gateway`.
3. Start `conference-app-maccatalyst`, `conference-app-ios-simulator` or `conference-app-android-emulator` from the dashboard. The iOS SDK picks the simulator, so boot the one you want first (with none booted it may pick an iPad).

On local runs, the AppHost enables Docker Model Runner, pulls the approximately 2 GB Qwen GGUF model if needed, and loads it before starting the gateway. Docker Desktop must be running; later runs reuse the cached model.

In local run mode, the app's LiteLLM virtual key (alias `conference-app`, limited to `azure` and `ollama`) is created automatically from the `litellm-app-key` parameter once `ai-gateway` is healthy. App resources wait for registration to succeed before starting; a fresh Postgres volume needs no manual key setup. Subsequent runs reuse the existing key without resetting its spend or limits. Lookup or registration errors fail readiness rather than letting an app start with an unregistered key. This initialization is local-only, not a production deployment step.

Any other demo client can use the same key; create separate keys in the Admin UI when you want per-client budgets or spend.

The `litellm-master-key`, `litellm-app-key` and `postgres-password` parameters have demo defaults; override them in the dashboard or with `aspire secret set`. The Admin UI is at `ai-gateway` → `/ui` (user `admin`, password = `litellm-master-key`).

## The app

[`ChatExtensions.cs`](../src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) registers a keyed `remote` client (`CreateRemoteChatClient`: OpenAI protocol → `https+http://ai-gateway/v1`), a keyed `local` on-device client, and a default `IChatClient` that is `OrderedFailoverChatClient(remote, local)`:

- Requests normally try the gateway first; the gateway is retried after any cooldown.
- Any error before output is streamed — unreachable server, timeout (15 s, no client retries; the gateway does its own), quota, or a guardrail block — falls back to on-device AI. After a gateway failure, `GatewayCooldownChatClient` sends every chat straight to on-device AI for 30 s before trying the gateway again, so only the first message waits for the timeout. If on-device AI fails too, the reply says the assistant can't answer right now and the error is logged. A blocked topic stays blocked at the gateway for the rest of that chat because the history is resent; use **New chat**.
- Background catalogue enrichment always uses the on-device client.
- Each reply's author shows the agent and the endpoint that answered, e.g. `conference-assistant (azure)`, or the on-device model after a fallback.

[`MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) adds environment variables to configuration and enables Aspire service discovery for every `HttpClient`, so `ai-gateway` resolves to the address Aspire supplied (directly on Mac, via the dev tunnel on devices).

## MCP server (desktop)

The Mac Catalyst (and Windows) build also hosts the app's AI tools as an MCP server at `http://localhost:5299/mcp/`, using the [C# MCP SDK](https://csharp.sdk.modelcontextprotocol.io/) (`ModelContextProtocol.Core`) in stateless Streamable HTTP mode over `HttpListener`, because ASP.NET Core isn't available in MAUI apps. [`.mcp.json`](../.mcp.json) connects Copilot to it, and [`.vscode/mcp.json`](../.vscode/mcp.json) configures VS Code's editor-managed MCP client. Run the desktop app and approve the `conference-app` server in your client. The Aspire dashboard links the endpoint on the `conference-app-maccatalyst` resource. Tools such as `navigate_to_page` and `add_favorite` change the running app. iOS and Android don't host the server.

## Notes

- Foundry Local isn't used: Microsoft documents it as a single-user on-device runtime, not a server. Docker Model Runner provides the local Apple Silicon model; publish mode uses the Ollama container. A production model service would need to be deployed separately (for example, vLLM on a supported GPU host) and configured behind the same gateway.
- The tunnel uses anonymous access, so the gateway is reachable from the internet while it runs; the LiteLLM virtual key is the only protection.
- Aspire launches the iOS app with `dotnet build -t:Run`. [`Directory.Build.targets`](../Directory.Build.targets) works around [dotnet/sdk#57310](https://github.com/dotnet/sdk/issues/57310) when the .NET SDK path contains a space (for example dotnetup).
