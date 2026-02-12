using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Castr.Data;
using Castr.Data.Repositories;
using Castr.Services;
using Castr.Components;
using Castr.Hubs;
using MudBlazor.Services;

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

// Add memory cache for RSS feed caching
builder.Services.AddMemoryCache();

// Podcast feed service (scoped to match IPodcastDataService dependency)
builder.Services.AddScoped<PodcastFeedService>();

// YouTube download services
builder.Services.AddSingleton<IYouTubeDownloadService, YouTubeDownloadService>();
builder.Services.AddSingleton<IPlaylistWatcherTrigger, PlaylistWatcherTrigger>();
builder.Services.AddHostedService<PlaylistWatcherService>();

// Settings service for localStorage persistence (scoped for Blazor)
builder.Services.AddScoped<ISettingsService, SettingsService>();

// SignalR for real-time updates
builder.Services.AddSignalR();

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// Add EF Core database context with multi-provider support
builder.Services.AddCastrDatabase(builder.Configuration);

// Add EF Core repositories
builder.Services.AddScoped<IFeedRepository, FeedRepository>();
builder.Services.AddScoped<IEpisodeRepository, EpisodeRepository>();
builder.Services.AddScoped<IDownloadRepository, DownloadRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();

// Add data service layer
builder.Services.AddScoped<IPodcastDataService, PodcastDataService>();

// Add Blazor Server with MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddMudServices();

// Add HttpContextAccessor for Blazor components
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add health checks for monitoring and container orchestration
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize EF Core database
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<CastrDbContext>();

    try
    {
        await db.Database.MigrateAsync();
        startupLogger.LogInformation("EF Core database migrations applied successfully");
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Failed to initialize EF Core database. Check your database configuration and connection string.");
        throw new InvalidOperationException("Database initialization failed. The application cannot start without a working database.", ex);
    }
}

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Castr started");

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

// Serve static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map API controllers (feed endpoints remain public)
app.MapControllers();

// Map Razor Pages (for login)
app.MapRazorPages();

// Map Blazor components (dashboard routes)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub for real-time updates
app.MapHub<DownloadProgressHub>("/hubs/download-progress");

// Add health check endpoint for monitoring and container orchestration
app.MapHealthChecks("/health");

app.Run();
