# Speaker Runbook & Talking Points

**Session:** *Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI*  
**Duration:** approximately 45 minutes

The AI layers are merged: walk through the **styled .NET MAUI 11 conference app** directly in the editor, with packaged semantic search, native on-device AI, conference tools, and multi-agent workflows.

The [ordered PR stack](../README.md#demo-branches) remains useful as a historical walkthrough.

## Runbook sections

| Part | Duration | Guide |
|---|---|---|
| **1 — What's New in .NET 11, C# 15 & MAUI 11** | 5–8 mins | [Framework and baseline code tour](01-whats-new-net11-maui11.md) |
| **2 — Agents Building Apps** | 12–15 mins | [Preparation, exploration, and optional bug hunt](02-agents-building-apps.md) |
| **3 — Giving the App That Same AI** | ~20 mins | [Native AI-stack walkthrough](03-giving-the-app-ai.md) |

## Pre-flight

1. Check the [toolchain and single-target run instructions](../README.md#check-tooling-then-run-one-target); verify the selected SDK, workloads, Xcode / Android tooling, and target device are compatible. Don't install or change shared tooling during a demo check.
2. Reuse an already-booted simulator. On macOS, inspect devices with `xcrun simctl list devices booted`; boot a suitable installed simulator only if necessary.
3. Run [`00-prepare-demo.prompt.md`](../.github/prompts/00-prepare-demo.prompt.md) before the live portion. It uses .NET 11 `dotnet run` for iOS only, opens available device/Inspector canvases, and **clears app preferences and live Agenda search/filter state**.
4. Confirm the app with `maui devflow agent status`, including platform and device UDID, and ensure the installed build comes from the intended workspace/branch. Use its discovered agent port and Inspector URL from `maui devflow list`, not a hardcoded agent ID. Release the Inspector's writer lease before CLI UI work; see [preview leases](02-agents-building-apps.md#devflow-preview-leases).
5. For UI changes, inspect the requested platform(s) against [`DESIGN.md`](../DESIGN.md). A separate cross-platform rehearsal should cover phone/wide desktop and light/dark themes; an explicitly iOS-only demo does not require desktop or Android builds.
6. For the optional bug hunt, prepare `mattleibow-demo-bugs-for-devflow-bug-hunt` rebased onto current `main` in isolation before the talk, then launch that workspace. Follow [the bug-hunt preflight](02-agents-building-apps.md#step-02-optional-bug-hunt), not the functional `main` exploration.
7. For Part 3, use a session on the intended AI branch, verify local-model availability and vector indexing, and keep logs visible.

```sh
maui devflow list
maui devflow logs --follow --no-json
```

The live prompts explicitly leave the app running for the presenter. At the end of the demo, quit apps you launched and shut down only simulators you booted; leave shared devices and processes alone.
