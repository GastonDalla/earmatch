<p align="center"><img src="docs/images/icon.png" width="96" alt="Earmatch icon"></p>

<h1 align="center">Earmatch</h1>

<p align="center">Practise beatmatching by ear on rekordbox 7. Free for Mac and Windows.</p>

<p align="center">
  <a href="https://github.com/GastonDalla/earmatch/releases/latest"><b>Download</b></a> ·
  <a href="https://earmatch.gastondalla.com">Website</a> ·
  <a href="docs/GUIDE.md">Guide</a>
</p>

![Earmatch's Overlay section](docs/images/overlay.jpg)

## What it does

- **Hides what you would match by eye:** the decimals of the jog BPM (or all of it) and the stacked waveforms, split per deck or hidden.
- **Adds a Pioneer-style phase meter** when you want a check: XDJ/CDJ-2000NXS boxes or a CDJ-3000 line, meter or waveform, with a bar counter or a cue countdown.
- **Times every mix** with the mix trainer and keeps your practice history.
- **Peek:** hold a key to see rekordbox unchanged.

Earmatch only reads rekordbox. It never clicks, types into or controls it.

## Install

| | Mac | Windows |
| --- | --- | --- |
| Needs | macOS 14 or later, Apple silicon or Intel | Windows 10 (2004 or later) or 11, x64 or Arm64 |
| Download | `Earmatch-<version>.dmg` | `Earmatch-win-x64-Setup.exe` (Windows on Arm: `-arm64-`) |
| First run | Drag Earmatch to Applications, open it and allow **Accessibility** and **Screen Recording** | Run the installer. Until it is signed, SmartScreen asks: **More info** › **Run anyway** |

Both use rekordbox 7 in **PERFORMANCE** mode with a 2Deck or 4Deck layout, and update themselves.

## Hotkeys

| Mac | Windows | Action |
| --- | --- | --- |
| Hold **Right Option** | Hold **Right Alt** | Peek at rekordbox unchanged |
| **⌃⌥⌘E** | **Ctrl+Alt+Win+E** | Turn Earmatch on or off |
| **⌘1**–**⌘4** | **Ctrl+1**–**Ctrl+4** | Live, Overlay, Practice, Setup |

<p align="center">
  <img src="docs/images/live.jpg" width="49%" alt="Earmatch's Live section">
  <img src="docs/images/practice.jpg" width="49%" alt="Earmatch's Practice section">
</p>

## Privacy

Earmatch reads only the rekordbox window. Frames stay in memory and are never saved or sent anywhere. It goes online only to check for updates.

## Build

- Mac: `./scripts/build-app.sh`, tests with `swift test`.
- Windows: `dotnet build windows/Earmatch.slnx`, tests with `dotnet test windows/Earmatch.slnx`.
- Releases: [docs/RELEASING.md](docs/RELEASING.md). Everything else: [docs/GUIDE.md](docs/GUIDE.md).

## Credits

Made by [GastonDalla](https://github.com/GastonDalla). Independent utility, not affiliated with AlphaTheta, Pioneer DJ or rekordbox. rekordbox, Pioneer DJ, CDJ and XDJ are trademarks of their respective owners.
