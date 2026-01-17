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

// Database service for episode tracking
builder.Services.AddSingleton<IPodcastDatabaseService, PodcastDatabaseService>();
builder.Services.AddSingleton<PodcastFeedService>();

// YouTube download services
builder.Services.AddSingleton<IYouTubeDownloadService, YouTubeDownloadService>();
builder.Services.AddHostedService<PlaylistWatcherService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Initialize databases for all configured feeds at startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var config = app.Services.GetRequiredService<IOptions<PodcastFeedsConfig>>();
var database = app.Services.GetRequiredService<IPodcastDatabaseService>();

logger.LogInformation("Initializing databases for {Count} configured feed(s)", config.Value.Feeds.Count);

foreach (var (feedName, feedConfig) in config.Value.Feeds)
{
    // Validate configuration
    if (string.IsNullOrWhiteSpace(feedConfig.Directory))
        throw new InvalidOperationException($"Feed {feedName}: Directory is required");
    
    if (string.IsNullOrWhiteSpace(feedConfig.Title))
        throw new InvalidOperationException($"Feed {feedName}: Title is required");
    
    // Validate directory path for security issues
    try
    {
        // Check for obvious path traversal patterns first
        if (feedConfig.Directory.Contains("..") || 
            feedConfig.Directory.Contains("~"))
        {
            throw new InvalidOperationException($"Feed {feedName}: Directory path contains invalid characters");
        }
        
        // Validate that the path is rooted (absolute path)
        if (!Path.IsPathRooted(feedConfig.Directory))
        {
            throw new InvalidOperationException($"Feed {feedName}: Directory path must be an absolute path");
        }
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
        throw new InvalidOperationException($"Feed {feedName}: Invalid directory path: {feedConfig.Directory}", ex);
    }
    
    // Create directory if it doesn't exist
    // Note: Directory.CreateDirectory is safe to call on existing directories
    try
    {
        Directory.CreateDirectory(feedConfig.Directory);
        logger.LogDebug("Directory ready for feed {FeedName}: {Directory}", 
            feedName, feedConfig.Directory);
    }
    catch (UnauthorizedAccessException ex)
    {
        throw new InvalidOperationException($"Feed {feedName}: Access denied when creating directory: {feedConfig.Directory}", ex);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Feed {feedName}: Failed to create directory: {feedConfig.Directory}", ex);
    }
    
    try
    {
        logger.LogDebug("Initializing database for feed: {FeedName}", feedName);

        var dbPath = feedConfig.DatabasePath ?? Path.Combine(feedConfig.Directory, "podcast.db");
        var dbExistedBefore = File.Exists(dbPath);

        await database.InitializeDatabaseAsync(feedName);

        if (dbExistedBefore)
        {
            logger.LogInformation("Database for feed {FeedName} already existed at {Path}", feedName, dbPath);
        }
        else
        {
            logger.LogInformation("Created new database for feed {FeedName} at {Path}", feedName, dbPath);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database for feed {FeedName}", feedName);
        throw;
    }
}

logger.LogInformation("All databases initialized successfully");

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

app.Run();
