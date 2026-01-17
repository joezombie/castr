# Behind the Bastards Podcast Manager

A comprehensive toolkit for managing "Behind the Bastards" podcast episodes, featuring intelligent fuzzy matching for episode files and an automated RSS feed generation API.

## Overview

This project consists of two main components:

1. **Python Episode Matcher** (`match_episodes.py`) - Fuzzy matches YouTube playlist titles to downloaded MP3 files using intelligent string matching algorithms
2. **Castr API** - ASP.NET Core Web API that serves podcast RSS feeds from local MP3 directories with YouTube integration

## Quick Start

### Python Episode Matcher

Match YouTube playlist episode titles to your downloaded MP3 files:

```bash
# Run fuzzy matching to link playlist names to MP3 files
python3 match_episodes.py match

# Preview file renames (dry run, default)
python3 match_episodes.py rename --dry-run

# Execute file renames with playlist order prefixes
python3 match_episodes.py rename --execute

# Generate bash script for manual review/execution
python3 match_episodes.py script
```

### Castr - Podcast RSS Feed API

Serve your podcast episodes via RSS with automatic YouTube synchronization:

```bash
cd Castr

# Build the project
~/.dotnet/dotnet build

# Run the API (default port 5000)
~/.dotnet/dotnet run
```

Or using Docker:

```bash
cd Castr
docker-compose up -d
```

## Features

### Episode Matcher Features

- **Fuzzy String Matching** - Intelligently matches episode titles even with variations
- **Multi-Part Episode Support** - Handles "Part One", "Part Two", etc. with automatic extraction
- **Unicode Normalization** - Proper handling of special characters and diacritics
- **Batch Renaming** - Add zero-padded order prefixes to organize episodes
- **Safe Operations** - Dry-run mode to preview changes before execution

### Castr API Features

- **Automatic RSS Generation** - Creates standards-compliant podcast RSS feeds
- **YouTube Integration** - Automatically downloads new episodes from playlists
- **ID3 Tag Reading** - Extracts metadata from MP3 files
- **Range Request Support** - Efficient media streaming with resume capability
- **Multiple Feed Support** - Manage multiple podcast feeds from one instance
- **Database Tracking** - SQLite database for download history

## API Endpoints

Once running, the Castr API provides:

- `GET /feed` - List all available podcast feeds
- `GET /feed/{feedName}` - Get RSS feed XML for a specific podcast
- `GET /feed/{feedName}/media/{fileName}` - Stream media file with range support
- `GET /health` - Health check endpoint (when implemented)

### Example Usage

```bash
# List all feeds
curl http://localhost:5000/feed

# Get RSS feed
curl http://localhost:5000/feed/btb

# Access in podcast app
http://your-domain.com/feed/btb
```

## Configuration

### Castr API Configuration

Edit `Castr/appsettings.json` to configure podcast feeds:

```json
{
  "PodcastFeeds": {
    "Feeds": {
      "btb": {
        "Title": "Behind the Bastards",
        "Description": "Behind the Bastards podcast episodes",
        "Directory": "/Podcasts/Behind the Bastards",
        "Author": "Robert Evans",
        "Language": "en-us",
        "Category": "Society & Culture",
        "ImageUrl": "https://example.com/image.jpg",
        "YouTube": {
          "Enabled": true,
          "PlaylistUrl": "https://www.youtube.com/playlist?list=...",
          "PollIntervalMinutes": 60,
          "MaxConcurrentDownloads": 1
        }
      }
    }
  }
}
```

### Environment Variables

Override configuration using environment variables:

```bash
# Feed directory
export PodcastFeeds__Feeds__btb__Directory="/custom/path"

# YouTube settings
export PodcastFeeds__Feeds__btb__YouTube__Enabled=false
export PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=120

# Logging level
export Logging__LogLevel__Default=Debug
```

### Episode Matcher Configuration

The matcher uses these input files (in the project root):

- `files1.txt` - Output from `ls -l` of your MP3 files
- `playlist-bulletized1.txt` - YouTube playlist titles in JSON array format

