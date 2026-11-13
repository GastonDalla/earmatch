<p align="center"><img src="docs/images/icon.png" width="96" alt="Earmatch icon"></p>

<h1 align="center">Earmatch</h1>

<p align="center">Practise beatmatching by ear on rekordbox 7. Free for Mac and Windows.</p>

<p align="center">
  <a href="https://github.com/GastonDalla/earmatch/releases/latest"><b>Download</b></a> ·
  <a href="https://earmatch.gastondalla.com">Website</a> ·
  <a href="docs/GUIDE.md">Guide</a>
</p>

![Earmatch over rekordbox 7: each deck gets its own half of the waveform, a box phase meter above it and a jog BPM cut to one decimal](docs/images/rekordbox.jpg)

## What it does

- **Hides what you would match by eye:** the decimals of the jog BPM (or all of it) and the stacked waveforms, split per deck or hidden.
- **Adds a Pioneer-style phase meter** when you want a check: CDJ-2000NXS boxes, the NXS2 line, or the CDJ-3000 meter or waveform, with a bar counter or a cue countdown.
- **Times every mix** with the mix trainer and keeps your practice history.
- **Peek:** hold a key to see rekordbox unchanged.

Earmatch only reads rekordbox. It never clicks, types into or controls it.

<details>
<summary><b>See every mode in rekordbox</b></summary>

**rekordbox alone**, for comparison

![rekordbox 7 without Earmatch: stacked waveforms and both jog BPMs at 140.00](docs/images/rekordbox-alone.jpg)

**Type 2 · Line** phase meter (CDJ-2000NXS2, XDJ-1000MK2)

![Line phase meters above each deck's waveform](docs/images/rekordbox-line.jpg)

**CDJ-3000 · Meter**

![CDJ-3000 phase meters: two rulers with the bar counters on the left](docs/images/rekordbox-3000-meter.jpg)

**CDJ-3000 · Wave**: the master deck's waveform above yours

![CDJ-3000 waveform meter: deck 1 shows the master deck's waveform above its own](docs/images/rekordbox-3000-wave.jpg)

**Hide Both**: only the meters stay

![The waveforms are hidden and only the phase meters stay](docs/images/rekordbox-hide-both.jpg)

**Full blind mode**: also covers the pitch and the track BPM in the deck header

![Full blind mode: the deck header BPM and the pitch percentage are covered](docs/images/rekordbox-blind.jpg)

**Jog BPM**: rekordbox alone, One decimal, No decimals, Cover fully and Full blind mode

![Five jogs side by side: 140.00, 140.0, 140, no BPM, and 140.0 without the pitch percentage](docs/images/rekordbox-jog-bpm.jpg)

</details>

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
  <img src="docs/images/live.jpg" width="32%" alt="Earmatch's Live section">
  <img src="docs/images/overlay.jpg" width="32%" alt="Earmatch's Overlay section">
  <img src="docs/images/practice.jpg" width="32%" alt="Earmatch's Practice section">
</p>

## Privacy

Earmatch reads only the rekordbox window. Frames stay in memory and are never saved or sent anywhere. It goes online only to check for updates.

## Build

- Mac: `./scripts/build-app.sh`, tests with `swift test`.
- Windows: `dotnet build windows/Earmatch.slnx`, tests with `dotnet test windows/Earmatch.slnx`.
- Releases: [docs/RELEASING.md](docs/RELEASING.md). Everything else: [docs/GUIDE.md](docs/GUIDE.md).

## Credits

Made by [GastonDalla](https://github.com/GastonDalla). Independent utility, not affiliated with AlphaTheta, Pioneer DJ or rekordbox. rekordbox, Pioneer DJ, CDJ and XDJ are trademarks of their respective owners.
