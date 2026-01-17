# GitHub Copilot Agents Configuration

## Agent Definitions

This document defines specialized Copilot agents for different tasks in the btb_pod project.

---

## 1. Code Review Agent

**Name:** `code-reviewer`
**Model:** GPT-5.2 Codex
**Purpose:** Review code changes for security, style, and best practices

**Instructions:**
- Check for SQL injection vulnerabilities (ensure parameterized queries)
- Verify input validation on all public endpoints
- Check for path traversal vulnerabilities
- Verify async/await usage for I/O operations
- Check logging is at appropriate levels
- Verify resource disposal (using/await using)
- Check for proper error handling
- Flag any hardcoded secrets or sensitive data

**Example Usage:**
```
@code-reviewer Review this new endpoint for security issues
```

---

## 2. Test Generator Agent

**Name:** `test-generator`
**Model:** GPT-5.2 Codex
**Purpose:** Generate comprehensive unit and integration tests

**Instructions:**
- Use xUnit for .NET tests
- Use unittest for Python tests
- Mock external dependencies (YouTube API, file system, database)
- Include edge cases (null, empty, malformed input)
- Include security tests (SQL injection, path traversal)
- Test error handling paths
- Follow Arrange-Act-Assert pattern
- Add descriptive test names (MethodName_Scenario_ExpectedResult)

**Example Usage:**
```
@test-generator Generate tests for the GetMedia endpoint
```

---

## 3. Documentation Agent

**Name:** `doc-writer`
**Model:** GPT-5.2 Codex
**Purpose:** Generate and update documentation

**Instructions:**
- Write clear, concise documentation
- Include code examples where applicable
- Update CLAUDE.md when architecture changes
- Keep BUILD.md and TRAEFIK.md in sync with configuration
- Use proper markdown formatting
- Include benefits/rationale for features
- Link to related documentation
- Include usage examples

**Example Usage:**
```
@doc-writer Document this new configuration option
```

---

## 4. API Developer Agent

**Name:** `api-dev`
**Model:** GPT-5.2 Codex
**Purpose:** Develop ASP.NET Core API endpoints

**Context:**
- ASP.NET Core 10.0 patterns
- Dependency injection usage
- Controller best practices
- Input validation requirements
- Logging standards

**Instructions:**
- Add input validation at method start (length, null, path traversal)
- Use async/await for all I/O operations
- Add comprehensive logging (Debug + Information levels)
- Return appropriate HTTP status codes
- Use dependency injection for services
- Include XML documentation comments
- Handle errors with try-catch and appropriate responses
- Pass CancellationToken through async chain

