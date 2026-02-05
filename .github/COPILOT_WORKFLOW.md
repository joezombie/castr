# GitHub Copilot Workflow Guide

Complete guide for using GitHub Copilot to automate issue resolution in this project.

---

## Quick Start

### Assign an Issue to Copilot

```bash
# Simple assignment
.github/assign-to-copilot.sh 4

# With custom agent
.github/assign-to-copilot.sh 4 --agent api-dev --follow

# Non-interactive mode
.github/assign-to-copilot.sh 5 --no-confirm --follow
```

---

## Prerequisites

### 1. GitHub CLI Installation

**Minimum Version:** 2.80.0 (current: 2.85.0)

```bash
# Check version
gh --version

# Upgrade if needed (macOS)
brew upgrade gh

# Upgrade (Linux)
sudo apt update && sudo apt upgrade gh
```

### 2. Authentication

```bash
# Login to GitHub
gh auth login

# Verify authentication
gh auth status
```

### 3. GitHub Copilot Subscription

- Required: GitHub Copilot Individual or Business subscription
- Verify at: https://github.com/settings/copilot

---

## assign-to-copilot.sh Script

### Features

- ✅ Fetches full issue context automatically
- ✅ Supports custom agents from `.github/COPILOT_AGENTS.md`
- ✅ Includes project coding standards reference
- ✅ Creates properly formatted task descriptions
- ✅ Supports multiple repositories
- ✅ Real-time log following
- ✅ Custom base branch selection
- ✅ Additional instruction support

### Usage

```bash
.github/assign-to-copilot.sh [OPTIONS] ISSUE_NUMBER
```

### Options

| Flag | Description | Example |
|------|-------------|---------|
| `-h, --help` | Show help message | `--help` |
| `-b, --base BRANCH` | Base branch for PR | `--base develop` |
| `-a, --agent AGENT` | Custom agent name | `--agent api-dev` |
| `-f, --follow` | Follow logs in real-time | `--follow` |
| `-R, --repo REPO` | Repository (OWNER/REPO) | `--repo OWNER/REPO` |
| `-i, --instructions` | Additional instructions | `--instructions "Use async/await"` |
| `--no-confirm` | Skip confirmation | `--no-confirm` |

### Available Agents

| Agent | Purpose | Use For |
|-------|---------|---------|
| `code-reviewer` | Code quality review | Security fixes, refactoring |
| `test-generator` | Test creation | Adding test coverage |
| `api-dev` | API development | Endpoint creation/modification |
| `db-dev` | Database operations | Schema changes, migrations |
| `youtube-dev` | YouTube features | YouTube integration tasks |
| `python-dev` | Python scripts | Python script modifications |
| `security-audit` | Security fixes | Vulnerability remediation |
| `perf-optimizer` | Performance | Optimization tasks |
| `doc-writer` | Documentation | Documentation updates |
| `devops` | Docker/deployment | Infrastructure changes |

---

## Examples

### Example 1: Input Validation (Issue #4)

```bash
# Assign with API development agent
.github/assign-to-copilot.sh 4 --agent api-dev --follow
```

**What happens:**
1. Fetches issue #4 details
2. Creates task with API-specific context
3. Copilot creates a draft PR with input validation
4. Follows logs to show real-time progress

### Example 2: Add Health Check (Issue #7)

```bash
# Simple assignment, follow logs
.github/assign-to-copilot.sh 7 --follow
```

**What happens:**
1. Fetches issue #7 (health check endpoint)
2. Creates task with project coding standards
3. Copilot generates health check implementation
4. Opens draft PR for review

### Example 3: Python Tests (Issue #14)

```bash
# Use Python development agent
.github/assign-to-copilot.sh 14 --agent python-dev --follow
```

**What happens:**
1. Fetches issue #14 (Python unit tests)
2. Uses `python-dev` agent context
3. Copilot generates unittest test cases
4. Creates PR with comprehensive tests

### Example 4: Security Fix with Custom Instructions

```bash
# Security audit with specific requirements
.github/assign-to-copilot.sh 8 \
  --agent security-audit \
  --instructions "Add timeout of 30 seconds and log at Debug level" \
  --follow
```

**What happens:**
1. Fetches issue #8 (database lock timeout)
2. Uses security-focused agent
3. Includes custom timeout requirement
4. Copilot implements with security best practices

### Example 5: Batch Assignment

```bash
# Assign multiple issues (non-interactive)
for issue in 4 5 6 7 8; do
  .github/assign-to-copilot.sh $issue --no-confirm
done
```

---

## Workflow Integration

### Pre-Assignment Checklist

Before assigning an issue:

1. ✅ Issue has clear description
2. ✅ Requirements are well-defined
3. ✅ Acceptance criteria listed
4. ✅ Relevant labels applied
5. ✅ Related files/code identified

### During Processing

Monitor the agent's progress:

```bash
# List all agent tasks
gh agent-task list

# View specific task
gh agent-task view ISSUE_NUMBER

# View by session ID
gh agent-task view SESSION_ID
```

### After Completion

When Copilot creates a draft PR:

1. **Review the changes**
   ```bash
   gh pr view PR_NUMBER --web
   ```

2. **Test locally**
   ```bash
   gh pr checkout PR_NUMBER
   cd Castr && dotnet build
   dotnet test  # if tests exist
   ```

3. **Request changes or approve**
   - Add review comments
   - Request specific modifications
   - Approve and merge when ready

---

## Advanced Usage

### Custom Agents

Create specialized agents in `.github/agents/`:

