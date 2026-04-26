#!/usr/bin/env bash
# Cross-platform release script for Outer Wilds Head Tracking mod.
# Mirrors release.ps1 functionality using bash + python3 for JSON handling.
#
# Usage: pixi run release [version]
# Example: pixi run release 1.0.0
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
MANIFEST_PATH="$PROJECT_DIR/manifest.json"
CHANGELOG_PATH="$PROJECT_DIR/CHANGELOG.md"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
GRAY='\033[0;37m'
NC='\033[0m'

get_current_version() {
    python3 -c "import json; print(json.load(open('$MANIFEST_PATH'))['version'])"
}

update_manifest_version() {
    local version="$1"
    python3 -c "
import json
with open('$MANIFEST_PATH', 'r') as f:
    m = json.load(f)
m['version'] = '$version'
with open('$MANIFEST_PATH', 'w') as f:
    json.dump(m, f, indent=4)
    f.write('\n')
"
}

generate_changelog() {
    local version="$1"
    local date
    date=$(date +%Y-%m-%d)

    # Check if entry already exists
    if grep -q "\[$version\]" "$CHANGELOG_PATH"; then
        echo -e "${GRAY}CHANGELOG.md already has entry for v$version${NC}"
        return
    fi

    # Get commits since last tag
    local last_tag
    last_tag=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
    local commit_range
    if [ -n "$last_tag" ]; then
        commit_range="$last_tag..HEAD"
    else
        commit_range="HEAD"
    fi

    local commits
    commits=$(git log "$commit_range" --pretty=format:"%s" --reverse -- src/ 2>/dev/null || echo "Initial release")

    # Categorize commits
    local features="" fixes="" changes="" other=""
    while IFS= read -r commit; do
        [ -z "$commit" ] && continue
        if [[ "$commit" =~ ^feat(\(.*\))?:\ (.+)$ ]]; then
            features="${features}- ${BASH_REMATCH[2]}\n"
        elif [[ "$commit" =~ ^fix(\(.*\))?:\ (.+)$ ]]; then
            fixes="${fixes}- ${BASH_REMATCH[2]}\n"
        elif [[ "$commit" =~ ^(chore|refactor|perf|docs)(\(.*\))?:\ (.+)$ ]]; then
            changes="${changes}- ${BASH_REMATCH[3]}\n"
        else
            other="${other}- $commit\n"
        fi
    done <<< "$commits"

    # Build new entry
    local entry="\n## [$version] - $date\n\n"

    if [ -n "$features" ]; then
        entry+="### Added\n\n${features}\n"
    fi
    if [ -n "$changes" ]; then
        entry+="### Changed\n\n${changes}\n"
    fi
    if [ -n "$fixes" ]; then
        entry+="### Fixed\n\n${fixes}\n"
    fi
    # Include uncategorized only if nothing else was categorized
    if [ -z "$features" ] && [ -z "$changes" ] && [ -z "$fixes" ] && [ -n "$other" ]; then
        entry+="${other}\n"
    fi

    # Insert after "# Changelog" header
    python3 -c "
import re
with open('$CHANGELOG_PATH', 'r') as f:
    content = f.read()
entry = '''$(echo -e "$entry")'''
# Insert after the first heading line
content = re.sub(r'(# Changelog\n)', r'\1' + entry, content, count=1)
with open('$CHANGELOG_PATH', 'w') as f:
    f.write(content)
"
    echo -e "${GREEN}CHANGELOG.md generated from commits${NC}"
}

# --- Main ---

echo -e "${CYAN}=== Outer Wilds Head Tracking Release ===${NC}"
echo ""

CURRENT_VERSION=$(get_current_version)
VERSION="${1:-}"

# If no version provided, show current and exit
if [ -z "$VERSION" ]; then
    echo -e "${YELLOW}Current version: ${WHITE}$CURRENT_VERSION${NC}"
    echo ""
    echo -e "${YELLOW}Usage: ${WHITE}pixi run release <major|minor|patch|X.Y.Z>${NC}"
    echo -e "${YELLOW}Example: ${WHITE}pixi run release patch${NC}"
    exit 0
