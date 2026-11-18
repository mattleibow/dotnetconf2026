# Part 2: Agents Building Apps — Explore & Debug (12–15 mins)

Use a coding agent and MAUI DevFlow to inspect and drive the **already-styled app on `main`**. This is not a wireframe-to-design exercise, and it does not require the in-app AI stack.

## Step 00: Prepare before the talk

Run [`00-prepare-demo.prompt.md`](../.github/prompts/00-prepare-demo.prompt.md). It loads the DevFlow debugging skill, targets the booted iOS simulator, builds/installs/launches the app, opens the **Mobile Device** and **DevFlow Inspector** canvases, and releases the Inspector's writer lease so CLI mutations can proceed.

**It clears saved preferences.** Do this before creating the favourites you want to show. It leaves the app running on Agenda and does not build other platforms.

## Step 01: Explore the app

Run [`01-explore-app.prompt.md`](../.github/prompts/01-explore-app.prompt.md):

1. Reuse the running app, navigate to Agenda, find the **.NET MAUI** talk, and open its details.
2. Add it to Favourites with `favorite-button`; inspect `maui devflow storage preferences list`.
3. Open its speaker profile and take a screenshot.
4. Return to Agenda, enable **Showing: Favourites**, and leave the app running.

| Surface | What to show |
|---|---|
| **Mobile Device canvas** (`mobile-device`) | The running simulator, real taps, and navigation |
| **Inspector — UI tree** | `agenda-search`, `favorites-filter`, `session-<id>`, `session-favorite-<id>`, and `favorite-button` |
| **Inspector — Logs** | Actual `ILogger` diagnostics and exceptions; AI pipeline logs exist only on the AI stack |
| **Inspector — Storage** | `selected_city` (city ID) and `favorites` (comma-separated session IDs) |
| **Layout diagnostics / screenshots** | Clipped text, off-screen elements, zero-sized controls, and visual artifacts |

Top-level Shell routes are `//agenda`, `//event`, `//speakers`, and `//assistant`. Detail navigation uses `session?id=<id>` and `speaker?id=<id>`, not route templates. The toolbar `city-picker` selects **Durban**, **Cape Town**, or **Bloemfontein**.

Search on `main` matches case-insensitive substrings in the current city's session titles, abstracts, and speaker names. Search `.NET MAUI` for this walkthrough; synonym queries belong to the semantic-search stack demo.

### Batch UI steps

The prompts ask the agent to pipe mutating steps into one `maui devflow batch` call to share a CLI lease. For example, on macOS:

```sh
printf 'ui navigate //agenda\nui fill agenda-search ".NET MAUI"\n' \
  | maui devflow batch --delay 1500 --human
```

Read-only diagnostics can be checked separately:

```sh
maui devflow storage preferences list
maui devflow logs --limit 20
maui devflow ui diagnostics --profile agent
```

## Step 02: Optional bug hunt

Use [PR #33's bug branch](https://github.com/mattleibow/dotnetconf2026/pull/33), then run [`02-bug-hunt.prompt.md`](../.github/prompts/02-bug-hunt.prompt.md). The four reported issues are **branch-specific**, not expected behavior on `main`:

| Symptom | Evidence requested by the prompt |
|---|---|
| Blank speaker details | Open a speaker and inspect the logged exception / stack trace |
| Empty favourites filter | Save a favourite, inspect actual stored keys and IDs, then toggle the filter |
| Invisible venue map | Inspect `venue-map` bounds and zero-height diagnostics on Event |
| Search/chat input artifacts | Capture a screenshot and inspect the inner `Entry` styling |

The correct preference keys on `main` are **`selected_city` and `favorites`**. Don't assume an old `favorite_sessions` / `favorite-sessions` story matches the checked-out bug branch; compare its actual reads and writes.

The prompt requests one diagnosis pass, one combined fix, and one iOS rebuild/redeploy, then stops without a post-fix click-through or extra platform builds. Its `--no-restore` rebuild assumes Step 00 has already restored that checkout; don't use it for the first build of a fresh worktree. If fixes are already present and the fixed app is running, the prompt asks for diff inspection rather than repeated reproduction.

## Design and MAUI talking points

These are already part of the baseline, not a missing “make it beautiful” prompt: themed surfaces, a gradient Event hero, native map, swipeable Agenda tickets, inline favourites, and Assistant starter suggestions. Use [`DESIGN.md`](../DESIGN.md) and [`PRODUCT.md`](../PRODUCT.md) for phone/desktop layout checks. Chat remains unavailable on `main`.

Next: [**Part 3 — Giving the App That Same AI**](03-giving-the-app-ai.md) (switch to the native AI stack).
