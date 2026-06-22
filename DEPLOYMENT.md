# Deployment

Castr runs in production as a Docker container on the unraid host
**superstore.ht2.io**, fronted by Traefik at <https://castr.ht2.io>.

## Production environment

| Item | Value |
|------|-------|
| Host | `ssh root@superstore.ht2.io` |
| Compose stack | `/boot/config/stacks/castr/docker-compose.yml` |
| Image | `ghcr.io/joezombie/castr:latest` (also tagged `:<release-tag>`) |
| Container name | `castr` |
| App port | 8080 (proxied by Traefik) |
| SQLite DB (host) | `/mnt/user/data/castr/castr.db` |
| Media volumes | `/mnt/user/Stuff/Podcasts` → `/Podcasts`, `/mnt/user/Stuff/Audiobooks` → `/Audiobooks` |
| DataProtection keys | persisted to `/data/keys` (deploys do not log users out) |

## How releases build the image

The image is **not** built on push to `main`. It is built and pushed by
`.github/workflows/release.yml` when a **GitHub release is published**. The
release tag name becomes both the `:<tag>` image tag and the `APP_VERSION`
build-arg (shown in logs as `Castr <version> started`).

## Deploy procedure

1. Commit and push the change to `main`:

   ```bash
   git push origin main
   ```

2. Publish a release (use the next semver tag — bump patch for fixes):

   ```bash
   gh release create vX.Y.Z --target main --title "vX.Y.Z" --notes "..."
   ```

3. Wait for CI (test + build-and-push) to succeed:

   ```bash
   gh run watch "$(gh run list --workflow=release.yml --limit 1 --json databaseId -q '.[0].databaseId')" --exit-status
   ```

4. Pull and restart on the host:

   ```bash
   ssh root@superstore.ht2.io 'cd /boot/config/stacks/castr && docker compose pull && docker compose up -d'
   ```

5. Verify the running version:

   ```bash
   ssh root@superstore.ht2.io 'docker logs castr 2>&1 | grep -iE "started|now listening" | tail -3'
   curl -s -o /dev/null -w "%{http_code}\n" https://castr.ht2.io/   # 302 = login redirect, healthy
   ```

   You should see `Castr X.Y.Z started` matching the release tag.

## Notes

- Container logs are rotated (`docker logs castr` shows only recent history).
- `sqlite3` is available on the host for direct DB inspection at the path above.
- EF Core migrations are applied automatically on startup (look for
  `Applying migration` lines in the logs).
