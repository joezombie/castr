# YouTube Sync Filters — Design

**Date:** 2026-06-18
**Status:** Approved

## Goal

Let each YouTube-monitored feed filter which playlist videos get downloaded, by:

1. **Date** — only download videos uploaded on/after a configured cutoff.
2. **Include keywords** — if set, a video's title must match at least one term.
3. **Exclude keywords** — if a title matches any term, the video is never downloaded (exclude wins over include).

Filters affect **future sync decisions only**. Already-downloaded episodes are never retroactively removed.

## Matching semantics

- Keywords are **comma-separated**, **case-insensitive**, matched on **whole words** (regex `\b<term>\b`). E.g. `ad` matches the standalone word `ad`, not `address` or `broadcast`.
- Include logic: if `YouTubeIncludeKeywords` is empty/null, all titles pass the include check. If set, the title must contain at least one include term as a whole word.
- Exclude logic: if the title contains any exclude term as a whole word, the video fails — regardless of include matches.
- Date filter: `UploadDate >= YouTubeDownloadAfterDate` (inclusive). Null cutoff = no date filter. If a video's upload date is unknown after fetch, treat it as **passing** the date filter (don't skip on missing data).

## 1. Feed config fields

Add to `Castr/Data/Entities/Feed.cs`, all optional:

```csharp
public DateTime? YouTubeDownloadAfterDate { get; set; }

[MaxLength(1000)]
public string? YouTubeIncludeKeywords { get; set; }

[MaxLength(1000)]
public string? YouTubeExcludeKeywords { get; set; }
```

One EF Core migration adds the three columns to the `feeds` table (follow the existing migration pattern in `Castr/Data/Migrations/`). Run `dotnet ef migrations add AddYouTubeSyncFilters` and verify generated Up/Down + snapshot.

## 2. Filter evaluator helper

New pure, unit-testable class (e.g. `Castr/Services/YouTubeFilterEvaluator.cs`):

- `bool PassesTitleFilters(string title)` — applies include + exclude keyword rules.
- `bool PassesDateFilter(DateTime? uploadDate)` — applies the cutoff (inclusive; null upload date passes).
- Constructed from the feed's three filter fields (parse comma-separated keyword strings into normalized term lists once).
- Whole-word matching via precompiled, case-insensitive regex with `Regex.Escape` on each term.

## 3. Two-stage filtering in PlaylistWatcherService

`ProcessFeedAsync` in `Castr/Services/PlaylistWatcherService.cs`:

- **Stage A — title keyword filters (at selection, zero API cost).** Title is already available for every playlist video. Integrate into / wrap `SelectNewVideosToDownload`: a video failing the title filters is recorded as skipped (reason = `keyword`) and excluded from the candidate list. This avoids any per-video fetch for keyword-rejected videos.
- **Stage B — date filter (in the download loop).** Upload date is only available after `GetVideoDetailsAsync`. After that fetch and before `DownloadAudioAsync`, apply `PassesDateFilter`. Failing videos are recorded as skipped (reason = `date`) instead of downloaded.

## 4. Skip memory — new `SkippedVideo` table

New entity `Castr/Data/Entities/SkippedVideo.cs` (+ migration, + repository access):

```
SkippedVideo {
  int Id
  int FeedId          // FK to Feed
  string VideoId
  string SkipReason    // "keyword" | "date"
  string FilterHash    // hash of the three filter fields at skip time
  DateTime SkippedAt
}
```

Behavior:

- **Selection excludes both downloaded and skipped video IDs.** Extend the "already handled" set used by `SelectNewVideosToDownload` to include skipped IDs for the feed.
- **Filter-change invalidation.** At the start of each poll for a feed, compute the current `FilterHash` from its three filter fields. Delete all `SkippedVideo` rows for that feed whose `FilterHash` differs. This re-admits previously-skipped videos for re-evaluation whenever any filter changes — title-skips re-check for free, date-skips re-fetch once. No per-field re-evaluation logic needed.
- `FilterHash` is a stable hash (e.g. SHA-256 hex) of a canonical string combining the normalized cutoff date + include + exclude values.

## 5. UI

In `Castr/Components/Pages/Feeds/FeedEdit.razor`, inside the existing `@if (_feed.YouTubeEnabled)` block:

- `MudDatePicker` bound to `_feed.YouTubeDownloadAfterDate`, label "Download videos uploaded after", helper text noting it requires a per-video date fetch.
- `MudTextField` bound to `_feed.YouTubeIncludeKeywords`, label "Include keywords", helper "Comma-separated. Title must match at least one (whole word, case-insensitive). Leave blank to allow all."
- `MudTextField` bound to `_feed.YouTubeExcludeKeywords`, label "Exclude keywords", helper "Comma-separated. Title matching any of these is skipped. Takes priority over include."

Saved through the existing `SaveFeed()` path — no new persistence logic.

## 6. Testing

`YouTubeFilterEvaluator` unit tests:
- Whole-word boundaries (`ad` does not match `address`/`broadcast`).
- Case-insensitivity.
- Exclude wins over include.
- Empty/null include = all pass; empty/null exclude = none excluded.
- Date boundary is inclusive; null cutoff passes all; null upload date passes.

Selection/skip-memory tests:
- Skipped IDs are excluded from selection.
- Changing the filter hash deletes stale skip rows so a previously-skipped video is re-admitted.

## Non-goals

- No retroactive filtering/pruning of already-downloaded episodes.
- No title+description matching (title-only by decision — keeps keyword filtering free of per-video fetches).
- No date-range (before+after) filter — single "after" cutoff only.
