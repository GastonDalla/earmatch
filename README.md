# Earmatch

Made by [GastonDalla](https://github.com/GastonDalla).

Earmatch is a native macOS overlay for rekordbox 7 for practising beatmatching by ear. It covers the parts of rekordbox that let you mix by eye: the decimals of the jog BPM, the stacked waveforms that line up the beat grids, and it adds a Pioneer-style phase meter you can switch on when you want a check.

Earmatch only reads rekordbox. It never clicks, types into or controls rekordbox, and it never posts keyboard or mouse events.

## Requirements

- macOS 14 or later, on Apple silicon or Intel (the app is a universal binary).
- rekordbox 7 in **PERFORMANCE** mode with any of the **2Deck Horizontal**, **2Deck Vertical**, **4Deck Horizontal** or **4Deck Vertical** layouts. The overlays hide in Browse, in other modes, and while another app or another rekordbox window is in front.
  - In vertical layouts every deck already has its own column, so Split behaves like Normal and the phase meters sit beside the columns.
  - In 4-deck layouts Split and Hide Both use a 2x2 grid that mirrors the deck quadrants; empty decks show nothing.
  - The cue countdown and the CDJ-3000 master waveform band work in horizontal layouts.
- To build: Xcode 27 with Swift 6.4 (Swift Package Manager; every target builds in the Swift 6 language mode) for the universal app. With only the Command Line Tools, the build script makes an app for this Mac's architecture and warns about it.

## Build

```sh
./scripts/build-app.sh
```

This builds `build/Earmatch.app` for arm64 and x86_64 (only for this Mac's architecture, with a warning, when Xcode is not installed), copies the icon, embeds the Sparkle update framework and signs the app. With an Apple Development or Developer ID identity in the keychain, macOS keeps the permission grants across rebuilds; set `EARMATCH_SIGN_IDENTITY` to choose one. Without an identity the app is signed ad hoc and macOS asks for both permissions again after every build.

In released builds, opening Earmatch checks for a new version and, when there is one, asks whether to install it. Earmatch never takes the focus from rekordbox for an update: when rekordbox is in front, and for the daily check while Earmatch runs, it shows **Update Available…** in the menus instead.

Development builds have automatic updates off: the updater starts only in builds made with the Sparkle public key in `EARMATCH_SPARKLE_PUBLIC_KEY`, and **Check for Updates…** appears only then.

Run the tests with `swift test`.

## Releasing

[docs/RELEASING.md](docs/RELEASING.md) describes the one-time setup (Developer ID certificate, notarization credentials, Sparkle keys) and each release: `./scripts/release.sh` builds a signed, notarized and stapled DMG and the signed update feed, and prints what to upload. `./scripts/release.sh --dry-run` checks everything that needs no credentials.

## First run

1. Move `Earmatch.app` to `/Applications` first. macOS ties the permissions to the app's location.
2. Open Earmatch. Until both permissions are allowed its window shows only the permissions screen, and nothing else works: the overlays stay hidden, the hotkeys are off, and the menu bar menu only offers to allow the missing permissions, open the window or quit. Earmatch shows no system prompt at launch; it asks from that screen. The two permissions:
   - **Accessibility**: finds rekordbox's decks, labels and window position, and lets the hotkeys work.
   - **Screen Recording**: sees the rekordbox window's waveforms and BPM digits. Only the rekordbox window is captured.
3. Click **Open Settings** next to each one and turn Earmatch on in System Settings › Privacy & Security. The window re-checks every second, turns each row green once it is allowed, and opens on **Setup** a moment after both are.
4. If macOS asks you to quit and reopen Earmatch, click **Restart Earmatch**.

If you later turn either permission off, Earmatch locks again and the window goes back to the permissions screen.

Running the binary from a terminal uses the terminal's permissions instead of the app's.

While the window is open Earmatch shows a Dock icon and its own menu bar; closing the window keeps Earmatch running in the menu bar only. Choose **Open Earmatch…** from the menu bar icon (or open the app again) to bring it back. The window reopens where you left it, on the section you used last (on the permissions screen while a permission is missing).

## The window

The window has a sidebar with four sections, and each one fits on a 13-inch MacBook without scrolling (the window is 1040 × 700 points by default and at least 940 × 640). The sidebar also shows whether Earmatch is live, off or waiting for rekordbox.

- **Live**: **Enable Earmatch**, the **BOOTH** presets, **LIVE DECKS** and the hotkeys.
- **Overlay**: **BPM PRECISION**, **WAVEFORM** and **PHASE METER**, each with its options, preview and status line.
- **Practice**: the **Mix trainer** switch, the mix being timed, the last mix, its replay and your progress.
- **Setup**: the permissions, the **LANGUAGE** selector, the hotkeys, **Check for Updates…** (in released builds), **Copy Diagnostics**, **Quit Earmatch** and the credit.

Switch sections with **Command-1** to **Command-4** (also in the **View** menu), or with **Up** and **Down** while the sidebar has focus.

- **LIVE DECKS** mirrors the decks while rekordbox is in front (two or four, as in your layout): deck number, track title, a **SYNC MASTER** chip on the master deck, and each deck's phase meter in the type you chose, drawn exactly as the overlay draws it. Its status line shows the frame rate and the capture-to-screen latency (`Live · 112 fps · 2.1 ms`), or what Earmatch is waiting for.
- Every option has a one-line explanation under it, and each card ends with a status line that says what Earmatch is waiting for (rekordbox, layout, a capture problem) or that it is live.
- Every control works from the keyboard: **Tab** moves between switches, option pickers and buttons (a lime ring marks the focused one), **Space** flips a switch or presses a button, **Left** and **Right** change a picker's option, and holding **Space** on a preview's eye icon shows that preview without Earmatch. VoiceOver reads each control as the native switch, segmented control or button it stands for.

## Options

Once both permissions are allowed, all options are in the window and in the menu bar menu, and apply immediately.

- **Enable Earmatch**: turns every overlay on or off.
- **BPM PRECISION**: **Limit BPM display** covers part of the BPM in each jog centre, which includes the pitch. **One decimal** reads `127.2` for `127.22`, **No decimals** reads `127`, **Cover fully** hides it. The track's original BPM in the deck header stays visible.
- **WAVEFORM**: **Normal** leaves rekordbox's stacked waveform alone. **Split** shows each deck in its own half with its own centred playhead, so the beat grids cannot be lined up by eye. **Hide Both** blacks out both waveforms.
- **PHASE METER**: an on/off switch and two looks. **Type 1 (Box)** shows four boxes per bar, as on the XDJ-1000 and CDJ-2000NXS. **Type 2 (Line)** shows scrolling beat lines around a centre line, as on the CDJ-3000. The master deck is orange and the other deck blue, with a bar counter such as `06.3 Bars` for each. In Split and Hide Both the meters sit above each deck's half; in Normal each deck's meter sits on a small dark band at the top of its waveform.

The **WAVEFORM** and **PHASE METER** cards each have a PREVIEW that animates the chosen option; while rekordbox is connected they switch to the decks' live beat positions and show a **LIVE** chip. Hold the eye icon next to PREVIEW to see that preview without Earmatch (to see rekordbox itself unchanged, hold Right Option). The **BPM PRECISION** card has no preview; its status line shows a sample BPM with the covered digits marked.

## Languages

Earmatch speaks English and Spanish. **LANGUAGE** in the window's Setup section picks **System**, **English** or **Español**; the window, the app menus and the menu bar menu switch right away, without restarting, and Earmatch keeps the choice. **System** (the default) follows the language order in System Settings › General › Language & Region: the first of your languages that Earmatch has is used, and English is the fallback. Numbers, dates and durations follow the chosen language in your region. Copy Diagnostics always writes its report in English.

The labels a Pioneer DJ player prints on its own screen stay in English on the overlay in both languages, exactly as on the hardware: `MASTER`, `PLAYER`, `Bars`, the empty counter `--.-` and the player model names (CDJ-3000, CDJ-2000NXS2, XDJ-1000). A DJ who learns the meter on Earmatch reads the same words on the players in the booth. VoiceOver still reads the counters in your language.

Translations live in `Support/en.lproj` and `Support/es.lproj`. `swift test` fails when a key used in the code is missing from either language, when a translation reads different format arguments than its key, or when a window or menu text skips the translation tables.

## Hotkeys

| Keys | Action |
| --- | --- |
| Hold **Right Option** | Peek: shows rekordbox unchanged while held |
| **Control-Option-Command-E** | Turn Earmatch on or off |
| **Command-1** to **Command-4** | Show Live, Overlay, Practice or Setup (while the window is in front, once both permissions are allowed) |

The peek starts only when Right Option is pressed on its own, and ends when you press any other key with it. The peek and on/off hotkeys work once both permissions are allowed, without restarting Earmatch, and stop if either is turned off. Earmatch only observes keys; rekordbox still receives every key you press.

## Diagnostics

- **Copy Diagnostics** (in the window's Setup section and the menu bar menu, once both permissions are allowed) copies a plain-text report to the clipboard: Earmatch, macOS and rekordbox versions, Mac model and architecture, permission status, the current status line and your settings, then the capture pipeline's numbers (capture status, whether the stacked waveform was found, frame rate, analysis time, capture-to-screen latency) and each deck's beat-grid reading. It contains no track titles or other personal data. Paste it into a bug report.
- `Earmatch --dump-ax` prints rekordbox's main window as a JSON snapshot (the test-fixture format) on stdout and what Earmatch located in it on stderr. Use it to support a new rekordbox version:

  ```sh
  /Applications/Earmatch.app/Contents/MacOS/Earmatch --dump-ax > rekordbox-ax.json
  ```

- `Earmatch --render-window <directory>` writes PNG images of the window at 2x, one per section plus the four-deck Live view: `live.png`, `overlay.png`, `practice.png`, `setup.png` and `live-4deck.png`. They show sample data only (sample track titles and a sample practice history). The command does not read rekordbox, your settings or your practice history, asks for no permission, starts no capture, hotkeys or update checks, and quits when it is done. `--render-size minimum` (or `default`, or `WIDTHxHEIGHT` in points) picks the window size. Run the app's own binary to get its icon, version and translations; `-AppleLanguages '(es)'` renders the Spanish window:

  ```sh
  /Applications/Earmatch.app/Contents/MacOS/Earmatch --render-window ~/Desktop/earmatch-window --render-size default
  ```

## Privacy

Earmatch reads rekordbox's Accessibility labels and captures only the rekordbox window. Frames are processed in memory and are never saved to disk or sent anywhere.

Released builds connect to the network only for updates (Sparkle): each time you open Earmatch, once a day while it runs, and when you choose **Check for Updates…**, Earmatch downloads `https://earmatch.gastondalla.com/appcast.xml`, and it downloads the new version from GitHub when you install one. Sparkle's system profiling stays off, so these requests carry only Earmatch's name and version, plus the IP address every web request shows. Development builds make no network connections.

## Credits

Made by [GastonDalla](https://github.com/GastonDalla). The credit is also in the window's Setup section and permissions screen, in **About Earmatch** (app menu, and the menu bar menu once both permissions are allowed) and in the app's copyright.

## Independence

Independent utility. Not affiliated with AlphaTheta, Pioneer DJ or rekordbox. rekordbox, Pioneer DJ, CDJ and XDJ are trademarks of their respective owners.
