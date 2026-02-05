# GitHub Copilot Instructions

## Project Overview

This repository contains **Castr**, a podcast RSS feed API with YouTube playlist integration, and a Python fuzzy matching tool for episode management.

**Primary Components:**
1. **Castr** (.NET 10 API) - Podcast RSS feed server with automatic YouTube downloads
2. **match_episodes.py** - Fuzzy matching script for linking playlist titles to MP3 files

**Model Preference:** Use GPT-5.2 Codex for code generation and suggestions.

---

## Architecture

### Castr (.NET API)

**Technology Stack:**
- ASP.NET Core 10.0
- SQLite with ADO.NET (Microsoft.Data.Sqlite)
- YouTubeExplode for YouTube integration
- TagLibSharp for ID3 tag reading
- Docker containerization

**Key Services:**
- `PodcastFeedService` - RSS XML generation, episode ordering
- `PodcastDatabaseService` - SQLite database operations, fuzzy matching
- `YouTubeDownloadService` - YouTube playlist fetching and audio downloads
- `PlaylistWatcherService` - Background service for polling playlists

**Database:**
- SQLite with schema migrations
- Episodes table with fuzzy matching scores
- Downloaded videos tracking
- Playlist index preservation

### Python Script

**Functionality:**
- Fuzzy matches YouTube playlist titles to MP3 filenames
- Handles multi-part episodes with part number extraction
- Uses Longest Common Subsequence (LCS) algorithm
- Generates episode order files for the API

---

## Coding Standards

### C# (.NET)

**Style Guide:**
- Use modern C# features (records, pattern matching, top-level statements)
- Async/await for all I/O operations
- Dependency injection for all services
- Comprehensive logging at Debug level for operations
- Use `required` keyword for required properties
- Nullable reference types enabled

**Patterns to Follow:**
```csharp
// ✅ Good: Async with proper cancellation
public async Task<List<Video>> GetVideosAsync(CancellationToken ct = default)
{
    _logger.LogDebug("Fetching videos...");
    var videos = await _service.FetchAsync(ct);
    _logger.LogInformation("Fetched {Count} videos", videos.Count);
    return videos;
}

// ✅ Good: Parameterized queries
command.CommandText = "SELECT * FROM episodes WHERE video_id = @videoId";
command.Parameters.AddWithValue("@videoId", videoId);

// ✅ Good: Proper resource disposal
await using var connection = new SqliteConnection(connectionString);
using var command = connection.CreateCommand();

// ❌ Bad: Sync I/O
var data = File.ReadAllText(path); // Should be ReadAllTextAsync

// ❌ Bad: String concatenation in SQL
command.CommandText = $"SELECT * FROM episodes WHERE id = {id}"; // SQL injection risk
```