Output files:
- `matched_episodes.json` - Matching results with scores
- `episode_mapping.txt` - Human-readable mapping
- `rename_episodes.sh` - Generated rename script

## Requirements

### Python Episode Matcher

- Python 3.8 or higher
- No external dependencies (uses standard library only)

### Castr API

- .NET 10.0 SDK or higher
- Docker (for containerized deployment, optional)
- SQLite (embedded, no separate installation needed)

### System Requirements

- Linux, macOS, or Windows
- Sufficient disk space for podcast episodes
- Network access for YouTube integration (if enabled)

## Documentation

Comprehensive documentation is available:

- **[CLAUDE.md](CLAUDE.md)** - AI assistant guidance and project architecture overview
- **[Castr/BUILD.md](Castr/BUILD.md)** - Build, deployment, and CI/CD instructions
- **[Castr/TRAEFIK.md](Castr/TRAEFIK.md)** - Reverse proxy setup with Traefik
- **[CODE_REVIEW.md](CODE_REVIEW.md)** - Comprehensive code review and security analysis
- **[RECOMMENDATIONS.md](RECOMMENDATIONS.md)** - Implementation recommendations and improvement roadmap

## Architecture

### Python Episode Matcher

Single-file Python script (`match_episodes.py`) with three operation modes:

- **match** - Fuzzy matches playlist titles to MP3 filenames using `difflib.SequenceMatcher`
- **rename** - Renames MP3 files with zero-padded order prefix (e.g., `001_filename.mp3`)
- **script** - Generates `rename_episodes.sh` bash script for manual review

Handles multi-part episodes by extracting and comparing part numbers separately from titles.

### Castr API Architecture

ASP.NET Core Web API with modular service architecture:

- **Controllers/FeedController.cs** - API endpoints with range request support
- **Services/PodcastFeedService.cs** - RSS XML generation, ID3 tag reading via TagLibSharp
- **Services/PodcastDatabaseService.cs** - SQLite database operations
- **Services/YouTubeDownloadService.cs** - YouTube audio downloads
- **Services/PlaylistWatcherService.cs** - Background service for playlist monitoring
- **Models/PodcastFeedConfig.cs** - Configuration models

## Deployment

### Development

```bash
# Python matcher
python3 match_episodes.py match

# Castr API
cd Castr
~/.dotnet/dotnet run
```

### Production with Docker

```bash
cd Castr
./build-and-push.sh
docker-compose up -d
```

### Behind Reverse Proxy (Traefik)

See [Castr/TRAEFIK.md](Castr/TRAEFIK.md) for detailed Traefik configuration with automatic HTTPS.

## Security

The codebase implements several security best practices:

- ✅ **SQL Injection Protection** - All queries use parameterized statements
- ✅ **Path Traversal Protection** - Security checks prevent directory traversal
- ✅ **Input Validation** - Validates user input at API boundaries
- ✅ **No Secrets in Code** - Configuration-based sensitive data management
- ✅ **Forward Header Security** - Properly configured for reverse proxy deployment

See [CODE_REVIEW.md](CODE_REVIEW.md) for complete security analysis.

## Contributing

When contributing to this project:

1. Follow the existing code style and conventions
2. Update documentation for any changed functionality
3. Review [RECOMMENDATIONS.md](RECOMMENDATIONS.md) for improvement opportunities
4. Ensure changes pass existing builds and tests

## License

This project's license is to be determined. Please contact the project maintainer for licensing information.

## Support

For questions, issues, or feature requests:

1. Check the documentation files listed above
2. Review [CODE_REVIEW.md](CODE_REVIEW.md) for detailed technical explanations
3. Open an issue on the GitHub repository

## Acknowledgments

- Built for the "Behind the Bastards" podcast by Robert Evans
- Uses TagLibSharp for ID3 tag reading
- Uses YoutubeExplode for YouTube integration
- Fuzzy matching powered by Python's difflib

---

**Project Status**: ✅ Production-ready with recommended improvements available in [RECOMMENDATIONS.md](RECOMMENDATIONS.md)
