#!/usr/bin/env bash
# Run the same checks CI runs, locally.
#
#   ./tools/verify.sh              # clone/reuse upstream at the pinned SHA, run everything
#   ./tools/verify.sh --no-dotnet  # not supported any more; see the note below
#
# The checks live in tools/Verify, an F# project, so a .NET SDK is required for all of them - not
# only for compiling the snippets. That is a deliberate trade: the tooling is now in the same
# language as the thing it documents, and the claim data in tools/Verify/Data.fs is checked by the
# compiler instead of parsed from JSON at runtime.
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ "${1:-}" == "--no-dotnet" ]]; then
  cat <<'EOF'
--no-dotnet is no longer available: the checks themselves are an F# project now, so there is no
Python fallback to fall back to.

In a cloud container the usual .NET installers are typically blocked by the egress proxy - dot.net,
builds.dotnet.microsoft.com, aka.ms and dotnetcli.azureedge.net all 403 on CONNECT, so
dotnet-install.sh cannot work. Do not fight the proxy; the Ubuntu 24.04 archive carries the SDK:

    apt-get update                     # refresh first or the .deb URLs 404
    apt-get install -y dotnet-sdk-8.0  # matches upstream's global.json pin

See the "Running the checks locally" section of README.md.
EOF
  exit 2
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found. Install the SDK first:" >&2
  echo "    apt-get update && apt-get install -y dotnet-sdk-8.0" >&2
  echo "See the 'Running the checks locally' section of README.md." >&2
  exit 2
fi

# Read the pin with grep rather than a JSON parser: this runs before the tool is built, and pulling
# in an interpreter just to read two fields would reintroduce the dependency this script shed.
SHA=$(grep -o '"sha"[[:space:]]*:[[:space:]]*"[0-9a-f]\{40\}"' upstream.json | grep -o '[0-9a-f]\{40\}')
REPO_URL=$(grep -o '"repo"[[:space:]]*:[[:space:]]*"[^"]*"' upstream.json | sed 's/.*"\(https[^"]*\)"/\1/')
UPSTREAM=${UPSTREAM_DIR:-.upstream}

# Ask git rather than testing for a .git directory: in a linked worktree .git is a FILE, so a
# directory test wrongly concludes there is no repo and then fails trying to clone over it.
if ! git -C "$UPSTREAM" rev-parse --git-dir >/dev/null 2>&1; then
  echo "==> cloning $REPO_URL into $UPSTREAM"
  git clone --quiet --filter=blob:none "$REPO_URL" "$UPSTREAM"
fi

if [[ "$(git -C "$UPSTREAM" rev-parse HEAD)" != "$SHA" ]]; then
  echo "==> checking out pinned $SHA"
  git -C "$UPSTREAM" fetch --quiet origin "$SHA" || git -C "$UPSTREAM" fetch --quiet origin
  git -C "$UPSTREAM" checkout --quiet "$SHA"
fi

echo
echo "==> building the verification tool"
dotnet build tools/Verify/Verify.fsproj -c Release --nologo -v quiet
VERIFY="dotnet tools/Verify/bin/Release/net8.0/verify.dll"

echo
$VERIFY claims --upstream "$UPSTREAM"

echo
echo "==> coverage gap"
# Captured rather than piped to head: under `set -o pipefail` a closing head sends SIGPIPE and the
# whole script would report failure on a passing check.
gap_out=$($VERIFY gap --upstream "$UPSTREAM" \
  --covered-by reference/generic-functions.md --max-gap 53)
printf '%s\n' "$gap_out" | sed -n '1,3p'

echo
echo "==> generating snippet project (+ FSI script)"
$VERIFY snippets --out build/verify --fsi

echo
echo "==> compiling"
dotnet build build/verify/verify.fsproj -c Release --nologo

echo
echo "==> executing value assertions (project target)"
dotnet run --project build/verify/verify.fsproj -c Release --no-build

# Second target. Upstream PR #372 showed FSI and project compilation disagree on SRTP-heavy code,
# and FSI is how a user actually pastes a snippet, so passing one is not passing both.
echo
echo "==> executing value assertions (dotnet fsi target)"
dotnet fsi --nologo build/verify/verify.fsx