fi

# Resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
ARG="$(echo "$VERSION" | tr '[:upper:]' '[:lower:]')"
if [[ "$ARG" == "major" || "$ARG" == "minor" || "$ARG" == "patch" ]]; then
    CORE="${CURRENT_VERSION%%-*}"
    CORE="${CORE%%+*}"
    if [[ ! "$CORE" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
        echo -e "${RED}Error: Cannot bump '$ARG': current version '$CURRENT_VERSION' is not in X.Y.Z form${NC}"
        exit 1
    fi
    MAJ="${BASH_REMATCH[1]}"; MIN="${BASH_REMATCH[2]}"; PAT="${BASH_REMATCH[3]}"
    case "$ARG" in
        major) VERSION="$((MAJ + 1)).0.0" ;;
        minor) VERSION="${MAJ}.$((MIN + 1)).0" ;;
        patch) VERSION="${MAJ}.${MIN}.$((PAT + 1))" ;;
    esac
elif ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}Error: Invalid version '$VERSION'${NC}"
    echo -e "${YELLOW}Use 'major', 'minor', 'patch', or X.Y.Z (e.g., 1.0.0, 1.2.3)${NC}"
    exit 1
fi

# Check if we're on main branch
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ "$CURRENT_BRANCH" != "main" ]; then
    echo -e "${RED}Error: Must be on 'main' branch to release (currently on '$CURRENT_BRANCH')${NC}"
    exit 1
fi

# Check for uncommitted changes
if [ -n "$(git status --porcelain)" ]; then
    echo -e "${RED}Error: Working directory has uncommitted changes${NC}"
    echo -e "${YELLOW}Please commit or stash changes before releasing${NC}"
    exit 1
fi

# Check if tag already exists
TAG_NAME="v$VERSION"
if git tag -l "$TAG_NAME" | grep -q "$TAG_NAME"; then
    echo -e "${RED}Error: Tag '$TAG_NAME' already exists${NC}"
    exit 1
fi

echo -e "${GRAY}Current version: $CURRENT_VERSION${NC}"
echo -e "${GREEN}New version:     $VERSION${NC}"
echo ""

# Confirm
echo -e "${YELLOW}This will:${NC}"
echo -e "  1. Update version in manifest.json to $VERSION"
echo -e "  2. Generate CHANGELOG from commits"
echo -e "  3. Commit the change"
echo -e "  4. Create tag $TAG_NAME"
echo -e "  5. Push to GitHub (triggers release workflow)"
echo ""

read -rp "Continue? (y/N) " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    echo -e "${YELLOW}Cancelled${NC}"
    exit 0
fi

echo ""

# Step 1: Update version in manifest.json
echo -e "${CYAN}Updating manifest.json version to $VERSION...${NC}"
update_manifest_version "$VERSION"

# Step 2: Generate CHANGELOG
echo -e "${CYAN}Generating CHANGELOG from commits...${NC}"
generate_changelog "$VERSION"

# Step 3: Commit
echo -e "${CYAN}Committing release...${NC}"
git add "$MANIFEST_PATH" "$CHANGELOG_PATH"
git commit -m "Release v$VERSION"

# Step 4: Create tag
echo -e "${CYAN}Creating tag $TAG_NAME...${NC}"
git tag "$TAG_NAME"

# Step 5: Push
echo -e "${CYAN}Pushing to GitHub...${NC}"
git push origin main
git push origin "$TAG_NAME"

echo ""
echo -e "${GREEN}Release $TAG_NAME initiated!${NC}"
echo ""
echo -e "${YELLOW}The GitHub Actions release workflow will now:${NC}"
echo -e "  - Build the release"
echo -e "  - Create GitHub release with artifacts"
echo ""
echo -e "${YELLOW}Watch progress at:${NC}"
echo -e "${CYAN}  https://github.com/itsloopyo/outer-wilds-headtracking/actions${NC}"
