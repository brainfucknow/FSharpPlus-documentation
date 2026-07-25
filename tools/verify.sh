#!/usr/bin/env bash
# Run the same checks CI runs, locally.
#
#   ./tools/verify.sh              # clone/reuse upstream at the pinned SHA, run everything
#   ./tools/verify.sh --no-dotnet  # skip compile+execute (for environments without an SDK)
#
# The claim checks need only Python and a git clone. The snippet checks need a .NET 8 SDK and
# NuGet access; if you do not have those, --no-dotnet still verifies citations, API claims and
# snippet structure.
set -euo pipefail

cd "$(dirname "$0")/.."

RUN_DOTNET=1
[[ "${1:-}" == "--no-dotnet" ]] && RUN_DOTNET=0

SHA=$(python3 -c "import json;print(json.load(open('upstream.json'))['sha'])")
REPO_URL=$(python3 -c "import json;print(json.load(open('upstream.json'))['repo'])")
UPSTREAM=${UPSTREAM_DIR:-.upstream}

if [[ ! -d "$UPSTREAM/.git" ]]; then
  echo "==> cloning $REPO_URL into $UPSTREAM"
  git clone --quiet --filter=blob:none "$REPO_URL" "$UPSTREAM"
fi

if [[ "$(git -C "$UPSTREAM" rev-parse HEAD)" != "$SHA" ]]; then
  echo "==> checking out pinned $SHA"
  git -C "$UPSTREAM" fetch --quiet origin "$SHA" || git -C "$UPSTREAM" fetch --quiet origin
  git -C "$UPSTREAM" checkout --quiet "$SHA"
fi

echo
echo "==> citations"
python3 tools/check_citations.py --upstream "$UPSTREAM"

echo
echo "==> API presence/absence"
python3 tools/check_absence.py --upstream "$UPSTREAM"

echo
echo "==> snippet lint"
python3 tools/extract_snippets.py --lint-only

if [[ "$RUN_DOTNET" == "0" ]]; then
  echo
  echo "==> skipping compile+execute (--no-dotnet)"
  exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo
  echo "==> dotnet not found; skipping compile+execute."
  echo "    Claim checks passed. Re-run with an SDK, or let CI do it."
  exit 0
fi

echo
echo "==> generating snippet project"
python3 tools/extract_snippets.py --out build/verify

echo
echo "==> compiling"
dotnet build build/verify/verify.fsproj -c Release --nologo

echo
echo "==> executing value assertions"
dotnet run --project build/verify/verify.fsproj -c Release --no-build
