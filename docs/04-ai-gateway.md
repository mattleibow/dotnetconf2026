# AI gateway with Aspire

The app answers from a server first and falls back to its on-device model when the server can't help.

```
app ─► OrderedFailoverChatClient ─┬─► LiteLLM gateway ─┬─► Azure OpenAI (gpt-4.1-mini)
                                  │                    └─► Ollama (qwen2.5:3b)   ← if Azure fails
                                  └─► on-device AI (Apple Intelligence / Gemini Nano / Windows AI)
```

Everything is orchestrated by the AppHost in [`src/ConferenceApp.AppHost`](../src/ConferenceApp.AppHost/AppHost.cs).

## What Aspire runs

| Resource | What it is |
|---|---|
| `ollama` + `ollama-chat` | Ollama container with `qwen2.5:3b`; models persist in a volume. On a Mac the container runs on CPU. |
| `ollama-openwebui` | Open WebUI, to chat with the local model directly. |
| `foundry` + `azure-chat` | An Azure AI Foundry account and `gpt-4.1-mini` deployment, provisioned by Aspire on first run (as in eShop). API keys are enabled so the gateway container can call it. |
| `postgres` / `litellm` | LiteLLM's database: virtual keys, guardrails, spend logs. |
| `ai-gateway` | The [LiteLLM](https://docs.litellm.ai) proxy and Admin UI (`/ui`). |
| `gateway-tunnel` | A dev tunnel so simulators and emulators can reach the gateway over HTTPS. |
| `conference-app-*` | The MAUI app on Mac Catalyst, the iOS Simulator and the Android emulator (run mode only; start them from the dashboard). |

## Gateway configuration

[`litellm-config.yaml`](../src/ConferenceApp.AppHost/litellm-config.yaml) defines two endpoints. The app asks for `azure`; each endpoint decides which model it runs, and LiteLLM falls back to `ollama` if Azure fails. The config also has one demo guardrail that blocks "Mary Poppins".

Virtual keys, budgets, rate limits and any further guardrails are managed in the Admin UI and stored in Postgres. Items defined in the config file are read-only in the UI.

## First run

1. Set the Azure context for provisioning in the AppHost's user secrets (`Azure:SubscriptionId`, `Azure:Location`, `Azure:TenantId`, `Azure:CredentialSource=AzureCli`), then run `aspire start` from the repository root.
2. After `foundry` is provisioned, copy its key into the `azure-openai-key` parameter (dashboard or `aspire secret set Parameters:azure-openai-key …`) and restart `ai-gateway`.
3. Create the app's virtual key once, with the value of the `litellm-app-key` parameter (it persists in Postgres):

   ```sh
   curl -X POST <ai-gateway-url>/key/generate \
     -H "Authorization: Bearer <litellm-master-key>" -H "Content-Type: application/json" \
     -d '{"key": "<litellm-app-key>", "key_alias": "conference-app", "models": ["azure", "ollama"]}'
   ```

4. Start `conference-app-maccatalyst`, `conference-app-ios-simulator` or `conference-app-android-emulator` from the dashboard. The iOS SDK picks the simulator, so boot the one you want first (with none booted it may pick an iPad).

The `litellm-master-key`, `litellm-app-key` and `postgres-password` parameters have demo defaults; override them in the dashboard or with `aspire secret set`. The Admin UI is at `ai-gateway` → `/ui` (user `admin`, password = `litellm-master-key`).

## The app

[`ChatExtensions.cs`](../src/ConferenceApp/AI/ChatClients/ChatExtensions.cs) registers a keyed `remote` client (`CreateRemoteChatClient`: OpenAI protocol → `https+http://ai-gateway/v1`), a keyed `local` on-device client, and a default `IChatClient` that is `OrderedFailoverChatClient(remote, local)`:

- Every request tries the gateway first; nothing is pinned after a failure.
- Any error before output is streamed — unreachable server, timeout (15 s, no client retries; the gateway does its own), quota, or a guardrail block — falls back to on-device AI. If that fails too, the reply says the assistant can't answer right now and the error is logged. A blocked topic stays blocked at the gateway for the rest of that chat because the history is resent; use **New chat**.
- Background catalogue enrichment always uses the on-device client.
- Each reply's author shows the agent and the endpoint that answered, e.g. `conference-assistant (azure)`, or the on-device model after a fallback.

[`MauiProgram.cs`](../src/ConferenceApp/MauiProgram.cs) adds environment variables to configuration and enables Aspire service discovery for every `HttpClient`, so `ai-gateway` resolves to the address Aspire supplied (directly on Mac, via the dev tunnel on devices).

## Notes

- Foundry Local isn't used: Microsoft documents it as a single-user on-device runtime, not a server. Ollama stands in for an internal model server; in production that would be vLLM or Foundry Local on Azure Local behind the same gateway.
- The tunnel uses anonymous access, so the gateway is reachable from the internet while it runs; the LiteLLM virtual key is the only protection.
- Aspire launches the iOS app with `dotnet build -t:Run`. [`Directory.Build.targets`](../Directory.Build.targets) works around [dotnet/sdk#57310](https://github.com/dotnet/sdk/issues/57310) when the .NET SDK path contains a space (for example dotnetup).
