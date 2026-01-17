# Traefik Reverse Proxy Configuration

This document explains how to deploy the Castr behind a Traefik reverse proxy.

## Prerequisites

- Traefik v2+ running on your server
- A Traefik network named `traefik` (or modify the network name in docker-compose.yml)
- DNS configured to point your domain to your server

## Configuration Steps

### 1. Update docker-compose.yml

The docker-compose.yml file includes Traefik labels. Update the following:

**Required Changes:**
- Replace `podcast.example.com` with your actual domain in both HTTP and HTTPS router rules

```yaml
- "traefik.http.routers.castr.rule=Host(`your-domain.com`)"
- "traefik.http.routers.castr-http.rule=Host(`your-domain.com`)"
```

**Optional Changes:**
- Update `certresolver` if your Traefik uses a different name (default: `letsencrypt`)
- Update entrypoints if different (defaults: `web` for HTTP, `websecure` for HTTPS)

### 2. Traefik Network

The service connects to an external Traefik network. Create it if it doesn't exist:

```bash
docker network create traefik
```

Or if using a different network name, update in docker-compose.yml:

```yaml
networks:
  your-network-name:
    external: true
```

### 3. Deploy

```bash
cd Castr
docker-compose up -d
```

## How It Works

### Forwarded Headers

The application is configured to respect reverse proxy headers:

- `X-Forwarded-Proto` - Used to determine HTTP vs HTTPS
- `X-Forwarded-Host` - Used to determine the domain name
- `X-Forwarded-For` - Client IP address

This ensures RSS feed URLs use the correct public domain and HTTPS protocol.

### URL Generation

All URLs in the RSS feed are generated using:
```
{scheme}://{host}/feed/{feedName}/media/{filename}
```

Behind Traefik, this becomes:
```
https://your-domain.com/feed/btb/media/episode.mp3
```

### Traefik Labels Explained

```yaml
# Enable Traefik routing for this container
- "traefik.enable=true"

# HTTPS router (main route)
- "traefik.http.routers.castr.rule=Host(`podcast.example.com`)"
- "traefik.http.routers.castr.entrypoints=websecure"
- "traefik.http.routers.castr.tls=true"
- "traefik.http.routers.castr.tls.certresolver=letsencrypt"

# Tell Traefik which container port to use
- "traefik.http.services.castr.loadbalancer.server.port=8080"

# HTTP router (redirects to HTTPS)
- "traefik.http.routers.castr-http.rule=Host(`podcast.example.com`)"
- "traefik.http.routers.castr-http.entrypoints=web"
- "traefik.http.routers.castr-http.middlewares=redirect-to-https"
- "traefik.http.middlewares.redirect-to-https.redirectscheme.scheme=https"
```

## Advanced Configuration

### Path Prefix

To deploy under a path (e.g., `/podcast`), update the router rules:

```yaml
- "traefik.http.routers.castr.rule=Host(`example.com`) && PathPrefix(`/podcast`)"
- "traefik.http.routers.castr-http.rule=Host(`example.com`) && PathPrefix(`/podcast`)"

# Optional: Strip prefix before forwarding to container
- "traefik.http.routers.castr.middlewares=podcast-stripprefix"
- "traefik.http.middlewares.podcast-stripprefix.stripprefix.prefixes=/podcast"
```

**Note:** If using a path prefix, you'll need to update the route templates in the application code.

### Additional Security Headers

Uncomment and customize these labels for security headers:

```yaml
- "traefik.http.routers.castr.middlewares=podcast-headers"
- "traefik.http.middlewares.podcast-headers.headers.customResponseHeaders.X-Robots-Tag=noindex,nofollow"
- "traefik.http.middlewares.podcast-headers.headers.customResponseHeaders.X-Content-Type-Options=nosniff"
```

### Basic Auth

Add basic authentication:

```yaml
- "traefik.http.routers.castr.middlewares=podcast-auth"
- "traefik.http.middlewares.podcast-auth.basicauth.users=user:$$apr1$$password$$hash"
```

Generate the password hash:
```bash
htpasswd -nb username password
```

## Troubleshooting

### RSS URLs are HTTP instead of HTTPS

Check that:
1. Traefik is forwarding the `X-Forwarded-Proto` header
2. The forwarded headers middleware is enabled (it is by default)
3. Check logs for the generated base URL:
   ```
   Generated base URL: https://your-domain.com (Scheme: https, Host: your-domain.com)
   ```

### 404 Not Found

1. Verify Traefik can reach the container:
   ```bash
   docker logs traefik
   ```

2. Check the service is running:
   ```bash
   docker ps | grep castr
   ```

3. Verify the Traefik network connection:
   ```bash
   docker network inspect traefik
   ```

### Certificate Issues

If using Let's Encrypt:
1. Ensure ports 80 and 443 are open
2. Verify DNS points to your server
3. Check Traefik cert resolver configuration
4. View Traefik logs: `docker logs traefik`

## Direct Access (Without Traefik)

To expose the service directly without Traefik:

1. Comment out the Traefik labels and network
2. Uncomment the ports section:
   ```yaml
   ports:
     - "5000:8080"
   ```

3. Access at: `http://localhost:5000/feed`
