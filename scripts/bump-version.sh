#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_FILE="$ROOT_DIR/ZV Player.csproj"

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Project file not found: $PROJECT_FILE" >&2
  exit 1
fi

current_version="$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$PROJECT_FILE" | head -n 1)"
if [[ -z "$current_version" ]]; then
  echo "Could not find <Version> in $PROJECT_FILE" >&2
  exit 1
fi

if [[ ! "$current_version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Version must be MAJOR.MINOR.PATCH (e.g. 2.0.0). Found: $current_version" >&2
  exit 1
fi

major="${BASH_REMATCH[1]}"
minor="${BASH_REMATCH[2]}"
patch="${BASH_REMATCH[3]}"
next_patch=$((patch + 1))
next_version="${major}.${minor}.${next_patch}"

sed -i "s|<Version>${current_version}</Version>|<Version>${next_version}</Version>|" "$PROJECT_FILE"

echo "Version bumped: ${current_version} -> ${next_version}"
