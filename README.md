# Outer Wilds Head Tracking

![Mod GIF](https://raw.githubusercontent.com/itsloopyo/outer-wilds-headtracking/main/assets/readme-clip.gif)

An **unofficial** head tracking mod for Outer Wilds that lets you look around naturally using your phone or dedicated head tracker.

## Features

- **Decoupled look + aim**: Look around freely with your head while your aim stays independent
- **6DOF head tracking**: Yaw, pitch, roll rotation plus positional tracking (lean in/out/side-to-side) via OpenTrack UDP protocol
- **Per-connection smoothing**: Separate smoothing for a tracker on this machine and for a remote WiFi device, picked from the packet source address
- **Smart auto-disable**: Tracking automatically pauses during model ship piloting, signalscope zoom, and pause menu
- **Full game integration**: Flashlight follows your gaze, Nomai Translator targets where you look, quantum objects respect head-tracked view direction

## Requirements

- [Outer Wilds](https://store.steampowered.com/app/753640/Outer_Wilds/) (Steam or Epic)
- [Outer Wilds Mod Manager](https://outerwildsmods.com/mod-manager/)
- A head tracking source (see below)

## Quick Start

1. Install [pixi](https://pixi.sh)
2. Clone this repository
3. Run `pixi run install`
4. Configure OpenTrack to output UDP data to `127.0.0.1:4242`
5. Launch the game

## Manual Installation

See [INSTALL.md](INSTALL.md) for detailed manual installation instructions.

## Head Tracking Setup

This mod receives tracking data via the OpenTrack UDP protocol (port 4242 by default). You can use a phone or webcam with [OpenTrack](https://github.com/opentrack/opentrack) or any OpenTrack-compatible head tracking software.

The mod detects whether the tracking data comes from this machine or from a remote device (e.g. phone over WiFi) and applies `localSmoothing` or `remoteSmoothing` accordingly.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

## Configuration

Settings are available in the OWML Mod Manager or in-game mod menu. The mod creates a config file with default settings on first run. Edit it to customize:

| Setting | Default | Description |
|---------|---------|-------------|
| `opentrackPort` | 4242 | UDP port for receiving tracking data |
| `yawSensitivity` | 1.0 | Horizontal look sensitivity |
| `pitchSensitivity` | 1.0 | Vertical look sensitivity |
| `rollSensitivity` | 1.0 | Head tilt sensitivity |
| `localSmoothing` | 0.0 | Smoothing when the tracker runs on this machine (loopback). 0 = none, 1 = heavy |
| `remoteSmoothing` | 0.15 | Smoothing when the tracker is a remote device on the network. 0 = none, 1 = heavy |
| `positionEnabled` | true | Enable positional tracking (lean in/out/side-to-side) |
| `positionSensitivityX` | 4.0 | Lateral position multiplier |
| `positionSensitivityY` | 4.0 | Vertical position multiplier |
| `positionSensitivityZ` | 4.0 | Depth position multiplier |
| `positionLimitX` | 0.30 | Max lateral displacement (meters) |
| `positionLimitY` | 0.20 | Max vertical displacement (meters) |
| `positionLimitZ` | 0.40 | Max depth displacement (meters) |

Position uses the same `localSmoothing` / `remoteSmoothing` value as rotation; there is no separate position smoothing setting.

## Building from Source

This project uses [pixi](https://pixi.sh) for build management.

```bash
# Install dependencies
pixi run restore

# Build the mod
pixi run build

# Build and install to OWML Mods folder
pixi run install

# Create release package
pixi run package
```

The mod depends on the shared `CameraUnlock.Core` library, included as a git submodule in the `cameraunlock-core/` directory. Clone with `--recurse-submodules`, or run `git submodule update --init` in an existing clone.

Game types are resolved at build time from the community `OuterWildsGameLibs` NuGet package, so the build needs no local Outer Wilds install and no game files are copied into this repository. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Project Structure

```
outer-wilds/
├── manifest.json              # OWML mod manifest
├── default-config.json        # Default settings
├── pixi.toml                  # Build configuration
├── scripts/
│   ├── deploy.ps1             # Deploy to OWML
│   ├── package-release.ps1    # Create release zip
│   └── uninstall.ps1          # Remove from OWML
└── src/OuterWildsHeadTracking/
    ├── HeadTrackingMod.cs     # Main mod entry point
    ├── Camera/
    │   ├── Core/              # Camera rotation patches
    │   ├── Effects/           # Flashlight, fog, quantum
    │   ├── UI/                # Reticle, markers, translator
    │   └── Utilities/         # Rotation helpers
    ├── Configuration/         # Constants
    └── Tracking/              # OpenTrack client
```

## Technical Details

- Built for .NET Framework 4.8
- Uses Harmony 2.4 for runtime patching
- Integrates with OWML 2.9+
- Shared core library provides: UDP receiver, rotation processing, smoothing algorithms

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

This mod ships `CameraUnlock.Core.dll`, which is MIT under a separate copyright
holder, so its notice travels in the release ZIP as well. Every third-party
component, what it is licensed under and whether it is redistributed is recorded
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- [Mobius Digital](https://www.mobiusdigitalgames.com/) and Annapurna Interactive - Outer Wilds
- [OWML](https://github.com/ow-mods/owml) - Mod loading framework
- [Outer Wilds Mod Manager](https://github.com/ow-mods/ow-mod-man) - How users install this mod
- [Harmony](https://github.com/pardeike/Harmony) / [HarmonyX](https://github.com/BepInEx/HarmonyX) - Runtime patching library
- [OpenTrack](https://github.com/opentrack/opentrack) - The tracking protocol this mod speaks

Outer Wilds is the property of Mobius Digital and Annapurna Interactive. This is
an unofficial fan project, not affiliated with or endorsed by them, and it needs
a legitimately purchased copy of the game. It redistributes no game code or
assets. The demo clip above is gameplay footage and remains theirs.
