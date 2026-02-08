# Castr

A self-hosted podcast RSS server that serves local MP3 directories as podcast feeds. Optionally monitors YouTube playlists and automatically downloads new episodes.

## Features

- Generates standard RSS feeds from local MP3 directories
- Monitors YouTube playlists and auto-downloads new episodes
- Web dashboard for managing feeds, episodes, and downloads
- Real-time download progress via SignalR
- ID3 tag extraction for episode metadata
- Range request support for streaming
- Multi-feed support from a single instance

## Quickstart

Create a `docker-compose.yml`:

```yaml
services:
  castr:
    image: ghcr.io/joezombie/castr:latest
    container_name: castr
    ports:
      - "5000:8080"
    volumes:
      - /path/to/your/podcasts:/Podcasts:rw
      - castr-data:/data:rw
    environment:
      - Dashboard__Username=admin
      - Dashboard__Password=changeme
    restart: unless-stopped

volumes:
  castr-data:
```

```bash
docker compose up -d
```

The dashboard is at `http://localhost:5000` and feeds are at `http://localhost:5000/feed`.

## Configuration

Feeds are managed entirely through the web dashboard. No `appsettings.json` feed configuration is needed.

The dashboard lets you create, edit, and delete feeds with all settings including YouTube playlist monitoring.

## API

- `GET /feed` - List all feeds
- `GET /feed/{name}` - RSS feed XML
- `GET /feed/{name}/media/{file}` - Stream media file

## License

[MIT](LICENSE)
