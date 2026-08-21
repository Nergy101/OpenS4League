#!/usr/bin/env bash
# Populate the opens4l_clientdata Docker volume from a directory of S4 game-content data.
#
# The volume holds the server's authoritative game data (the extracted .x7 files + language
# tables) that the auth and game servers mount at /app/data. This script copies it in once, so
# the source path never has to appear in docker-compose.
#
# The source is the extracted game data (from the client's resource.s4hd), e.g.:
#   C:\path\to\extracted\client-data   (contains xml/ and language/)
#
# Usage:
#   ./populate-data.sh /path/to/client-data
#   make data SRC=/path/to/client-data
set -euo pipefail

SRC="${1:?usage: ./populate-data.sh <path-to-client-data>}"
VOL="opens4l_clientdata"

# Resolve to an absolute path Docker can bind-mount (Windows -> C:/Users/...; else native).
SRC="$(cd "$SRC" && pwd)"
if command -v cygpath >/dev/null 2>&1; then
  SRC="$(cygpath -m "$SRC")"
fi

if ! docker volume inspect "$VOL" >/dev/null 2>&1; then
  docker volume create "$VOL" >/dev/null
  echo "Created Docker volume: $VOL"
fi

# Copy the contents into the volume via a throwaway container (this is what keeps the host path
# out of compose — only the named volume is referenced there).
docker run --rm -v "$VOL:/data" -v "$SRC:/src:ro" alpine sh -c 'cp -a /src/. /data/'

echo
echo "Populated $VOL from: $SRC"
echo "The auth + game servers mount this volume at /app/data."
echo "Next: docker compose up -d   (or: make up)"
