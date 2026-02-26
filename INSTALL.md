# Installation Guide

## Prerequisites

1. **Outer Wilds** - The base game (Steam or Epic Games)
2. **Outer Wilds Mod Manager** - Download from [outerwildsmods.com](https://outerwildsmods.com/mod-manager/)
3. **Head tracking app** - See [Tracking Apps](#tracking-apps) below

## Installing the Mod

### Option 1: From Mod Manager (Recommended)

1. Open the Outer Wilds Mod Manager
2. Search for "Head Tracking"
3. Click Install
4. Launch the game through the mod manager

### Option 2: Manual Installation

1. Download the latest release zip from the releases page
2. Extract to: `%APPDATA%\OuterWildsModManager\OWML\Mods\HeadCannon.OuterWildsHeadTracking\`
3. The folder should contain:
   - `OuterWildsHeadTracking.dll`
   - `HeadCannon.Core.dll`
   - `manifest.json`
   - `default-config.json`

## Tracking Apps

You need a head tracking source that sends data via the OpenTrack UDP protocol.

**Note:** This mod includes built-in smoothing to handle network jitter, so if your tracking app already provides a filtered signal, you can send directly from your phone to the mod on port 4242 without needing OpenTrack on PC. OpenTrack is still useful for curve mapping and visual preview.

### Phone App (iOS/Android)

1. Install an OpenTrack-compatible head tracking app from your phone's app store (search for "head tracker" or "opentrack")
2. Open the app and go to Settings
3. Set the IP address to your PC's local IP (find with `ipconfig` in Command Prompt)
4. Set the port to `4242`
5. Select "OpenTrack" as the output type
6. Tap "Start" to begin tracking

### OpenTrack (Windows/Linux)

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your input source (webcam neuralnet, phone via UDP, or dedicated hardware)
3. Set output to "UDP over network"
4. Configure: IP = `127.0.0.1`, Port = `4242`
5. Start tracking

### Webcam/Face Tracker

1. Use OpenTrack's built-in neuralnet tracker, or install standalone face-tracking software
2. Configure your webcam as the input
3. Set output to UDP, port `4242`
4. Start tracking

## First-Time Setup

1. Start your tracking app and verify it's sending data
2. Launch Outer Wilds through the mod manager
3. Load your save or start a new game
4. Look straight ahead at your monitor
5. Press **Home** to set your center position
6. Move your head - you should see the camera respond

## Troubleshooting

### Camera not responding to head movement

1. Check that your tracking app is running and sending data
2. Verify the port matches (default: 4242)
3. If using WiFi, ensure your phone is on the same network as your PC
4. Check Windows Firewall - allow UDP port 4242 for inbound connections
5. Press **End** to ensure tracking is enabled (not toggled off)

### Camera jittering

1. Enable **Adaptive Smoothing** in mod settings (enabled by default)
2. If using WiFi, ensure good signal strength
3. Increase the **Smoothing** value in mod settings

### Tracking feels inverted

Adjust the sensitivity values in mod settings. Negative values invert the axis.

### Center position drifts over time

Press **Home** to recenter at any time. Some phone tracking apps may drift slightly over extended play sessions.

## Configuration

Access mod settings through:
- **OWML Mod Manager** - Click the gear icon next to the mod
- **In-game** - Pause menu > Mod Settings > Head Tracking

| Setting | Description |
|---------|-------------|
| **opentrackPort** | UDP port for tracking data (restart required) |
| **yawSensitivity** | Left/right look multiplier |
| **pitchSensitivity** | Up/down look multiplier |
| **rollSensitivity** | Head tilt multiplier |
| **smoothing** | Smoothing amount (0-1) |
| **adaptiveSmoothing** | Auto-smooth for WiFi connections |

## Firewall Configuration

If tracking doesn't work, you may need to allow the port through Windows Firewall:

1. Open Windows Defender Firewall
2. Click "Advanced settings"
3. Select "Inbound Rules" > "New Rule"
4. Select "Port" > Next
5. Select "UDP" and enter `4242`
6. Select "Allow the connection"
7. Apply to all profiles
8. Name it "Outer Wilds Head Tracking"

## Uninstalling

### Via Mod Manager
Click the uninstall button next to the mod in OWML.

### Manual
Delete the folder: `%APPDATA%\OuterWildsModManager\OWML\Mods\HeadCannon.OuterWildsHeadTracking\`
