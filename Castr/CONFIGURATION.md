# Configuration Guide

This guide explains how to configure the Castr podcast feed API using environment variables. Environment variables are particularly useful for containerized deployments and managing environment-specific configurations without modifying code.

## Why Environment Variables?

- **Easier configuration management** - Change settings without editing files
- **Better for containerized deployments** - Docker-friendly configuration
- **Secrets management** - Keep sensitive data out of version control
- **Environment-specific configurations** - Different settings for dev/staging/production

## How Environment Variables Work

ASP.NET Core uses a hierarchical configuration system with double underscores (`__`) to represent nested JSON properties:

```
JSON:                    appsettings.json path
PodcastFeeds.Feeds.btb   →  PodcastFeeds__Feeds__btb
```

## Environment Variable Examples

### Feed Configuration

```bash
# Override feed directory
export PodcastFeeds__Feeds__btb__Directory="/custom/path"

# Override feed title
export PodcastFeeds__Feeds__btb__Title="Custom Feed Title"

# Override feed author
export PodcastFeeds__Feeds__btb__Author="Custom Author Name"

# Override database path
export PodcastFeeds__Feeds__btb__DatabasePath="/custom/path/podcast.db"
```

### YouTube Integration Settings

```bash
# Disable YouTube integration
export PodcastFeeds__Feeds__btb__YouTube__Enabled=false

# Change polling interval (in minutes)
export PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=120

# Change max concurrent downloads
export PodcastFeeds__Feeds__btb__YouTube__MaxConcurrentDownloads=2

# Override playlist URL
export PodcastFeeds__Feeds__btb__YouTube__PlaylistUrl="PLxxx..."
```

### Logging Configuration

```bash
# Set default logging level
export Logging__LogLevel__Default=Debug

# Set ASP.NET Core logging level
export Logging__LogLevel__Microsoft.AspNetCore=Information

# Set specific namespace logging level
export Logging__LogLevel__PodcastFeedApi=Trace
```

### Dashboard Authentication

```bash
# Set dashboard username (required - no default)
export Dashboard__Username=admin

# Set dashboard password (required - no default)
export Dashboard__Password=your-secure-password
```

> **Security Note**: Credentials are NOT set by default.
> **You MUST configure these via environment variables** for the dashboard to function.

### Central Database Configuration

```bash
# Set central database path (optional, default: /data/castr.db)
export PodcastFeeds__CentralDatabasePath=/custom/path/castr.db
```

The central database stores all feed configurations, episodes, activity logs, and download queue. On first startup, feeds are automatically migrated from `appsettings.json` to the central database.

### Multiple Feeds

```bash
# Configure the 'btb' feed
export PodcastFeeds__Feeds__btb__Directory="/podcasts/btb"

# Configure the 'btbc' feed
export PodcastFeeds__Feeds__btbc__Directory="/podcasts/btbc"
```

## Docker Compose Configuration

### Basic Example

In your `docker-compose.yml`:

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      # Feed configuration
      - PodcastFeeds__Feeds__btb__Directory=/podcasts
      
      # Logging
      - Logging__LogLevel__Default=Information
      
      # YouTube settings
      - PodcastFeeds__Feeds__btb__YouTube__Enabled=true
      - PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=60
    volumes:
      - /host/podcasts:/podcasts:rw
```

### Production Example

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      # Use production paths
      - PodcastFeeds__Feeds__btb__Directory=/mnt/storage/podcasts/btb
      - PodcastFeeds__Feeds__btbc__Directory=/mnt/storage/podcasts/btbc
      
      # Dashboard authentication (CHANGE THESE!)
      - Dashboard__Username=admin
      - Dashboard__Password=YourSecurePasswordHere123!
      
      # Production logging (less verbose)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft.AspNetCore=Warning
      
      # Production environment
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
    volumes:
      - /mnt/storage/podcasts:/mnt/storage/podcasts:rw
    restart: unless-stopped
```

### Development Example

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      # Use local development paths
      - PodcastFeeds__Feeds__btb__Directory=/app/test-data
      
      # Verbose logging for debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft.AspNetCore=Debug
      - Logging__LogLevel__PodcastFeedApi=Trace
      
      # Disable YouTube for faster testing
      - PodcastFeeds__Feeds__btb__YouTube__Enabled=false
      
      # Development environment
      - ASPNETCORE_ENVIRONMENT=Development
    volumes:
      - ./test-data:/app/test-data:rw
    ports:
      - "5000:8080"
