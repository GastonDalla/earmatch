#!/usr/bin/env bash
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
