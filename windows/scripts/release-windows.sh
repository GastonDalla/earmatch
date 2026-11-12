#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."

usage() {
    cat <<'USAGE'
Builds the Windows release of Earmatch on this Mac: a self-contained app for x64 and for Arm64, each packed by
Velopack (vpk) into an installer (Setup.exe, per-user, with Start menu and desktop shortcuts) and the update package
and feed that installed copies read from the GitHub release.

It never uploads anything. At the end it prints the files to upload to the GitHub release v<version>, the same
release as the macOS DMG, and the command to do it. The installer is not signed yet, so Windows SmartScreen warns
the first time it runs. One-time setup and the checklist: docs/RELEASING.md, "Windows".

Usage:
  windows/scripts/release-windows.sh [--dry-run] [--notes FILE]

  --dry-run    Build, pack and check everything in build/windows-release/dry-run/<version>, and print no upload
               commands.
  --notes FILE Markdown release notes for this version (default: docs/release-notes/<version>.md). Velopack
               keeps them in the update package.
USAGE
}

dry_run=0
notes=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run) dry_run=1 ;;
        --notes)
            [[ $# -ge 2 ]] || { echo "error: --notes needs a file" >&2; exit 2; }
            notes="$2"
            shift
            ;;
        -h|--help) usage; exit 0 ;;
        *) echo "error: unknown option $1 (see --help)" >&2; exit 2 ;;
    esac
    shift
done

step() { printf '\n==> %s\n' "$*"; }
skip() { printf 'SKIPPED: %s\n' "$*"; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

repo="GastonDalla/earmatch"
root="$PWD"
version="$(plutil -extract CFBundleShortVersionString raw Support/Info.plist)"
tag="v$version"
notes="${notes:-docs/release-notes/$version.md}"
pack_id="Earmatch"
icon="windows/src/Earmatch.App/Assets/AppIcon.ico"
project="windows/src/Earmatch.App/Earmatch.App.csproj"
# One Velopack channel per architecture, named like the runtime: an installed copy reads releases.<channel>.json and
# so never downloads the other architecture's package.
runtimes=(win-x64 win-arm64)
if [[ $dry_run == 1 ]]; then
    out="build/windows-release/dry-run/$version"
else
    out="build/windows-release/$version"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step "Earmatch for Windows $version$([[ $dry_run == 1 ]] && echo ', dry run')"

step "Checking what the release needs"
[[ -s "$notes" ]] || fail "release notes not found: $notes (write them in Markdown, or pass --notes FILE)"
echo "release notes: $notes"
props_version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' windows/Directory.Build.props | head -n 1)"
[[ "$props_version" == "$version" ]] \
    || fail "windows/Directory.Build.props says ${props_version:-no version}, Support/Info.plist says $version. Keep both the same: one release carries both apps"
echo "version: $version (Support/Info.plist and windows/Directory.Build.props agree)"
command -v dotnet >/dev/null || fail "dotnet was not found. Install the .NET 10 SDK"
echo "dotnet SDK: $(dotnet --version)"
(cd windows && dotnet tool restore >/dev/null) || fail "dotnet tool restore failed in windows/ (it installs vpk from windows/.config/dotnet-tools.json)"
vpk_version="$(cd windows && dotnet vpk -h 2>/dev/null | sed -n 's/.*Velopack CLI \([^,]*\),.*/\1/p' | head -n 1)"
[[ -n "$vpk_version" ]] || fail "vpk does not run (cd windows && dotnet vpk -h)"
echo "vpk: $vpk_version"
[[ -s "$icon" ]] || fail "the icon is missing: $icon"
skip "code signing: the installer and the app are not signed yet, so SmartScreen warns on first run (docs/RELEASING.md, Windows)"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# A rebuild of the same version starts from an empty folder: vpk refuses a version its folder already holds.
rm -rf "$out"
mkdir -p "$out"

for rid in "${runtimes[@]}"; do
    step "Publishing $rid (self-contained, Release)"
    publish="$work/$rid"
    dotnet publish "$project" -c Release -r "$rid" --self-contained -o "$publish" -v quiet -nologo -p:DebugType=None \
        || fail "dotnet publish failed for $rid"
    [[ -s "$publish/Earmatch.exe" ]] || fail "the $rid publish has no Earmatch.exe"
    echo "ok: $(du -sh "$publish" | cut -f1) in $(find "$publish" -type f | wc -l | tr -d ' ') files"

    step "Packing $rid with Velopack"
    (cd windows && dotnet vpk '[win]' pack \
        --packId "$pack_id" \
        --packVersion "$version" \
        --packTitle "Earmatch" \
        --packAuthors "GastonDalla" \
        --packDir "$publish" \
        --mainExe "Earmatch.exe" \
        --runtime "$rid" \
        --channel "$rid" \
        --icon "$root/$icon" \
        --releaseNotes "$root/$notes" \
        --shortcuts "Desktop,StartMenuRoot" \
        --noPortable \
        --outputDir "$root/$out") \
        || fail "vpk pack failed for $rid"
done

step "Checking the packages"
uploads=()
for rid in "${runtimes[@]}"; do
    setup="$out/$pack_id-$rid-Setup.exe"
    package="$out/$pack_id-$version-$rid-full.nupkg"
    feed="$out/releases.$rid.json"
    for file in "$setup" "$package" "$feed"; do
        [[ -s "$file" ]] || fail "vpk did not write $file"
    done
    [[ "$(head -c 2 "$setup")" == "MZ" ]] || fail "$setup is not a Windows program"
    # Read whole listings into variables: grep -q in a pipeline would end it early, which pipefail reports as a failure.
    listing="$(unzip -l "$package")"
    manifest="$(unzip -p "$package" '*.nuspec')"
    grep -q " lib/app/Earmatch.exe$" <<<"$listing" || fail "$package has no Earmatch.exe"
    grep -q "<version>$version</version>" <<<"$manifest" || fail "$package is not version $version"
    grep -q "<channel>$rid</channel>" <<<"$manifest" || fail "$package is not on the $rid channel"
    grep -q "\"$(basename "$package")\"" "$feed" || fail "$feed does not list $(basename "$package")"
    grep -q "\"Version\": *\"$version\"" "$feed" || fail "$feed does not list version $version"
    echo "ok: $rid: $(du -h "$setup" | cut -f1) Setup.exe, $(du -h "$package" | cut -f1) update package, feed releases.$rid.json"
    uploads+=("$setup" "$package" "$feed")
done

if [[ $dry_run == 1 ]]; then
    step "Dry run finished. Nothing was uploaded."
    echo "Packages (unsigned) in $out:"
    for file in "${uploads[@]}"; do echo "  $file"; done
    exit 0
fi

step "Earmatch for Windows $version is ready. Nothing was uploaded."
echo "Upload to the GitHub release $tag ($repo), next to Earmatch-$version.dmg:"
for file in "${uploads[@]}"; do echo "  $file"; done
echo
echo "Command (after scripts/release.sh's gh release create, so the release exists):"
echo "  gh release upload $tag --repo $repo \\"
for index in "${!uploads[@]}"; do
    if [[ $index -lt $((${#uploads[@]} - 1)) ]]; then echo "    ${uploads[$index]} \\"; else echo "    ${uploads[$index]}"; fi
done