```

## Using .env Files

For local development, you can use a `.env` file with Docker Compose:

**`.env` file:**
```bash
PODCAST_DIR=/mnt/user/Stuff/Podcasts
LOG_LEVEL=Information
YOUTUBE_ENABLED=true
POLL_INTERVAL=60
```

**docker-compose.yml:**
```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      - PodcastFeeds__Feeds__btb__Directory=${PODCAST_DIR}/Behind the Bastards
      - Logging__LogLevel__Default=${LOG_LEVEL}
      - PodcastFeeds__Feeds__btb__YouTube__Enabled=${YOUTUBE_ENABLED}
      - PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=${POLL_INTERVAL}
```

**Note:** Don't commit `.env` files with secrets to version control!

## Testing Your Configuration

To verify environment variables are working:

1. **Set environment variables:**
   ```bash
   export Logging__LogLevel__Default=Debug
   export PodcastFeeds__Feeds__btb__Directory="/test/path"
   ```

2. **Run the application:**
   ```bash
   cd Castr
   dotnet run
   ```

3. **Check logs for confirmation:**
   Look for log messages showing the configured paths and settings.

## Configuration Precedence

ASP.NET Core applies configuration in this order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g., `appsettings.Production.json`)
3. Environment variables
4. **Command-line arguments** ← Highest priority

This means environment variables will override values in JSON files, but command-line arguments have the final say.

## Common Configuration Scenarios

### Scenario 1: Override Directory for All Feeds
```bash
export PodcastFeeds__Feeds__btb__Directory="/media/podcasts/btb"
export PodcastFeeds__Feeds__btbc__Directory="/media/podcasts/btbc"
```

### Scenario 2: Disable YouTube for Testing
```bash
export PodcastFeeds__Feeds__btb__YouTube__Enabled=false
export PodcastFeeds__Feeds__btbc__YouTube__Enabled=false
```

### Scenario 3: Increase Polling Interval
```bash
export PodcastFeeds__Feeds__btb__YouTube__PollIntervalMinutes=180
export PodcastFeeds__Feeds__btbc__YouTube__PollIntervalMinutes=180
```

### Scenario 4: Debug Logging for Troubleshooting
```bash
export Logging__LogLevel__Default=Debug
export Logging__LogLevel__PodcastFeedApi=Trace
```

## Reference

### Complete Configuration Schema

See `appsettings.json` for the complete configuration schema with all available options and default values.

### Related Documentation

- [BUILD.md](BUILD.md) - Building and deploying with Docker
- [TRAEFIK.md](TRAEFIK.md) - Reverse proxy configuration
- [CODE_REVIEW.md](../CODE_REVIEW.md) - Configuration recommendations
- [RECOMMENDATIONS.md](../RECOMMENDATIONS.md) - Additional implementation recommendations

### Configuration Model Structure

Based on `appsettings.json`, the configuration structure is:

```
Logging
  └─ LogLevel
       ├─ Default
       ├─ Microsoft.AspNetCore
       └─ PodcastFeedApi

PodcastFeeds
  └─ Feeds
       ├─ btb
       │    ├─ Title
       │    ├─ Description
       │    ├─ Directory
       │    ├─ Author
       │    ├─ Language
       │    ├─ Category
       │    ├─ FileExtensions
       │    ├─ DatabasePath
       │    └─ YouTube
       │         ├─ PlaylistUrl
       │         ├─ PollIntervalMinutes
       │         ├─ Enabled
       │         ├─ MaxConcurrentDownloads
       │         └─ AudioQuality
       └─ btbc (same structure as btb)
```

## Troubleshooting

### Environment Variables Not Taking Effect

1. **Check variable syntax:** Use double underscores (`__`), not single or dots
2. **Restart the application:** Changes require an app restart
3. **Check precedence:** Ensure no command-line args override your env vars
4. **Case sensitivity:** Environment variable names are case-insensitive on Windows, case-sensitive on Linux

### Docker Environment Not Working

1. **Verify syntax:** Use `-` prefix for each environment variable in YAML
2. **Check indentation:** YAML is whitespace-sensitive
3. **Rebuild containers:** Run `docker-compose up -d --force-recreate`
4. **Inspect running container:** 
   ```bash
   docker exec castr env | grep PodcastFeeds
   ```

### Path Issues

- **Use absolute paths** in Docker (e.g., `/podcasts` not `./podcasts`)
- **Ensure volume mounts** match the directory paths in environment variables
- **Check permissions** on mounted directories

## Support

For issues or questions:
1. Review the [CODE_REVIEW.md](../CODE_REVIEW.md) for configuration best practices
2. Check container logs: `docker logs castr`
3. Verify your environment variables: `docker exec castr env`