```bash
# Create custom agent
mkdir -p .github/agents
cat > .github/agents/my-agent.md << 'EOF'
# My Custom Agent

You are a specialized agent for [specific task].

## Context
- Project uses .NET 10 and SQLite
- Follow patterns in Services/

## Instructions
- Always add logging
- Use async/await
- Include XML documentation comments
EOF

# Use custom agent
.github/assign-to-copilot.sh 10 --agent my-agent
```

### Complex Task Descriptions

For complex issues, create a detailed task file:

```bash
# Create task description
cat > task.md << 'EOF'
# Implement Feature X

## Requirements
1. Add new endpoint
2. Include validation
3. Add database migration
4. Write unit tests

## Constraints
- Must maintain backwards compatibility
- Follow existing patterns in FeedController.cs

## References
- See CODE_REVIEW.md section #10
- Similar implementation in line 123
EOF

# Create agent task from file
gh agent-task create -F task.md --agent api-dev --follow
```

### Multi-Repository Management

Work across multiple repositories:

```bash
# Assign issue in different repo
.github/assign-to-copilot.sh 42 \
  --repo other-org/other-repo \
  --agent api-dev
```

---

## Monitoring & Troubleshooting

### Check Agent Status

```bash
# List recent tasks
gh agent-task list

# View task details with logs
gh agent-task view ISSUE_NUMBER

# View specific session
gh agent-task view SESSION_ID
```

### Common Issues

#### Issue: "gh agent-task: command not found"

**Solution:** Upgrade gh CLI to version 2.80.0 or later

```bash
gh --version
brew upgrade gh  # macOS
```

#### Issue: "Authentication failed"

**Solution:** Re-authenticate with GitHub

```bash
gh auth logout
gh auth login
```

#### Issue: "Could not fetch issue"

**Solution:** Check issue number and repository access

```bash
# Verify issue exists
gh issue view ISSUE_NUMBER

# Check repo access
gh repo view
```

#### Issue: "Agent definition not found"

**Solution:** Create agent file in `.github/agents/`

```bash
# Check if agent file exists
ls -l .github/agents/

# Create if missing
cp .github/COPILOT_AGENTS.md .github/agents/api-dev.md
```

### Debug Mode

Enable verbose output:

```bash
# Set debug environment variable
export GH_DEBUG=api

# Run assignment
.github/assign-to-copilot.sh 4 --follow

# Unset when done
unset GH_DEBUG
```

---

## Best Practices

### 1. Clear Issue Descriptions

Write issues that Copilot can understand:

**Good:**
```markdown
## Problem
The GetMedia endpoint doesn't validate input, allowing potential path traversal.

## Solution
Add validation:
- Check for null/empty feedName and fileName
- Reject if length > 255
- Block path traversal characters (., /, \)

## Files
- Castr/Controllers/FeedController.cs (line 73)

## References
- CODE_REVIEW.md: Issue #10
```

**Bad:**
```markdown
fix the thing
```

### 2. Choose Appropriate Agents

Match agent to task type:

- **API changes** → `api-dev`
- **Security fixes** → `security-audit`
- **Database work** → `db-dev`
- **Tests** → `test-generator`
- **Documentation** → `doc-writer`

### 3. Provide Context

Include references to existing code:

```bash
.github/assign-to-copilot.sh 4 \
  --instructions "Follow the pattern in GetFeed() method at line 42. Use similar validation and logging."
```

### 4. Follow Logs for Complex Tasks

Use `--follow` for multi-file changes:

```bash
.github/assign-to-copilot.sh 15 --agent test-generator --follow
```

### 5. Review Before Merging

Always review Copilot's changes:

1. Check for security issues
2. Verify coding standards
3. Test functionality
4. Review comments and documentation

---

## Integration with CI/CD

### Automated Testing

When Copilot creates a PR, CI can run automatically:

```yaml
# .github/workflows/copilot-pr-check.yml
name: Copilot PR Check

on:
  pull_request:
    types: [opened, synchronize]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Build
        run: cd Castr && dotnet build
      - name: Test
        run: cd Castr && dotnet test
```

### Auto-Label PRs

Label Copilot-generated PRs:

```yaml
# If PR is from Copilot, add label
if: contains(github.event.pull_request.user.login, 'copilot')
run: gh pr edit ${{ github.event.pull_request.number }} --add-label "copilot-generated"
```

---

## API Alternative

For programmatic access, use the GraphQL API:

```bash
# Assign issue via GraphQL
gh api graphql -f query='
mutation {
  assignIssue(input: {
    issueId: "ISSUE_ID"
    assigneeIds: ["COPILOT_USER_ID"]
  }) {
    issue {
      id
      number
      assignees(first: 10) {
        nodes {
          login
        }
      }
    }
  }
}'
```

---

## Resources

### Documentation

- [GitHub Copilot Coding Agent](https://docs.github.com/en/copilot/using-github-copilot/coding-agent)
- [gh agent-task command](https://cli.github.com/manual/gh_agent-task)
- [Assign issues to Copilot via API](https://github.blog/changelog/2025-12-03-assign-issues-to-copilot-using-the-api/)

### Project-Specific

- `.github/copilot-instructions.md` - Copilot configuration
- `.github/COPILOT_AGENTS.md` - Agent definitions
- `CODE_REVIEW.md` - Review findings
- `RECOMMENDATIONS.md` - Implementation guides

### Support

- GitHub Issues: Check the repository's issue tracker
- Copilot Feedback: https://github.com/github/copilot-cli/issues

---

**Last Updated:** 2026-01-17
**Script Version:** 1.0
**Requires:** gh CLI 2.80.0+
