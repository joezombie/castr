# Castr.Tests

Unit and integration tests for the Castr podcast RSS feed API.

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~FeedControllerTests"

# Run tests with code coverage
dotnet test /p:CollectCoverage=true
```

## Test Organization

### FeedControllerTests (24 tests)
Tests for the API controller endpoints:
- `GetFeeds()` - List all available feeds
- `GetFeed(feedName)` - Get RSS XML for a specific feed
- `GetMedia(feedName, fileName)` - Serve media files

**Key test areas:**
- Input validation (empty, too long, whitespace)
- Security: Path traversal prevention (`../../../etc/passwd`)
- MIME type detection for audio formats
- HTTP response types and status codes

### PodcastFeedServiceTests (18 tests)
Tests for the feed generation and management service:
- Feed configuration management
- RSS XML generation with iTunes namespace
- Episode ordering and sorting (database-backed)
- Media file path resolution
- Security: Directory traversal prevention

### YouTubeDownloadServiceTests (14 tests)
Tests for YouTube download and fuzzy matching functionality:
- File name sanitization (removes invalid characters)
- Fuzzy matching for existing files (LCS algorithm)
- Exact match detection
- Directory and file system handling

### PodcastDatabaseServiceTests (32 tests)
Tests for SQLite database operations:
- Database initialization and schema migration
- Episode CRUD operations
- Downloaded video tracking
- Directory synchronization
- Playlist synchronization with fuzzy matching
- Display order management

### FuzzyMatchingTests (17 tests)
Tests for string matching algorithms used throughout the application:
- String normalization (Unicode, whitespace, suffixes)
- Longest Common Subsequence (LCS) similarity calculation
- Real-world episode matching scenarios
- Edge cases and boundary conditions

## Test Coverage

The test suite provides comprehensive coverage including:

### Functional Tests
- ✅ All API endpoints
- ✅ RSS feed generation
- ✅ Episode management
- ✅ Database operations
- ✅ Fuzzy matching algorithms

### Security Tests
- ✅ Path traversal attacks
- ✅ Input validation
- ✅ File system security
- ✅ SQL injection prevention (parameterized queries)

### Integration Tests
- ✅ Controller + Service integration
- ✅ Service + Database integration
- ✅ File system operations
- ✅ Configuration loading

## Dependencies

The test project uses:
- **xUnit** (v2.9.2) - Test framework
- **Moq** (v4.20.72) - Mocking framework
- **Microsoft.AspNetCore.Mvc.Testing** (v10.0.1) - ASP.NET Core testing utilities
- **Microsoft.NET.Test.Sdk** (v17.13.0) - Test SDK
- **coverlet.collector** (v6.0.2) - Code coverage collector

## Writing New Tests

### Example: Controller Test

```csharp
[Fact]
public async Task GetFeed_WithValidFeedName_ReturnsRssXml()
{
    // Arrange
    var feedName = "testfeed";
    _mockDatabase.Setup(d => d.GetEpisodesAsync(feedName))
        .ReturnsAsync(new List<EpisodeRecord>());

    // Act
    var result = await _controller.GetFeed(feedName);

    // Assert
    var contentResult = Assert.IsType<ContentResult>(result);
    Assert.Equal("application/rss+xml; charset=utf-8", contentResult.ContentType);
    Assert.Contains("<rss", contentResult.Content);
}
```

### Example: Service Test

```csharp
[Fact]
public async Task AddEpisodeAsync_AddsEpisode()
{
    // Arrange
    await _service.InitializeDatabaseAsync("testfeed");
    var episode = new EpisodeRecord
    {
        Filename = "episode001.mp3",
        VideoId = "abc123",
        DisplayOrder = 1,
        AddedAt = DateTime.UtcNow
    };

    // Act
    await _service.AddEpisodeAsync("testfeed", episode);
    var episodes = await _service.GetEpisodesAsync("testfeed");

    // Assert
    Assert.Single(episodes);
    Assert.Equal("episode001.mp3", episodes[0].Filename);
}
```

## Continuous Integration

These tests are designed to run in CI/CD pipelines:
- Fast execution (< 1 second for most tests)
- No external dependencies required
- Temporary files automatically cleaned up
- OS-agnostic (tested on Linux and Windows)

## Test Principles

1. **Arrange-Act-Assert (AAA)** - All tests follow this pattern
2. **Isolated** - Each test is independent
3. **Fast** - Total test suite runs in under 1 second
4. **Deterministic** - No flaky tests or timing issues
5. **Meaningful** - Test names describe what they test
6. **Cleanup** - Temporary files and directories are always cleaned up

## Known Limitations

- Tests use reflection to access private methods for fuzzy matching algorithms
- Some tests are integration tests rather than pure unit tests (e.g., database tests)
- File system operations may behave differently on different operating systems
- No mocking of TagLibSharp for ID3 tag reading (tested with real files)

## Future Improvements

Potential enhancements for the test suite:
- [ ] Add performance benchmarks for fuzzy matching
- [ ] Add integration tests for the full HTTP pipeline
- [ ] Add tests for concurrent database access
- [ ] Add tests for the PlaylistWatcherService
- [ ] Increase code coverage with edge case tests
- [ ] Add mutation testing to verify test quality
