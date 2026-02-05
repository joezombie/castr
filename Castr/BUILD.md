# Building and Deploying

This document explains how to build and deploy the Castr container.

## Quick Start

### Build and Push to Registry

```bash
./build-and-push.sh
```

This will:
1. Build the Docker image
2. Push it to `reg.ht2.io/castr:latest`
3. Delete the local copy to save space

### Custom Tag

To build with a specific tag (e.g., version number):

```bash
./build-and-push.sh v1.0.0
```

This creates and pushes: `reg.ht2.io/castr:v1.0.0`

## Prerequisites

### First Time Setup

1. **Log in to the registry:**
   ```bash
   docker login reg.ht2.io
   ```

2. **Ensure you're in the correct directory:**
   ```bash
   cd Castr
   ```

## Deployment

### Production Deployment (Using Registry)

The docker-compose.yml is configured to use the registry image by default:

```bash
# Pull latest image and start
docker-compose pull
docker-compose up -d
```

### Development (Local Build)

If you want to build locally instead of using the registry:

1. Edit `docker-compose.yml`:
   ```yaml
   # Comment out the image line:
   # image: reg.ht2.io/castr:latest

   # Uncomment the build section:
   build:
     context: .
     dockerfile: Dockerfile
   ```

2. Run:
   ```bash
   docker-compose up -d --build
   ```

## Build Process Details

### What the Script Does

The `build-and-push.sh` script performs these steps:

1. **Build**: Creates Docker image with tag `reg.ht2.io/castr:TAG`
2. **Push**: Uploads to the registry
3. **Clean**: Removes local image to free disk space

### Error Handling

If the build fails:
- Check Dockerfile syntax
- Ensure all required files are present
- Check Docker daemon is running

If the push fails:
- Ensure you're logged in: `docker login reg.ht2.io`
- Check network connectivity
- Verify registry permissions

If cleanup fails:
- The image was still pushed successfully
- Manually remove: `docker rmi reg.ht2.io/castr:TAG`

## Manual Build (Without Script)

If you need to build manually:

```bash
# Build
docker build -t reg.ht2.io/castr:latest .

# Push
docker push reg.ht2.io/castr:latest

# Clean up (optional)
docker rmi reg.ht2.io/castr:latest
```

## Version Tagging Strategy

### Recommended Workflow

1. **Development builds**: Use `dev` or `test` tags
   ```bash
   ./build-and-push.sh dev
   ```

2. **Release candidates**: Use version with `-rc` suffix
   ```bash
   ./build-and-push.sh v1.2.0-rc1
   ```

3. **Production releases**: Use semantic versioning
   ```bash
   ./build-and-push.sh v1.2.0
   ```

4. **Update latest**: After testing, tag as latest
   ```bash
   docker pull reg.ht2.io/castr:v1.2.0
   docker tag reg.ht2.io/castr:v1.2.0 reg.ht2.io/castr:latest
   docker push reg.ht2.io/castr:latest
   ```

## Updating Production

### Standard Update

```bash
# On build machine:
./build-and-push.sh

# On production server:
docker-compose pull
docker-compose up -d
```

### Zero-Downtime Update

Using Docker Compose:

```bash
docker-compose pull
docker-compose up -d --no-deps --build castr
```

This will:
1. Pull the new image
2. Start a new container
3. Stop the old container only after the new one is healthy

## Multi-Architecture Builds

To build for multiple platforms (e.g., AMD64 and ARM64):

```bash
# Create and use buildx builder
docker buildx create --use

# Build and push multi-arch image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t reg.ht2.io/castr:latest \
  --push \
  .
```

## Troubleshooting

### "Cannot connect to Docker daemon"

```bash
# Check if Docker is running
sudo systemctl status docker

# Start Docker if needed
sudo systemctl start docker
```

### "Registry authentication required"

```bash
# Log in again
docker login reg.ht2.io
```

