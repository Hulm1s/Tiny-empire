#!/usr/bin/env bash
#
# Publishes docs/ to the live site.
#
# GitHub Pages serves the `gh-pages` branch, which is force-pushed with a single
# parentless commit every time. That is the whole point: a Unity web build is about
# 13 MB of wasm and data that changes completely on every build and does not delta
# compress, so committing it to main added that much to the repository *permanently*,
# every publish. At 9 builds the history was already 113 MB. This way it never grows.
#
# Nothing here touches the working tree or the real git index - the tree is built
# from docs/ through a throwaway index file.
#
# Usage:  ./publish.sh
set -euo pipefail

cd "$(dirname "$0")"

if [ ! -f docs/index.html ]; then
  echo "publish: docs/index.html is missing - run the web build first." >&2
  exit 1
fi

VERSION=$(grep -o 'bundleVersion: .*' game/ProjectSettings/ProjectSettings.asset | awk '{print $2}')
SOURCE=$(git rev-parse --short HEAD)
INDEX=$(mktemp)
trap 'rm -f "$INDEX"' EXIT

# Stage docs/ as if it were the repository root, into an index we throw away.
GIT_INDEX_FILE="$INDEX" git --work-tree=docs add -A
TREE=$(GIT_INDEX_FILE="$INDEX" git write-tree)

# No -p: a parentless commit, so the branch carries exactly one build and no history.
COMMIT=$(git commit-tree "$TREE" -m "Publish $VERSION

Built from $SOURCE. Force-pushed with no history; see publish.sh.")

git branch -f gh-pages "$COMMIT"
git push -f origin gh-pages

echo
echo "published $VERSION  (source $SOURCE)"
echo "https://hulm1s.github.io/Tiny-empire/  - live in a minute or two"
