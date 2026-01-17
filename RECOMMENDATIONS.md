# Code Review Recommendations

## Quick Reference

**Status:** ✅ Code is production-ready with recommended improvements  
**Security:** ✅ No critical vulnerabilities found  
**Build:** ✅ All projects compile successfully  

---

## Priority 1 - Security & Reliability (Implement Soon)

### 1. Add Input Validation to Controllers

**File:** `Castr/Controllers/FeedController.cs`

```csharp
public IActionResult GetMedia(string feedName, string fileName)
{
    // Add at the start of method
    if (string.IsNullOrWhiteSpace(feedName) || feedName.Length > 100)
        return BadRequest("Invalid feed name");
    
    if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        return BadRequest("Invalid file name");
    
    if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        return BadRequest("Invalid file name");
    
    // ... rest of method
}
```

### 2. Add Timeout to Downloads

**File:** `Castr/Services/YouTubeDownloadService.cs`  
**Line:** 107

```csharp
// Before the download
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromMinutes(30)); // 30 min timeout

await _youtube.Videos.DownloadAsync(
    videoId,
    outputPath,
    o => o.SetContainer("mp3").SetPreset(ConversionPreset.Medium),
    cancellationToken: cts.Token);
```

### 3. Add Configuration Validation

**File:** `Castr/Program.cs`  
**After line 44:**

```csharp
foreach (var (feedName, feedConfig) in config.Value.Feeds)
{
    // Validate configuration
    if (string.IsNullOrWhiteSpace(feedConfig.Directory))
        throw new InvalidOperationException($"Feed {feedName}: Directory is required");
    
    if (string.IsNullOrWhiteSpace(feedConfig.Title))
        throw new InvalidOperationException($"Feed {feedName}: Title is required");
    
    // Create directory if it doesn't exist
    if (!Directory.Exists(feedConfig.Directory))
    {
        logger.LogInformation("Creating directory for feed {FeedName}: {Directory}", 
            feedName, feedConfig.Directory);
        Directory.CreateDirectory(feedConfig.Directory);
    }
    
    // Rest of initialization...
}
```

### 4. Add Health Check Endpoint

**File:** `Castr/Program.cs`

```csharp
// After builder.Services.AddControllers()
builder.Services.AddHealthChecks();

// After app.MapControllers()
app.MapHealthChecks("/health");
```

### 5. Add Database Lock Timeout

**File:** `Castr/Services/PodcastDatabaseService.cs`  
**Line 94:**

```csharp
// Replace: await _dbLock.WaitAsync();
if (!await _dbLock.WaitAsync(TimeSpan.FromSeconds(30)))
{
    _logger.LogError("Timeout waiting for database lock for feed {FeedName}", feedName);
    throw new TimeoutException($"Database lock timeout for feed {feedName}");
}
```

### 6. Fix appsettings.json Formatting

**File:** `Castr/appsettings.json`  
**Line 56:** Replace tab with spaces (use 2 or 4 spaces consistently)

---

## Priority 2 - Code Quality (Recommended)

### 1. Add Python Logging

**File:** `match_episodes.py`

```python
import logging

# Add at top of file after imports
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)

# Replace print statements with logger calls:
# print("Reading files1.txt...") becomes:
logger.info("Reading files1.txt...")

# print(f"ERROR: ...") becomes:
logger.error("Could not find path for %s", mp3_file)
```

### 2. Improve Python Error Handling

**File:** `match_episodes.py`

```python
def do_matching():
    """Run the fuzzy matching process."""
    # Add validation
    if not os.path.exists('files1.txt'):
        logger.error("files1.txt not found")
        return
    
    if not os.path.exists('playlist-bulletized1.txt'):
        logger.error("playlist-bulletized1.txt not found")
        return
    
    try:
        with open('files1.txt', 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except IOError as e:
        logger.error("Failed to read files1.txt: %s", e)
        return
    
    # ... rest of function
```

### 3. Optimize String Operations

**File:** `Castr/Services/PodcastDatabaseService.cs`  
**Line 694:**

```csharp
// Replace:
// while (normalized.Contains("  "))
//     normalized = normalized.Replace("  ", " ");

// With:
normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
```

### 4. Implement IDisposable

**File:** `Castr/Services/PodcastDatabaseService.cs`

```csharp
public class PodcastDatabaseService : IPodcastDatabaseService, IDisposable
{
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _disposed;
    
    // ... existing code ...
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _dbLock?.Dispose();
        _disposed = true;
    }
}
```

---

## Priority 3 - Documentation (Nice to Have)

### 1. Complete README.md

**File:** `README.md`

```markdown
# Behind the Bastards Podcast Manager

Tools for managing "Behind the Bastards" podcast episodes with fuzzy matching and automated RSS feed generation.

## Components

### Python Episode Matcher (`match_episodes.py`)
Fuzzy matches YouTube playlist titles to downloaded MP3 files.

**Usage:**
```bash
# Run fuzzy matching
python3 match_episodes.py match

# Preview file renames (dry run)
python3 match_episodes.py rename --dry-run

