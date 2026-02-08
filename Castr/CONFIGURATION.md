# Configuration Guide

This guide explains how to configure the Castr podcast feed API.

## Feed Configuration

Feeds are managed entirely through the web dashboard (database-only). Create, edit, and delete feeds at `/feeds` in the dashboard.

## Environment Variables

Environment variables are used for application-level settings (not feed configuration).

### Dashboard Authentication

```bash
# Set dashboard username (required - no default)
export Dashboard__Username=admin

# Set dashboard password (required - no default)
export Dashboard__Password=your-secure-password
```

> **Security Note**: Credentials are NOT set by default.
> **You MUST configure these via environment variables** for the dashboard to function.

### Database Configuration

```bash
# Set database provider (default: SQLite)
export Database__Provider=SQLite

# Set database connection string (default: Data Source=/data/castr.db)
export Database__ConnectionString="Data Source=/custom/path/castr.db"
```

The database stores all feed configurations, episodes, activity logs, and download queue. Supports SQLite, PostgreSQL, SQL Server, and MariaDB.

### Logging Configuration

```bash
# Set default logging level
export Logging__LogLevel__Default=Debug

# Set ASP.NET Core logging level
export Logging__LogLevel__Microsoft.AspNetCore=Information
```

## Docker Compose Configuration

### Basic Example

```yaml
services:
  castr:
    image: ghcr.io/joezombie/castr:latest
    environment:
      - Dashboard__Username=admin
      - Dashboard__Password=changeme
      - Logging__LogLevel__Default=Information
    volumes:
      - /host/podcasts:/podcasts:rw
      - castr-data:/data:rw

volumes:
  castr-data:
```

### Production Example

```yaml
services:
  castr:
    image: ghcr.io/joezombie/castr:latest
    environment:
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
      - castr-data:/data:rw
    restart: unless-stopped

volumes:
  castr-data:
```

## Configuration Precedence

ASP.NET Core applies configuration in this order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g., `appsettings.Production.json`)
3. Environment variables
4. **Command-line arguments** — Highest priority

## Related Documentation

- [BUILD.md](BUILD.md) - Building and deploying with Docker
- [TRAEFIK.md](TRAEFIK.md) - Reverse proxy configuration