### "Disk space issues"

```bash
# Clean up unused images
docker image prune -a

# Or clean everything
docker system prune -a
```

### Build is slow

Docker builds can be slow. To speed up:

1. Ensure Docker buildkit is enabled:
   ```bash
   export DOCKER_BUILDKIT=1
   ```

2. Use multi-stage builds (already configured in Dockerfile)

3. Don't build on the same machine serving the API (resource contention)

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Push

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Login to Registry
        run: echo "${{ secrets.REGISTRY_PASSWORD }}" | docker login reg.ht2.io -u "${{ secrets.REGISTRY_USERNAME }}" --password-stdin

      - name: Build and Push
        run: |
          cd Castr
          ./build-and-push.sh
```

### GitLab CI Example

```yaml
build:
  image: docker:latest
  services:
    - docker:dind
  before_script:
    - docker login -u $CI_REGISTRY_USER -p $CI_REGISTRY_PASSWORD reg.ht2.io
  script:
    - cd Castr
    - ./build-and-push.sh
  only:
    - main
```

## Database Migrations

Castr uses EF Core migrations for schema management across all supported database providers.

### Automatic Migrations (Default)

Migrations apply automatically on startup via `Database.MigrateAsync()`. This is suitable for:
- Development environments
- Simple deployments
- Docker containers

No action needed - the application handles migrations on startup.

### Creating New Migrations

When entity models change, create a new migration:

```bash
cd Castr

# Install EF Core tools (one-time setup)
dotnet tool install --global dotnet-ef

# Default (SQLite)
dotnet ef migrations add <MigrationName> --output-dir Data/Migrations

# PostgreSQL
EF_PROVIDER=PostgreSQL EF_CONNECTION="Host=localhost;Database=castr;Username=user;Password=pass" \
  dotnet ef migrations add <MigrationName> --output-dir Data/Migrations

# SQL Server
EF_PROVIDER=SqlServer EF_CONNECTION="Server=localhost;Database=castr;User Id=user;Password=pass" \
  dotnet ef migrations add <MigrationName> --output-dir Data/Migrations

# MariaDB/MySQL
EF_PROVIDER=MariaDB EF_CONNECTION="Server=localhost;Database=castr;User=user;Password=pass" \
  dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
```

### Manual Migration (Production)

For production environments where automatic migrations are not desired, use migration bundles or SQL scripts:

```bash
# Generate idempotent SQL script
dotnet ef migrations script --idempotent --output deploy.sql

# Or use migration bundle (recommended for production)
dotnet ef migrations bundle --output efbundle
./efbundle --connection "your-connection-string"
```

### Supported Database Providers

Configure via `appsettings.json` or environment variables:

| Provider | Config Value | Example Connection String |
|----------|--------------|---------------------------|
| SQLite | `SQLite` | `Data Source=/data/castr.db` |
| PostgreSQL | `PostgreSQL` | `Host=localhost;Database=castr;Username=user;Password=pass` |
| SQL Server | `SqlServer` | `Server=localhost;Database=castr;User Id=user;Password=pass` |
| MariaDB/MySQL | `MariaDB` | `Server=localhost;Database=castr;User=user;Password=pass` |

### Rolling Back Migrations

```bash
# Rollback to a specific migration
dotnet ef database update <PreviousMigrationName>

# Rollback all migrations (reset database)
dotnet ef database update 0

# Remove the most recent migration (if not applied)
dotnet ef migrations remove
```

### Viewing Migration Status

```bash
# List all migrations and their status
dotnet ef migrations list

# Get current database info
dotnet ef dbcontext info
```

### Migration Best Practices

1. **Always test migrations** on a copy of production data before applying
2. **Back up the database** before applying migrations in production
3. **Review generated migrations** - EF Core may generate suboptimal SQL
4. **Use idempotent scripts** (`--idempotent`) for manual deployment
5. **Keep migrations small** - one logical change per migration
