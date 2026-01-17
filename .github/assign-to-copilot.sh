#!/bin/bash
# GitHub Copilot Issue Assignment Script
# Assigns GitHub issues to Copilot coding agent for automated resolution

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Help text
show_help() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS] ISSUE_NUMBER

Assign a GitHub issue to Copilot coding agent for automated resolution.

OPTIONS:
    -h, --help              Show this help message
    -b, --base BRANCH       Base branch for the pull request (default: main)
    -a, --agent AGENT       Custom agent to use (from .github/COPILOT_AGENTS.md)
    -f, --follow            Follow agent session logs in real-time
    -R, --repo REPO         Repository in format OWNER/REPO (default: current repo)
    -i, --instructions      Additional custom instructions for the agent
    --no-confirm            Skip confirmation prompt

AGENT OPTIONS:
    code-reviewer           Review and improve code quality
    test-generator          Generate comprehensive tests
    api-dev                 Develop API endpoints
    db-dev                  Database operations and migrations
    youtube-dev             YouTube integration features
    python-dev              Python script development
    security-audit          Security-focused review and fixes
    perf-optimizer          Performance optimization
    doc-writer              Documentation updates
    devops                  Docker and deployment

EXAMPLES:
    # Assign issue #4 to Copilot (input validation)
    $(basename "$0") 4

    # Assign with custom agent and follow logs
    $(basename "$0") 4 --agent api-dev --follow

    # Assign to specific base branch
    $(basename "$0") 7 --base develop

    # Assign with additional instructions
    $(basename "$0") 10 --instructions "Follow the patterns in FeedController.cs"

    # Non-interactive mode
    $(basename "$0") 5 --no-confirm --follow

REQUIREMENTS:
    - gh CLI version 2.80.0 or later
    - GitHub Copilot subscription
    - Authenticated with 'gh auth login'

NOTES:
    - The script fetches issue details and creates a Copilot agent task
    - A draft PR will be created automatically
    - You can monitor progress with 'gh agent-task list'
    - Custom agents are defined in .github/COPILOT_AGENTS.md

LEARN MORE:
    - https://docs.github.com/en/copilot/using-github-copilot/coding-agent
    - https://github.blog/changelog/2025-09-25-kick-off-and-track-copilot-coding-agent-sessions-from-the-github-cli/

EOF
}

# Parse command line arguments
ISSUE_NUMBER=""
BASE_BRANCH=""
CUSTOM_AGENT=""
FOLLOW_LOGS=false
REPO=""
CUSTOM_INSTRUCTIONS=""
NO_CONFIRM=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_help
            exit 0
            ;;
        -b|--base)
            BASE_BRANCH="$2"
            shift 2
            ;;
        -a|--agent)
            CUSTOM_AGENT="$2"
            shift 2
            ;;
        -f|--follow)
            FOLLOW_LOGS=true
            shift
            ;;
        -R|--repo)
            REPO="$2"
            shift 2
            ;;
        -i|--instructions)
            CUSTOM_INSTRUCTIONS="$2"
            shift 2
            ;;
        --no-confirm)
            NO_CONFIRM=true
            shift
            ;;
        -*)
            echo -e "${RED}Error: Unknown option $1${NC}"
            show_help
            exit 1
            ;;
        *)
            if [ -z "$ISSUE_NUMBER" ]; then
                ISSUE_NUMBER="$1"
            else
                echo -e "${RED}Error: Multiple issue numbers specified${NC}"
                show_help
                exit 1
            fi
            shift
            ;;
    esac
done

# Validate issue number
if [ -z "$ISSUE_NUMBER" ]; then
    echo -e "${RED}Error: Issue number required${NC}"
    show_help
    exit 1
fi

# Validate issue number is numeric
if ! [[ "$ISSUE_NUMBER" =~ ^[0-9]+$ ]]; then
    echo -e "${RED}Error: Issue number must be numeric${NC}"
    exit 1
fi

# Check gh CLI version
GH_VERSION=$(gh --version | head -n1 | awk '{print $3}')
REQUIRED_VERSION="2.80.0"

version_compare() {
    if [[ "$1" == "$2" ]]; then
        return 0
    fi
    local IFS=.
    local i ver1=($1) ver2=($2)
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++)); do
        ver1[i]=0
    done
    for ((i=0; i<${#ver1[@]}; i++)); do
        if [[ -z ${ver2[i]} ]]; then
            ver2[i]=0
        fi
        if ((10#${ver1[i]} > 10#${ver2[i]})); then
            return 0
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]})); then
            return 1
        fi
    done
    return 0
}

if ! version_compare "$GH_VERSION" "$REQUIRED_VERSION"; then
    echo -e "${YELLOW}Warning: gh CLI version $GH_VERSION is older than recommended $REQUIRED_VERSION${NC}"
    echo -e "${YELLOW}Some features may not be available. Consider upgrading: brew upgrade gh${NC}"
fi

