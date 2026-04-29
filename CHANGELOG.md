# Changelog

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
