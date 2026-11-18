# Speaker Runbook & Talking Points

**Session:** *Agents Building Agentic Apps: AI Goes Mobile with .NET MAUI*  
**Duration:** approximately 45 minutes

Start on `main`: a **styled, functional .NET MAUI 11 conference app** with offline content, literal agenda search, manual favourites, speaker profiles, and an Assistant availability stub. It has no on-device model integration.

This guide covers the whole talk, including the separate native AI stack. Follow [the README's ordered PR stack](../README.md#demo-branches) for the AI portion; Part 3 links to that branch's source. Local inference needs a supported device and an available model. Offline content does not make map tiles, external links, or initial model provisioning offline.

## Runbook sections

| Part | Duration | Guide |
|---|---|---|
| **1 — What's New in .NET 11, C# 15 & MAUI 11** | 5–8 mins | [Framework and baseline code tour](01-whats-new-net11-maui11.md) |
| **2 — Agents Building Apps** | 12–15 mins | [Preparation, exploration, and optional bug hunt](02-agents-building-apps.md) |
| **3 — Giving the App That Same AI** | ~20 mins | [Native AI-stack walkthrough](03-giving-the-app-ai.md) |

## Pre-flight

1. Check the [toolchain and single-target build instructions](../README.md#check-tooling-then-build-one-target); verify the selected SDK, workloads, Xcode / Android tooling, and target device are compatible. Don't install or change shared tooling during a demo check.
2. Reuse an already-booted simulator. On macOS, inspect devices with `xcrun simctl list devices booted`; boot a suitable installed simulator only if necessary.
3. Run [`00-prepare-demo.prompt.md`](../.github/prompts/00-prepare-demo.prompt.md) before the live portion. It builds/launches only iOS, opens the Mobile Device and DevFlow Inspector canvases, and **clears app preferences**, including favourites.
4. Confirm the app is visible in `maui devflow list`. The Inspector URL comes from that output; don't hardcode an agent ID.
5. Verify the phone layout and a wide desktop layout, in light and dark themes. Use the existing design tokens in [`DESIGN.md`](../DESIGN.md).
6. For Part 3, switch to the intended AI layer, verify local-model availability and vector indexing, and keep logs visible. Windows tool calling is experimental; retain the manual Agenda / Speakers fallback.

```sh
maui devflow list
maui devflow logs --follow --no-json
```

The live prompts explicitly leave the app running for the presenter. At the end of the demo, quit apps you launched and shut down only simulators you booted; leave shared devices and processes alone.
