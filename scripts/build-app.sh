#!/usr/bin/env bash
# Builds build/Earmatch.app as a universal binary (Apple silicon and Intel), embeds Sparkle and signs it.
# A universal build needs Xcode; with only the Command Line Tools installed it falls back
# to this Mac's architecture and says so.
# A real signing identity keeps the code requirement stable, so macOS keeps the
# Accessibility and Screen Recording grants across rebuilds. Ad-hoc signing works,
# but macOS will ask for both permissions again after every build.
#
# Environment:
#   EARMATCH_SIGN_IDENTITY       codesign identity, or "-" for ad-hoc; default: the first "Apple Development"
#                                or "Developer ID Application" identity in the keychain, else ad-hoc.
#   EARMATCH_SPARKLE_PUBLIC_KEY  Sparkle EdDSA public key (base64, from generate_keys). Written to
#                                SUPublicEDKey. Without it the app keeps the updater off.
#   EARMATCH_RELEASE=1           release build: automatic update checks on by default
#                                (SUEnableAutomaticChecks). Set by scripts/release.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

public_key="${EARMATCH_SPARKLE_PUBLIC_KEY:-}"
if [[ -n "$public_key" && ! "$public_key" =~ ^[A-Za-z0-9+/]{43}=$ ]]; then
    echo "error: EARMATCH_SPARKLE_PUBLIC_KEY is not a Sparkle EdDSA public key (44 base64 characters)" >&2
    exit 1
fi

archs=(--arch arm64 --arch x86_64)
if swift build -c release "${archs[@]}" --product Earmatch; then
    bin="$(swift build -c release "${archs[@]}" --show-bin-path)"
elif ! xcodebuild -version >/dev/null 2>&1; then
    echo "warning: a universal build needs Xcode and only the Command Line Tools are installed;" >&2
    echo "warning: building for $(uname -m) only (this app will not run on the other Mac architecture)" >&2
    swift build -c release --product Earmatch
    bin="$(swift build -c release --show-bin-path)"
else
    echo "error: the universal build failed" >&2
    exit 1
fi

sparkle="$(find .build/artifacts/sparkle -type d -path '*/Sparkle.xcframework/macos-*/Sparkle.framework' -prune 2>/dev/null | head -n 1 || true)"
if [[ -z "$sparkle" ]]; then
    echo "error: Sparkle.framework not found under .build/artifacts; run 'swift package resolve'" >&2
    exit 1
fi

app="build/Earmatch.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$app/Contents/Frameworks"
cp "$bin/Earmatch" "$app/Contents/MacOS/Earmatch"
cp Support/Info.plist "$app/Contents/Info.plist"
cp Support/AppIcon.icns "$app/Contents/Resources/AppIcon.icns"
cp -R Support/*.lproj "$app/Contents/Resources/"
ditto "$sparkle" "$app/Contents/Frameworks/Sparkle.framework"

plist="$app/Contents/Info.plist"
if [[ -n "$public_key" ]]; then
    plutil -replace SUPublicEDKey -string "$public_key" "$plist"
    echo "updates: on (SUPublicEDKey from EARMATCH_SPARKLE_PUBLIC_KEY)"
else
    echo "updates: off (EARMATCH_SPARKLE_PUBLIC_KEY is not set; the updater stays off in this build)"
fi
if [[ "${EARMATCH_RELEASE:-0}" == "1" ]]; then
    plutil -replace SUEnableAutomaticChecks -bool YES "$plist"
else
    plutil -replace SUEnableAutomaticChecks -bool NO "$plist"
fi

identity="${EARMATCH_SIGN_IDENTITY:-}"
if [[ -z "$identity" ]]; then
    identity="$(security find-identity -v -p codesigning | awk -F'"' '/Apple Development|Developer ID Application/ {print $2; exit}')"
fi

if [[ "$identity" == "-" ]]; then
    flags=(--force --sign -)
elif [[ -n "$identity" ]]; then
    flags=(--force --options runtime --sign "$identity")
    if [[ "$identity" == "Developer ID Application"* ]]; then
        flags+=(--timestamp)
    else
        flags+=(--timestamp=none)
    fi
else
    echo "warning: no signing identity found; signing ad-hoc (permissions reset on every build)" >&2
    flags=(--force --sign -)
fi

# Signs inside-out: Sparkle's XPC services, its helpers, the framework, then the app.
# The XPC services keep the entitlements Sparkle ships them with (Sparkle's own instructions).
# Autoupdate and Updater.app are signed without entitlements: the ad-hoc build of Autoupdate claims
# com.apple.application-identifier, which a Developer ID binary may not carry without a provisioning profile.
framework="$app/Contents/Frameworks/Sparkle.framework"
for service in "$framework"/Versions/B/XPCServices/*.xpc; do
    if [[ -e "$service" ]]; then codesign "${flags[@]}" --preserve-metadata=entitlements "$service"; fi
done
for helper in "$framework/Versions/B/Autoupdate" "$framework/Versions/B/Updater.app"; do
    if [[ -e "$helper" ]]; then codesign "${flags[@]}" "$helper"; fi
done
codesign "${flags[@]}" "$framework"
codesign "${flags[@]}" "$app"
[[ -n "$identity" && "$identity" != "-" ]] || identity="ad-hoc"
echo "built $app ($(lipo -archs "$app/Contents/MacOS/Earmatch")), signed by $identity"
