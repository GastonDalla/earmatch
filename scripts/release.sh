#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

usage() {
    cat <<'USAGE'
Builds a release of Earmatch that anyone can download and open without Gatekeeper warnings:
Developer ID signature with the hardened runtime, a designed drag-to-Applications DMG, notarization,
stapling, and a Sparkle appcast item signed with the EdDSA key.

It never uploads anything. At the end it prints the files to upload and the commands to do it.
One-time setup and the full release checklist: docs/RELEASING.md.

Usage:
  scripts/release.sh [--dry-run] [--notes FILE]

  --dry-run    Build, package and check everything that needs no credentials. Skips Developer ID
               signing, notarization, stapling and the signed appcast, and says so. Like a release,
               it lays out the DMG window with Finder, so it needs a logged-in session.
  --notes FILE Markdown release notes for this version (default: docs/release-notes/<version>.md).

Environment:
  EARMATCH_SIGN_IDENTITY             Developer ID Application identity (default: the first one in the keychain)
  EARMATCH_NOTARY_PROFILE            notarytool keychain profile (xcrun notarytool store-credentials)
  EARMATCH_SPARKLE_PUBLIC_KEY        Sparkle EdDSA public key, built into Info.plist as SUPublicEDKey
  EARMATCH_SPARKLE_PRIVATE_KEY_FILE  optional file holding the EdDSA private key; default: the login keychain
  EARMATCH_SPARKLE_ACCOUNT           optional keychain account of the EdDSA key (generate_keys --account)
  EARMATCH_RELEASE_DIR               folder that keeps every released DMG and appcast.xml (default: build/releases)
  EARMATCH_PREVIOUS_APPCAST          optional published appcast.xml to start from when the folder has none
  EARMATCH_DOWNLOAD_URL_PREFIX       where the DMG will be downloaded from
                                     (default: https://github.com/GastonDalla/earmatch/releases/download/v<version>/)
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
skip() { printf 'SKIPPED (dry run): %s\n' "$*"; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

repo="GastonDalla/earmatch"
website_public="public/ in the earmatch-web repository"
version="$(plutil -extract CFBundleShortVersionString raw Support/Info.plist)"
build_number="$(plutil -extract CFBundleVersion raw Support/Info.plist)"
tag="v$version"
notes="${notes:-docs/release-notes/$version.md}"
release_dir="${EARMATCH_RELEASE_DIR:-build/releases}"
download_prefix="${EARMATCH_DOWNLOAD_URL_PREFIX:-https://github.com/$repo/releases/download/$tag/}"
[[ "$download_prefix" == */ ]] || download_prefix="$download_prefix/"
dmg_name="Earmatch-$version.dmg"
app="build/Earmatch.app"
if [[ $dry_run == 1 ]]; then
    out="build/dry-run"
else
    out="build/release-$version"
fi
dmg="$out/$dmg_name"

step "Earmatch $version (build $build_number)$([[ $dry_run == 1 ]] && echo ', dry run')"

step "Checking what the release needs"
[[ -s "$notes" ]] || fail "release notes not found: $notes (write them in Markdown, or pass --notes FILE)"
echo "release notes: $notes"

identities="$(security find-identity -v -p codesigning)"
identity="${EARMATCH_SIGN_IDENTITY:-}"
if [[ -z "$identity" ]]; then
    identity="$(awk -F'"' '/Developer ID Application/ {print $2; exit}' <<<"$identities")"
fi
if [[ $dry_run == 1 ]]; then
    if [[ "$identity" == "Developer ID Application"* ]]; then
        echo "Developer ID identity: $identity (not used in a dry run)"
    else
        echo "Developer ID identity: none (a real release needs one: docs/RELEASING.md, step 1)"
    fi
    build_identity="${EARMATCH_SIGN_IDENTITY:-}"
    if [[ -z "$build_identity" || "$build_identity" == "Developer ID Application"* ]]; then
        build_identity="$(awk -F'"' '/Apple Development/ {print $2; exit}' <<<"$identities")"
    fi
    skip "signing with Developer ID; the app is signed with ${build_identity:-an ad-hoc signature} for local checks"
    build_identity="${build_identity:--}"
    identity=""
elif [[ "$identity" == "Developer ID Application"* ]]; then
    grep -qF "\"$identity\"" <<<"$identities" || fail "'$identity' is not a valid code signing identity in the keychain"
    echo "signing identity: $identity"
    build_identity="$identity"
else
    fail "no Developer ID Application identity in the keychain${identity:+ (EARMATCH_SIGN_IDENTITY is '$identity')}. Create one first: docs/RELEASING.md, step 1"
fi

profile="${EARMATCH_NOTARY_PROFILE:-}"
if [[ -z "$profile" ]]; then
    [[ $dry_run == 1 ]] && skip "EARMATCH_NOTARY_PROFILE is not set; notarization will be skipped" \
        || fail "EARMATCH_NOTARY_PROFILE is not set. Store notary credentials first: docs/RELEASING.md, step 2"
else
    echo "notary profile: $profile"
fi

if [[ -z "${EARMATCH_SPARKLE_PUBLIC_KEY:-}" ]]; then
    [[ $dry_run == 1 ]] && skip "EARMATCH_SPARKLE_PUBLIC_KEY is not set; this build has updates off" \
        || fail "EARMATCH_SPARKLE_PUBLIC_KEY is not set; a release without it could never update. See docs/RELEASING.md, steps 3 and 4"
else
    echo "Sparkle public key: set"
fi

generate_appcast="$(find .build/artifacts/sparkle -type f -path '*/bin/generate_appcast' 2>/dev/null | head -n 1 || true)"
if [[ -z "$generate_appcast" ]]; then
    swift package resolve --disable-keychain >/dev/null
    generate_appcast="$(find .build/artifacts/sparkle -type f -path '*/bin/generate_appcast' 2>/dev/null | head -n 1 || true)"
fi
[[ -x "$generate_appcast" ]] || fail "Sparkle's generate_appcast was not found under .build/artifacts"
echo "generate_appcast: $generate_appcast"

if [[ $dry_run == 0 ]]; then
    if [[ -n "${EARMATCH_SPARKLE_PRIVATE_KEY_FILE:-}" ]]; then
        [[ -r "$EARMATCH_SPARKLE_PRIVATE_KEY_FILE" ]] || fail "cannot read EARMATCH_SPARKLE_PRIVATE_KEY_FILE ($EARMATCH_SPARKLE_PRIVATE_KEY_FILE)"
        key_check="$(mktemp -d)/derive.swift"
        cat > "$key_check" <<'SWIFT'
import CryptoKit
import Foundation
let raw = (try? String(contentsOfFile: CommandLine.arguments[1], encoding: .utf8))?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
guard let data = Data(base64Encoded: raw), data.count == 32 || data.count == 64,
      let key = try? Curve25519.Signing.PrivateKey(rawRepresentation: data.prefix(32)) else { exit(1) }
print(key.publicKey.rawRepresentation.base64EncodedString())
SWIFT
        key_status=0
        file_key="$(swift "$key_check" "$EARMATCH_SPARKLE_PRIVATE_KEY_FILE" 2>"$(dirname "$key_check")/err")" || key_status=$?
        key_error="$(cat "$(dirname "$key_check")/err" 2>/dev/null || true)"
        rm -rf "$(dirname "$key_check")"
        if [[ $key_status == 1 && -z "$key_error" ]]; then
            [[ "${EARMATCH_SKIP_KEY_CHECK:-}" == 1 ]] \
                || fail "the key file is not a raw 32- or 64-byte base64 EdDSA key, so it cannot be checked against EARMATCH_SPARKLE_PUBLIC_KEY; export it with generate_keys -x, or set EARMATCH_SKIP_KEY_CHECK=1 to release without this check"
            echo "warning: EARMATCH_SKIP_KEY_CHECK=1, releasing without checking the key file against EARMATCH_SPARKLE_PUBLIC_KEY"
        elif [[ $key_status != 0 ]]; then
            fail "could not check the key file with swift: ${key_error:-exit $key_status}"
        elif [[ "$file_key" != "$EARMATCH_SPARKLE_PUBLIC_KEY" ]]; then
            fail "the EdDSA private key in EARMATCH_SPARKLE_PRIVATE_KEY_FILE does not match EARMATCH_SPARKLE_PUBLIC_KEY; updates signed with it would be rejected"
        fi
    else
        account_args=()
        [[ -z "${EARMATCH_SPARKLE_ACCOUNT:-}" ]] || account_args=(--account "$EARMATCH_SPARKLE_ACCOUNT")
        keychain_key="$("$(dirname "$generate_appcast")/generate_keys" ${account_args[@]+"${account_args[@]}"} -p 2>/dev/null | tr -d '[:space:]' || true)"
        [[ "$keychain_key" == "$EARMATCH_SPARKLE_PUBLIC_KEY" ]] \
            || fail "the EdDSA key in the keychain (generate_keys -p) does not match EARMATCH_SPARKLE_PUBLIC_KEY; updates signed with it would be rejected"
        echo "EdDSA private key: in the keychain, matches the public key"
    fi
fi

if [[ $dry_run == 0 && -e "$release_dir/$dmg_name" ]]; then
    fail "$release_dir/$dmg_name already exists. Bump CFBundleShortVersionString and CFBundleVersion in Support/Info.plist"
fi

step "Building $app"
EARMATCH_RELEASE=1 EARMATCH_SIGN_IDENTITY="$build_identity" ./scripts/build-app.sh
built_version="$(plutil -extract CFBundleShortVersionString raw "$app/Contents/Info.plist")"
[[ "$built_version" == "$version" ]] || fail "the built app says $built_version, expected $version"

step "Checking the app's signature"
codesign --verify --deep --strict --verbose=2 "$app"
framework="$app/Contents/Frameworks/Sparkle.framework"
for part in "$framework"/Versions/B/XPCServices/*.xpc "$framework/Versions/B/Autoupdate" "$framework/Versions/B/Updater.app" "$framework" "$app"; do
    details="$(codesign -dvv "$part" 2>&1)"
    grep -q 'flags=.*runtime' <<<"$details" || [[ "$build_identity" == "-" ]] \
        || fail "$part is not signed with the hardened runtime"
    if [[ -n "$identity" ]]; then
        grep -q "^Authority=Developer ID Application" <<<"$details" || fail "$part is not signed with Developer ID"
        grep -q "^Timestamp=" <<<"$details" || fail "$part has no secure timestamp"
    fi
    echo "ok: ${part#"$app/"} ($(grep '^Authority=' <<<"$details" | head -n 1 | cut -d= -f2-))"
done
entitlements="$(codesign -d --entitlements - --xml "$app" 2>/dev/null || true)"
if grep -q '<key>' <<<"$entitlements"; then
    echo "app entitlements:"
    echo "$entitlements"
else
    echo "app entitlements: none"
fi
if [[ -n "${EARMATCH_SPARKLE_PUBLIC_KEY:-}" ]]; then
    [[ "$(plutil -extract SUPublicEDKey raw "$app/Contents/Info.plist")" == "$EARMATCH_SPARKLE_PUBLIC_KEY" ]] \
        || fail "SUPublicEDKey in the app does not match EARMATCH_SPARKLE_PUBLIC_KEY"
fi
[[ "$(plutil -extract SUEnableAutomaticChecks raw "$app/Contents/Info.plist")" == "true" ]] \
    || fail "automatic update checks are not on in the release build"

step "Gatekeeper assessment of the app (before notarization)"
if spctl -a -vv -t execute "$app"; then
    echo "ok: Gatekeeper accepts the app"
else
    echo "note: Gatekeeper rejects the app until it is notarized (expected here; checked again after stapling)"
fi

work="$(mktemp -d)"
mounts=()
cleanup() {
    for mount in ${mounts[@]+"${mounts[@]}"}; do hdiutil detach -quiet -force "$mount" 2>/dev/null || true; done
    rm -rf "$work"
}
trap cleanup EXIT

attach() {
    mount="$(mktemp -d)"
    hdiutil attach -readonly -nobrowse -noautoopen -quiet -mountpoint "$mount" "$1"
    mounts+=("$mount")
}
# Unmounts a DMG; Spotlight or Finder can hold a fresh mount for a moment, so it retries before forcing.
detach() {
    local attempt
    for attempt in 1 2 3 4 5; do
        hdiutil detach -quiet "$1" 2>/dev/null && break
        if [[ $attempt == 5 ]]; then hdiutil detach -quiet -force "$1"; else sleep 1; fi
    done
    local kept=()
    for mount in ${mounts[@]+"${mounts[@]}"}; do [[ "$mount" == "$1" ]] || kept+=("$mount"); done
    mounts=(${kept[@]+"${kept[@]}"})
    rmdir "$1" 2>/dev/null || true
}

step "Creating $dmg"
# The DMG opens on a designed window: Earmatch and an Applications link on a drawn background
# (scripts/dmg-background.swift), laid out by Finder (scripts/dmg-layout.applescript) in a read-write
# image that is then compressed. Finder must run, so this needs a logged-in session, and macOS asks once
# to let the terminal control Finder.
mkdir -p "$out"
rm -f "$dmg"
staging="$work/content"
mkdir -p "$staging/.background"
ditto "$app" "$staging/Earmatch.app"
ln -s /Applications "$staging/Applications"
swift scripts/dmg-background.swift "$work/background" || fail "could not draw the DMG background"
tiffutil -cathidpicheck "$work/background/background.png" "$work/background/background@2x.png" \
    -out "$staging/.background/background.tiff" >/dev/null
rw_image="$work/Earmatch-rw.dmg"
hdiutil create -quiet -volname "Earmatch" -srcfolder "$staging" -fs HFS+ -format UDRW \
    -size "$(( $(du -sm "$staging" | cut -f1) + 20 ))m" -ov "$rw_image"
# Mounted at a private folder and addressed by path, so an "Earmatch" volume already open in Finder
# (an older DMG) is left alone. Finder must see this mount, so it is attached without -nobrowse.
layout="$work/layout"
mkdir "$layout"
hdiutil attach -readwrite -noverify -noautoopen -quiet -mountpoint "$layout" "$rw_image"
mounts+=("$layout")
chflags hidden "$layout/.background"
osascript scripts/dmg-layout.applescript "$layout" \
    || fail "Finder could not lay out the DMG window. Run the release in a logged-in session and allow the terminal to control Finder in System Settings > Privacy & Security > Automation"
[[ -s "$layout/.DS_Store" ]] || fail "Finder did not save the DMG window layout (no .DS_Store)"
# The disk icon goes in after the layout: saving it, Finder removes .VolumeIcon.icns and the custom-icon flag.
setfile="$(xcrun --find SetFile 2>/dev/null || true)"
volume_icon=0
if [[ -n "$setfile" ]]; then
    cp Support/AppIcon.icns "$layout/.VolumeIcon.icns"
    chflags hidden "$layout/.VolumeIcon.icns"
    "$setfile" -a C "$layout"
    volume_icon=1
else
    echo "note: SetFile was not found (Xcode or the Command Line Tools); the DMG keeps the generic disk icon"
fi
rm -rf "$layout/.fseventsd" "$layout/.Trashes"
sync
detach "$layout"
hdiutil convert -quiet "$rw_image" -format UDZO -imagekey zlib-level=9 -o "$dmg"
hdiutil verify -quiet "$dmg"
echo "ok: $(du -h "$dmg" | cut -f1) $dmg"

step "Checking the DMG layout"
attach "$dmg"
ls -l "$mount"
[[ -d "$mount/Earmatch.app" ]] || fail "the DMG has no Earmatch.app"
[[ -L "$mount/Applications" && "$(readlink "$mount/Applications")" == "/Applications" ]] || fail "the DMG has no Applications link"
[[ -s "$mount/.background/background.tiff" ]] || fail "the DMG has no background picture"
LC_ALL=C grep -qa icvp "$mount/.DS_Store" 2>/dev/null || fail "the DMG has no Finder icon view layout in .DS_Store"
if [[ $volume_icon == 1 ]]; then
    [[ -s "$mount/.VolumeIcon.icns" ]] || fail "the DMG lost its disk icon (.VolumeIcon.icns)"
    [[ "$("$(dirname "$setfile")/GetFileInfo" -aC "$mount")" == 1 ]] || fail "the DMG's disk icon flag is not set"
fi
codesign --verify --deep --strict "$mount/Earmatch.app"
echo "ok: Earmatch.app, the Applications link, the background and the Finder layout; the app's signature is intact inside the DMG"
detach "$mount"

if [[ $dry_run == 1 ]]; then
    step "Signing, notarizing and stapling the DMG"
    skip "signing the DMG with Developer ID"
    skip "notarization (xcrun notarytool submit --keychain-profile \"\$EARMATCH_NOTARY_PROFILE\" --wait)"
    skip "stapling the notarization ticket (xcrun stapler staple)"
    skip "Gatekeeper assessment of the notarized DMG and app"

    step "Appcast"
    "$generate_appcast" -h >/dev/null 2>&1 || "$generate_appcast" --help >/dev/null 2>&1 \
        || fail "$generate_appcast does not run"
    echo "ok: generate_appcast runs; release notes found at $notes"
    skip "generating and signing appcast.xml needs the EdDSA private key; a real run would do:"
    echo "  cp $dmg $notes -> $release_dir/ (notes as ${dmg_name%.dmg}.md)"
    echo "  $generate_appcast --embed-release-notes --versions $build_number --download-url-prefix $download_prefix --link https://earmatch.gastondalla.com $release_dir"

    step "Dry run finished"
    echo "DMG (unsigned, not notarized, for local checks only): $dmg"
    echo "Nothing was uploaded. A real release needs: Developer ID identity, EARMATCH_NOTARY_PROFILE,"
    echo "EARMATCH_SPARKLE_PUBLIC_KEY and the EdDSA private key. See docs/RELEASING.md."
    exit 0
fi

step "Signing the DMG"
codesign --force --sign "$identity" --timestamp "$dmg"
codesign --verify --strict --verbose=2 "$dmg"

step "Notarizing (this usually takes a few minutes)"
result="$out/notarization.json"
xcrun notarytool submit "$dmg" --keychain-profile "$profile" --wait --output-format json >"$result" || true
status="$(plutil -extract status raw "$result" 2>/dev/null || echo unknown)"
submission="$(plutil -extract id raw "$result" 2>/dev/null || echo unknown)"
if [[ "$status" != "Accepted" ]]; then
    cat "$result" >&2 || true
    fail "notarization status is '$status'. Details: xcrun notarytool log $submission --keychain-profile \"$profile\""
fi
echo "ok: notarization $submission accepted"

step "Stapling"
xcrun stapler staple "$dmg"
xcrun stapler validate "$dmg"

step "Gatekeeper assessment of the notarized DMG and app"
spctl -a -vv -t open --context context:primary-signature "$dmg"
attach "$dmg"
codesign --verify --deep --strict --verbose=2 "$mount/Earmatch.app"
spctl -a -vv -t execute "$mount/Earmatch.app"
detach "$mount"

step "Generating appcast.xml"
mkdir -p "$release_dir"
if [[ ! -e "$release_dir/appcast.xml" && -n "${EARMATCH_PREVIOUS_APPCAST:-}" ]]; then
    cp "$EARMATCH_PREVIOUS_APPCAST" "$release_dir/appcast.xml"
    echo "starting from $EARMATCH_PREVIOUS_APPCAST"
fi
cp "$dmg" "$release_dir/$dmg_name"
cp "$notes" "$release_dir/${dmg_name%.dmg}.md"
key_args=()
if [[ -n "${EARMATCH_SPARKLE_PRIVATE_KEY_FILE:-}" ]]; then
    key_args+=(--ed-key-file "$EARMATCH_SPARKLE_PRIVATE_KEY_FILE")
elif [[ -n "${EARMATCH_SPARKLE_ACCOUNT:-}" ]]; then
    key_args+=(--account "$EARMATCH_SPARKLE_ACCOUNT")
fi
appcast_backup=""
if [[ -f "$release_dir/appcast.xml" ]]; then
    appcast_backup="$(mktemp)"
    cp "$release_dir/appcast.xml" "$appcast_backup"
fi
rollback_appcast() {
    rm -f "$release_dir/$dmg_name" "$release_dir/${dmg_name%.dmg}.md"
    if [[ -n "$appcast_backup" ]]; then cp "$appcast_backup" "$release_dir/appcast.xml" && rm -f "$appcast_backup" || echo "warning: could not restore appcast.xml; the previous copy is at $appcast_backup"; else rm -f "$release_dir/appcast.xml"; fi
}
if ! "$generate_appcast" ${key_args[@]+"${key_args[@]}"} \
    --embed-release-notes \
    --versions "$build_number" \
    --download-url-prefix "$download_prefix" \
    --link "https://earmatch.gastondalla.com" \
    "$release_dir"; then
    rollback_appcast
    fail "generate_appcast failed; the notarized DMG is still at $dmg. Fix the problem and run the release again"
fi
if [[ -z "${EARMATCH_DOWNLOAD_URL_PREFIX:-}" ]] && ! scripts/pin-appcast-urls.sh "$release_dir/appcast.xml" "$repo"; then
    rollback_appcast
    fail "could not point each appcast item at its own GitHub release; the previous appcast was restored"
fi
if ! grep -q "sparkle:edSignature" "$release_dir/appcast.xml"; then
    rollback_appcast
    fail "appcast.xml has no EdDSA signature; the previous appcast was restored"
fi
if ! grep -q "$download_prefix$dmg_name" "$release_dir/appcast.xml"; then
    rollback_appcast
    fail "appcast.xml does not point at $download_prefix$dmg_name; the previous appcast was restored"
fi
[[ -z "$appcast_backup" ]] || rm -f "$appcast_backup"

deltas=()
while IFS= read -r delta; do deltas+=("$delta"); done < <(find "$release_dir" -maxdepth 1 -name "*$build_number-*.delta" 2>/dev/null)

step "Release $version is ready. Nothing was uploaded."
echo "Upload to GitHub Releases ($repo, tag $tag):"
echo "  $release_dir/$dmg_name"
for delta in ${deltas[@]+"${deltas[@]}"}; do echo "  $delta"; done
echo "Copy to the website ($website_public), then deploy it:"
echo "  $release_dir/appcast.xml"
echo
echo "Commands:"
echo "  git tag $tag && git push origin $tag"
echo "  gh release create $tag --repo $repo --title \"Earmatch $version\" --notes-file $notes \\"
echo "    $release_dir/$dmg_name${deltas[*]:+ ${deltas[*]}}"
echo "  cp $release_dir/appcast.xml <earmatch-web>/public/appcast.xml"
