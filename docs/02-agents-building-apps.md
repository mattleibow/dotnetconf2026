# Part 2: Agents Building Apps — Explore & Debug (12–15 mins)

Use a coding agent and MAUI DevFlow to inspect and drive the functional app on `main`. The optional bug hunt uses a different, explicitly prepared baseline.

## Step 00: Prepare before the talk

Run [`00-prepare-demo.prompt.md`](../.github/prompts/00-prepare-demo.prompt.md). It loads the DevFlow debugging skill and uses .NET 11 `dotnet run --framework net11.0-ios --device <UDID> --no-launch-profile` from `src/ConferenceApp` to build/install/launch on the booted simulator. It opens the **Mobile Device** and **DevFlow Inspector** canvases when available and releases the Inspector's writer lease for CLI-driven UI work. If the device canvas is unavailable, use the native Simulator.

**It clears saved preferences and explicitly resets live Agenda search/filter state.** Clearing storage alone does not reset view models; recreating the default `selected_city` is expected. Do this before creating the favourites you want to show. It leaves the app and launch process running on Agenda and does not build other platforms.

## Step 01: Explore the app

Run [`01-explore-app.prompt.md`](../.github/prompts/01-explore-app.prompt.md):

1. Reuse an app known to come from this workspace/branch, set Agenda to All Sessions, and find the **.NET MAUI** talk in the selected city using its actual AutomationId.
2. Add it with `favorite-button` only if not already saved; inspect `maui devflow storage preferences list` for its ID in `favorites`.
3. Open its speaker link, confirm the profile loaded, and take a screenshot.
4. Return to Agenda, clear search, set **Showing: Favourites**, and confirm the saved session is visible. Leave the app running.

| Surface | What to show |
|---|---|
| **Mobile Device canvas** (`mobile-device`), when available | The running simulator, real taps, and navigation; otherwise use the native Simulator |
| **Inspector — UI tree** | `agenda-search`, `favorites-filter`, `session-<id>`, `session-favorite-<id>`, and `favorite-button` |
| **Inspector — Logs** | Actual `ILogger` diagnostics and exceptions, including `[AI …]` pipeline entries |
| **Inspector — Storage** | `selected_city` (city ID) and `favorites` (comma-separated session IDs) |
| **Layout diagnostics / screenshots** | Clipped text, off-screen elements, zero-sized controls, and visual artifacts |

Top-level Shell routes are `//agenda`, `//event`, `//speakers`, and `//assistant`. Detail navigation uses `session?id=<id>` and `speaker?id=<id>`, not route templates. The toolbar `city-picker` selects **Durban**, **Cape Town**, or **Bloemfontein**.

Search first matches case-insensitive substrings in the current city's session titles, abstracts, and speaker names, then appends semantic matches once indexing finishes. Search `.NET MAUI` for this walkthrough; synonym queries such as `football forecasts` belong to the [Part 3](03-giving-the-app-ai.md) semantic-search demo.

### Batch UI steps

Batch related UI steps to share one CLI lease, with readiness queries between navigation and interaction. Discover actual item IDs; a named speaker might not be realized in the current CollectionView viewport. Use another batch when a read determines the next action rather than forcing everything into one blind script, and inspect each command's result for failures. Reset search with `ui clear agenda-search`; the current `ui fill` rejects empty text. For example, on macOS (add `--agent-port <port>` when selecting among agents):

```sh
printf '%s\n' \
  'ui navigate //agenda' \
  'ui query --automationId agenda-search --wait-until exists --timeout 15' \
  'ui fill agenda-search ".NET MAUI"' \
  | maui devflow batch --delay 1000 --human
```

The current batch command supports **`ui` and `webview`**, not `storage` or `logs`. Fetch these separately:

```sh
maui devflow storage preferences list
maui devflow logs --limit 20
```

Correlate logs with the current reproduction's timestamps/category; older launches persist in the log history.

### DevFlow preview leases

These are constraints of the DevFlow preview, not missing .NET 11 features:

- The Inspector claims and renews a writer lease. In the current Inspector, dispatching `window.dispatchEvent(new Event('pagehide'));` through the Browser canvas's `evaluate_javascript` releases it while leaving the page open as an observer. Selecting its **Inspect** radio does not release it; reloading or **Take control** can reacquire it.
- Separate UI CLI processes also claim leases. A completed command can leave its lease until expiry (currently about 10 seconds); the next process can fail immediately rather than wait automatically. Batch related UI work, identify the holder if there is contention, and do not force-take an unrelated session's lease.
- The app's Debug-only `RequireMutationLease=false` disables server enforcement, **not** voluntary client claims. The current storage CLI mutations do not send lease headers, so enabling enforcement would reject preference clears/writes. Keep this compatibility choice until the installed tooling supports those operations with leases; it is not a runtime workaround or an authentication setting.

## Step 02: Optional bug hunt

Use **`mattleibow-demo-bugs-for-devflow-bug-hunt`** ([PR #33](https://github.com/mattleibow/dotnetconf2026/pull/33)) in an isolated session/worktree. **Before the talk**, fetch the current refs and check that `origin/main` is an ancestor of the bug-hunt baseline:

```sh
git fetch origin main mattleibow-demo-bugs-for-devflow-bug-hunt
git merge-base --is-ancestor origin/main HEAD
```

If behind, rebase the bug branch onto `origin/main` in its clean isolated worktree and inspect the resulting seed patch, preserving the intended defects. Do not switch/rebase the active session's branch during the live fix. Rebuild/run that workspace before testing; a running agent's matching bundle ID does not prove which branch produced it.

Then run [`02-bug-hunt.prompt.md`](../.github/prompts/02-bug-hunt.prompt.md). The seed introduces the speaker, favourites, and map regressions; the Entry artifact is also present on `main`:

| Symptom | Evidence requested by the prompt |
|---|---|
| Blank speaker details | Open a realized speaker card or session speaker link and inspect the current logged exception / stack trace |
| Empty favourites filter | Save a favourite, inspect actual stored keys and IDs, then toggle the filter |
| Invisible venue map | Query `venue-map` bounds on Event and run `ui diagnostics --profile agent --root venue-map`; empty findings do not override a measured height of zero |
| Search/chat input artifacts | Capture both inputs and inspect the inner `Entry` styling |

After the fixes, use the same .NET 11 launch command (or an already-configured Hot Reload host) and perform one focused verification pass. Check favourite read-back/filtering, both speaker entry points without a new exception, positive map height/visibility, and both input screenshots. A source diff alone is not proof that the installed build changed. Keep framework/runtime defaults intact and leave the verified app running.

## Design and MAUI talking points

Show themed surfaces, a gradient Event hero, native map, swipeable Agenda tickets, inline favourites, and Assistant starter suggestions. Use [`DESIGN.md`](../DESIGN.md) and [`PRODUCT.md`](../PRODUCT.md) for phone/desktop layout checks.

Next: [**Part 3 — Giving the App That Same AI**](03-giving-the-app-ai.md).
