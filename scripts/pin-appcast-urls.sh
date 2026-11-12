#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 2 ]] || { echo "usage: scripts/pin-appcast-urls.sh <appcast.xml> <owner/repo>" >&2; exit 2; }
appcast=$1
repo=$2
[[ -f "$appcast" ]] || { echo "error: $appcast not found" >&2; exit 1; }
if grep -q 'sparkle-signatures' "$appcast"; then
    echo "error: $appcast carries a feed signature, and editing it would break that signature" >&2
    exit 1
fi
REPO="$repo" perl -0pi -e '
    s{(<item>.*?</item>)}{
        my $item = $1;
        my ($version) = $item =~ m{<sparkle:shortVersionString>([^<]+)</sparkle:shortVersionString>};
        $item =~ s{(https://github\.com/\Q$ENV{REPO}\E/releases/download/)v[^/"]+/}{${1}v$version/}g if defined $version;
        $item
    }gse
' "$appcast"
