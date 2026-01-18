# Dashboard Authentication

The Castr dashboard provides a web interface for managing podcast feeds, monitoring downloads, and viewing system status.

## Features

- **Authentication**: Cookie-based authentication with configurable credentials
- **Dark Theme**: Default dark mode UI using MudBlazor
- **Navigation**: Easy access to Dashboard, Feeds, Downloads, and Settings
- **Public API**: RSS feed endpoints remain publicly accessible without authentication

## ⚠️ Required Configuration

**Dashboard credentials are NOT set by default.** You MUST configure them using environment variables before starting the application.

The application will fail to start if credentials are not properly configured.

## Configuration

### Using Environment Variables (Required for Production)

```bash
export Dashboard__Username=admin
export Dashboard__Password=your-secure-password
```

### Using appsettings.json (Development Only)

**Note**: Do not commit credentials to version control. Use appsettings.Development.json or user secrets for local development.

```json
{
  "Dashboard": {
    "Username": "admin",
    "Password": "your-secure-password"
  }
}
```

### Using Environment Variables

```bash
export Dashboard__Username=admin
export Dashboard__Password=your-secure-password
```

### Docker Compose

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      - Dashboard__Username=admin
      - Dashboard__Password=YourSecurePasswordHere123!
```

## Accessing the Dashboard

1. Navigate to `http://localhost:5178/` (or your configured URL)
2. You will be redirected to `/login`
3. Enter your username and password
4. Upon successful login, you'll be redirected to the dashboard

## Session Management

- Sessions persist for 7 days by default
- Sessions use sliding expiration (auto-renew on activity)
- Logout is available via the navigation menu or `/logout` endpoint

## Public Endpoints

The following endpoints remain publicly accessible without authentication:

- `/feed` - List all available feeds
- `/feed/{feedName}` - Get RSS feed XML
- `/feed/{feedName}/media/{fileName}` - Serve media files
- `/health` - Health check endpoint

## Security Best Practices

1. **Change default credentials immediately**
2. **Use strong passwords** (minimum 12 characters, mix of upper/lower/numbers/symbols)
3. **Use HTTPS in production** (configure your reverse proxy)
4. **Limit dashboard access** to trusted networks if possible
5. **Enable firewall rules** to restrict dashboard access
6. **Monitor login attempts** via application logs

## Troubleshooting

### Cannot Login

- Check that credentials match configuration
- Verify `Dashboard:Username` and `Dashboard:Password` are set
- Check application logs for authentication errors

### Redirect Loop

- Clear browser cookies
- Verify cookie authentication is properly configured
- Check for reverse proxy configuration issues

### Dashboard Not Loading

- Verify MudBlazor static files are being served
- Check browser console for JavaScript errors
- Ensure Blazor Server is configured in Program.cs

## Phase 2 Implementation

This dashboard represents Phase 2 of the MudBlazor implementation:

- ✅ Blazor Server setup with MudBlazor
- ✅ Cookie-based authentication
- ✅ Login/Logout functionality
- ✅ Basic layout and navigation
- ✅ Placeholder dashboard pages

**Coming in Phase 3**: Feed management, download monitoring, real-time updates, and settings pages.
