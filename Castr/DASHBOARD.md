# Castr Dashboard

The Castr dashboard provides a web interface for managing podcast feeds, monitoring downloads, and viewing system status.

## Features

- **Dashboard**: Real-time statistics, active downloads with progress bars, activity timeline
- **Feed Management**: Add, edit, delete podcast feeds via UI
- **Episode Management**: View episodes, trigger redownload, manage metadata
- **Download Queue**: Monitor active, queued, completed, and failed downloads
- **Real-time Updates**: SignalR-powered live updates for download progress
- **Authentication**: Cookie-based authentication with configurable credentials
- **Dark Theme**: Default dark mode UI using MudBlazor

## Routes

| Route | Description |
|-------|-------------|
| `/` | Dashboard with statistics and activity |
| `/feeds` | Feed list and management |
| `/feeds/new` | Create new feed |
| `/feeds/{name}` | Feed details |
| `/feeds/{name}/edit` | Edit feed configuration |
| `/feeds/{name}/episodes` | Episode list for feed |
| `/downloads` | Download queue monitoring |
| `/login` | Authentication page |
| `/logout` | Logout |

## Public Endpoints (No Authentication)

The following endpoints remain publicly accessible:

- `/feed` - List all available feeds
- `/feed/{feedName}` - Get RSS feed XML
- `/feed/{feedName}/media/{fileName}` - Serve media files
- `/health` - Health check endpoint

## Configuration

### Required: Dashboard Credentials

Dashboard credentials are NOT set by default. You MUST configure them using environment variables.

```bash
# Required
export Dashboard__Username=admin
export Dashboard__Password=your-secure-password
```

### Optional: Database Configuration

```bash
# Database provider (SQLite, PostgreSQL, SQLServer, MySQL)
export Database__Provider=SQLite

# Connection string (default shown for SQLite)
export Database__ConnectionString="Data Source=/data/castr.db"
```

### Docker Compose Example

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      # Dashboard authentication (REQUIRED)
      - Dashboard__Username=admin
      - Dashboard__Password=YourSecurePasswordHere123!
      # Optional: Database configuration
      - Database__Provider=SQLite
      - Database__ConnectionString=Data Source=/data/castr.db
    volumes:
      - /host/podcasts:/Podcasts:rw
      - castr-data:/data:rw  # Persist database

volumes:
  castr-data:
```

## Database

The dashboard uses an EF Core database that stores:

- **Feeds**: All feed configurations (migrated from appsettings.json on first run)
- **Episodes**: Episode metadata, ordering, and YouTube info
- **DownloadedVideos**: Tracks which videos have been downloaded
- **ActivityLogs**: Activity history for monitoring
- **DownloadQueueItems**: Active and completed downloads

### Supported Providers

Configure the database provider via `Database__Provider`:

- **SQLite** (default) - `Data Source=/data/castr.db`
- **PostgreSQL** - `Host=localhost;Database=castr;Username=user;Password=pass`
- **SQLServer** - `Server=localhost;Database=castr;User Id=user;Password=pass`
- **MySQL** - `Server=localhost;Database=castr;User=user;Password=pass`

### Migration

On first startup, if the database is empty:
1. EF Core applies migrations automatically
2. Feeds are migrated from `appsettings.json` configuration

## Redownload Feature

To trigger a redownload of an episode:

1. Navigate to `/feeds/{name}/episodes`
2. Click the refresh icon on the episode
3. Confirm the redownload
4. The video will be re-downloaded on the next playlist poll

This removes the video from the `downloaded_videos` table, causing the playlist watcher to treat it as new.

## Session Management

- Sessions persist for 7 days by default
- Sessions use sliding expiration (auto-renew on activity)
- Logout is available via the navigation menu or `/logout` endpoint

## Security Best Practices

1. **Use strong passwords** (minimum 12 characters, mix of upper/lower/numbers/symbols)
2. **Use HTTPS in production** (configure your reverse proxy)
3. **Limit dashboard access** to trusted networks if possible
4. **Monitor login attempts** via application logs
5. **Never commit credentials** to version control

## Troubleshooting

### Cannot Login

- Verify `Dashboard__Username` and `Dashboard__Password` environment variables are set
- Check application logs for authentication errors
- Ensure cookies are enabled in your browser

### Dashboard Not Loading

- Verify the container is running: `docker logs castr`
- Check for JavaScript errors in browser console
- Ensure port 8080 is accessible

### Data Not Showing

- Check if central database is initialized: look for "Central database initialized" in logs
- Verify volume mounts for `/data` directory
- Check database permissions

### Redirect Loop

- Clear browser cookies
- Check reverse proxy configuration
- Verify cookie settings match your domain

## Architecture

```
Components/
├── App.razor              # Root component
├── Routes.razor           # Router configuration
├── _Imports.razor         # Global usings
├── Layout/
│   ├── MainLayout.razor   # Main layout with navigation
│   └── NavMenu.razor      # Navigation menu
└── Pages/
    ├── Dashboard.razor    # Main dashboard
    ├── Downloads.razor    # Download queue
    ├── Login.razor        # Login page
    ├── Feeds/
    │   ├── FeedList.razor    # Feed list
    │   ├── FeedEdit.razor    # Add/edit feed
    │   └── FeedDetails.razor # Feed details
    └── Episodes/
        ├── EpisodeList.razor        # Episode list
        └── EpisodeDetailsDialog.razor # Episode details dialog

Hubs/
└── DownloadProgressHub.cs  # SignalR hub for real-time updates

Data/
├── CastrDbContext.cs       # EF Core database context
├── Entities/               # Entity models
│   ├── Feed.cs
│   ├── Episode.cs
│   ├── DownloadedVideo.cs
│   ├── DownloadQueueItem.cs
│   └── ActivityLog.cs
└── Repositories/           # Repository pattern for data access

Services/
└── PodcastDataService.cs   # High-level data service facade
```

## Related Documentation

- [CONFIGURATION.md](CONFIGURATION.md) - Environment variable configuration
- [TRAEFIK.md](TRAEFIK.md) - Reverse proxy setup
- [BUILD.md](BUILD.md) - Building and deploying
