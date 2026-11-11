#!/usr/bin/env bash
# Sets up Claude's workspace: loads the .NET SDK and the NuGet packages from the release
# "claude-werkzeuge", registers the offline package source, writes the environment variables.
# Usage: tools/claude/setup-workspace.sh [path-to-the-original-files]
set -euo pipefail

REPO="${REPO:-kaaasi/bomb-redone}"
TAG="claude-werkzeuge"
BASE="${CLAUDE_TOOLS:-$HOME/claude-tools}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
mkdir -p "$BASE"

download() {
  local name="$1" id
  id=$(gh api "repos/$REPO/releases/tags/$TAG" -q ".assets[] | select(.name==\"$name\") | .id")
  [ -n "$id" ] || { echo "Datei $name fehlt im Release $TAG – Workflow 'Werkzeuge für Claude' starten." >&2; exit 1; }
  curl -fsSL -H "Authorization: Bearer $(gh auth token)" -H "Accept: application/octet-stream" \
    -o "$BASE/$name" "https://api.github.com/repos/$REPO/releases/assets/$id"
}

if [ ! -x "$BASE/dotnet/dotnet" ]; then
  echo "Lade .NET-SDK …"
  download dotnet-sdk-linux-x64.tar.gz
  mkdir -p "$BASE/dotnet" && tar -xzf "$BASE/dotnet-sdk-linux-x64.tar.gz" -C "$BASE/dotnet"
  rm -f "$BASE/dotnet-sdk-linux-x64.tar.gz"
fi

echo "Lade NuGet-Pakete …"
download nuget-packages.tar.gz
rm -rf "$BASE/nuget-packages" && tar -xzf "$BASE/nuget-packages.tar.gz" -C "$BASE" && rm -f "$BASE/nuget-packages.tar.gz"

# Use the offline source only (nuget.org cannot be reached from the workspace)
mkdir -p "$HOME/.nuget/NuGet"
cat > "$HOME/.nuget/NuGet/NuGet.Config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="offline" value="$BASE/nuget-packages" />
  </packageSources>
</configuration>
XML

cat > "$BASE/env.sh" <<ENV
export DOTNET_ROOT="$BASE/dotnet"
export PATH="$BASE/dotnet:\$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV

if [ $# -ge 1 ]; then
  ln -sfn "$(cd "$1" && pwd)" "$ROOT/gamedata"
  echo "Originaldateien: $ROOT/gamedata -> $1"
fi

echo "Fertig. Vor dem Bauen: source $BASE/env.sh"