**Security Requirements:**
- ALWAYS use parameterized SQL queries
- ALWAYS validate user input at controller boundaries
- ALWAYS check for path traversal (`..,` `/`, `\`)
- NEVER log sensitive information (paths should be filenames only)

### Python

**Style Guide:**
- Follow PEP 8
- Type hints for function signatures
- Docstrings for all functions
- Use `logging` module instead of `print` statements
- Error handling with specific exception types

**Patterns to Follow:**
```python
# ✅ Good: Type hints and logging
import logging
from typing import Optional

logger = logging.getLogger(__name__)

def normalize_title(title: str) -> str:
    """Normalize title for comparison."""
    logger.debug("Normalizing title: %s", title)
    return title.lower().strip()

# ✅ Good: Specific exception handling
try:
    with open(filepath, 'r', encoding='utf-8') as f:
        data = f.read()
except FileNotFoundError:
    logger.error("File not found: %s", filepath)
    return None
except IOError as e:
    logger.error("Failed to read file: %s", e)
    return None

# ❌ Bad: Print statements
print(f"Processing {filename}")  # Should use logger.info()

# ❌ Bad: Generic exception catching
except Exception as e:  # Too broad
    pass
```

---

## Project-Specific Guidelines

### Fuzzy Matching Algorithm

Both Python and C# implement the same fuzzy matching logic:

1. **Normalization:** Remove common suffixes, normalize Unicode
2. **LCS Similarity:** Longest Common Subsequence ratio
3. **Threshold:** 0.80 (80% similarity)
4. **Part Numbers:** Extract and compare part numbers separately

**When modifying fuzzy matching:**
- Keep Python and C# implementations in sync
- Test with edge cases (different Unicode, part numbers)
- Update both if threshold changes

### Database Operations

**Migration Pattern:**
```csharp
// Always check for column existence before altering
var checkCommand = connection.CreateCommand();
checkCommand.CommandText =
    "SELECT COUNT(*) FROM pragma_table_info('episodes') WHERE name='column_name'";
var hasColumn = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

if (!hasColumn)
{
    var alterCommand = connection.CreateCommand();
    alterCommand.CommandText = "ALTER TABLE episodes ADD COLUMN column_name TEXT";
    await alterCommand.ExecuteNonQueryAsync();
    _logger.LogInformation("Migrated database: added column_name column");
}
```

### YouTube Integration

**Rate Limiting:**
- 5 second delay between downloads (line 348 in PlaylistWatcherService.cs)
- Max concurrent downloads configurable (default: 1)
- Poll interval configurable per feed (default: 60 minutes)

**Optimization:**
- Skip metadata fetch for existing files
- Use fuzzy matching to check file existence
- COALESCE in SQL to preserve existing metadata

### Logging Standards

**Log Levels:**
- `Trace` - Fuzzy match scores, file checks
- `Debug` - Method entry/exit, SQL queries, HTTP requests
- `Information` - Major operations (downloads, syncs), request completion
- `Warning` - Retryable errors, suspicious input
- `Error` - Failed operations with exceptions

**Format:**
```csharp
_logger.LogInformation(
    "Processing feed {FeedName}: {Count} episodes found",
    feedName,
    episodes.Count
);
```

---

## Security Checklist

When generating code, ensure:

- [ ] All SQL queries use parameters (`@paramName`)
- [ ] User input validated (length, null checks, path traversal)
- [ ] File paths validated with `Path.GetFullPath()` and prefix check
- [ ] No secrets in code (use configuration)
- [ ] Timeouts on long-running operations
- [ ] Proper resource disposal (`using`, `await using`)
- [ ] Cancellation tokens passed through async chains

---

## Common Tasks

### Adding a New Feed Configuration Property

1. Update `PodcastFeedConfig.cs` model
2. Update `appsettings.json` with example
3. Update validation in `Program.cs` if required
4. Update `CLAUDE.md` documentation
5. Update fuzzy matching if it affects file detection

### Adding a New Database Column

1. Add migration check in `InitializeDatabaseAsync()`
2. Add property to `EpisodeRecord` model
3. Update INSERT/UPDATE queries with new column
4. Use `COALESCE` if preserving existing data
5. Test with both new and existing databases

### Adding a New API Endpoint

1. Add method to `FeedController.cs`
2. Add input validation at method start
3. Add comprehensive logging (Debug + Information levels)
4. Add error handling with appropriate HTTP status codes
5. Update API documentation

---

## Testing Guidelines

**When generating tests:**

1. Use xUnit for .NET tests
2. Use unittest for Python tests
3. Mock external dependencies (YouTube API, file system)
4. Test edge cases (null, empty, malformed input)
5. Test security (path traversal attempts, SQL injection attempts)
6. Test error handling paths

**Example Test Pattern:**
```csharp
[Fact]
public async Task GetMedia_WithPathTraversal_ReturnsBadRequest()
{
    // Arrange
    var controller = CreateController();

    // Act
    var result = controller.GetMedia("feed", "../../../etc/passwd");

    // Assert
    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("Invalid", badRequest.Value.ToString());
}
```

---

## File Locations

**Configuration:**
- `Castr/appsettings.json` - Feed configuration, logging, YouTube settings
- `CLAUDE.md` - Project overview and commands
- `.github/copilot-instructions.md` - This file

**Documentation:**
- `CODE_REVIEW.md` - Comprehensive code review findings
- `RECOMMENDATIONS.md` - Prioritized improvement suggestions
- `REVIEW_SUMMARY.md` - Executive summary
- `Castr/BUILD.md` - Build and deployment guide
- `Castr/TRAEFIK.md` - Reverse proxy configuration

**Core Services:**
- `Castr/Services/PodcastFeedService.cs` - RSS generation
- `Castr/Services/PodcastDatabaseService.cs` - Database operations
- `Castr/Services/YouTubeDownloadService.cs` - YouTube integration
- `Castr/Services/PlaylistWatcherService.cs` - Background polling

**Models:**
- `Castr/Models/PodcastFeedConfig.cs` - Configuration models

**Controllers:**
- `Castr/Controllers/FeedController.cs` - API endpoints

**Python:**
- `match_episodes.py` - Episode matching script

---

## Known Issues & Improvements

Reference the GitHub issues for current work items:
- See `CODE_REVIEW.md` for detailed findings
- See `RECOMMENDATIONS.md` for implementation guides
- Check GitHub issues for prioritized tasks

**High Priority:**
- Input validation at API boundaries (#4)
- Timeout for YouTube downloads (#5)
- Configuration validation (#6)
- Health check endpoint (#7)
- Database lock timeout (#8)

---

## Build & Run

**Build .NET:**
```bash
cd Castr
~/.dotnet/dotnet build
~/.dotnet/dotnet run
```

**Build Docker:**
```bash
cd Castr
./build-and-push.sh
```

**Run Python Script:**
```bash
python3 match_episodes.py match
python3 match_episodes.py rename --dry-run
```

---

## Important Notes

1. **Naming:** Project was renamed from "PodcastFeedApi" to "Castr" (commit 8c21780)
2. **Database:** SQLite files stored per feed in feed directory
3. **Fuzzy Matching:** Threshold is 0.80, uses LCS algorithm
4. **Docker Registry:** `reg.ht2.io/castr:latest`
5. **Traefik:** Configured with forwarded headers support

---

## Code Review References

When suggesting improvements, consider:
- No critical security vulnerabilities found (SQL injection protected, path traversal protected)
- Add input validation to all public endpoints
- Add timeouts to long-running operations (downloads, database locks)
- Implement IDisposable where SemaphoreSlim is used
- Replace while loops with regex for string operations
- Add comprehensive logging at appropriate levels

For detailed review findings, see `CODE_REVIEW.md`.

---

## Questions to Ask Before Generating Code

1. Does this operation need input validation?
2. Should this use async/await?
3. Does this need a timeout?
4. Is logging at the appropriate level?
5. Are resources properly disposed?
6. Is this safe from SQL injection / path traversal?
7. Does this preserve backwards compatibility with existing databases?
8. Should this be covered by unit tests?

---

**Last Updated:** 2026-01-17
**Project Version:** .NET 10, Python 3.8+
**Target Model:** GPT-5.2 Codex
