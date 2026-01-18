# Security Configuration

## ⚠️ CRITICAL: Credentials Required

The `appsettings.json` file does **NOT** contain default credentials. You **MUST** configure authentication credentials before the application will function.

**Credentials must be set via environment variables or configuration.**

## Production Configuration

### Option 1: Environment Variables (Required for Production)

Set these environment variables:

```bash
export Dashboard__Username="your-admin-username"
export Dashboard__Password="your-secure-password"
```

### Option 2: User Secrets (Development Only)

For local development, use .NET User Secrets:

```bash
cd Castr
dotnet user-secrets set "Dashboard:Username" "your-username"
dotnet user-secrets set "Dashboard:Password" "your-password"
```

### Option 3: Override appsettings.json

Create `appsettings.Production.json` (add to .gitignore):

```json
{
  "Dashboard": {
    "Username": "your-admin-username",
    "Password": "your-secure-password"
  }
}
```

## Password Requirements

For production, use a strong password with:
- Minimum 12 characters
- Mix of uppercase and lowercase letters
- Numbers
- Special characters
- No dictionary words

Example strong password: `MyP0dc@st!Dashb0ard#2026`

## Additional Security Measures

1. **Use HTTPS**: Always use HTTPS in production (configure your reverse proxy)
2. **Firewall Rules**: Restrict dashboard access to trusted networks
3. **Regular Updates**: Keep the application and dependencies updated
4. **Monitor Logs**: Watch for failed login attempts
5. **Session Management**: Sessions expire after 7 days of inactivity

## Docker Security

When deploying with Docker, pass credentials via environment variables:

```yaml
services:
  castr:
    image: reg.ht2.io/castr:latest
    environment:
      - Dashboard__Username=${DASHBOARD_USER}
      - Dashboard__Password=${DASHBOARD_PASS}
```

Then create a `.env` file (add to .gitignore):

```bash
DASHBOARD_USER=your-admin-username
DASHBOARD_PASS=your-secure-password
```

## Checking Configuration

The application will log a warning if default credentials are detected in production.

See [DASHBOARD.md](DASHBOARD.md) for more information about dashboard authentication.
