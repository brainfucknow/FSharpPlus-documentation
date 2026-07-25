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

echo
echo "==> exported corpus freshness"
python3 tools/export_corpus.py --check

echo
echo "==> coverage gap"
# Captured rather than piped to head: under `set -o pipefail` a closing head sends SIGPIPE
# and the whole script would report failure on a passing check.
gap_out=$(python3 tools/coverage_gap.py --upstream "$UPSTREAM" \
  --covered-by reference/generic-functions.md --max-gap 53)
printf '%s\n' "$gap_out" | sed -n '1,3p'

if [[ "$RUN_DOTNET" == "0" ]]; then
  echo
  echo "==> skipping compile+execute (--no-dotnet)"
  exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo
  echo "==> dotnet not found; skipping compile+execute. Claim checks passed."
  echo
  echo "    In a cloud/remote container the usual installers are typically blocked by the"
  echo "    egress proxy - dot.net, builds.dotnet.microsoft.com, aka.ms and"
  echo "    dotnetcli.azureedge.net all 403 on CONNECT, so dotnet-install.sh cannot work."
  echo "    Do not fight the proxy; the Ubuntu 24.04 archive carries the SDK:"
  echo
  echo "        apt-get update                     # refresh first or the .deb URLs 404"
  echo "        apt-get install -y dotnet-sdk-10.0"
  echo
  echo "    The generated project targets net8.0 but sets RollForward=LatestMajor, so a"
  echo "    newer-major runtime is fine. See the 'Running the checks locally' section of"
  echo "    README.md."
  exit 0
fi

echo
echo "==> generating snippet project (+ FSI script)"
python3 tools/extract_snippets.py --out build/verify --fsi

echo
echo "==> compiling"
dotnet build build/verify/verify.fsproj -c Release --nologo

echo
echo "==> executing value assertions (project target)"
dotnet run --project build/verify/verify.fsproj -c Release --no-build

# Second target. Upstream PR #372 showed FSI and project compilation disagree on SRTP-heavy
# code, and FSI is how a user actually pastes a snippet, so passing one is not passing both.
echo
echo "==> executing value assertions (dotnet fsi target)"
dotnet fsi --nologo build/verify/verify.fsx
