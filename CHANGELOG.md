# Changelog

## [1.3.0] - 2026-08-20

### Added

- drop mod-side centring, let the tracker app own the centre

### Fixed

- migrate to the per-connection smoothing pair
- give the forward lean its own travel budget again

## [Unreleased]

### Changed

- The flashlight now turns 1.5x your head rotation rather than matching it, which is
  what every other mod in the fleet does. When you turn your head your eyes end up
  past the centre of the screen, so a beam matched to the view alone lands short of
  what you are actually looking at. `flashlightMultiplier` in the mod settings sets
  it: `1.0` restores the old behaviour, `0` leaves the beam on the aim.

### Fixed

- Rotation was filtered twice. `OpenTrackClient` built its `TrackingProcessor` with
  both smoothing values pinned to 0 and called that "smoothing disabled", then
  `SimpleCameraPatch` ran a second exponential filter over the result. 0 is not a
  bypass: the speed clamp inside the shared smoothing helper turns it into a flat
  20 ms time constant, so the two stages in series roughly doubled the intended lag,
  and `localSmoothing` / `remoteSmoothing` never reached the rotation pipeline at all.
  The processor now takes the configured pair and re-reads the connection's locality
  every frame, as the position pipeline already did, and the camera patch applies what
  it is given.
- Nine guards meant to catch an uncaptured base camera rotation could never fire.
  They tested the quaternion for the all-zero value, but the field is initialised to
  `Quaternion.identity` and only ever assigned the product of two unit quaternions, so
  the all-zero value never occurs. Seven of them sit behind a check that the head
  tracking rotation is not identity, which already proves the base rotation was
  captured, and are gone; an eighth went with `TemporaryWorldRotationScope`, a class
  nothing constructed. The signalscope aim override is the one site with no such
  precheck, and now tests an explicit capture flag, so it no longer overrides the
  game's own aim direction before the camera patch has run once, and no longer
  disables itself for the fraction of a degree where a level view compares equal to
  identity.

### Added

- Name the UDP port in the startup log line, and point the troubleshooting steps
  at `%APPDATA%\OuterWildsModManager\OWML\Logs\latest.txt` so a bug report has
  one file to attach

### Changed

- Replace the `smoothing` setting with `localSmoothing` (default 0.0) and `remoteSmoothing` (default 0.15), selected per connection from the packet source address
- Remove `adaptiveSmoothing` and `positionSmoothing`: position now uses the same connection-selected value as rotation
- Remove the hidden 0.15 baseline smoothing floor. `localSmoothing` 0.0 leaves only
  the frame-interpolation floor, a flat 20 ms time constant that keeps a low-rate
  tracker smooth on a high-refresh display
- Remove all mod-side recentring: the `Home` / `Ctrl+Shift+T` hotkey, the tracker-app
  recenter request handling and the centre offset. The tracker app owns the centre, so
  the mod now applies the incoming pose as absolute. Centre the view in your tracking app

## [1.2.3] - 2026-08-07

### Fixed

- keep the tracking center across toggles and loop resets

## [1.2.2] - 2026-08-03

### Fixed

- snap the reticle back to centre when tracking goes identity

## [1.2.1] - 2026-08-03

### Fixed

- keep interaction raycasts and prompts glued to the reticle
- recenter from the live pose and honor tracker app recenter requests

## [1.2.0] - 2026-07-27

### Added

- add PoseInterpolator to OpenTrack rotation pipeline

### Fixed

- invert positional Z so leaning in moves the camera forward
- remove renderer-scan log spam from the OWML console
- stop crosshair judder during mouse look by keying dialogue damping to conversation state
- stop rotating the helmet HUD camera

## [1.1.0] - 2026-04-29

### Other

- Remove automatic recenter on unpause
- Add asymmetric Z position limit (positionLimitZBack) to prevent backward camera clipping
- Rewrite camera system: transform-based rotation with per-axis smoothing
- Add tracking mode cycling and chord hotkey bindings

## [1.0.3] - 2026-03-05

### Other

- Rename headcannon-core to cameraunlock-core and add cross-platform release support
- Add 6DOF positional tracking with neck model simulation


## [1.0.2] - 2026-02-26

- Restructure mod around shared cameraunlock-core library

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - Unreleased

### Added
- Initial release of Outer Wilds Head Tracking mod
- Head tracking support via OpenTrack UDP protocol
- Configurable sensitivity and smoothing
- In-game toggle and recenter controls
