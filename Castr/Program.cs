using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Castr.Models;
using Castr.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for reverse proxy support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;

    // Accept forwarded headers from any source (for Traefik/reverse proxy)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.Configure<PodcastFeedsConfig>(
    builder.Configuration.GetSection("PodcastFeeds"));

// Add memory cache for RSS feed caching
builder.Services.AddMemoryCache();

// Central database service for all feeds
builder.Services.AddSingleton<ICentralDatabaseService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<CentralDatabaseService>>();
    var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    
    // Use a configurable database path or default to app data directory
    var dbPath = builder.Configuration.GetValue<string>("CentralDatabasePath");
    if (string.IsNullOrWhiteSpace(dbPath))
    {
        // Default: store in ContentRootPath/Data directory
        dbPath = Path.Combine(env.ContentRootPath, "Data", "central.db");
    }
    
    return new CentralDatabaseService(logger, dbPath);
});

// Keep old database service for backward compatibility during migration
builder.Services.AddSingleton<IPodcastDatabaseService, PodcastDatabaseService>();
builder.Services.AddSingleton<PodcastFeedService>();

// YouTube download services
builder.Services.AddSingleton<IYouTubeDownloadService, YouTubeDownloadService>();
builder.Services.AddHostedService<PlaylistWatcherService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add health checks for monitoring and container orchestration
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize central database and migrate feeds
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var config = app.Services.GetRequiredService<IOptions<PodcastFeedsConfig>>();
var centralDatabase = app.Services.GetRequiredService<ICentralDatabaseService>();
var legacyDatabase = app.Services.GetRequiredService<IPodcastDatabaseService>();

logger.LogInformation("=== Starting Central Database Initialization ===");

// Step 1: Initialize central database
logger.LogInformation("Step 1: Initializing central database");
await centralDatabase.InitializeDatabaseAsync();
logger.LogInformation("Central database initialized successfully");

// Step 2: Load or migrate feeds from configuration
logger.LogInformation("Step 2: Loading/migrating {Count} feed(s) from configuration", config.Value.Feeds.Count);

foreach (var (feedName, feedConfig) in config.Value.Feeds)
{
    logger.LogInformation("Processing feed: {FeedName}", feedName);
    
    // Validate configuration
    if (string.IsNullOrWhiteSpace(feedConfig.Directory))
        throw new InvalidOperationException($"Feed {feedName}: Directory cannot be empty");
    
    if (string.IsNullOrWhiteSpace(feedConfig.Title))
        throw new InvalidOperationException($"Feed {feedName}: Title cannot be empty");
    
    // Validate directory path
    try
    {
        var fullPath = Path.GetFullPath(feedConfig.Directory);
        // Path.GetFullPath normalizes the path and validates it
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Feed {FeedName}: Invalid directory path: {Directory}", feedName, feedConfig.Directory);
        throw new InvalidOperationException($"Feed {feedName}: Invalid directory path: {feedConfig.Directory}", ex);
    }
    
    // Create directory if it doesn't exist
    if (!Directory.Exists(feedConfig.Directory))
    {
        try
        {
            Directory.CreateDirectory(feedConfig.Directory);
            logger.LogInformation("Created directory for feed {FeedName}: {Directory}", 
                feedName, feedConfig.Directory);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Feed {FeedName}: Access denied for directory: {Directory}", feedName, feedConfig.Directory);
            throw new InvalidOperationException($"Feed {feedName}: Access denied when creating directory: {feedConfig.Directory}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Feed {FeedName}: Failed to create directory: {Directory}", feedName, feedConfig.Directory);
            throw new InvalidOperationException($"Feed {feedName}: Failed to create directory: {feedConfig.Directory}", ex);
        }
    }
    
    // Add or update feed in central database
    try
    {
        logger.LogDebug("Adding/updating feed {FeedName} in central database", feedName);
        
        var feedRecord = new FeedRecord
        {
            Name = feedName,
            Title = feedConfig.Title,
            Description = feedConfig.Description,
            Directory = feedConfig.Directory,
            Author = feedConfig.Author,
            ImageUrl = feedConfig.ImageUrl,
            Link = feedConfig.Link,
            Language = feedConfig.Language ?? "en-us",
            Category = feedConfig.Category,
            FileExtensions = feedConfig.FileExtensions,
            DatabasePath = feedConfig.DatabasePath,
            YoutubePlaylistUrl = feedConfig.YouTube?.PlaylistUrl,
            YoutubePollIntervalMinutes = feedConfig.YouTube?.PollIntervalMinutes ?? 60,
            YoutubeEnabled = feedConfig.YouTube?.Enabled ?? false,
            YoutubeMaxConcurrentDownloads = feedConfig.YouTube?.MaxConcurrentDownloads ?? 1,
            YoutubeAudioQuality = feedConfig.YouTube?.AudioQuality ?? "highest",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var feedId = await centralDatabase.AddOrUpdateFeedAsync(feedRecord);
        logger.LogInformation("Feed {FeedName} registered in central database with ID {FeedId}", feedName, feedId);
        
        // Step 3: Migrate legacy per-feed database if it exists
        var legacyDbPath = feedConfig.DatabasePath ?? Path.Combine(feedConfig.Directory, "podcast.db");
        if (File.Exists(legacyDbPath))
        {
            logger.LogInformation("Found legacy database for {FeedName} at {Path}, starting migration", feedName, legacyDbPath);
            
            try
            {
                await centralDatabase.MigrateLegacyDatabaseAsync(feedName, legacyDbPath, feedId);
                logger.LogInformation("Successfully migrated legacy database for {FeedName}", feedName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate legacy database for {FeedName}, continuing with empty feed", feedName);
                // Don't fail startup, just log the error
            }
        }
        else
        {
            logger.LogDebug("No legacy database found for {FeedName}, starting with clean state", feedName);
        }
        
        // Step 4: Sync directory to catch any existing files
        logger.LogDebug("Syncing directory for feed {FeedName}", feedName);
        await centralDatabase.SyncDirectoryAsync(feedId, feedConfig.Directory, feedConfig.FileExtensions ?? [".mp3"]);
        logger.LogDebug("Directory sync completed for feed {FeedName}", feedName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize feed {FeedName}", feedName);
        throw;
    }
}

logger.LogInformation("=== Central Database Initialization Complete ===");
logger.LogInformation("All {Count} feed(s) initialized successfully", config.Value.Feeds.Count);

// Configure forwarded headers (MUST be before other middleware)
app.UseForwardedHeaders();

// Request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var startTime = DateTime.UtcNow;

    logger.LogInformation("HTTP {Method} {Path}{QueryString} started",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString);

    try
    {
        await next();

        var elapsed = DateTime.UtcNow - startTime;
        logger.LogInformation("HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
    catch (Exception ex)
    {
        var elapsed = DateTime.UtcNow - startTime;
        logger.LogError(ex, "HTTP {Method} {Path}{QueryString} failed after {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            elapsed.TotalMilliseconds);
        throw;
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint for monitoring and container orchestration
app.MapHealthChecks("/health");

app.Run();