# Fetch issue details
echo -e "${BLUE}Fetching issue #${ISSUE_NUMBER}...${NC}"

REPO_FLAG=""
if [ -n "$REPO" ]; then
    REPO_FLAG="--repo $REPO"
fi

ISSUE_TITLE=$(gh issue view "$ISSUE_NUMBER" $REPO_FLAG --json title --jq '.title')
ISSUE_BODY=$(gh issue view "$ISSUE_NUMBER" $REPO_FLAG --json body --jq '.body // ""')
ISSUE_URL=$(gh issue view "$ISSUE_NUMBER" $REPO_FLAG --json url --jq '.url')
ISSUE_LABELS=$(gh issue view "$ISSUE_NUMBER" $REPO_FLAG --json labels --jq '.labels[].name' | tr '\n' ',' | sed 's/,$//')

if [ -z "$ISSUE_TITLE" ]; then
    echo -e "${RED}Error: Could not fetch issue #${ISSUE_NUMBER}${NC}"
    exit 1
fi

# Display issue information
echo -e "\n${GREEN}Issue #${ISSUE_NUMBER}: ${ISSUE_TITLE}${NC}"
echo -e "${BLUE}URL: ${ISSUE_URL}${NC}"
if [ -n "$ISSUE_LABELS" ]; then
    echo -e "${BLUE}Labels: ${ISSUE_LABELS}${NC}"
fi

# Show agent information if custom agent specified
if [ -n "$CUSTOM_AGENT" ]; then
    echo -e "${BLUE}Custom Agent: ${CUSTOM_AGENT}${NC}"

    # Check if agent definition exists
    AGENT_FILE=".github/agents/${CUSTOM_AGENT}.md"
    if [ ! -f "$AGENT_FILE" ]; then
        echo -e "${YELLOW}Warning: Agent definition not found at ${AGENT_FILE}${NC}"
        echo -e "${YELLOW}Make sure to create the agent file before running this task${NC}"
    fi
fi

# Build task description from issue
TASK_DESCRIPTION="Fix GitHub Issue #${ISSUE_NUMBER}: ${ISSUE_TITLE}

Issue URL: ${ISSUE_URL}

## Issue Description
${ISSUE_BODY}

## Requirements
- Address all points mentioned in the issue
- Follow coding standards in .github/copilot-instructions.md
- Add appropriate logging
- Include tests if applicable
- Update documentation if needed"

# Add custom instructions if provided
if [ -n "$CUSTOM_INSTRUCTIONS" ]; then
    TASK_DESCRIPTION="${TASK_DESCRIPTION}

## Additional Instructions
${CUSTOM_INSTRUCTIONS}"
fi

# Add reference to code review if issue is from code review
if echo "$ISSUE_LABELS" | grep -q "enhancement\|bug"; then
    TASK_DESCRIPTION="${TASK_DESCRIPTION}

## References
- See CODE_REVIEW.md for detailed context
- See RECOMMENDATIONS.md for implementation patterns
- Follow security checklist in .github/copilot-instructions.md"
fi

# Confirmation prompt
if [ "$NO_CONFIRM" = false ]; then
    echo -e "\n${YELLOW}Ready to assign issue #${ISSUE_NUMBER} to Copilot${NC}"
    [ -n "$BASE_BRANCH" ] && echo -e "${BLUE}Base branch: ${BASE_BRANCH}${NC}"
    [ -n "$CUSTOM_AGENT" ] && echo -e "${BLUE}Custom agent: ${CUSTOM_AGENT}${NC}"
    echo ""
    read -p "Continue? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Cancelled${NC}"
        exit 0
    fi
fi

# Build gh agent-task create command
TASK_FILE=$(mktemp)
echo "$TASK_DESCRIPTION" > "$TASK_FILE"

GH_CMD="gh agent-task create -F \"$TASK_FILE\""
[ -n "$BASE_BRANCH" ] && GH_CMD="$GH_CMD --base \"$BASE_BRANCH\""
[ -n "$CUSTOM_AGENT" ] && GH_CMD="$GH_CMD --custom-agent \"$CUSTOM_AGENT\""
[ "$FOLLOW_LOGS" = true ] && GH_CMD="$GH_CMD --follow"
[ -n "$REPO" ] && GH_CMD="$GH_CMD --repo \"$REPO\""

# Execute command
echo -e "\n${BLUE}Creating Copilot agent task...${NC}"
eval "$GH_CMD"

# Cleanup
rm -f "$TASK_FILE"

echo -e "\n${GREEN}✓ Successfully assigned issue #${ISSUE_NUMBER} to Copilot!${NC}"
echo -e "\n${BLUE}Next steps:${NC}"
echo -e "  - Monitor progress: ${YELLOW}gh agent-task list${NC}"
echo -e "  - View details: ${YELLOW}gh agent-task view ${ISSUE_NUMBER}${NC}"
echo -e "  - Check draft PR when ready"
echo ""
