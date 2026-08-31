# Third-Party Notices

OuterWildsHeadTracking bundles, links against, or credits the third-party
components listed below. Each remains the property of its authors and is used
under its own licence. Where a licence requires the copyright notice, the
conditions and the disclaimer to accompany a binary distribution, the full text
is reproduced here verbatim, and this file ships at the root of the release ZIP
alongside `LICENSE` and `licenses/cameraunlock-core-LICENSE.txt`.

This repository contains no Outer Wilds code, no extracted Outer Wilds assets
and no Outer Wilds data files. The one piece of Outer Wilds material it holds is
the README demo clip, declared below. The release ZIP contains neither that clip
nor any part of the game.

| Component | Version | Licence | How it ships |
|-----------|---------|---------|--------------|
| cameraunlock-core | `fec3b4c8a6fe9c45401cf65d3d43d4f5acd22b72` | MIT | Shipped as `CameraUnlock.Core.dll` in the release ZIP |
| OWML | 2.16.2 (compile), 2.9.0+ (runtime) | MIT | Not shipped. Compile-time reference; supplied at runtime by the Outer Wilds Mod Manager |
| Lib.Harmony | 2.4.1 | MIT | Not shipped. Compile-time reference only |
| HarmonyX | supplied by OWML | MIT | Not shipped. Loaded at runtime from OWML |
| OuterWildsGameLibs | 1.1.16.1372 | see below | Not shipped. Compile-time reference only |
| OpenTrack | n/a | ISC | Not shipped. Wire-protocol interoperability only |

---

## cameraunlock-core

Git submodule at `cameraunlock-core/`, built into `CameraUnlock.Core.dll`, which
is redistributed in the release ZIP next to `OuterWildsHeadTracking.dll`.

It is our own shared code, but it carries a different copyright holder from this
mod's `LICENSE` (which is `Copyright (c) 2025 itsloopyo`), so its notice is
reproduced here and also shipped as `licenses/cameraunlock-core-LICENSE.txt`.

- Pinned commit: `fec3b4c8a6fe9c45401cf65d3d43d4f5acd22b72`
- Source: https://github.com/itsloopyo/cameraunlock-core

```
MIT License

Copyright (c) 2026 CameraUnlock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## OWML (Outer Wilds Mod Loader)

- **Licence:** MIT
- **Authors:** AmazingAlek, Raicuparta, _nebula, TAImatem, and the ow-mods contributors
- **Source:** https://github.com/ow-mods/owml
- **Usage:** the mod loader this plugin runs under. Referenced at build time via
  the `OWML` NuGet package (2.16.2) with `IncludeAssets=compile` and
  `ExcludeAssets=runtime`, so no OWML assembly is copied into our build output or
  the release ZIP. At runtime OWML is supplied by the Outer Wilds Mod Manager.
  This mod does not bundle, download, patch or modify OWML.

## Outer Wilds Mod Manager

- **Licence:** GPL-3.0
- **Authors:** Bwc9876, Raicuparta, and the ow-mods contributors
- **Source:** https://github.com/ow-mods/ow-mod-man
- **Usage:** the application users install this mod with, and which provisions
  OWML on their behalf. Not bundled, not linked, not modified, and no part of it
  is redistributed here. Named only to tell users where to get it, so its
  copyleft terms do not reach this mod.

---

## Lib.Harmony and HarmonyX

Two distinct projects, both MIT, neither redistributed by us. They are listed
separately because the mod compiles against one and runs against the other.

- **Lib.Harmony 2.4.1** - Copyright (c) Andreas Pardeike.
  https://github.com/pardeike/Harmony
  Referenced at build time only (`IncludeAssets=compile`, `PrivateAssets=all`),
  so `0Harmony.dll` is not copied into our build output or the release ZIP.
- **HarmonyX** - a fork of Harmony 2 maintained by the BepInEx contributors.
  https://github.com/BepInEx/HarmonyX
  This is the implementation that actually services our `[HarmonyPatch]`
  attributes at runtime, loaded from OWML's own installation. We neither ship
  nor download it.

---

## OuterWildsGameLibs

- **Version:** 1.1.16.1372
- **Publisher:** the ow-mods organisation
- **Source:** https://github.com/ow-mods/OuterWildsGameLibs
- **Usage:** the NuGet package the Outer Wilds modding ecosystem uses as its
  build-time reference assemblies. It contains stripped and publicized Outer
  Wilds and Unity assemblies, which is how this mod resolves game types such as
  `PlayerCameraController` at compile time.

Stated plainly, because it is the one place game-derived material touches this
project: **we reference it, we do not redistribute it.** The package reference
sets `IncludeAssets=compile`, `ExcludeAssets=runtime` and `PrivateAssets=all`,
so nothing from it is copied into `OuterWildsHeadTracking.dll`, into our build
output, or into the release ZIP. It is not committed to this repository. It is
restored from NuGet onto the machine doing the build, and only the compiler
sees it. At runtime the real assemblies come from the user's own installed copy
of the game.

The underlying assemblies remain the property of Mobius Digital and Annapurna
Interactive and are not covered by this project's MIT licence. We make no claim
of any right in them, and this mod requires a legitimately purchased copy of the
game. Anyone uncomfortable with that packaging arrangement should raise it with
the ow-mods organisation, who publish the package.

---

## OpenTrack

- **Licence:** ISC
- **Source:** https://github.com/opentrack/opentrack
- **Usage:** not bundled and not linked. This mod implements the OpenTrack UDP
  pose datagram layout so that OpenTrack and compatible trackers can drive it.
  No OpenTrack code, headers or binaries are copied, linked or redistributed, so
  its licence triggers no notice obligation here. It is credited because the wire
  format is its work.

---

## Outer Wilds footage

- **File:** `assets/readme-clip.gif`
- **Rights holder:** Mobius Digital and Annapurna Interactive, together with the
  rights holders of any third-party marks visible in frame.
- **Usage:** roughly ten seconds of ordinary gameplay recorded from the game
  running with this mod, captured on a legitimately purchased copy, embedded at
  the top of the README so a reader can see what the mod does before installing
  it. No cutscene, no story content, no soundtrack, and not re-uploaded
  marketing material.
- **Distribution:** kept in this repository only. The packaging script copies no
  part of `assets/`, so the clip is in neither the release ZIP nor anything the
  Outer Wilds Mod Manager or the Lopari launcher deploys. The README's copy
  inside the ZIP references it by absolute URL rather than carrying the file.
- **Licence:** none is granted or implied. This material is not covered by the
  MIT licence in `LICENSE`, and nothing here permits reuse of it. Rights holders
  who would rather it were not published: open an issue or reach us on Discord
  and it comes down.

---

## Outer Wilds

Outer Wilds is the property of Mobius Digital and Annapurna Interactive. This
mod is an unofficial fan project, is not affiliated with or endorsed by them,
and requires a legitimately purchased copy of the game. Buy Outer Wilds at
https://store.steampowered.com/app/753640/Outer_Wilds/.

Outer Wilds and all related names, logos, characters and marks are trademarks of
their respective owners. They are used here only to identify the game this mod
applies to, which is nominative use and not a claim of any right in them.

This mod redistributes no game code, no game assets and no proprietary game
DLLs. It works entirely through runtime reflection and Harmony method patching
against type and method names, which are resolved from the user's own installed
copy of the game. It contains no decompiled or disassembled game code, no copied
byte signatures, and no hardcoded engine addresses or structure offsets.