**Template:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetItem(string id, CancellationToken ct = default)
{
    _logger.LogDebug("Getting item {Id}", id);

    // Input validation
    if (string.IsNullOrWhiteSpace(id) || id.Length > 100)
        return BadRequest("Invalid ID");

    try
    {
        var item = await _service.GetItemAsync(id, ct);
        if (item == null)
            return NotFound();

        _logger.LogInformation("Retrieved item {Id}", id);
        return Ok(item);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get item {Id}", id);
        return StatusCode(500, "Internal server error");
    }
}
```

**Example Usage:**
```
@api-dev Create a new endpoint to delete episodes
```

---

## 5. Database Agent

**Name:** `db-dev`
**Model:** GPT-5.2 Codex
**Purpose:** SQLite database operations and migrations

**Context:**
- SQLite with ADO.NET
- Schema migration patterns
- Fuzzy matching implementation
- Transaction usage

**Instructions:**
- ALWAYS use parameterized queries (@paramName)
- Use await using for connections and commands
- Add migration checks before ALTER TABLE
- Use COALESCE to preserve existing data during updates
- Add appropriate indexes for query performance
- Use transactions for multiple related operations
- Log all database operations at Debug level
- Handle SqliteException specifically

**Migration Template:**
```csharp
private async Task MigrateColumnIfMissingAsync(
    SqliteConnection connection,
    string columnName,
    string columnType)
{
    var checkCommand = connection.CreateCommand();
    checkCommand.CommandText =
        $"SELECT COUNT(*) FROM pragma_table_info('episodes') WHERE name='{columnName}'";
    var hasColumn = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

    if (!hasColumn)
    {
        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText =
            $"ALTER TABLE episodes ADD COLUMN {columnName} {columnType}";
        await alterCommand.ExecuteNonQueryAsync();
        _logger.LogInformation("Migrated database: added {ColumnName} column", columnName);
    }
}
```

**Example Usage:**
```
@db-dev Add a new column for episode duration
```

---

## 6. YouTube Integration Agent

**Name:** `youtube-dev`
**Model:** GPT-5.2 Codex
**Purpose:** YouTube API integration and download logic

**Context:**
- YouTubeExplode library
- Rate limiting requirements
- Concurrent download control
- File existence checking

**Instructions:**
- Respect rate limits (5 second delay between downloads)
- Check for existing files before downloading
- Use fuzzy matching to detect existing files
- Pass CancellationToken to all YouTube operations
- Add timeouts to prevent hung downloads (30 min recommended)
- Log all YouTube API calls at Debug level
- Handle network failures gracefully
- Use SemaphoreSlim for concurrency control

**Example Usage:**
```
@youtube-dev Add support for fetching video chapters
```

---

## 7. Python Script Agent

**Name:** `python-dev`
**Model:** GPT-5.2 Codex
**Purpose:** Python fuzzy matching script development

**Context:**
- PEP 8 style guide
- Fuzzy matching algorithm (LCS)
- Episode normalization
- Part number extraction

**Instructions:**
- Follow PEP 8 style guide
- Add type hints to all functions
- Use logging module instead of print
- Add docstrings to all functions
- Handle FileNotFoundError and IOError specifically
- Validate file existence before operations
- Keep fuzzy matching in sync with C# implementation
- Use argparse for CLI commands

**Example Usage:**
```
@python-dev Add a new command to export to CSV
```

---

## 8. Security Auditor Agent

**Name:** `security-audit`
**Model:** GPT-5.2 Codex
**Purpose:** Security-focused code review

**Instructions:**
- Check for SQL injection (string concatenation in SQL)
- Check for path traversal (`..,` `/`, `\` in user input)
- Check for command injection (unvalidated shell commands)
- Verify input validation on all public endpoints
- Check for hardcoded secrets or API keys
- Verify proper authentication/authorization
- Check for insecure deserialization
- Flag any use of MD5/SHA1 for security
- Verify HTTPS usage for external APIs
- Check for information disclosure in logs

**Example Usage:**
```
@security-audit Review this endpoint for vulnerabilities
```

---

## 9. Performance Optimizer Agent

**Name:** `perf-optimizer`
**Model:** GPT-5.2 Codex
**Purpose:** Performance optimization and efficiency

**Instructions:**
- Identify N+1 query problems
- Suggest caching opportunities
- Replace while loops with more efficient alternatives
- Suggest async/await where appropriate
- Identify unnecessary allocations
- Suggest batch operations where applicable
- Check for proper disposal of resources
- Suggest database indexes for queries
- Identify blocking operations

**Example Usage:**
```
@perf-optimizer Optimize this fuzzy matching loop
```

---

## 10. Docker/DevOps Agent

**Name:** `devops`
**Model:** GPT-5.2 Codex
**Purpose:** Docker, deployment, and infrastructure

**Context:**
- Multi-stage Docker builds
- Traefik reverse proxy
- Docker Compose
- Container registry (reg.ht2.io)

**Instructions:**
- Use multi-stage builds for .NET
- Minimize layer count and image size
- Use .dockerignore to exclude unnecessary files
- Add health checks to containers
- Use proper environment variable handling
- Add labels for Traefik routing
- Document deployment steps
- Consider security (non-root user, minimal base images)

**Example Usage:**
```
@devops Add health check to the Dockerfile
```

---

## Agent Best Practices

### When to Use Agents

1. **Code Review:** Before committing significant changes
2. **Test Generation:** After implementing new features
3. **Documentation:** When adding new features or configuration
4. **Security Audit:** Before merging to main, after API changes
5. **Performance:** When experiencing slowness or high resource usage

### Agent Chaining

You can chain agents for complex tasks:

```
@test-generator Generate tests for this endpoint
@code-reviewer Review the generated tests
```

### Custom Agent Invocation

For specialized tasks, you can invoke with specific instructions:

```
@api-dev Create a new endpoint for podcast statistics
Following the coding standards in .github/copilot-instructions.md
```

---

## Model Configuration

**Primary Model:** GPT-5.2 Codex
**Fallback Model:** GPT-4 Turbo

**Model Selection Criteria:**
- Use Codex for code generation and review
- Use GPT-4 Turbo for documentation and explanations
- Use Codex for security analysis
- Use GPT-4 Turbo for brainstorming and architecture

---

## Integration with Workflow

### Pre-Commit

```bash
# Before committing
@code-reviewer Review these changes
@security-audit Check for vulnerabilities
```

### During Development

```bash
# While coding
@api-dev Create a new endpoint for X
@test-generator Generate tests for Y
@doc-writer Document feature Z
```

### Code Review

```bash
# During PR review
@code-reviewer Analyze this PR
@security-audit Review security implications
@perf-optimizer Check for performance issues
```

---

**Last Updated:** 2026-01-17
**Compatible With:** GitHub Copilot Chat, GitHub Copilot Workspace
**Model:** GPT-5.2 Codex
