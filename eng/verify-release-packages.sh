#!/usr/bin/env bash
#
# Verifies that `dotnet pack` produced exactly the packages this repository
# publishes, at the version named by a release tag, and that each carries the
# repository metadata a debugger needs.
#
#   eng/verify-release-packages.sh v1.2.3 ./packages

set -euo pipefail

readonly PACKAGE_IDS=(QuickCheck QuickCheck.Xunit.v3)

if [ "$#" -ne 2 ]; then
  echo "usage: ${0##*/} <tag> <package-directory>" >&2
  exit 2
fi

readonly tag="$1"
readonly directory="$2"
readonly version="${tag#v}"

expected="$(
  for id in "${PACKAGE_IDS[@]}"; do
    echo "$id.$version.nupkg"
    echo "$id.$version.snupkg"
  done | sort
)"

packed="$(
  find "$directory" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) |
    sed 's#.*/##' | sort
)"

if [ "$expected" != "$packed" ]; then
  echo "the packed files do not match tag $tag:" >&2
  diff --unified=0 --label expected --label packed \
    <(echo "$expected") <(echo "$packed") >&2 || true
  exit 1
fi

# `PublishRepositoryUrl` derives the repository url from the git remote at pack
# time. A remote SourceLink cannot map to a host drops the url and keeps the
# commit, so the package still validates and installs but cannot be stepped
# into; check both attributes rather than the element's presence.
for id in "${PACKAGE_IDS[@]}"; do
  nuspec="$(unzip -p "$directory/$id.$version.nupkg" "$id.nuspec")"
  repository="$(grep -o '<repository [^>]*>' <<<"$nuspec" || true)"

  if ! grep -q 'url="https://' <<<"$repository" ||
    ! grep -q 'commit="[0-9a-f]\{40\}"' <<<"$repository"; then
    echo "$id.$version.nupkg is not source-linked, its repository metadata is" \
      "\"$repository\"" >&2
    exit 1
  fi
done

echo "$tag: verified $(echo "$packed" | wc -l | tr -d ' ') packages in $directory"
