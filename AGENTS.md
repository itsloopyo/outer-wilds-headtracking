<!-- managed by ticketing app - edit via Agents in the sidebar; changes here are overwritten on the next sync -->

<!-- agent: Head Tracking -->
# CameraUnlock Head Tracking Mods — The Definitive Guide

Single source of truth for building decoupled look+aim head tracking mods across the CameraUnlock monorepo.

---

## Ethics and Conduct

Non-negotiable:

- **No copyrighted game code in this repo.** Never copy, decompile, or redistribute game source, assets, or proprietary DLLs. Game assemblies are referenced at build time from local installs and are gitignored.
- **No piracy facilitation.** Mods require a legitimately purchased game. Never bypass DRM, license checks, or anti-cheat.
- **Reverse engineering only where necessary.** Reflection and Harmony at runtime; no distributing decompiled source or reconstructed game logic.
- **Respect framework licenses** (BepInEx LGPL-2.1, HarmonyX MIT, MelonLoader Apache-2.0, OpenTrack ISC). Attribute in THIRD-PARTY-NOTICES.
- **All our code is MIT.** Copyright: itsloopyo.
- **Never malicious.** No data exfil, phone-home, ads, destructive save edits, anti-cheat interference, or multiplayer cheating.
- **Credit game developers** in READMEs.

---

## Project Philosophy

1. **Decoupled look and aim.** Head moves the view; mouse/controller still controls aim. This is the core value prop.
2. **Zero impact on game logic.** Raycasts, physics, hitboxes, and aim direction are identical whether tracking is on or off.
3. **Fail fast, never fail silent.** No swallowed exceptions, no silent fallbacks.
4. **No over-engineering.** Don't add features, abstractions, or error handling beyond what's asked.
5. **Easy to churn out.** New mod from scratch to working ZIP in a single session — don't fight the shared patterns.

---

## Hard Rules

- **Isolate tracking from game logic.** Either (1) modify `camera.worldToCameraMatrix` only (rendering path, game reads `camera.transform` for aim), or (2) modify `camera.transform` with save/restore so game logic sees the clean rotation. In C++ engines: save clean camera state before the game's update tick, inject tracking only in the render phase.
- **Fail fast.** If something fails, let it throw.
- **CRLF for .cmd files.** `Write` outputs LF. After writing any `.cmd`/`.bat`, run `unix2dos <file>`.
- **cameraunlock-core** is the shared submodule name. DLLs are `CameraUnlock.Core.*`.
- **Never commit:** `.claude/`, `.pixi/`, `bin/`, `obj/`, `libs/`, `release/`, `.vs/`, `*.user`.
- **Never use em-dashes (—).** Use normal dashes (-) only. Applies everywhere: code, comments, docs, commit messages, chat.

---

## Architecture

### Data Flow

```
OpenTrack / Phone App (UDP:4242, variable sample rate)
  → OpenTrackReceiver          [Core.Protocol]     Thread-safe UDP socket
    → PoseInterpolator         [Core.Processing]   Sample rate → frame rate (EMA interval estimate + velocity extrapolation)
      → TrackingProcessor      [Core.Processing]   center → deadzone → smooth → sensitivity
        → ViewMatrixModifier   [Core.Unity]        camera.worldToCameraMatrix (or C++ equivalent)
```

**Sample rate is not fixed.** The interpolator estimates the incoming sample interval via EMA (`IntervalBlend = 0.3f`, clamped 0.001–0.2s). Tracker rate can be anything — 30, 60, 90, 120Hz, or irregular — and the interpolator bridges it to frame rate with velocity extrapolation (`MaxExtrapolationFraction = 0.5f`) to eliminate flat spots on high-refresh displays. The old "30Hz" constant exists only as the seed estimate until real samples arrive.

### Core Library Assemblies (C#)

| Assembly | Purpose | Unity |
|----------|---------|-------|
| `CameraUnlock.Core` | Framework-agnostic: types, protocol, processing, math, config | No |
| `CameraUnlock.Core.Unity` | ViewMatrixModifier, SelfHealingModBase, UI, canvas compensation | Yes |
| `CameraUnlock.Core.Unity.BepInEx` | BepInEx config binding, hotkey base class | Yes + BepInEx |
| `CameraUnlock.Core.Unity.Harmony` | Harmony transpiler patterns | Yes + Harmony |

Multi-targets net35/net40/netstandard2.0/net472/net48 for Unity 2017 (Obra Dinn) through modern (Subnautica).

### C++ Core (cameraunlock-core/cpp/)

Headers mirror the C# types: `math/smoothing_utils.h`, processing/interpolator primitives, REFramework utilities (`cameraunlock_reframework`), GUI marker compensation helpers.

### Key Entry Points

- **`StaticHeadTrackingCore`** — static singleton. `Initialize()`, `Update()`, `GetProcessedPose()`.
- **`SelfHealingModBase`** — MonoBehaviour base that survives scene changes via `DontDestroyOnLoad` + auto-recreate.
- **`ViewMatrixModifier`** — `ApplyHeadRotation(cam, yaw, pitch, roll)`. `ApplyHeadRotationDecomposed()` for world-space yaw.
- **`AimDecoupler`** — `ComputeAimDirectionLocal()` inverts tracking rotation for stable aim vector.
- **`ScreenOffsetCalculator`** — FOV-based tangent projection for reticle/UI compensation.

---

## Camera System

### The Fundamental Rule

**Head tracking only modifies what the player sees. It never modifies what the game thinks the camera is doing.**

### View Matrix Modification (preferred, Unity)

Used by: Subnautica, Green Hell, Gone Home, Firewatch.

