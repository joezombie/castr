# Log View Viewport Height Fix — Design

**Date:** 2026-06-22
**Status:** Approved

## Problem

The Logs page (`/ui/logs`, `Castr/Components/Pages/Logs.razor`) is built as a
full-height flex column: a fixed header/filter/search region at the top and a
scrollable log list (`#castr-log-scroll`) that should fill the remaining space.
In practice the log box extends below the bottom of the window and the whole
page scrolls instead of just the log list.

### Root cause

The page container uses `height:100%`, which only resolves when every ancestor
has a definite height. The chain is broken:

- `html, body` (`wwwroot/css/app.css:37`) set **no height**.
- `MudMainContent` (`Components/Layout/MainLayout.razor:33`) uses
  `min-height:100vh`, not a definite `height`.

So `height:100%` collapses to content height, the inner
`flex:1; min-height:0; overflow-y:auto` log box can't compute a bounded height,
and it grows to fit all log lines — pushing past the viewport bottom. The
`min-height:100vh` plus the 62px appbar padding also makes the content region
taller than the viewport even when empty.

## Chosen approach — global height chain

Establish a definite height at the root and make the main content region the
single scroll container for the whole app. This fixes the Logs page and leaves
every other page's scroll behavior visually unchanged.

### Changes

**1. `wwwroot/css/app.css`** — definite root height:

```css
html, body { height: 100%; }
```

(`body` already has `margin:0` via MudBlazor's reset.)

**2. `Components/Layout/MainLayout.razor:33`** — make `MudMainContent` a
fixed-height scroll container:

```razor
<MudMainContent Style="background:#15140f;height:100dvh;overflow-y:auto;">
```

- `height:100dvh` (was `min-height:100vh`) gives a definite height; `dvh`
  accounts for mobile browser chrome.
- MudMainContent already pads its top by the 62px appbar height and MudBlazor
  uses `box-sizing:border-box`, so the content box is exactly `100dvh − 62px`
  — the visible area below the appbar.
- `overflow-y:auto` makes this element the single scrollbar. Pages taller than
  the viewport (Dashboard, feed/episode lists) scroll here — visually identical
  to today's single window scrollbar, with no double scrollbars.

**3. `Components/Pages/Logs.razor`** — no change required. Its `height:100%`
now resolves against MudMainContent's definite height, so the existing
`flex:1; min-height:0; overflow-y:auto` on `#castr-log-scroll` bounds correctly
and only the log list scrolls.

### Why this is safe

Every page's content now lives inside one bounded, scrollable region. Full-height
pages (Logs) get a resolvable `height:100%`; normal flowing pages scroll within
MudMainContent exactly as they scrolled the window before. Fixed/overlay elements
(MudAppBar, MudDrawer, MudPopover, MudDialog) use fixed positioning and are
unaffected.

## Verification

After building and running the app:

1. **Logs page** — only the log list scrolls; header, filter chips, and search
   stay pinned; nothing extends past the bottom of the window. With many log
   lines, the page itself does not scroll.
2. **Dashboard and an episode list** — content longer than the viewport still
   scrolls normally with a single scrollbar (no double scrollbars, no clipped
   content).
3. **Mobile width** — the responsive drawer and appbar still behave; the Logs
   list still fits the viewport.

## Out of scope

- No changes to log buffering, SignalR streaming, filtering logic, or the Logs
  page markup beyond what is described above.
- No broader CSS refactor of the dashboard theme.
