# Log View Viewport Height Fix — Implementation Plan

Design: `2026-06-22-log-view-viewport-height-design.md`

## Task 1 — Root height in app.css

**File:** `Castr/wwwroot/css/app.css`
Add a definite root height to the existing `html, body` rule (around line 37):

```css
html, body {
    /* ...existing declarations... */
    height: 100%;
}
```

Do not remove existing declarations (font-family, smoothing, background).

## Task 2 — MudMainContent as the scroll container

**File:** `Castr/Components/Layout/MainLayout.razor` (line 33)
Change:

```razor
<MudMainContent Style="background:#15140f;min-height:100vh;">
```

to:

```razor
<MudMainContent Style="background:#15140f;height:100dvh;overflow-y:auto;">
```

## Task 3 — Confirm Logs.razor needs no change

**File:** `Castr/Components/Pages/Logs.razor`
Verify the root container still uses `height:100%;display:flex;flex-direction:column;min-height:0;`
and `#castr-log-scroll` still uses `flex:1;min-height:0;overflow-y:auto;`. No edit
expected.

## Task 4 — Build and verify

1. `cd Castr && ~/.dotnet/dotnet build` — must succeed, 0 errors.
2. Manual/visual check per the design's Verification section:
   - Logs page: only the log list scrolls; header/filters/search pinned;
     nothing past the bottom.
   - Dashboard + an episode list: long content scrolls with a single scrollbar.
   - Mobile width: drawer/appbar behave; Logs list fits viewport.

## Notes

- 3-line change; no tests added (pure CSS/layout).
- Do not touch log buffering, SignalR, or filtering logic.
