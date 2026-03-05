#!/usr/bin/env bash
set -euo pipefail

# ── Smart Test Runner ──────────────────────────────────────────
# Runs only test projects affected by staged changes.
# Skips tests entirely for docs/config-only commits.
# Runs ALL tests if global-impact files changed.

STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACMR)

if [ -z "$STAGED_FILES" ]; then
  echo "No staged files — skipping tests."
  exit 0
fi

# Global-impact files → run all tests
GLOBAL_PATTERNS=(
  "Directory.Build.props"
  "Directory.Build.targets"
  "Directory.Packages.props"
  ".editorconfig"
  "JD.AI.slnx"
  "global.json"
)

RUN_ALL=false
for pattern in "${GLOBAL_PATTERNS[@]}"; do
  if echo "$STAGED_FILES" | grep -q "$pattern"; then
    RUN_ALL=true
    break
  fi
done

if [ "$RUN_ALL" = true ]; then
  echo "Global-impact file changed — running all tests."
  dotnet test JD.AI.slnx \
    --configuration Release \
    --no-build \
    --filter "Category!=Integration" \
    --collect:"XPlat Code Coverage" \
    -- \
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
  exit $?
fi

# Extract changed src/ project directories
CHANGED_SRC_PROJECTS=$(
  echo "$STAGED_FILES" \
    | grep '^src/' \
    | sed 's|^src/\([^/]*\)/.*|\1|' \
    | sort -u \
    || true
)

# Extract changed test project directories (run directly)
CHANGED_TEST_PROJECTS=$(
  echo "$STAGED_FILES" \
    | grep '^tests/' \
    | sed 's|^tests/\([^/]*\)/.*|\1|' \
    | sort -u \
    || true
)

if [ -z "$CHANGED_SRC_PROJECTS" ] && [ -z "$CHANGED_TEST_PROJECTS" ]; then
  echo "No src/ or tests/ changes — skipping tests."
  exit 0
fi

# Build list of test projects to run
TEST_PROJECTS=()

# Map src projects → test projects via ProjectReference
for src_proj in $CHANGED_SRC_PROJECTS; do
  # Find test .csproj files referencing this src project
  MATCHING=$(
    grep -rl "src[/\\\\]${src_proj}[/\\\\]" tests/*/*.csproj 2>/dev/null \
      || true
  )
  for csproj in $MATCHING; do
    TEST_PROJECTS+=("$csproj")
  done
done

# Add directly changed test projects
for test_proj in $CHANGED_TEST_PROJECTS; do
  CSPROJ="tests/${test_proj}/${test_proj}.csproj"
  if [ -f "$CSPROJ" ]; then
    TEST_PROJECTS+=("$CSPROJ")
  fi
done

# Deduplicate
if [ ${#TEST_PROJECTS[@]} -eq 0 ]; then
  echo "No matching test projects — skipping tests."
  exit 0
fi

UNIQUE_PROJECTS=$(
  printf '%s\n' "${TEST_PROJECTS[@]}" \
    | sort -u
)

if [ -z "$UNIQUE_PROJECTS" ]; then
  echo "No matching test projects — skipping tests."
  exit 0
fi

echo "Running targeted tests:"
echo "$UNIQUE_PROJECTS"
echo ""

FAILED=0
for proj in $UNIQUE_PROJECTS; do
  # Skip UI test project (requires Playwright)
  if [[ "$proj" == *"Specs.UI"* ]]; then
    echo "Skipping UI test project: $proj"
    continue
  fi

  echo "Testing: $proj"
  dotnet test "$proj" \
    --configuration Release \
    --no-build \
    --filter "Category!=Integration" \
    --collect:"XPlat Code Coverage" \
    -- \
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
    || FAILED=1
done

# Coverage summary (best-effort, don't block commit)
REPORTS=$(find . -type f -path "*/TestResults/*/coverage.cobertura.xml" \
  -newer .husky/pre-commit 2>/dev/null | tr '\n' ';')
if [ -n "$REPORTS" ] && command -v reportgenerator &>/dev/null; then
  reportgenerator \
    -reports:"$REPORTS" \
    -targetdir:"coverage-report" \
    -reporttypes:"TextSummary" \
    -assemblyfilters:"+JD.AI;-*Tests*" \
    2>/dev/null || true
  if [ -f "coverage-report/Summary.txt" ]; then
    echo ""
    echo "── Coverage Summary ──"
    cat coverage-report/Summary.txt
  fi
fi

exit $FAILED
