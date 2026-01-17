# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a podcast episode management tool for "Behind the Bastards" that matches YouTube playlist episode titles to downloaded MP3 files using fuzzy string matching, then optionally renames files with playlist order prefixes.

## Commands

```bash
# Run fuzzy matching to link playlist names to MP3 files
python3 match_episodes.py match

# Preview file renames (dry run, default)
python3 match_episodes.py rename --dry-run

# Execute file renames
python3 match_episodes.py rename --execute

# Generate bash script for manual review/execution
python3 match_episodes.py script
```

## Architecture

**match_episodes.py** - Single-file Python script with three modes:
- `match` - Fuzzy matches playlist titles (from `playlist-bulletized1.txt`) to MP3 filenames (from `files1.txt`) using `difflib.SequenceMatcher`. Handles multi-part episodes by extracting/comparing part numbers. Outputs `matched_episodes.json` and `episode_mapping.txt`.
- `rename` - Renames MP3 files to add zero-padded order prefix (e.g., `001_filename.mp3`)
- `script` - Generates `rename_episodes.sh` for review before execution

**Key data files:**
- `files1.txt` - `ls -l` output of MP3 files (paths with escaped spaces)
- `playlist-bulletized1.txt` - YouTube playlist titles in JSON array format
- `matched_episodes.json` - Output mapping with order, names, scores, indices

## Castr (.NET)

ASP.NET Core Web API that serves podcast RSS feeds from local MP3 directories.

### Commands

```bash
cd Castr

# Build
~/.dotnet/dotnet build

# Run (default port 5000)
~/.dotnet/dotnet run
```

### API Endpoints

- `GET /feed` - List all available feeds
- `GET /feed/{feedName}` - Get RSS feed XML
- `GET /feed/{feedName}/media/{fileName}` - Stream media file

### Configuration

Feeds configured in `appsettings.json` under `PodcastFeeds.Feeds`. Each feed has:
- `Title`, `Description`, `Directory` (required)
- `Author`, `ImageUrl`, `Link`, `Language`, `Category`, `FileExtensions` (optional)

### Architecture

- `Models/PodcastFeedConfig.cs` - Configuration models
- `Services/PodcastFeedService.cs` - RSS XML generation, reads ID3 tags via TagLibSharp
- `Controllers/FeedController.cs` - API endpoints with range request support for streaming
