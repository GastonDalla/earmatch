# Releasing Earmatch

A release is a disk image (`Earmatch-<version>.dmg`) that anyone can download and open without a Gatekeeper warning, plus an entry in the update feed that existing copies of Earmatch read:

- The app is signed with a **Developer ID Application** certificate and the hardened runtime, the DMG is signed too, Apple **notarizes** the DMG and the ticket is **stapled** to it.
- The DMG goes to **GitHub Releases** (`GastonDalla/earmatch`, tag `v<version>`).
- **Sparkle** updates: `appcast.xml` goes to the website's `public/` folder and is served at `https://earmatch.gastondalla.com/appcast.xml` (`SUFeedURL` in `Support/Info.plist`). Each item is signed with an EdDSA key, so Earmatch only installs updates signed by you.

`scripts/release.sh` does everything on this Mac and uploads nothing. At the end it prints the files to upload and the commands to do it.

Earmatch for Windows goes into the same GitHub release, built by `windows/scripts/release-windows.sh`: see [Windows](#windows).

> **Before the first public release.** Assets of a private GitHub repository cannot be downloaded without signing in to GitHub, so neither people nor Sparkle can fetch the DMG while the repository is private. Publish releases once the repository is public, or set `EARMATCH_DOWNLOAD_URL_PREFIX` to another public location (for example a `downloads/` folder on the website) and upload the DMG there.

## One-time setup

Do these steps once, in this order.

### 1. Create the Developer ID Application certificate

Needs a paid Apple Developer Program membership. Only the team's Account Holder can create Developer ID certificates.

- **In Xcode:** Xcode › Settings… › Accounts, select your Apple ID and team, click **Manage Certificates…**, click **+** and choose **Developer ID Application**.
- **Or on the web:** in Keychain Access choose Keychain Access › Certificate Assistant › Request a Certificate From a Certificate Authority…, save the request to disk; then on [developer.apple.com](https://developer.apple.com/account/resources/certificates/list) › Certificates › **+** › **Developer ID Application** (G2 Sub-CA), upload the request, download the certificate and double-click it to add it to the login keychain.

Check that it is there:

```sh
security find-identity -v -p codesigning
# 1) … "Developer ID Application: <Your Name> (<TEAMID>)"
```

Back it up: in Keychain Access, under My Certificates, export **Developer ID Application: …** with its private key as a `.p12` file and keep it somewhere safe. If you have several Developer ID identities, set `EARMATCH_SIGN_IDENTITY` to the full name of the one to use.

### 2. Store the notarization credentials

`notarytool` keeps the credentials in the login keychain under a profile name. Use either an app-specific password or an App Store Connect API key.

**App-specific password:** create one at [account.apple.com](https://account.apple.com) › Sign-In and Security › App-Specific Passwords, then:

```sh
xcrun notarytool store-credentials "earmatch-notary" \
  --apple-id "<your Apple ID email>" --team-id "<TEAMID>"
# it asks for the app-specific password
```

**App Store Connect API key:** in App Store Connect › Users and Access › Integrations › App Store Connect API › Team Keys, generate a key with the **Developer** role, download `AuthKey_<KEYID>.p8` (it can be downloaded only once) and note the Issuer ID, then:

```sh
xcrun notarytool store-credentials "earmatch-notary" \
  --key /path/to/AuthKey_<KEYID>.p8 --key-id "<KEYID>" --issuer "<ISSUER ID>"
```

Check it:

```sh
xcrun notarytool history --keychain-profile "earmatch-notary"
```

### 3. Generate the Sparkle EdDSA keys

Sparkle's tools come with the Swift package:

```sh
swift package resolve
.build/artifacts/sparkle/Sparkle/bin/generate_keys
```

`generate_keys` creates the key pair in the login keychain (account `ed25519`) and prints the **public** key, a 44-character base64 string. Run it once: running it again only prints the existing public key (`generate_keys -p` prints it too).

Back up the **private** key and never commit it:

```sh
.build/artifacts/sparkle/Sparkle/bin/generate_keys -x ~/Documents/earmatch-sparkle-private-key
```

Copies of Earmatch only accept updates signed by this key. If it is lost, those copies can never update again.

### 4. Put the public key and the notary profile in your environment

Add to `~/.zshrc` (the public key is not secret; it ships inside every release):

```sh
export EARMATCH_SPARKLE_PUBLIC_KEY="<the public key from step 3>"
export EARMATCH_NOTARY_PROFILE="earmatch-notary"
export EARMATCH_RELEASE_DIR="$HOME/Earmatch-releases"
```

`EARMATCH_RELEASE_DIR` keeps every released DMG and `appcast.xml` between releases: `generate_appcast` updates the existing feed and builds small delta updates from the previous versions it finds there. Keep this folder; without it, start from the published feed with `EARMATCH_PREVIOUS_APPCAST=/path/to/earmatch-web/public/appcast.xml`.

Optional: `EARMATCH_SPARKLE_PRIVATE_KEY_FILE` signs with a private key file instead of the keychain (for a CI machine), and `EARMATCH_SPARKLE_ACCOUNT` picks another keychain account.

Open a new terminal so the variables are set.

## Each release

1. **Version.** In `Support/Info.plist`, set `CFBundleShortVersionString` (for example `0.2.0`) and raise `CFBundleVersion` (a whole number, always higher than the last release: Sparkle compares it to decide what is newer). Commit.
2. **Release notes.** Write `docs/release-notes/<version>.md` in Markdown. It becomes the GitHub release text and is embedded in the appcast, where Sparkle shows it in the update window.
3. **Dry run.** Builds, packages and checks everything that needs no credentials, and says what it skips:

   ```sh
   ./scripts/release.sh --dry-run
   ```

4. **Release.**

   ```sh
   ./scripts/release.sh
   ```

   It stops with a clear message if the Developer ID identity, the notary profile, the public key or the release notes are missing. Then it:
   1. builds `build/Earmatch.app` (universal) with the public key and automatic update checks on, and signs Sparkle's parts, Sparkle and the app with the Developer ID identity, the hardened runtime and a secure timestamp;
   2. checks every signature (`codesign --verify --deep --strict`, Developer ID authority, hardened runtime, timestamp) and the entitlements (none);
   3. makes `build/release-<version>/Earmatch-<version>.dmg`, verifies the image and its layout, and signs it. The DMG opens on a designed window: `Earmatch.app` and an `Applications` link to drag it onto, on a background drawn by `scripts/dmg-background.swift` (title, a waveform arrow, light strips under the icon names), with Earmatch's icon as the disk icon. Finder saves that layout (`scripts/dmg-layout.applescript`), so the script runs Finder: run it in a logged-in session, and allow the terminal to control Finder the first time macOS asks;
   4. notarizes it (`xcrun notarytool submit --keychain-profile "$EARMATCH_NOTARY_PROFILE" --wait`; it prints the `notarytool log` command when Apple rejects it), staples the ticket and validates it;
   5. checks Gatekeeper (`spctl -a -vv`) on the DMG and on the app inside it;
   6. copies the DMG and the notes into `EARMATCH_RELEASE_DIR` and runs Sparkle's `generate_appcast`, which signs the new item with the EdDSA key (macOS asks once to let it read the key from the keychain), embeds the notes and points the download at `https://github.com/GastonDalla/earmatch/releases/download/v<version>/Earmatch-<version>.dmg`. `generate_appcast` also moves the older items to that new release's address, so `scripts/pin-appcast-urls.sh` then points every item back at its own `v<version>` release (skipped when `EARMATCH_DOWNLOAD_URL_PREFIX` is set).
5. **Upload.** Run the commands the script prints:

   ```sh
   git tag v<version> && git push origin v<version>
   gh release create v<version> --repo GastonDalla/earmatch --title "Earmatch <version>" \
     --notes-file docs/release-notes/<version>.md "$EARMATCH_RELEASE_DIR/Earmatch-<version>.dmg"
   cp "$EARMATCH_RELEASE_DIR/appcast.xml" <earmatch-web>/public/appcast.xml
   ```

   Upload any `.delta` files the script lists to the same GitHub release. Then commit and deploy the website so the new `appcast.xml` is live. Upload the DMG before the appcast: copies of Earmatch start downloading as soon as they see the new item.
6. **Check.** Download the DMG from the release page in Safari, open it, drag Earmatch to Applications and open it: no warning should appear. In an older copy, choose **Check for Updates…**.

## How it fits together

- **Updates on or off.** `scripts/build-app.sh` writes `SUPublicEDKey` into the app's Info.plist from `EARMATCH_SPARKLE_PUBLIC_KEY`. Without it (every development build) Earmatch never starts Sparkle, hides **Check for Updates…** and shows no update errors. `swift test` checks that the committed `Support/Info.plist` names the feed and carries no key.
- **Automatic checks.** Release builds (`EARMATCH_RELEASE=1`, set by `release.sh`) write `SUEnableAutomaticChecks = YES`: Earmatch checks once a day without asking first. Other builds write `NO`: only **Check for Updates…** checks.
- **Never during a set.** A scheduled check that finds an update while Earmatch is not the active app does not show Sparkle's window, which would take the focus from rekordbox and hide the overlays. The menu item changes to **Update Available…** instead, and choosing it opens the update.
- **Embedding.** `Sparkle.framework` (from the pinned Swift package, Sparkle 2.10.1) is copied with its symlinks into `Earmatch.app/Contents/Frameworks`; the app finds it through the `@executable_path/../Frameworks` rpath set in `Package.swift`. It keeps the parts Sparkle 2 uses for an app that is not sandboxed: `Autoupdate`, `Updater.app` and the `Installer.xpc` and `Downloader.xpc` services.
- **Signing order.** Inside-out: each XPC service (keeping the entitlements Sparkle ships them with), `Autoupdate`, `Updater.app`, the framework, then the app, all with `--options runtime`. `Autoupdate` is signed without entitlements: Sparkle's prebuilt copy claims `com.apple.application-identifier`, which a Developer ID binary cannot carry without a provisioning profile.
- **Entitlements.** None. Earmatch is not sandboxed. Screen Recording and Accessibility are permissions the user grants in System Settings, not entitlements; Sparkle needs none outside the sandbox. Check with `codesign -d --entitlements - build/Earmatch.app`.
- **Permissions after switching certificates.** macOS ties the Accessibility and Screen Recording grants to the signing certificate. A Developer ID build is a different app to macOS than a development build signed with an Apple Development certificate, so grant both permissions again the first time you run a release build.

## Windows

Earmatch for Windows ships in the same GitHub release as the macOS DMG (`v<version>`): an installer per architecture and the update package and feed that installed copies read. [Velopack](https://velopack.io) makes them, from this Mac: `windows/scripts/release-windows.sh` builds everything and uploads nothing.

- **One version.** `<Version>` in `windows/Directory.Build.props` must equal `CFBundleShortVersionString` in `Support/Info.plist`. `dotnet test windows/Earmatch.slnx` (`AppVersionTests`) and the release script both stop when they differ, so raise both together.
- **Unsigned for now.** The installer and the app are not code-signed, so Windows SmartScreen shows "Windows protected your PC" the first time someone runs `Setup.exe`, until they choose **More info › Run anyway**. Signing can be added later in the script (vpk's `--signTemplate` or `--azureTrustedSignFile`) without other changes.
- **Per architecture.** Each architecture has its own Velopack channel, named like its runtime (`win-x64`, `win-arm64`), so an installed copy only ever reads `releases.<channel>.json` of its own architecture.

### One-time setup

Install the .NET 10 SDK, then restore the pinned `vpk` tool (Velopack's CLI, from `windows/.config/dotnet-tools.json`):

```sh
cd windows && dotnet tool restore
```

The script runs `dotnet tool restore` too, so this only checks that it works.

### Each release

1. **Version and notes.** The same as for macOS (steps 1 and 2 above): `Support/Info.plist`, `windows/Directory.Build.props` and `docs/release-notes/<version>.md`. Velopack keeps the notes in the update package.
2. **Build.**

   ```sh
   windows/scripts/release-windows.sh            # or --dry-run to build into build/windows-release/dry-run/
   ```

   For `win-x64` and `win-arm64` it publishes a self-contained Release build and packs it with `vpk [win] pack` into `build/windows-release/<version>/`: Earmatch's icon, product name Earmatch, author GastonDalla, a per-user install in `%LocalAppData%\Earmatch` that needs no administrator, and shortcuts in the Start menu and on the desktop. It checks the version, the notes and every output (each `Setup.exe` is a Windows program, each package holds `Earmatch.exe` with the right version and channel, each feed lists its package), then prints what to upload.
3. **Upload** the six files it lists to the release `v<version>`, after the macOS release exists (`scripts/release.sh` prints its `gh release create`):

   ```sh
   gh release upload v<version> --repo GastonDalla/earmatch \
     build/windows-release/<version>/Earmatch-win-x64-Setup.exe \
     build/windows-release/<version>/Earmatch-<version>-win-x64-full.nupkg \
     build/windows-release/<version>/releases.win-x64.json \
     build/windows-release/<version>/Earmatch-win-arm64-Setup.exe \
     build/windows-release/<version>/Earmatch-<version>-win-arm64-full.nupkg \
     build/windows-release/<version>/releases.win-arm64.json
   ```

   Installed copies look for `releases.<channel>.json` in the repository's releases (Velopack's `GithubSource`, without a token, prereleases ignored), then download the `.nupkg` it names from the same release.
4. **Check.** On a Windows PC, download the `Setup.exe` of its architecture from the release page and run it: Earmatch installs and opens. In an older installed copy, choose **Check for Updates…**.

### How Windows updates work

- **Only installed copies.** The updater starts only when Earmatch runs from its Velopack install (`%LocalAppData%\Earmatch\current`). Development builds (`dotnet run`, a `dotnet publish` folder) never check, hide **Check for Updates…** and say `Updates: off (development build, …)` in **Copy Diagnostics**.
- **When it checks.** When Earmatch opens, once a day while it runs, and on **Check for Updates…** (tray menu and Setup pane).
- **Never during a set.** An update found by the check at opening asks at once only when rekordbox is not in front and Earmatch opened less than two minutes before (`UpdatePrompt`, as on macOS). Otherwise, and for the daily check, the tray menu and the Setup pane say **Update Available…** instead, and choosing it asks. Installing downloads the package, quits Earmatch the normal way and Velopack opens the new version; **No** leaves the item at **Check for Updates…** until the next check finds the update again.
- **Testing an update before publishing it.** Pack a higher version into a local folder (for example `0.1.3-test.1` with `dotnet publish … -p:Version=0.1.3-test.1` and the script's `vpk [win] pack` line with `--outputDir <folder>`), copy the folder to the PC, set the user environment variable `EARMATCH_UPDATE_FEED` to its full path (`C:\…`) and start the installed Earmatch: it reads that folder instead of GitHub. Anything that is not a full Windows folder path turns updates off rather than reading another feed. Remove the variable afterwards.
- **Uninstalling** (Settings › Apps › Installed apps › Earmatch › Uninstall) quits Earmatch and removes `%LocalAppData%\Earmatch`, the shortcuts and the Apps entry. Settings and the practice history stay in `%APPDATA%\Earmatch`.

## Troubleshooting

- **Notarization status `Invalid`:** run the `xcrun notarytool log …` command the script prints; it lists every file Apple rejected and why.
- **`spctl` says `Unnotarized Developer ID`:** the ticket is not stapled or notarization did not finish; run `xcrun stapler staple` on the DMG again.
- **`generate_appcast` cannot find the private key:** allow it in the keychain prompt, or set `EARMATCH_SPARKLE_PRIVATE_KEY_FILE` to the file exported in step 3.
- **`… already exists`:** that version was already released from `EARMATCH_RELEASE_DIR`. Raise the version in `Support/Info.plist`.
- **`Finder could not lay out the DMG window`:** the terminal is not allowed to control Finder, or there is no logged-in session (SSH, CI). Turn the terminal on under Finder in System Settings › Privacy & Security › Automation and run the script again.
- **Changing the DMG design:** edit `scripts/dmg-background.swift` and run `swift scripts/dmg-background.swift /tmp/earmatch-dmg` to preview `background@2x.png`. Keep the icon positions in it and in `scripts/dmg-layout.applescript` the same. Finder writes the icon names in black on a background picture, in dark mode too, so keep a light surface under them.
