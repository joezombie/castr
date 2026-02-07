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

Feeds are configured in `appsettings.json` or via environment variables. A minimal feed:

```json
{
  "PodcastFeeds": {
    "Feeds": {
      "mypodcast": {
        "Title": "My Podcast",
        "Description": "My podcast episodes",
        "Directory": "/Podcasts/My Podcast"
      }
    }
  }
}
```

To enable YouTube playlist monitoring, add a `YouTube` block:

```json
{
  "YouTube": {
    "PlaylistUrl": "https://www.youtube.com/playlist?list=...",
    "PollIntervalMinutes": 60,
    "Enabled": true
  }
}
```

## API

- `GET /feed` - List all feeds
- `GET /feed/{name}` - RSS feed XML
- `GET /feed/{name}/media/{file}` - Stream media file

## License

[MIT](LICENSE)