# Execute file renames
python3 match_episodes.py rename --execute
```

### Castr - Podcast RSS Feed API
ASP.NET Core Web API that serves podcast RSS feeds with YouTube integration.

**Quick Start:**
```bash
cd Castr
dotnet run
```

**API Endpoints:**
- `GET /feed` - List all feeds
- `GET /feed/{feedName}` - Get RSS feed XML
- `GET /feed/{feedName}/media/{fileName}` - Stream media file
- `GET /health` - Health check

## Configuration

Edit `Castr/appsettings.json` to configure podcast feeds and YouTube playlists.

## Documentation

- [CLAUDE.md](CLAUDE.md) - AI assistant guidance
- [Castr/BUILD.md](Castr/BUILD.md) - Build and deployment
- [Castr/TRAEFIK.md](Castr/TRAEFIK.md) - Reverse proxy setup
- [CODE_REVIEW.md](CODE_REVIEW.md) - Comprehensive code review
- [RECOMMENDATIONS.md](RECOMMENDATIONS.md) - Implementation recommendations

## Requirements

- Python 3.8+
- .NET 10.0+
- Docker (for containerized deployment)

## License

[Specify license here]
```

### 2. Add Environment Variables Documentation

**Create:** `Castr/CONFIGURATION.md`

```markdown
# Configuration Guide

## Environment Variables

Override appsettings.json values using environment variables:

```bash
# Feed directory
export PodcastFeeds__Feeds__btb__Directory="/custom/path"

# YouTube settings
export PodcastFeeds__Feeds__btb__YouTube__Enabled=false
export PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=120

# Logging level
export Logging__LogLevel__Default=Debug
```

## appsettings.json Structure

See [appsettings.json](appsettings.json) for the full schema.
```

---

## Priority 4 - Testing (Future Enhancement)

### 1. Add Python Tests

**Create:** `test_match_episodes.py`

```python
import unittest
from match_episodes import (
    normalize_title, 
    extract_part_number,
    similarity_score
)

class TestFuzzyMatching(unittest.TestCase):
    def test_normalize_title(self):
        title = "Episode Name | BEHIND THE BASTARDS"
        expected = "Episode Name"
        self.assertEqual(normalize_title(title), expected)
    
    def test_extract_part_number(self):
        self.assertEqual(extract_part_number("Part One: Title"), 1)
        self.assertEqual(extract_part_number("Part Two: Title"), 2)
        self.assertEqual(extract_part_number("Pt 3: Title"), 3)
        self.assertIsNone(extract_part_number("No Part"))
    
    def test_similarity_score(self):
        score = similarity_score("Episode Name", "Episode Name")
        self.assertGreater(score, 0.9)
        
        score = similarity_score("Episode Name", "Completely Different")
        self.assertLess(score, 0.5)

if __name__ == '__main__':
    unittest.main()
```

### 2. Add .NET Tests

**Create:** `Castr.Tests/FeedControllerTests.cs`

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Castr.Controllers;
using Castr.Services;

public class FeedControllerTests
{
    [Fact]
    public void GetMedia_WithInvalidFileName_ReturnsBadRequest()
    {
        // Arrange
        var mockService = new Mock<PodcastFeedService>();
        var mockLogger = new Mock<ILogger<FeedController>>();
        var controller = new FeedController(mockService.Object, mockLogger.Object);
        
        // Act
        var result = controller.GetMedia("btb", "../../../etc/passwd");
        
        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
```

---

## Optional Enhancements

### 1. Add Rate Limiting

```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", options =>
    {
        options.Window = TimeSpan.FromMinutes(1);
        options.PermitLimit = 60;
    });
});

app.UseRateLimiter();
```

### 2. Add Caching for RSS Feeds

```csharp
// In PodcastFeedService.cs
private readonly IMemoryCache _cache;

public async Task<string?> GenerateFeedAsync(string feedName, string baseUrl)
{
    var cacheKey = $"feed_{feedName}";
    
    if (_cache.TryGetValue<string>(cacheKey, out var cachedFeed))
        return cachedFeed;
    
    var feed = await GenerateFeedXmlAsync(feedName, config, baseUrl);
    
    _cache.Set(cacheKey, feed, TimeSpan.FromMinutes(5));
    
    return feed;
}
```

### 3. Add Authentication (if needed)

```csharp
// In Program.cs
builder.Services.AddAuthentication()
    .AddJwtBearer();

app.UseAuthentication();

// In FeedController.cs
[Authorize]
public class FeedController : ControllerBase
{
    // ...
}
```

---

## Implementation Checklist

Use this checklist to track implementation:

### High Priority
- [ ] Add input validation to GetMedia endpoint
- [ ] Add input validation to GetFeed endpoint
- [ ] Add timeout to YouTube downloads
- [ ] Add configuration validation at startup
- [ ] Add health check endpoint
- [ ] Add timeout to database lock acquisition
- [ ] Fix appsettings.json formatting

### Medium Priority
- [ ] Add Python logging framework
- [ ] Improve Python error handling with file validation
- [ ] Optimize string operations (regex instead of while loop)
- [ ] Implement IDisposable for SemaphoreSlim
- [ ] Complete README.md

### Low Priority
- [ ] Add environment variables documentation
- [ ] Add Python unit tests
- [ ] Add .NET unit tests
- [ ] Add rate limiting
- [ ] Add RSS feed caching
- [ ] Make Python file paths configurable

---

## Questions or Issues?

If you have questions about these recommendations:

1. Review the full [CODE_REVIEW.md](CODE_REVIEW.md) for detailed explanations
2. Check existing documentation in CLAUDE.md, BUILD.md, TRAEFIK.md
3. Test changes in a development environment before production

---

**Last Updated:** 2026-01-17
