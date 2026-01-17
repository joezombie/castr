# GitHub Configuration

This directory contains GitHub-specific configuration files for Copilot, workflows, and project settings.

## Files

### GitHub Copilot Workflow

#### `assign-to-copilot.sh`
Shell script for assigning GitHub issues to Copilot coding agent. Features:
- Automatic issue context fetching
- Custom agent support
- Real-time log following
- Additional instruction support
- Multi-repository support

**Quick Start:**
```bash
.github/assign-to-copilot.sh 4              # Assign issue #4
.github/assign-to-copilot.sh 4 --follow     # Assign and follow logs
.github/assign-to-copilot.sh 4 --agent api-dev --follow  # With custom agent
```

See `COPILOT_WORKFLOW.md` for complete documentation.

#### `COPILOT_WORKFLOW.md`
Comprehensive workflow guide covering:
- Prerequisites and setup
- Usage examples for all issue types
- Agent selection guide
- Monitoring and troubleshooting
- Best practices
- CI/CD integration

### GitHub Copilot Configuration

#### `copilot-instructions.md`
Primary instructions file for GitHub Copilot. Contains:
- Project architecture overview
- Coding standards for C# and Python
- Security requirements and checklists
- Common patterns and anti-patterns
- File locations and structure
- Known issues and improvements

This file is automatically read by GitHub Copilot to provide context-aware suggestions.

#### `copilot-settings.json`
JSON configuration for GitHub Copilot preferences:
- Model selection (GPT-5.2 Codex)
- Context files to include
- Coding standard preferences
- Security requirements
- Suggestion priorities

#### `COPILOT_AGENTS.md`
Defines 10 specialized Copilot agents for different tasks:
1. **code-reviewer** - Security and style review
2. **test-generator** - Test creation
3. **doc-writer** - Documentation
4. **api-dev** - API endpoint development
5. **db-dev** - Database operations
6. **youtube-dev** - YouTube integration
7. **python-dev** - Python script development
8. **security-audit** - Security-focused review
9. **perf-optimizer** - Performance optimization
10. **devops** - Docker and deployment

Each agent has specific instructions, templates, and example usage.

## Usage

### GitHub Copilot

GitHub Copilot will automatically use these instructions when:
- Generating code suggestions
- Completing code
- Providing explanations
- Reviewing code

### Invoking Agents

In GitHub Copilot Chat, invoke agents with:
```
@agent-name Your request here
```

Examples:
```
@code-reviewer Review this endpoint for security issues
@test-generator Generate tests for GetMedia method
@api-dev Create a new endpoint to list episodes by date
@security-audit Check for vulnerabilities in this file
```

### Updating Configuration

When updating these files:
1. Modify the relevant configuration file
2. Test with Copilot to ensure it's working as expected
3. Commit changes with descriptive message
4. Update this README if adding new files

## Best Practices

1. **Keep Instructions Updated** - Update `copilot-instructions.md` when:
   - Architecture changes
   - New patterns are adopted
   - Security requirements change
   - Known issues are resolved

2. **Use Agents Appropriately** - Different agents for different tasks:
   - Use `@code-reviewer` before committing
   - Use `@security-audit` for sensitive changes
   - Use `@test-generator` after implementing features
   - Use `@doc-writer` for documentation updates

3. **Provide Context** - Give agents enough context:
   ```
   @api-dev Create an endpoint to export episodes as JSON
   Following the patterns in FeedController.cs
   Include input validation and logging
   ```

4. **Chain Agents** - For complex tasks:
   ```
   @api-dev Create the endpoint
   @test-generator Generate tests
   @doc-writer Document the new endpoint
   ```

## Model Configuration

**Primary Model:** GPT-5.2 Codex
- Used for code generation and review
- Best for security analysis
- Optimal for pattern matching

**Context Sources:**
- `.github/copilot-instructions.md` (always included)
- `CLAUDE.md` (project overview)
- `CODE_REVIEW.md` (review findings)
- `RECOMMENDATIONS.md` (improvement suggestions)

## Maintenance

### Regular Updates

- **Weekly:** Review and update if architecture changes
- **After Major Changes:** Update patterns and examples
- **After Security Reviews:** Update security checklist
- **After Refactoring:** Update code examples

### Testing Configuration

Test Copilot configuration by:
1. Asking for code generation in various scenarios
2. Requesting code reviews
3. Generating tests
4. Checking if suggestions follow standards

## Resources

- [GitHub Copilot Documentation](https://docs.github.com/en/copilot)
- [Copilot Chat Documentation](https://docs.github.com/en/copilot/using-github-copilot/asking-github-copilot-questions-in-your-ide)
- Project Documentation: `../CLAUDE.md`
- Code Review: `../CODE_REVIEW.md`
- Recommendations: `../RECOMMENDATIONS.md`

---

**Last Updated:** 2026-01-17
**Configuration Version:** 1.0
**Target Model:** GPT-5.2 Codex
