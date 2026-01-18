using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Castr.Models;
using Castr.Services;
using Castr.Hubs;

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

// Central database service
builder.Services.AddSingleton<ICentralDatabaseService, CentralDatabaseService>();

// Database service for episode tracking (existing per-feed databases)
builder.Services.AddSingleton<IPodcastDatabaseService, PodcastDatabaseService>();
builder.Services.AddSingleton<PodcastFeedService>();

// YouTube download services
builder.Services.AddSingleton<IYouTubeDownloadService, YouTubeDownloadService>();
builder.Services.AddHostedService<PlaylistWatcherService>();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Add Blazor and MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add API controllers
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add health checks for monitoring and container orchestration
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize central database at startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var centralDatabase = app.Services.GetRequiredService<ICentralDatabaseService>();

logger.LogInformation("Initializing central database");
try
{
    await centralDatabase.InitializeDatabaseAsync();
    logger.LogInformation("Central database initialized successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize central database");
    throw;
}

// Initialize databases for all configured feeds at startup
var config = app.Services.GetRequiredService<IOptions<PodcastFeedsConfig>>();
var database = app.Services.GetRequiredService<IPodcastDatabaseService>();

logger.LogInformation("Initializing databases for {Count} configured feed(s)", config.Value.Feeds.Count);

foreach (var (feedName, feedConfig) in config.Value.Feeds)
{
    // Validate configuration
    // Note: 'required' keyword ensures non-null, but we also check for empty/whitespace
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

// Middleware for Blazor static files
app.UseStaticFiles();
app.UseAntiforgery();

// Authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

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

// Map Blazor components
app.MapRazorComponents<Castr.Components.App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hubs
app.MapHub<DownloadProgressHub>("/hubs/download-progress");

// Map API controllers
app.MapControllers();

// Add health check endpoint for monitoring and container orchestration
app.MapHealthChecks("/health");

app.Run();