1. Always call `cam.ResetWorldToCameraMatrix()` before reading `cam.worldToCameraMatrix` (otherwise it accumulates prior frames' modifications).
2. Save the original view matrix.
3. `cam.worldToCameraMatrix = headRotMatrix * gameViewMatrix`
4. Roll is inverted in the Euler variant: `Quaternion.Euler(pitch, yaw, -roll)`.
5. For world-space yaw (prevents leaning artifacts at extreme angles), use `ApplyHeadRotationDecomposed`.

`camera.transform.forward` remains the aim direction — aim decoupling falls out naturally.

### Transform Save/Restore (alternative, Unity)

Used by: Outer Wilds, Firewatch (screen-space effects layer).

1. `Camera.onPreCull`: save `camera.transform.rotation`, apply tracked rotation.
2. Rendering happens with tracked rotation (screen-space effects see it correctly).
3. `Camera.onPostRender`: restore.

Use when screen-space effects (lens flares, motion blur) read `camera.transform` instead of the view matrix.

### Harmony Prefix/Postfix (Unity, per game method)

Used by: Green Hell, Outer Wilds, Obra Dinn. For game methods that read camera direction:

```
Prefix:  remove tracking (restore base rotation / reset matrix)
Postfix: reapply tracking
```

### C++ Pre/Post Hook (RE Engine, REDengine, similar)

Used by: dying-light-2, resident-evil-requiem, witcher-3.

1. **Pre-hook (before game's camera update):** restore clean camera state.
2. **Game update runs** — weapon aim, projectile spawning, AI vision, physics all read the clean rotation.
3. **Post-hook:** save the game's intended rotation.
4. **Render-phase hook** (e.g., `OnPostBeginRendering`): inject head rotation into the view matrix, keeping the head-tracked position.

The render-phase injection is what the player sees; the pre/post sandwich ensures the game never observes tracked state. See RE:Requiem `camera_hook.cpp` for a concrete reference.

### Camera Timing

- Apply in `Camera.onPreCull` (after game camera positioning, before rendering), not `LateUpdate`.
- Register via `Camera.onPreCull += OnCameraPreCull`.
- Filter UI cameras via `cam.name` or `cam.cullingMask` if needed.
- Harmony variant: patch the game's camera controller `LateUpdate` with prefix (remove) + postfix (apply).

### Rotation Composition

Standard YPR order used across all mods:

```csharp
Quaternion yawQ   = Quaternion.AngleAxis( yaw,   Vector3.up);
Quaternion pitchQ = Quaternion.AngleAxis( pitch, Vector3.right);
Quaternion rollQ  = Quaternion.AngleAxis(-roll,  Vector3.forward);
Quaternion headRot = yawQ * pitchQ * rollQ;
// camera-local: camera.transform.rotation * headLocalRotation
```

DL2 (C++) uses quaternion shortest-arc rotation to avoid gimbal lock at extreme angles.

### Position Tracking (6DOF)

1. Process through `PositionProcessor` (sensitivity, limits, smoothing, inversion per axis).
2. Interpolate between samples via `PositionInterpolator`.
3. Apply as translation in **original view space** (before head rotation) so the offset follows body orientation, not head-rotated view.
4. Asymmetric limits: more range forward (0.30–0.40m) than backward (0.02–0.10m) to prevent clipping through the player model.
5. Horizon-locked basis (flat forward vector) for roll independence.

View-matrix math:
```csharp
Matrix4x4 H = cam.worldToCameraMatrix * originalViewMatrix.inverse;
cam.worldToCameraMatrix = H * Matrix4x4.Translate(-offset) * originalViewMatrix;
```

Transform approach: apply offset to `localPosition` in postfix, remove in next prefix.

---

## Aim Decoupling and Projectile Landing

Player head moves the view; mouse/controller controls aim. Projectiles must land where the **reticle** is drawn, not where the view is facing.

### Principle (engine-agnostic)

The game's aim/projectile/raycast code must read **clean camera rotation** (the direction the player is actually aiming). The player sees the **head-tracked** camera. The reticle is then drawn at the screen position where the clean aim direction projects into the head-tracked view.

This means: **projectiles fly straight along clean aim, and we move the reticle to match — never the other way around.**

### Unity implementation

View-matrix modification gives this for free: `camera.transform.forward` (clean) = aim, `camera.worldToCameraMatrix` (tracked) = render. Game aim/raycast code reads `transform.forward` and gets the mouse-controlled direction.

### C++ implementation (RE Engine and similar)

Pre/post hook sandwich around the game's camera update restores the clean rotation before game logic and re-injects tracking in the render phase. Weapons and projectiles see clean aim; the player sees the tracked view.

### Reticle Compensation (all engines)

1. Raycast along clean aim (`transform.forward` or clean camera matrix) to find hit distance.
2. Smooth the hit distance (~15Hz exponential) to prevent jitter.
3. Project the aim world-point through the head-tracked view/projection matrix to get screen coordinates.
4. Move the reticle UI element to that screen position.
5. If the aim point is behind the camera (extreme head turn), hide or clamp to screen edge.

**Per-framework:**
- BepInEx: find game's reticle via reflection, modify `RectTransform.anchoredPosition`.
- No-reticle games: draw custom via `IMGUIReticle` (Core.Unity).
- DL2/RE:Requiem: ImGui overlay with D3D hook, project using live FOV (read per-frame, smoothed).

**Do NOT project with per-axis yaw/pitch tangents.** The naive formula
`ndc_x = -tan(yaw) / tan(fov_h/2)`, `ndc_y = tan(pitch) / tan(fov_v/2)`
is roll-unaware and drifts horizontally the moment roll is combined
with pitch — because once the head is tilted, the pitch axis stops
being screen-vertical. Use spherical decomposition into the aim
direction, apply roll in direction space, *then* perspective-divide:

```
ax = -sin(yaw)
ay =  sin(pitch) * cos(yaw)   // `cos(yaw)` prevents orbiting on combined yaw+pitch
az =  cos(pitch) * cos(yaw)

// rotate (ax, ay) by roll in DIRECTION space, not screen space —
// screen-space roll rotation introduces FOV/aspect distortion.
rx = ax * cos(roll) - ay * sin(roll)
ry = ax * sin(roll) + ay * cos(roll)

ndc_x =  rx / az / tan(fov_h/2)
ndc_y = -ry / az / tan(fov_v/2)   // flip if NDC-y is positive-up
```

Reference implementations: `cameraunlock-core/csharp/.../Aim/ScreenOffsetCalculator.cs`
and the C++ equivalent `cameraunlock-core/cpp/.../crosshair_projection.h`.

**The roll sign must match between the camera modification and the
reticle projection.** If your camera hook writes `-roll` into the
engine's rotation state (e.g. BioShock Remastered: UE2.5 `FRotator.Roll`
is CW-positive while OpenTrack is CCW-positive, so `engine_hook` negates
the value at the boundary), then the reticle projection must use the
same-sign roll the game is actually rendering with. Using the raw
OpenTrack roll in both places puts them 180° out of phase, so the
reticle drifts horizontally on roll+pitch combinations. Concretely: if
you apply `-roll` to the game, apply `+roll` (the inverse) when
rotating `(ax, ay)` in the projection above. Easiest check: pure roll
with pitch = 0 should leave the reticle at screen centre; pure pitch
with roll = 0 should move the reticle purely vertically. If either
behaves oddly with roll involved, the signs are off.

### UI Compensation

- HUD markers/pings (Subnautica): reposition in `Canvas.willRenderCanvases`.
- Interaction text (Gone Home): move label to follow crosshair.
- Player mask/helmet (Subnautica): apply inverse H to keep screen-fixed.
- Map markers (Outer Wilds): temporarily apply/remove tracking in HUD update prefix/postfix.
- World-anchored GUI markers (RE:Requiem): roll-aware reprojection — when head roll exceeds ~0.1°, apply inverse roll to anchor points before offsetting.

### Crosshair Suppression

When drawing our own reticle, hide the game's built-in crosshair:
- Reflection (e.g., `NGUI_HUD.ReticuleSprite`, `HUDCrosshair`), disable GameObject or set alpha 0.
- Restore on mod disable.
- DL2: RTTI-based vtable scan to suppress `GuiCrosshairData`.

---

## Processing Pipeline

### `TrackingProcessor.Process()`

1. Raw Euler → Quaternion.
2. Center offset via quaternion inverse multiplication (gimbal-lock-free).
3. Quaternion → Euler (YXZ) for per-axis processing.
4. Per-axis deadzone (degrees, default 0).
5. Per-axis exponential smoothing, frame-rate independent: `t = 1 - exp(-speed * dt)` where `speed = Lerp(50, 0.1, smoothing)`.
6. Per-axis sensitivity multiplier and optional inversion.

### Smoothing Model

- **Baseline floor 0.15** (`SmoothingUtils.BaselineSmoothing` / `kBaselineSmoothing`). `GetEffectiveSmoothing()` enforces this minimum on every connection. Below the floor, high-refresh displays show jitter — particularly on wireless/WiFi trackers. Do not remove this floor.
- **Frame-rate independent:** the exponential formula converges on identical visual latency regardless of frame rate. At 60Hz with smoothing=0.15, per-frame factor ≈ 0.4, settling in ~100–150ms. At 144Hz, per-frame factor is smaller but cumulative settling time is unchanged.
- **User SmoothingFactor:** 0.0 = minimum (floor of 0.15 applied); 1.0 = heavy (~5s settling).
- **PoseInterpolator:** sits between receiver and processor. Active whenever tracking smoothing ≥ 0.001. EMA-estimates the tracker's true sample interval so any rate works. Velocity extrapolation up to half a sample period past the latest known position eliminates flat spots at high refresh rates.

### Auto-Recenter

- Recenter on first connection (transition from no-data to fresh-data).
- Wait ~5 stabilization frames — initial samples are noisy.
- `Recenter()` sets current smoothed pose as the center offset via quaternion inverse.

### Tracking Loss

- **Hold:** display last known pose (no snap to center).
- **Freshness:** `IsDataFresh(maxAgeMs = 500)`.
- **Resume:** smoothing blends back naturally — never snap.
- Outer Wilds variant fades out tracking over 0.5s, auto-recenters after 60 frames of no data.

---

## Game State Detection

Every mod detects gameplay vs. menus/loading/paused and suppresses tracking outside gameplay.

| Method | Used By | How |
|--------|---------|-----|
| Reflection on game singletons | Green Hell, Gone Home, Firewatch | `PauseManager.isPaused`, `MenuInGame.m_Active`, … |
| `Time.timeScale` | Subnautica, Obra Dinn | `<= 0` = paused |
| `Cursor.lockState` | Firewatch | `Locked` = gameplay |
| Scene name | Firewatch, Obra Dinn | Skip MainMenu, Loading, boot |
| Game-specific flags | Obra Dinn | `Clock.play.running`, `Player.inputEnabled` |
| Level pointer chain | DL2 (C++) | `CLobbySteam → CGame → CLevel → IsLoading()` |

**Best practices:**
- Cache reflection (Type, FieldInfo) once in static fields. Use `GameStateDetectorBase` from `Core.State`.
- Rate-limit detection to ~0.1s or every 30 frames.
- Pause in: main/pause menus, loading, inventory/map, dialogue/cutscenes, death screens.
- Hold (don't reset) during brief overlaps like walkie-talkies.
- Warmup ~1.5s after scene load before applying tracking.

---

## Controls

### Default Hotkeys (nav-cluster keys)

| Action | Key | VK | Description |
|--------|-----|----|-------------|
| Recenter | `Home` | `0x24` | Set current head pose as center |
| Toggle tracking | `End` | `0x23` | Enable/disable |
| Toggle position (6DOF) | `Page Up` | `0x21` | |
| Toggle yaw mode | `Page Down` | `0x22` | World ↔ local yaw |

### Chord Alternatives (for keyboards without a nav cluster)

Every mod must also register `Ctrl+Shift+<letter>` chord bindings, where the letters are drawn from the **T/Y/U/G/H/J** cluster (a 2x3 block in the middle of the keyboard, easy to find by touch). `Ctrl+Shift+<letter>` is universally avoided by games (Ctrl is crouch / interact, Shift is sprint / weapon-wheel; both together is well outside any in-game bind set), so this set works reliably across the whole CameraUnlock catalogue.

| Action | Chord | Position in cluster |
|--------|-------|---------------------|
| Recenter | `Ctrl+Shift+T` | top-left |
| Toggle tracking | `Ctrl+Shift+Y` | top-middle |
| Toggle position | `Ctrl+Shift+G` | bottom-left |
| (4th toggle, if needed) | `Ctrl+Shift+H` | bottom-middle |
| (5th toggle, if needed) | `Ctrl+Shift+U` | top-right |
| (6th toggle, if needed) | `Ctrl+Shift+J` | bottom-right |

Pick letters in the order above so the same action lands on the same chord across every mod. If a mod has fewer toggles, just drop the unused ones - never reshuffle.

If a specific game does bind any of these (very rare), document the conflict in the mod's README and pick the next-available letter from the same cluster - don't move to a different modifier or a scattered letter set.

### Implementation

- BepInEx: `ConfigEntry<KeyCode>`, `Input.GetKeyDown()` in `Update()`.
- MelonLoader: `MelonPreferences_Entry<string>` parsed to `KeyCode`.
- C++: virtual key codes polled via `GetAsyncKeyState()` on a ~60Hz thread.
- Standalone (Gone Home): INI with Unity `KeyCode` names.
- Debouncing: 0.3s minimum, or key-up/key-down state tracking, to prevent held-key repeats.

---

## Configuration Defaults

**All sensitivities default to 1.0** (rotation and position). Game-specific overrides must be justified.

| Setting | Default | Range | Notes |
|---------|---------|-------|-------|
| UDP Port | 4242 | 1024–65535 | OpenTrack standard |
| Bind Address | 0.0.0.0 | | All interfaces |
| Enable on Startup | true | | |
| Yaw / Pitch / Roll Sensitivity | 1.0 | 0.1–3.0 | |
| Invert Yaw / Pitch / Roll | false | | Some games need pitch inverted (game-specific) |
| Smoothing | 0.0 | 0.0–1.0 | Baseline 0.15 floor applied internally |
| Aim Decoupling | true | | Always on by default |
| Show Reticle | true | | |
| Data Freshness | 500 ms | | |
| Position Enabled | true | | |
| Position Sensitivity X / Y / Z | 1.0 | 0.0–5.0 | Override only when physically necessary (e.g., RE:Requiem uses 2.0 because native head-bob range is small) |
| Position Limit X | 0.30 m | 0.01–0.5 | |
| Position Limit Y (up) | 0.20 m | 0–0.5 | |
| Position Limit Y (down) | 0.05 m | 0–0.5 | Minimal |
| Position Limit Z (forward) | 0.40 m | 0.01–0.5 | |
| Position Limit Z (back) | 0.10 m | 0.01–0.5 | Prevents clipping |
| Position Smoothing | 0.15 | 0.0–1.0 | At/above baseline floor |

---

## Framework Reference

| Framework | Mods | Entry | Config | Deploy |
|-----------|------|-------|--------|--------|
| BepInEx 5 | subnautica, wobbly-life, valheim, peak, obra-dinn, tacoma | `BaseUnityPlugin` | `ConfigEntry<T>` | `BepInEx/plugins/` |
| BepInEx 6 IL2CPP | shadows-of-doubt | `BasePlugin` | `ConfigEntry<T>` | `BepInEx/plugins/` |
| MelonLoader | green-hell, firewatch | `MelonMod` | `MelonPreferences` | `Mods/` (mod), `UserLibs/` (core) |
| Mono.Cecil patcher | gone-home, eternal-afternoon, painscreek-killings | Patched `Assembly-CSharp.dll` → `ModLoader.Initialize()` | INI/cfg | `<Game>_Data/Managed/` |
| OWML | outer-wilds-headtracking | `ModBehaviour` | OWML JSON | `OWML/Mods/<uniqueName>/` |
| ASI Loader (C++) | dying-light-2 | `DllMain` → init thread | INI | Game exe dir |
| Custom (C++) | the-witness, fallout-new-vegas, witcher-3 | `DllMain` | INI | Game exe dir |
| REFramework plugin (C++) | resident-evil-requiem | `reframework_plugin_initialize` | INI | `reframework/plugins/` |

### Notes

**BepInEx 5:** install.cmd auto-downloads; config at `BepInEx/config/<GUID>.cfg`; DLLs flat in `plugins/` (except Valheim: subfolder). `.headtracking-state.json` tracks whether we installed BepInEx so uninstall only removes it if we put it there. Architecture: x64 (x86 for Obra Dinn / Unity 2017).

**MelonLoader:** mod DLL → `Mods/`, core DLLs → `UserLibs/`. Config auto-persists to `UserData/MelonPreferences.cfg`. Firewatch pins 0.5.7 (0.6.x crashes on Unity 2017 Mono). `[MelonInfo]` + `[MelonGame]` attributes required.

**Mono.Cecil patcher:** `BootstrapPatcher.cs` injects a reflection-based call into `Assembly-CSharp.dll`. Back up original as `.original`. `ModRecreator` handles scene changes. `Mono.Cecil.dll` ships in the release ZIP.

**OWML:** `manifest.json` (uniqueName, version, owmlVersion), `default-config.json` (mod manager settings UI). Deploy to `%APPDATA%\OuterWildsModManager\OWML\Mods\<uniqueName>\`. Uses extern aliases for Unity assembly collisions.

**ASI Loader (DL2):** CMake + VS2022, C++17. Output `.asi`. ASI Loader (`winmm.dll`, renamed from `dinput8.dll`) auto-downloaded. MinHook + ImGui + Kiero for D3D12 overlay. Pattern scanning for game functions.

**REFramework plugin (RE:Requiem):** `reframework_plugin_initialize` entry. Hooks game methods by type-name lookup via REFramework's managed-type registry, with a Transform/GameObject-parent-chain fallback. GUI compensation separates crosshair (small elements, child[2]) from world-anchored markers (child[1] under `Gui_ui2010*`).

### Special Cases

- **valheim:** `BepInEx/plugins/ValheimHeadTracking/` subfolder (not flat).
- **shadows-of-doubt:** BepInEx 6 IL2CPP, `BEPINEX_URL` override.
- **dying-light-2:** C++, no .csproj, dual ZIP (installer + Nexus extract-to-folder), exe at `ph/work/bin/x64/`.
- **obra-dinn:** net35, BepInEx x86.
- **outer-wilds-headtracking:** directory has `-headtracking` suffix.
- **firewatch:** MelonLoader 0.5.7 pinned.
- **gone-home:** standalone Mono.Cecil, no mod loader.

---

## Build & Release

### pixi.toml Tasks (standard)

```
sync | setup | restore | build | install | uninstall | package | clean | release
```

### Project Layout

```
<mod-root>/
├── pixi.toml, CHANGELOG.md, README.md, LICENSE, THIRD-PARTY-NOTICES.md
├── Directory.Build.props       # Sets UnityEnginePath for submodule
├── .gitattributes, .gitignore, .gitmodules
├── scripts/                    # setup-libs / deploy / package / release.ps1, install/uninstall.cmd
├── src/<ModName>/
│   ├── <ModName>.csproj
│   ├── libs/                   # Gitignored game DLLs + UnityStubs.cs (NOT gitignored)
│   └── *.cs
├── vendor/<loader-slug>/       # Committed. Refreshed by packager. See Vendoring section.
├── assets/                     # README media
├── release/                    # Output ZIPs (gitignored)
└── cameraunlock-core/          # Shared submodule
```

### .csproj Standards

```xml
<TargetFramework>net48</TargetFramework>    <!-- or net472 / net35 -->
<AssemblyName>GameNameHeadTracking</AssemblyName>
<RootNamespace>GameNameHeadTracking</RootNamespace>
<Version>1.0.0</Version>
<LangVersion>latest</LangVersion>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AllowUnsafeBlocks>false</AllowUnsafeBlocks>
```

Project references use the submodule (never NuGet for core libs). Game DLL references from `libs/` use `<Private>false</Private>`.

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <UnityEnginePath>$(MSBuildThisFileDirectory)src/GameNameHeadTracking/libs</UnityEnginePath>
  </PropertyGroup>
</Project>
```

### .gitattributes

```
* text=auto eol=lf
*.cmd text eol=crlf
*.bat text eol=crlf
```

.cmd/.bat must be CRLF or they silently fail on Windows.

### Release ZIPs

**Installer (GitHub Release):** `<ModName>-v<version>-installer.zip` - install.cmd, uninstall.cmd, plugins/ (or mod/), vendor/<loader-slug>/ (loader zip + LICENSE + README.md), README/LICENSE/CHANGELOG/THIRD-PARTY-NOTICES.

**Nexus (extract to game folder):** `<ModName>-v<version>-nexus.zip` - only the deploy-path subtree containing the DLLs. No vendored loader (Nexus users manage their own).

**MUST NOT be in release ZIPs:** `pixi.toml`, `modules/`, `bin/`, `obj/`, `libs/`, `.claude/`, `.pixi/`, any `.ps1`/`.bat`. Vendor dirs ship the loader zip + LICENSE + README.md only — no scripts.

### Shared Packager

`cameraunlock-core/scripts/package-bepinex-mod.ps1`: params `-ModName`, `-CsprojPath`, `-BuildOutputDir`, `-ModDlls`, `-ProjectRoot`, `-CreateNexusZip`. Output: `release/`.

### install.cmd / uninstall.cmd — Unified Launcher Contract

**Every mod in scope** (bioshock-remastered, dying-light-2, gone-home, green-hell, obra-dinn, peak, resident-evil-requiem, subnautica — and every new BepInEx/MelonLoader/ASI/REFramework/Cecil/shim mod) **ships install.cmd and uninstall.cmd with identical CLI semantics**. The CameraUnlock launcher drives them programmatically; the contract below is what the launcher relies on, and what a human running the `.cmd` by hand also gets.

Outer Wilds is the one explicit exception — it ships through OWML's own installer ecosystem and does not participate in this contract.

#### CLI surface

```
install.cmd   [GAME_PATH] [/y]
uninstall.cmd [GAME_PATH] [/y] [/force]
```

- **GAME_PATH** (optional, positional): Explicit game install root. Wins over all auto-detection. Must be an existing directory; if not, script errors out.
- **`/y`** (aliases: `-y`, `--yes`, `/Y`): Non-interactive mode. Skip every `pause`, prompt, and "type install to continue" gate. Error-exit paths still print the diagnostic — they just don't pause. The launcher always passes `/y`; users running by hand rarely do.
- **`/force`** (uninstall only; aliases: `--force`, `/Force`): Remove the mod loader/framework even if the state file says `installed_by_us: false`. Never touches anything outside the loader's own directories.

**Parsing is order-independent and case-insensitive.** The first positional arg that resolves to an existing directory is `GAME_PATH`; everything else must be a recognised flag. Unknown flags are a hard error (exit 2) — fail fast, don't silently ignore.

The legacy positional `UNATTENDED` arg-2 pattern from earlier template revisions is removed. `/y` replaces it.

#### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | User-fixable error (game not found, game running, vendored loader missing, extraction failed, loader init gate declined) |
| 2 | Unknown or malformed argument |

The launcher treats anything non-zero as "surface the last ~20 lines of stderr to the user".

#### Install flow (canonical)

1. Parse args.
2. Resolve `GAME_PATH`: explicit arg wins; otherwise call `find-game.ps1` shim reading `cameraunlock-core/data/games.json` by `GAME_ID`.
3. Check game isn't running. Exit 1 if it is.
4. Check loader presence via the canonical marker file (see Loader inventory table). If absent:
   - Extract `vendor/<loader-slug>/<loader>.zip` (the committed vendored copy) directly to the correct location. install.cmd never reaches out to the network — see Vendoring section.
   - If the vendored zip is missing, exit 1 with `"installer ZIP is corrupt, re-download"`.
   - Set `framework.installed_by_us = true` for the state file.
5. If loader was already present: log `"Existing <Loader> detected, skipping loader install, deploying plugin only."` and set `installed_by_us = false` — **but preserve `true` if the existing state file already says `true`** (we're updating a mod we previously installed; don't demote the flag).
6. Loader init gate:
   - `/y` mode: print `"Loader installed. It will initialize on first game launch."` and continue. BepInEx/MelonLoader/REFramework all self-init on first launch whether plugins are present or not.
   - Interactive mode (loader was absent before this run): show the "run the game once, then type install to continue" gate.
   - Interactive mode (loader already present): no gate, proceed.
7. Deploy mod files to the deploy path (framework-dependent — see inventory table).
8. Write updated `.headtracking-state.json`.
9. Final `pause` only if `/y` not set.

#### Uninstall flow (canonical)

1. Parse args.
2. Resolve `GAME_PATH`.
3. Check game isn't running.
4. Remove mod files listed in `MOD_DLLS` + any `LEGACY_DLLS` carried for backwards-compatibility cleanup.
5. Decide whether to remove the loader:
   - `/force` → remove unconditionally.
   - Else read `.headtracking-state.json`:
     - `framework.installed_by_us: true` → remove loader.
     - `false` or missing state → leave alone, log `"<Loader> was not installed by this mod - leaving intact. Use /force to remove anyway."`
6. Remove the state file.
7. Final `pause` only if `/y` not set.

#### State file: `.headtracking-state.json`

Lives at the **game install root** (the directory resolved as `GAME_PATH`, not a subfolder). One file per game, shared across all CameraUnlock mods for that game — in practice we only ever ship one per game, so no cross-mod collisions today. If that ever changes, the schema grows a `mods: []` array.

Canonical schema:

```json
{
  "schema_version": 1,
  "framework": {
    "type": "BepInEx",
    "installed_by_us": true,
    "version": "5.4.23.2"
  },
  "mod": {
    "id": "subnautica",
    "name": "SubnauticaHeadTracking",
    "version": "1.0.0",
    "installed_at": "2026-04-21T14:30:00Z"
  }
}
```

Field rules:
- `schema_version`: always `1`. Bump + migrate if the shape ever changes.
- `framework.type`: one of `"BepInEx"`, `"MelonLoader"`, `"MonoCecil"`, `"ASILoader"`, `"REFramework"`, `"None"` (for shim-only mods like BioShock Remastered).
- `framework.installed_by_us`: `true` only if this install.cmd extracted the loader. Never regress `true → false` across re-installs.
- `framework.version`: the loader version we shipped (informational; omit when we didn't install it).
- `mod.id`: the slug matching `games.json` (`subnautica`, `green-hell`, `resident-evil-requiem`, …).
- `mod.name`: the `AssemblyName` / `RootNamespace` (`SubnauticaHeadTracking`).
- `mod.version`: SemVer of the installed mod.
- `mod.installed_at`: UTC ISO-8601 timestamp.

**The state file is the only attribution marker.** No sidecar files inside loader directories, no registry keys, nothing else. Uninstall's decision to remove the framework is made solely from this file (+ `/force`).

#### Loader inventory

Each mod's `install.cmd` dispatches to exactly one `:install_<loader>` subroutine. The marker file is what presence-check tests look for; missing = install needed.

| Framework | Mods | Vendor slug | Marker file | Loader files to remove on uninstall |
|-----------|------|-------------|-------------|-------------------------------------|
| BepInEx 5 x64 | subnautica | `bepinex` | `BepInEx/core/BepInEx.dll` | `BepInEx/`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` |
| BepInEx 5 x86 | obra-dinn | `bepinex` | `BepInEx/core/BepInEx.dll` | Same as x64 |
| BepInExPack (Thunderstore-wrapped) | peak | `bepinex` | `BepInEx/core/BepInEx.dll` | Same as x64; vendor zip extracted through a `BepInExPack_PEAK/` subfolder that must be flattened on install |
| MelonLoader | green-hell | `melonloader` | `MelonLoader/net35/MelonLoader.dll` (or `net6/`) | `MelonLoader/`, `version.dll`, `dobby.dll`, `NOTICE.txt`; `Mods/`, `UserLibs/`, `UserData/` only if empty after mod files come out |
| Mono.Cecil patcher | gone-home | `mono-cecil` | `<Managed>/Assembly-CSharp.dll.original` | Restore `.original` over `Assembly-CSharp.dll`, delete `.original`, delete `Mono.Cecil.dll` |
| Ultimate ASI Loader | dying-light-2 | `asi-loader` | `<exe-dir>/winmm.dll` (renamed from `dinput8.dll`) | `winmm.dll` (or `dinput8.dll`), any `scripts/` stub created by the loader |
| REFramework | resident-evil-requiem | `reframework` | `dinput8.dll` + `reframework/` at game root | `dinput8.dll`, `reframework/` |
| None (shim-only) | bioshock-remastered | — | N/A (the mod DLL *is* the shim — `xinput1_3.dll`) | Just the mod DLL |

**Shim-only mods** (bioshock-remastered and any future xinput/dxgi shim): `framework.type: "None"`, `framework.installed_by_us: false`. `/force` on uninstall is a no-op for framework removal — the mod DLL always comes out regardless.

#### Template layout in cameraunlock-core

Source of truth lives at `cameraunlock-core/scripts/templates/`:

```
install.cmd             # BepInEx variant (default — most mods)
install-melonloader.cmd
install-cecil.cmd
install-asi.cmd
install-reframework.cmd
install-shim.cmd        # No-loader / xinput / dxgi shim mods
uninstall.cmd           # Shared across all loaders; dispatches loader-removal by reading state file
```

Per-mod `<mod>/scripts/install.cmd` and `uninstall.cmd`:
- Copy the matching template verbatim.
- Edit **only the CONFIG BLOCK** (`GAME_ID`, `MOD_DISPLAY_NAME`, `MOD_DLLS`, `MOD_INTERNAL_NAME`, `MOD_VERSION`, `MOD_CONTROLS`, plus any loader-specific vars like `BEPINEX_ARCH`, `MANAGED_SUBFOLDER`, `BEPINEX_SUBFOLDER`, etc.).
- Never modify logic outside the CONFIG BLOCK. If you need to, fix the template and re-sync all mods.

After editing a `.cmd`, **always run `unix2dos`** on it (the Write tool outputs LF; `.cmd` must be CRLF or it silently fails on Windows — this is already in Hard Rules, reiterated here because it's the #1 regression source).

#### Canonical arg-parser block

Every template begins with this block immediately after the CONFIG BLOCK, before `call :main`:

```cmd
call :main %*
set "_EC=%errorlevel%"
if not defined YES_FLAG ( echo. & pause )
exit /b %_EC%

:main
setlocal enabledelayedexpansion
set "YES_FLAG="
set "FORCE_FLAG="
set "_GIVEN_PATH="
:parse_args
if "%~1"=="" goto :args_done
set "_ARG=%~1"
if /i "!_ARG!"=="/y"      ( set "YES_FLAG=1"   & shift & goto :parse_args )
if /i "!_ARG!"=="-y"      ( set "YES_FLAG=1"   & shift & goto :parse_args )
if /i "!_ARG!"=="--yes"   ( set "YES_FLAG=1"   & shift & goto :parse_args )
if /i "!_ARG!"=="/force"  ( set "FORCE_FLAG=1" & shift & goto :parse_args )
if /i "!_ARG!"=="--force" ( set "FORCE_FLAG=1" & shift & goto :parse_args )
if "!_ARG:~0,2!"=="--" (
    echo ERROR: unknown flag "!_ARG!"
    exit /b 2
)
if "!_ARG:~0,1!"=="/" (
    echo ERROR: unknown flag "!_ARG!"
    exit /b 2
)
if "!_ARG:~0,1!"=="-" (
    echo ERROR: unknown flag "!_ARG!"
    exit /b 2
)
if not defined _GIVEN_PATH (
    if exist "!_ARG!\" ( set "_GIVEN_PATH=!_ARG!" & shift & goto :parse_args )
)
echo ERROR: unrecognised argument "!_ARG!"
exit /b 2
:args_done
```

`install.cmd` ignores `FORCE_FLAG`; `uninstall.cmd` uses it. Both honour `YES_FLAG`.

Every interactive stop is wrapped:

```cmd
if not defined YES_FLAG pause
```

and the loader-init gate:

```cmd
if defined YES_FLAG (
    echo Loader installed. It will initialize on first game launch.
) else (
    call :prompt_loader_init
)
```

**Never call `pause` unconditionally.**

#### Launcher expectations

The CameraUnlock launcher invokes mods with:

```
install.cmd   "<detected-game-path>" /y
uninstall.cmd "<detected-game-path>" /y
uninstall.cmd "<detected-game-path>" /y /force     # deep uninstall
```

and relies on:
- Zero stdin reads (no prompts, no `set /p`, no `pause`).
- Exit 0 = success; anything else = surface stderr tail to the user.
- State file present after install, absent after uninstall.
- No dialog boxes, no `color` screen flashes beyond plain console output.
- Idempotent re-runs: installing twice is a no-op on framework, only redeploys DLLs; uninstalling twice is a no-op after the first.

Any deviation from these is a launcher-breaking bug; fix the template, not the launcher.

### release.ps1 Workflow

1. Validate semver. 2. Check main branch, clean tree, tag unused. 3. Update version in .csproj (+ plugin constant). 4. Release build. 5. Generate CHANGELOG.md from git commits (via `ReleaseWorkflow.psm1`). 6. Commit version + changelog. 7. Annotated tag `v<version>`. 8. Push commits + tag (CI release workflow triggers).

### Shared PowerShell Modules (`cameraunlock-core/powershell/`)

| Module | Purpose |
|--------|---------|
| `ReleaseWorkflow.psm1` | Version validation, changelog from commits, tag management |
| `GamePathDetection.psm1` | Registry / Steam library / game path finding |
| `ModDeployment.psm1` | DLL copying, plugin path management |
| `ModLoaderSetup.psm1` | BepInEx / MelonLoader / REFramework / ASI Loader download, vendoring (`Refresh-VendoredLoader`, `Invoke-FetchLatestLoader`) |
| `AssemblyPatching.psm1` | Mono.Cecil utilities |

### CI Workflows

Each mod has its own `.github/workflows/build.yml` and `release.yml` in its own repo. **GitHub Actions resolves workflows from each repo's `.github/workflows/` path, so these YAMLs cannot be symlinked from `cameraunlock-core`.** One reusable workflow exists for new BepInEx mods - `itsloopyo/cameraunlock-core/.github/workflows/release-bepinex-mod.yml` (used by obra-dinn, peak) - but the `build.yml` is always inline.

**`build.yml` triggers:**
- `push` on **any branch** (`branches: ['**']`, not just `main`) so feature branches produce downloadable artifacts that can be handed to a user for pre-release testing.
- `pull_request` targeting `main`.
- `paths-ignore` drops pure docs/LICENSE changes so they don't burn CI minutes.
- `if: ${{ !startsWith(github.event.head_commit.message, 'Release v') }}` on the job to skip double-building when `release.ps1` lands its version-bump commit - `release.yml` handles that path.
- Tag pushes (`v*.*.*`) are handled exclusively by `release.yml`. Do not widen `build.yml`'s `push:` section to include tags.

**`build.yml` must produce a usable install artifact.** After the "Verify build outputs" step, run the mod's packaging script and upload the installer ZIP:

```yaml
- name: Package installer
  shell: pwsh
  run: |
    Write-Host "Packaging installer ZIP..." -ForegroundColor Cyan
    powershell -ExecutionPolicy Bypass -File scripts/package-release.ps1

    if ($LASTEXITCODE -ne 0) {
      Write-Host "::error::Packaging failed"
      exit 1
    }

    Write-Host "Package created" -ForegroundColor Green

- name: Upload installer artifact
  uses: actions/upload-artifact@v7
  with:
    name: <ModName>-installer
    path: release/*-installer.zip
    retention-days: 14
    if-no-files-found: error
```

For pixi-driven mods (currently bioshock-remastered), replace the Package step with `run: pixi run package`. The rest is identical.

Conventions:
- Pin Node-24-native majors on JS actions: `actions/checkout@v6`, `actions/upload-artifact@v7`, `microsoft/setup-msbuild@v3`. The earlier v4/v4/v2 trio was Node-20-based and triggers the deprecation warning (Node 20 forced off June 2026, removed Sept 2026). Bump majors when newer ones ship.
- Artifact name `<ModName>-installer` matches the csproj/AssemblyName, lowercase/uppercase kept as-is.
- Always `path: release/*-installer.zip` (glob) so the step doesn't have to know the version or the exact filename.
- `retention-days: 14` is plenty for test-branch iteration; longer just clutters the Actions UI.
- `if-no-files-found: error` catches packaging failures that don't exit nonzero.

The end-to-end workflow this enables: branch off `main` -> push -> workflow run completes -> download the `<ModName>-installer` artifact from the Actions run page -> hand the ZIP to the user to run `install.cmd` against their game. Only after they confirm the fix, cut a real release via `release.ps1`.

**`release.yml` triggers:** push of `v*.*.*` tags only. Does its own submodule-recursive checkout, full Release build, `scripts/package-release.ps1`, release-notes generation via `generate-release-notes.ps1`, and `gh release create` with the installer and nexus ZIPs attached.

**CI packaging is offline.** `scripts/package-release.ps1` consumes whatever is committed under `vendor/` - it never hits the network. Bumping vendored loaders is a manual `pixi run update-deps` + commit step the dev does locally before tagging a release (see Vendoring section).

---

## Vendoring Third-Party Dependencies

**Doctrine: vendored is the install-time source of truth. install.cmd never reaches out to the network for a mod loader.** A new upstream release that breaks (asset renamed, asset removed from a nightly, repo moved, rate-limited) cannot break our installer because the installer doesn't talk to upstream. The committed `vendor/<loader-slug>/` tree is what ships in the release ZIP and what `install.cmd` extracts.

This is a deliberate flip from the earlier "fetch latest, fall back to vendored" pattern, which broke a real install when a REFramework nightly stopped publishing `RE9.zip`. Live installs must not depend on the upstream's day-to-day publishing decisions. The dev decides when to bump, by running `pixi run update-deps` (manual) and committing the refreshed vendor tree.

The narrow exception is loaders with licenses too restrictive to vendor (none today). Those keep an upstream-fetch path inside install.cmd; document the exception in the mod's README.

### The pattern

1. **`pixi run update-deps`** (dev, manual): walks `vendor/<loader-slug>/` for the mod and rewrites each from the latest upstream release within a pinned version range. Writes `<loader>.zip`, `LICENSE`, `README.md`. The dev reviews the diff (`git diff --stat`, scanning the README's tag/SHA) and commits.
2. **`pixi run build` / `package` / `release`**: do **not** touch the network. They consume whatever is committed under `vendor/`. Builds and CI are deterministic; the release ZIP carries exactly the vendored bytes that were on disk at commit time.
3. **`install.cmd`** at user runtime: extracts `vendor/<loader-slug>/<loader>.zip` directly. Hard-errors with `"The installer ZIP is corrupt. Re-download the release."` if the vendored zip is missing. No upstream fetch, no fallback chain.
4. **Existence short-circuit stays**: `if not exist "%GAME_PATH%\BepInEx\core\BepInEx.dll"` (and equivalents) still wins before any extraction. User-installed loaders are left alone; state file records `installed_by_us: false`.

### Required layout in every mod

```
<mod-root>/
├── scripts/
│   └── update-deps.ps1        # Manual dev script. Calls Refresh-VendoredLoader once per loader.
├── vendor/<loader-slug>/
│   ├── <loader>.zip           # Committed. Refreshed only when dev runs update-deps. ~1-5 MB.
│   ├── LICENSE                # Verbatim from upstream.
│   └── README.md              # tag, commit SHA (nightlies), upstream URL, SHA-256, fetched_at
```

`fetch-latest.ps1` inside `vendor/<loader>/` is gone — it was only needed by the old install-time fetch path. Don't re-add it.

### Required pixi.toml wiring

```toml
update-deps = "powershell -ExecutionPolicy Bypass -File scripts/update-deps.ps1"
```

`build` does **not** depend on `update-deps`. Bumping vendored copies is a deliberate dev action with a commit attached, not a side-effect of every build. CI never refreshes; it consumes what is committed.

Vendored zips are committed to git (not LFS - they're small). They ship inside the GitHub installer ZIP alongside `install.cmd`. They are NOT shipped in the Nexus ZIP (Nexus users manage their own loader).

### Version-range pinning (mandatory)

Every loader has a hardcoded version-range prefix passed to `Refresh-VendoredLoader` from `update-deps.ps1`. This bounds surprise: a breaking `v6.0.0` tag upstream cannot silently upgrade users via a routine `update-deps`.

| Loader | `VersionPrefix` | `AssetPattern` | Prerelease? |
|--------|-----------------|----------------|-------------|
| BepInEx x64 | `v5.4.` | `^BepInEx_win_x64_.*\.zip$` | No |
| BepInEx x86 | `v5.4.` | `^BepInEx_win_x86_.*\.zip$` | No |
| BepInExPack (Thunderstore) | pinned URL | N/A | N/A (direct-URL mode) |
| MelonLoader 0.6.x x64 | `v0.6.` | `^MelonLoader\.x64\.zip$` | No |
| MelonLoader 0.5.x x64 (Firewatch) | `v0.5.` | `^MelonLoader\.x64\.zip$` | No |
| REFramework (per-game nightly) | (empty) | `^RE9\.zip$` (or RE2/RE4/...) | Yes |
| Ultimate ASI Loader | `v9.` | `^Ultimate-ASI-Loader.*\.zip$` (or `^dinput8\.zip$`) | No |
| UE4SS | `v3.` | `^UE4SS_v.*\.zip$` | No |

Bumping a major version (say BepInEx 5.4 -> 6) is a conscious per-mod change: update the prefix in `update-deps.ps1`, re-run, re-test, commit.

### Shared helpers (`cameraunlock-core/powershell/ModLoaderSetup.psm1`)

- **`Refresh-VendoredLoader`**: the only function `update-deps.ps1` should call. GitHub API query, filter by tag prefix + prerelease flag, download matching asset, write LICENSE + README.md + the zip into `vendor/<name>/`. Direct-URL mode for non-GitHub sources like Thunderstore.
- **`Invoke-FetchLatestLoader`**: lower-level building block; called by `Refresh-VendoredLoader`. Don't call directly from mods.

### install.cmd routine (canonical)

```cmd
:install_<loader>
set "VENDOR_ZIP=%SCRIPT_DIR%vendor\<loader-slug>\<loader>.zip"

if not exist "%VENDOR_ZIP%" (
    echo   ERROR: Bundled <Loader> not found at:
    echo     %VENDOR_ZIP%
    echo   The installer ZIP is corrupt. Re-download the release.
    exit /b 1
)

echo   Extracting bundled <Loader> to game directory...
"%SystemRoot%\System32\tar.exe" -xf "%VENDOR_ZIP%" -C "%GAME_PATH%"
if errorlevel 1 ( echo   ERROR: Extraction failed. & exit /b 1 )
```

No upstream fetch, no `USED_UPSTREAM` flag, no `LOADER_SOURCE` indirection. When the outer `if not exist "<loader marker>"` check short-circuits (user already has the loader), still log:

```
Existing <LoaderName> detected, skipping loader install, deploying plugin only.
```

### update-deps.ps1 routine (canonical)

`scripts/update-deps.ps1` in every mod that ships a loader. One `Refresh-VendoredLoader` call per loader slug:

```powershell
Import-Module (Join-Path $projectDir "cameraunlock-core/powershell/ModLoaderSetup.psm1") -Force
Refresh-VendoredLoader `
    -Name 'bepinex' `
    -OutputDir (Join-Path $projectDir 'vendor/bepinex') `
    -OutputFileName 'BepInEx_win_x64.zip' `
    -Owner 'BepInEx' -Repo 'BepInEx' `
    -VersionPrefix 'v5.4.' `
    -AssetPattern '^BepInEx_win_x64_.*\.zip$' `
    -LicenseUrl 'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE' | Out-Null
```

Use `-OutputFileName` to pin the on-disk filename so install.cmd can hardcode it. Without it, the upstream asset's filename leaks through and renames break installs.

### Ethics and license constraints

Only loaders with permissive licenses may be vendored:
- **MIT / Apache-2.0 / BSD / ISC**: attribution only. Ship upstream LICENSE; done.
- **LGPL-2.1** (BepInEx): attribution + source availability. Keep upstream LICENSE in the vendor dir and link to their repo in THIRD-PARTY-NOTICES so users can obtain source per LGPL §6. Do not modify the binary. Dynamic linking (what Harmony does) does not relicense our code.
- Anything GPL-3, AGPL, or no-license: not vendorable. For these, install.cmd has to keep an upstream-fetch path; document the exception in the mod's README and accept the install-time fragility risk.

Every mod's `THIRD-PARTY-NOTICES.md` must list each vendored component with: name, version, SPDX license identifier, upstream URL, and the phrase `"bundled in the release ZIP and used as the install-time source."`

### Out of scope

- Nexus ZIP path: Nexus users manage their own loader. Vendoring applies only to the GitHub installer ZIP.
- Game-side binary dependencies statically linked into our DLLs (HarmonyX, Newtonsoft.Json): already attributed via THIRD-PARTY-NOTICES; no `vendor/` entry needed.

---

## Documentation Standards

### README.md

Sections in order: title + one-liner · features · requirements · installation · manual installation (optional) · OpenTrack setup · phone app setup · controls (nav cluster + chord alternatives) · configuration · troubleshooting · updating/uninstalling · building from source · license · credits (game studio, frameworks, OpenTrack).

### CHANGELOG.md

Auto-generated by `release.ps1` from commits. `## [X.Y.Z] - YYYY-MM-DD`, categories Added/Changed/Fixed/Removed. Conventional prefixes (`feat:`, `fix:`, `perf:`, `chore:`) auto-categorize. First release hand-written.

### LICENSE

MIT, copyright `itsloopyo / CameraUnlock`.

### THIRD-PARTY-NOTICES.md (when applicable)

- BepInEx (LGPL-2.1), HarmonyX/Lib.Harmony (MIT), MelonLoader (Apache-2.0), OpenTrack (ISC), Mono.Cecil (MIT), ImGui (MIT), MinHook (BSD-2-Clause).
- Only list what's actually bundled or loaded at install time.

---

## Naming Conventions

| Thing | Convention | Example |
|-------|-----------|---------|
| Namespace / Assembly | `<GameName>HeadTracking` (PascalCase) | `SubnauticaHeadTracking` |
| Plugin GUID (new mods) | `com.cameraunlock.<game>.headtracking` | `com.cameraunlock.subnautica.headtracking` |
| Plugin class | `<GameName>HeadTrackingPlugin` / `Mod` | |
| Config class | `<GameName>HeadTrackingConfig` / `ConfigManager` | |
| Repo folder | `<game-lowercase-hyphenated>` | `green-hell`, `shadows-of-doubt` |
| Submodule path | `cameraunlock-core` | |
| Release ZIP | `<ModName>-v<version>-installer.zip` / `-nexus.zip` | |

Existing mods have inconsistent GUIDs (`com.headtracking.obradinn`, `com.<game>.headtracking`). New mods use the standard above. Don't rename existing GUIDs — it breaks user configs.

---

## Reflection Best Practices

- **Cache everything.** Look up `Type` / `FieldInfo` / `PropertyInfo` / `MethodInfo` once into static fields.
- **`GameTypeResolver` pattern** — lazy-initialized cached lookups per game type.
- **Find via `AppDomain.CurrentDomain.GetAssemblies()` + `asm.GetType(name)`.** Don't hardcode assembly names.
- **Graceful degradation** — if a type isn't found, log once and disable the feature; don't crash.
- **Null checks (Mono compat):** `ReferenceEquals(x, null)` for plain .NET; `x == null` for Unity objects (catches destroyed-but-not-GC'd). Never `is null` pattern on Unity objects.
- **Harmony `FastFieldRef`** for hot-path field access.

---

## Performance

- **Cache `Camera.main`** — it calls `FindGameObjectWithTag` internally. Cache per-frame or per-30-frames.
- **Rate-limit game state detection** to 0.1s or 30 frames.
- **No allocations in hot paths** (OnPreCull/LateUpdate/Update).
- **Multi-camera dedup** — games call OnPreCull multiple times per frame (shadows, reflections). Cache per-frame with a 1ms threshold (DL2).
- **Lock-free receiver** — OpenTrackReceiver uses volatile reads on the hot path.

---

## Test Checklist (before release)

1. Head rotation moves view; mouse still aims.
2. Crosshair/reticle stays on the aim point; weapons fire where reticle points.
3. Recenter (Home / Ctrl+Shift+T) returns view to center from any angle.
4. Toggle (End / Ctrl+Shift+Y) cleanly enables/disables with no residual rotation.
5. Position toggle (PageUp / Ctrl+Shift+G) on/off without jump.
6. Tracking pauses in menus/inventory/pause, resumes on return.
7. Tracking survives level loads and scene transitions.
8. Removing tracker holds last pose; reconnecting blends smoothly.
9. install.cmd on clean game → play → uninstall.cmd leaves the game vanilla.
10. With tracking disabled, gameplay is identical to unmodded.

---

## C++ Camera Discovery (REDengine / similar engines)

Lessons from hooking The Witcher 3 and RE:Requiem. Applies to any C++ game without source access.

### Phase 1 — Infrastructure

1. ASI Loader + DXGI Present hook. Create a temp D3D11 device to read `IDXGISwapChain::Present` from the vtable (index 8). Works for DX12 too since DXGI is shared. Hook with MinHook for a per-frame callback.
2. Validate stability with just Present + UDP + hotkey poller before adding camera hooks.

### Phase 2 — Find the Camera via RTTI

1. Scan `.rdata` for RTTI `_TypeDescriptor` strings matching likely class names (`CCustomCamera`, `CCamera`, `CCameraDirector`, `CRenderCamera`, `PlayerCameraController`, …).
2. Walk `_TypeDescriptor → _RTTICompleteObjectLocator → vtable` (COL is at `vtable[-1]`).
3. Log the first 8–10 vtable entries per camera class.

### Phase 3 — Hook a Virtual Function

**Do not backward-prologue-scan from a known instruction.** That found a function only called during loading — wasted hours.

1. Dump vtable entries 0–9 (entries beyond are often RTTI metadata, identifiable by absurd values).
2. Hook MinHook from `vfunc[2]` onward — `[0]` is typically destructor/RTTI, `[1]` is type info. Per-frame Update/Tick tends to be `[2]`–`[7]`.
3. **Preserve the return value.** Use `uintptr_t` return, not `void` — declaring `void` when the real function returns a value corrupts RAX and causes visual corruption (screen split, etc.).
4. Log `this` and call frequency. If not called after 15s, try the next vfunc.

### Phase 4 — Map the Camera Object

1. Dump `this + 0x000..0x200` as hex + float pairs, ONCE (guard with a counter). Do NOT follow unknown pointers — dereferencing into heap destabilizes the game.
2. Identify fields by value shape:
   - **Pointers:** large hex, absurd as floats.
   - **Position:** 3–4 floats with world-coordinate magnitudes.
   - **Euler angles:** floats in [-180, 180].
   - **Identity matrix:** 16 floats on a diagonal.
   - **FOV:** float ~50–90.
3. Witcher 3 REDengine layout (for reference):
   - `+0x000` vtable, `+0x060` position (xyzw=1), `+0x080` roll, `+0x084` pitch, `+0x088` yaw, `+0x0E8` FOV, `+0x140` 4x4 matrix (ineffective).

### Phase 5 — Modify Angles (Delta Approach)

```cpp
// Pre: undo last frame's head tracking
*camYaw -= s_prevDYaw; *camPitch -= s_prevDPitch; *camRoll -= s_prevDRoll;

uintptr_t ret = s_originalFunc(thisPtr, a2, a3, a4);  // Game runs with clean angles

// Post: apply tracking
float dYaw = -headYaw, dPitch = headPitch, dRoll = -headRoll;
*camYaw += dYaw; *camPitch += dPitch; *camRoll += dRoll;
s_prevDYaw = dYaw; s_prevDPitch = dPitch; s_prevDRoll = dRoll;
return ret;
```

Do NOT save/restore absolute angles — fights the game's mouse accumulator, mouse gets stuck at pitch clamp boundaries. Store the actual applied delta (with inversions baked in), otherwise undo and apply have mismatched signs.

### Pitfalls

| Pitfall | Symptom | Fix |
|---------|---------|-----|
| Scanning heap memory | Crashes 1–30s after scan | Never scan heap — use RTTI + vfunc hooks, objects come from `this` |
| Dereferencing pointers found in `.data` | Crashes shortly after | Don't dereference unknown pointers |
| `void` hook on non-void function | Screen split, visual corruption | Use `uintptr_t`, preserve RAX |
| Backward prologue scan | Hooks a load-only function | Use vtable vfuncs |
| Storing raw head values, applying inverted | Accumulation drift | Store the delta actually applied |
| Writing absolute angles | Mouse stuck at pitch clamps | Use delta add/undo |
| `__try/__except` around bad writes | Crashes still happen | SEH only helps for read probes, not downstream effects of bad writes |
