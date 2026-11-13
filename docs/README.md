# Speaker Runbook & Talking Points

**Session:** *Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI*  
**Duration:** 45 minutes  

We start with a functional, zero-styled wireframe .NET MAUI 11 app that already has its on-device AI stack wired up in [`src/ConferenceApp/AI/`](../src/ConferenceApp/AI/). Every feature—embeddings, semantic search, background summarization, tool calling, semantic routing, and multi-agent workflows—runs **100% locally on device and offline** with zero cloud calls.

---

## Runbook Sections

| Part | Duration | Guide |
|---|---|---|
| **Part 1 — What's New in .NET 11, C# 15 & .NET MAUI 11** | 5–8 mins | [`01-whats-new-net11-maui11.md`](01-whats-new-net11-maui11.md) |
| **Part 2 — Agents Building Apps (Prompts `01` & `02`)** | 12–15 mins | [`02-agents-building-apps.md`](02-agents-building-apps.md) |
| **Part 3 — Giving the App That Same AI (`src/ConferenceApp/AI/`)** | 20 mins | [`03-giving-the-app-ai.md`](03-giving-the-app-ai.md) |

---

## Pre-Flight Checklist (5 mins before going on stage)

Boot the iOS Simulator beforehand so Prompt `01` doesn't wait for a cold boot:

```sh
xcrun simctl boot "iPhone 17 Pro" || true
open -a Simulator
```

Verify the DevFlow broker is running:

```sh
maui devflow list
```

Keep a terminal tab ready for live AI logs during Part 3:

```sh
maui devflow logs --follow --no-json
```
