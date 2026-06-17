# Clear & Resync Feed Episode Data — Design

**Date:** 2026-06-17
**Status:** Approved, pending implementation plan

## Problem

There is no way to reset a feed's episode data and rebuild it from its YouTube
playlist. When the database state drifts from reality (stale metadata, bad
matches, orphaned `DownloadedVideo` tracking), the only recourse today is
deleting the entire feed (which cascades away config) or hand-editing rows.

## Goal

A per-feed action that clears the feed's episode metadata and download tracking
from the database — **without deleting MP3 files on disk** — then immediately
re-syncs from the YouTube playlist. Existing files are re-matched back into the
database; only genuinely-missing videos are re-downloaded.

## Scope decisions

- **Clear scope: DB metadata only (non-destructive).** Delete `Episode` rows and
  `DownloadedVideo` tracking rows for the feed. MP3 files on disk are preserved.
  Files on disk and other feeds are untouched.
- **Resync trigger: immediate.** Reuse the existing `IPlaylistWatcherTrigger`
  manual-refresh mechanism so the feed is processed within seconds rather than
  waiting up to `YouTubePollIntervalMinutes`.

## Why this is safe and fast

`PodcastDataService.SyncPlaylistInfoAsync` (Castr/Services/PodcastDataService.cs:203)
runs over **all** playlist videos in a single uncapped pass: it fuzzy-matches each
video title to files on disk, re-creates the `Episode` row, and marks the video
downloaded via `MarkVideoDownloadedAsync`. The `MaxDownloadsPerPoll = 5` cap
(Castr/Services/PlaylistWatcherService.cs:216) applies **only** to the actual
download step (`SelectNewVideosToDownload`).

Therefore, after a non-destructive clear, the first immediate poll re-matches the
entire existing library back into the database in one pass. Only videos with no
matching file on disk enter the capped download loop and drip in at ≤5/poll.
There is no slow per-file re-matching problem.

## Components

### 1. Data service method

`PodcastDataService.ClearFeedEpisodeDataAsync(int feedId)`:

- Bulk-delete all `Episode` rows where `FeedId == feedId`.
- Bulk-delete all `DownloadedVideo` rows where `FeedId == feedId`.
- Write an `ActivityLog` entry (`ActivityType = "clear_resync"`) recording counts
  cleared.
- Return counts (episodes cleared, tracking rows cleared) for UI feedback.
- Wrap both deletes (and the activity log) in a single transaction so a partial
  failure cannot leave tracking cleared but episodes intact, or vice versa.

### 2. Repository methods (new)

Only single-row `DeleteAsync` exists today, so add bulk deletes that run as one
DB operation rather than a row-by-row loop:

- `EpisodeRepository.DeleteByFeedIdAsync(int feedId)`
- `DownloadRepository.DeleteDownloadedVideosByFeedIdAsync(int feedId)`

Plus matching interface declarations on `IEpisodeRepository` and
`IDownloadRepository`.

### 3. UI action

Button on `FeedDetails.razor`, in the feed actions area alongside Edit /
Force-Resync.

- **Visibility:** only shown when the feed has YouTube enabled and a playlist URL
  set. Clearing a feed with no playlist to resync from would just empty it until
  the next directory scan, which is not the intent.
- **Label:** "Clear & Resync from Playlist".
- **Confirmation dialog** stating exactly what happens:

  > This deletes all N episode records and download history for this feed from the
  > database, then re-syncs from the YouTube playlist. **Your MP3 files on disk are
  > not deleted.** Existing files will be re-matched; only missing videos will be
  > re-downloaded.

- **On confirm:** call `ClearFeedEpisodeDataAsync`, fire `IPlaylistWatcherTrigger`
  for the feed, show a snackbar with the cleared counts, then refresh the page.

### 4. Trigger

Reuse the existing `IPlaylistWatcherTrigger` manual-refresh mechanism (already
wired for the poller's early wake-up). No new background plumbing.

## Data flow

```
FeedDetails button
  → confirm dialog
  → DataService.ClearFeedEpisodeDataAsync(feedId)        [transactional]
       → EpisodeRepository.DeleteByFeedIdAsync(feedId)
       → DownloadRepository.DeleteDownloadedVideosByFeedIdAsync(feedId)
       → ActivityRepository.Add("clear_resync", counts)
  → PlaylistWatcherTrigger.TriggerFeedProcessing(feed)
  → (background) ProcessFeedAsync runs within seconds:
       → SyncPlaylistInfoAsync re-matches all files
            → re-creates Episodes + marks downloaded (uncapped)
       → SelectNewVideosToDownload finds only truly-missing videos
            → downloads at ≤5/poll
```

## Error handling

- Both deletes plus the activity-log write run in a single transaction; on failure
  the transaction rolls back and the UI surfaces an error.
- Guard against unknown `feedId` (button hidden / action returns gracefully).
- Button hidden unless `YouTubeEnabled` and a playlist URL are set.

## Testing

- Unit-test `EpisodeRepository.DeleteByFeedIdAsync` and
  `DownloadRepository.DeleteDownloadedVideosByFeedIdAsync`: target feed's rows are
  gone, other feeds' rows are untouched.
- Unit-test `ClearFeedEpisodeDataAsync`: both tables cleared, activity logged,
  transactional rollback on a simulated mid-operation failure.
- The resync path itself is already covered by existing `SyncPlaylistInfoAsync` /
  `SelectNewVideosToDownload` tests.

## Out of scope

- Deleting MP3 files from disk.
- Bulk clear-and-resync across multiple feeds at once.
- Changing the `MaxDownloadsPerPoll` throttle.
