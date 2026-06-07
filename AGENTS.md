<!-- managed by Lab - edit via Agents in the sidebar; changes here are overwritten on the next sync -->

<!-- agent: Agents and skills -->
If you need to fix issues in or expand on AGENTS.md or any skills, please note that the ones in this repo are machine generated, the authoritative sources where they must be edited are /c/data/agents and /c/data/skills


<!-- agent: Be awesome -->
Strive for perfection in all your work


<!-- agent: Code quality minimums -->
## Code quality minimums

These apply to every line of code in every repo. They override convenience.

### Fail fast, don't fail silent

If something fails, let it throw. No swallowed exceptions. No silent
fallbacks that mask the underlying problem. No retry loops that paper
over a broken contract. The error message is the diagnostic; if you
catch and rewrite it, you've thrown the diagnostic away.

The narrow exception: validate at system boundaries (user input,
external APIs, file system). Everything inside the boundary trusts the
contract.

### No fallbacks for impossible cases

Don't add error handling, validation, or fallbacks for scenarios that
can't happen. Trust internal code and framework guarantees. A `Result`
that's always `Ok` doesn't need to be a `Result`. A null check on a
value that's constructed three lines earlier is noise.

### No over-engineering

Don't add features, refactor, or introduce abstractions beyond what the
task requires. A bug fix doesn't need surrounding cleanup. A one-shot
operation doesn't need a helper function. Don't design for hypothetical
future requirements. Three similar lines is better than a premature
abstraction.

No half-finished implementations either. If you can't complete it, say
so and stop, don't leave a stub that compiles but lies.

### No decorative comments

Default to writing zero comments. Only add a comment when the WHY is
non-obvious: a hidden constraint, a subtle invariant, a workaround for
a specific bug, behavior that would surprise a reader. If removing the
comment wouldn't confuse a future reader, don't write it.

Don't explain WHAT the code does (well-named identifiers do that).
Don't reference the current task, fix, or callers ("used by X", "added
for the Y flow", "fixes #123"). Those belong in the PR description and
rot as the codebase evolves.

### No backwards-compat hacks for unused code

If something is unused, delete it completely. Don't rename to `_unused`,
don't add `// removed` comments, don't re-export removed types as
aliases. Backwards compatibility is for shipped public APIs (see the
Libraries category rule); internal scaffolding gets cut clean.


<!-- agent: gitignore -->
.lab, .claude, our skills, agents.md/claude.md and any MCP servers should be added to the project's .gitignore so they are not tracked in git.


<!-- agent: Lab Notes -->
If there is important project-specific information, it will be found in a .lab/NOTES.md - read this file at the start of each session should it exist.


<!-- agent: Pixi rules -->
In pixi files:

The `project` field is deprecated. Use `workspace` instead.

## CI builds through the same `pixi run` a developer runs

A build that passes locally must build identically in CI. The only robust
way to guarantee that is to make CI invoke the *same* command a developer
runs - `pixi run package` - and put zero build logic in the workflow YAML.
Workflows own orchestration only: checkout, `setup-dotnet` + `setup-pixi`,
`pixi run package`, then artifact staging / release publishing.

This forbids the two ways local and CI drift:

- **No inline build steps in workflows.** If `build.yml`/`release.yml`
  contains its own `dotnet build`, stub generation, dependency download,
  or a `-p:` override that the local `pixi run` path doesn't also apply,
  it is a second, drifting copy of the build. Move it into the pixi task
  chain (a `setup`/`bootstrap` script that `restore`/`build` depend on).
- **Build dependencies must be game-independent.** `pixi run package`
  must succeed from a clean checkout with **no game installed** - on a CI
  runner, and on a contributor's machine who doesn't own the game.
  Resolve build references from repo files only: vendored loader archives,
  NuGet packages, and reference *stubs* compiled from a checked-in stub
  source (e.g. `UnityStubs.cs`) - never copied from a game install. A
  `setup` task that copies `GH_Data\Managed\*.dll` out of the game is the
  classic trap: a contributor with the game builds against the real DLLs,
  CI builds against the stubs, and a member the stub is missing passes
  locally and fails on push. `Directory.Build.props` must default to the
  generated stubs; a game path is an explicit `-p:UnityEnginePath=` opt-in
  for validation, never the default.

Prove it the only way that counts: run `pixi run package` on a machine
without the game and confirm it produces the installer ZIP.


<!-- agent: Debugging -->
Don't be shy to use Ghidra to help us on our quest to crowbar head tracking into games that aren't built for it. Headless if possible - feel free to run entirely headless, or failing that, write scripts we can execute in Jython. Note, Ghidra is installed, when I run it I run C:\ProgramData\chocolatey\lib\ghidra\tools\ghidra_12.0_PUBLIC\ghidraRun.bat

Similarly ilspy/ilspycmd is installed, and should be used to make our lives easier when working on Unity titles.

pe-sieve64 is present in /c/bin

## Autonomy when a mod "isn't working in game"

When doing the initial build of a mod, or when the user reports "no head tracking in game" / "check logs" / similar on a head tracking mod, do NOT stop to ask what to do next. The flow is fixed; drive it autonomously and only surface a question when genuinely blocked (missing input, ambiguous game build, decision only the user can make).

Default flow for "mod is being created brand new"/"mod isn't working":

1. **Inspect mod state first.** Read `src/dllmain.cpp` (or equivalent entry) and the build output. If the entry file is a bare scaffold (empty `DllMain`, no hooks, no logger) say so plainly and skip to step 3 - there is nothing to debug, the implementation hasn't been written.
2. **Verify the loader is actually engaging** before any RE work. Add a minimal `DLL_PROCESS_ATTACH` logger that writes `<ModName>.log` next to the game EXE. Build, deploy, launch the game headless if the game supports it, wait, kill the process, read the log. Do this without asking. Five minutes of work eliminates "is the ASI/BepInEx/MelonLoader/REFramework plugin even loading" as a variable.
   - Launch headless / unattended where possible (the game's own `-batchmode`, Steam `steam://run/<appid>`, or just `Start-Process` + `Stop-Process` after a timeout). Capture stdout/stderr to files. Never ask the user to "launch the game and tell me what you see" if you can launch it yourself.
   - If the game refuses to run headless or needs a real window, run it minimised / off-screen and use `Stop-Process` after a fixed timeout. Confirm via the log file, not by asking the user.
3. **Begin reverse engineering immediately.** Once loader-presence is confirmed (or confirmed-empty for a scaffold), proceed straight into Ghidra / pattern scanning / pe-sieve64 / ilspycmd as appropriate for the engine. Headless Ghidra (`analyzeHeadless`) + Jython post-scripts is the default - do not open the GUI, do not ask whether to start RE work, just start. Pick the obvious next concrete step (locate the EXE, fingerprint it, import to a Ghidra project, run an analysis script for camera-matrix-shaped functions) and do it.
4. **Surface progress, not decisions.** One-line status updates as you work ("loader confirmed via log line", "Ghidra import done, scanning for view matrix writers"). Stop and ask only when you hit something only the user can resolve - e.g. the game won't launch and needs a Steam login, or there are multiple plausible camera structs and you need them to wiggle their head to disambiguate.

We ship lots of these mods. Each one that needs a back-and-forth "what should I do next?" costs the user attention they don't have. Default to acting, log what you did, keep moving.


<!-- agent: Follow Lopari Package Management Recipe -->
Mod Repository Guidance

This repository contains a runtime package consumed by the launcher Lopari (../lopari)

The launcher is a game launcher first. Its integrated runtime/package management layer is responsible for installing, updating, repairing, verifying, migrating, and removing this package.

This repository should primarily describe what the package contains and requires.

It should not contain unnecessary installation logic.

Core Principle

This repository owns:

Runtime payload
Package metadata
Mod source code
Build output
Documentation
Release contents

The launcher owns:

Installation
Uninstallation
Updates
Repair
Verification
Loader management
Runtime provisioning
Legacy migration

Do not move launcher responsibilities into this repository.

Manifest First

Every package already ships a launcher manifest at the release ZIP root:

launcher-manifest.json

This is the file the launcher reads (the packaging step stamps it with the
real release version). There is NO separate `mod.json` - the launcher does
not look for one. The manifest is the contract between this package and the
launcher.

Prefer describing requirements in launcher-manifest.json rather than adding
logic to install.cmd, uninstall.cmd, PowerShell scripts, batch scripts, or
launcher-specific code.

Delivery Mode

launcher-manifest.json carries a `delivery_mode` discriminator that selects
how the launcher installs the package:

- `"install_cmd"` (the default when the field is absent): legacy path. The
  launcher shells out to the package's `install.cmd` / `uninstall.cmd`.
- `"manifest"`: native, receipt-tracked deployment. The launcher extracts
  the loader, copies the declared files, provisions runtime requirements,
  and records a receipt. No install.cmd runs.

A pre-v2 manifest (no `delivery_mode`, or `delivery_mode: "install_cmd"`)
keeps working unchanged. Migrate a package to native deployment by setting
`delivery_mode: "manifest"` and filling in the `loader` / `files` blocks
below. Field names are snake_case, matching the existing manifest.

Manifest Format (manifest mode)

A native-deployment manifest looks like this:

{
  "schema_version": 2,
  "mod_info": {
    "name": "SubnauticaHeadTracking",
    "version": "1.4.0",
    "game_id": "subnautica"
  },
  "strategy": "BepInEx5",
  "delivery_mode": "manifest",

  "loader": {
    "archives": [
      { "source": "vendor/bepinex/BepInEx_win_x64.zip" }
    ],
    "detect": [ "BepInEx/core/BepInEx.dll", "BepInEx/core/BepInEx.Core.dll" ],
    "seed": [
      { "target": "BepInEx/config/BepInEx.cfg", "content_b64": "..." }
    ]
  },

  "files": [
    { "source": "plugins/HeadTracking.dll", "target": "BepInEx/plugins/HeadTracking.dll" }
  ],

  "runtime_requirements": [],
  "dependencies": []
}

Required fields for every manifest:

{
  "schema_version": 2,
  "mod_info": { "name": "", "version": "", "game_id": "" },
  "delivery_mode": "manifest"
}

Field meaning:

schema_version: Manifest schema version (2 for native deployment).
mod_info.name: Human-readable / internal package name (stamped into the state file).
mod_info.version: Package version matching the release version.
mod_info.game_id: Catalog game identifier the launcher detects against.
strategy: Loader family, informational ("BepInEx5", "MelonLoader",
  "AsiLoader", "MonoCecil", "REFramework", "Shim"). The deploy engine is
  strategy-agnostic - it drives off `loader` and `files`.

Files

All deployed package files are declared explicitly.

{
  "files": [
    { "source": "plugins/HeadTracking.dll", "target": "BepInEx/plugins/HeadTracking.dll" },
    { "source": "managed/Assembly-CSharp.dll", "target": "Game_Data/Managed/Assembly-CSharp.dll", "backup": true }
  ]
}

Rules:

source is relative to the package/release root.
target is relative to the game install root.
Do not use absolute paths. Do not use ../ (the launcher rejects both).
Set "backup": true for a file that REPLACES a game-shipped file (MonoCecil's
  Assembly-CSharp.dll, shim loader DLLs). The launcher moves the original
  aside before overwriting and restores it on uninstall.
Do not rely on scripts to copy files that can be declared here.

Loader

Declare loader provisioning in the `loader` block. The launcher extracts the
vendored loader archive into the game folder - the same vendored zip the
legacy install.cmd already bundles (see "bump vendored loader" skill).

{
  "loader": {
    "archives": [ { "source": "vendor/bepinex/BepInEx_win_x64.zip", "dest": "", "flatten": null } ],
    "detect": [ "BepInEx/core/BepInEx.dll" ],
    "seed": [ { "target": "BepInEx/config/BepInEx.cfg", "content_b64": "<base64>" } ]
  }
}

archives[].source: vendored loader zip inside the package.
archives[].dest: game-relative extract directory (empty = game root).
archives[].flatten: for a Thunderstore BepInExPack, the wrapper subfolder
  name (e.g. "BepInExPack_Subnautica") whose contents are flattened into dest.
detect: files whose presence means a loader is already installed. When any
  exists, the launcher records that it did NOT install the loader, so
  uninstall leaves it intact (the `installed_by_us` contract).
seed: config files written ONLY when absent (never clobbers an existing one).
  content_b64 is base64 so arbitrary bytes / line endings survive the JSON.

Strategies that replace a file instead of bootstrapping a loader (shim,
MonoCecil) omit `loader` entirely and use `files[].backup: true`.

The package should not install loaders itself in manifest mode. The launcher
installs and validates loaders.

Dependencies

Declare first-party dependencies explicitly:

{ "dependencies": [] }

NOTE: the launcher does not yet resolve dependencies - declaring a non-empty
`dependencies` array currently makes the deploy FAIL FAST with a clear error
rather than silently dropping it. Keep this empty until launcher-side
resolution lands. For head-tracking mods today, shared core DLLs
(CameraUnlock.Core.*) are bundled in `files` rather than declared as deps.

Runtime Requirements

Some requirements are not ordinary package files - e.g. copying a
Windows-provided DLL into the mod directory. Represent these declaratively:

{
  "runtime_requirements": [
    {
      "id": "dxdiag",
      "type": "system-file-copy",
      "source": "%WINDIR%/System32/dxdiag.dll",
      "target": "Mods/dxdiag.dll",
      "required": true,
      "reason": "Runtime dependency required by the mod"
    }
  ]
}

Only `system-file-copy` is supported in v2.0. The launcher:

- Expands %WINDIR% / %SYSTEMROOT% style placeholders in source.
- Requires source to resolve inside the Windows system directory
  (System32 / SysWOW64); anything else is rejected.
- Copies the file into the (game-relative) target and records its hash.
- Repairs the target from source, and removes it on uninstall (ownership
  proven by hash).
- Skips an optional (required: false) requirement whose source is absent;
  errors when a required source is missing.

Do not hide this behaviour inside install scripts. When a new requirement
type is needed (native-library, generated-file, ...), extend the launcher's
runtime model rather than adding package-specific installer logic.

Configuration

User configuration is preserved automatically: it is not listed in `files`,
so the launcher never deploys, verifies, repairs, or removes it. A loader
config you ship as a one-time default belongs in `loader.seed` (written only
when absent). Do not write scripts that delete user configuration unless the
user explicitly requested a full clean removal.

Receipts (launcher-owned, informational)

On a manifest install the launcher writes a receipt at
%APPDATA%/Lopari/receipts/<game_id>/<package_id>.json recording every file
it deployed (with SHA-256), any backups, and runtime requirements. This is
the launcher's source of truth for verify / repair / uninstall. Mod repos do
not create or ship receipts - this is documented only so you understand how
the launcher reasons about your package.

Install and Uninstall Scripts

install.cmd and uninstall.cmd remain supported for `delivery_mode:
"install_cmd"` (the legacy path) and are unchanged. They are no longer the
preferred mechanism. Once a package moves to manifest mode the launcher does
not run them.

If custom script behaviour is genuinely unavoidable for a package that can't
be expressed declaratively, keep that package on `delivery_mode:
"install_cmd"` and document why.

All scripts should be deterministic. Scripts should not silently download
additional payloads, and should not modify files outside the game/runtime
scope unless explicitly documented and unavoidable.

Release Layout

The installer ZIP root the launcher consumes:

GreenHellHeadTracking-vX.Y.Z-installer.zip
├── launcher-manifest.json
├── plugins/HeadTracking.dll        (mod files referenced by files[])
├── vendor/<loader>/<loader>.zip    (vendored loader, manifest mode)
├── install.cmd / uninstall.cmd     (legacy mode only)
├── shared/find-game.ps1            (legacy mode only)
└── README.md / LICENSE / ...

In manifest mode the launcher deploys the release using launcher-manifest.json
alone; install.cmd / shared/ are not required.

Versioning

mod_info.version should match the GitHub Release version. Do not make
arbitrary version changes; they should reflect actual package changes. The
launcher uses versions for update checks and version pinning.

Backwards Compatibility

Existing users may have installed this package using the legacy script path.
Do not casually remove compatibility files if older launcher versions still
depend on them. Before removing legacy scripts, confirm the launcher
migration path is complete: existing working installs must remain migratable.

Legacy State

Packages create / rely on the legacy state file:

.headtracking-state.json

The launcher still writes this in manifest mode (so detection and the UI
flip to Installed) and reads it to import a pre-v2 install into a receipt. Do
not change its format casually; document any migration impact.

Security and Safety

Package contents must be safe for automated deployment. Do not include files
that require unsafe extraction behaviour. Do not use paths that escape the
game directory or absolute target paths (the launcher rejects both). For
system-provided files, use runtime_requirements (system-file-copy) - do not
bundle system DLLs.

AI Contributor Guidance

When modifying this repository, ask:

Can this be represented in launcher-manifest.json?
Is this actually a launcher responsibility?
Am I adding hidden installation behaviour?
Am I making migration harder?
Am I making update, repair, or verification harder?
Am I preserving user configuration?
Am I keeping the release easy for the launcher to consume?

Prefer boring, explicit metadata over clever scripts.

Good Changes

Adding or updating launcher-manifest.json (especially moving a package to
  delivery_mode: "manifest").
Declaring loader provisioning, files, and runtime requirements.
Making release layout more predictable.
Removing install-script logic after the package is on manifest mode.

Bad Changes

Introducing a separate mod.json (the launcher reads launcher-manifest.json).
Adding new hidden install logic.
Copying files from scripts when manifest entries would work.
Deleting user configuration during update.
Bundling redistributable-unclear system DLLs.
Introducing absolute install paths or ../ targets.
Declaring dependencies the launcher can't yet resolve.

Success Criteria

A healthy package repository:

Builds the runtime payload.
Provides a complete launcher-manifest.json.
Declares loader and runtime requirements.
Avoids custom deployment logic.
Preserves backwards compatibility where needed.
Produces predictable GitHub Release assets.
Can be installed, updated, repaired, verified, and migrated by the launcher.

The launcher owns deployment. This repository owns the package.


<!-- agent: Head tracking mod doctrine -->
## Ethics and Conduct

Non-negotiable:

- **No copyrighted game code in this repo.** Never copy, decompile, or redistribute game source, assets, or proprietary DLLs. Game assemblies are referenced at build time from local installs and are gitignored.
- **No piracy facilitation.** Mods require a legitimately purchased game. Never bypass DRM, license checks, or anti-cheat.
- **Reverse engineering only where necessary.** Reflection and Harmony at runtime; never distribute decompiled source or reconstructed game logic.
- **Respect framework licenses** (BepInEx LGPL-2.1, HarmonyX MIT, MelonLoader Apache-2.0, OpenTrack ISC). Attribute in THIRD-PARTY-NOTICES.
- **All our code is MIT**, copyright itsloopyo.
- **Never malicious.** No data exfil, phone-home, ads, destructive save edits, anti-cheat interference, or multiplayer cheating.
- **Credit game developers** in READMEs.

---

## Project Philosophy

1. **Decoupled look and aim.** Head moves the view; mouse/controller still controls aim. The core value prop.
2. **Zero impact on game logic.** Raycasts, physics, hitboxes, and aim direction are identical whether tracking is on or off.
3. **Fail fast, never fail silent.** No swallowed exceptions, no silent fallbacks.
4. **No over-engineering.** Don't add features, abstractions, or error handling beyond what's asked.
5. **Easy to churn out.** New mod from scratch to working ZIP in a single session - don't fight the shared patterns.

---

## Hard Rules

- **Isolate tracking from game logic.** Either (1) modify `camera.worldToCameraMatrix` only (rendering path; game reads `camera.transform` for aim), or (2) modify `camera.transform` with save/restore so game logic sees the clean rotation. In C++ engines: save clean camera state before the game's update tick, inject tracking only in the render phase.
- **CRLF for `.cmd`/`.bat` files.** `Write` outputs LF; after writing any `.cmd`/`.bat`, run `unix2dos <file>`. CRLF or they silently fail on Windows. This is the #1 regression source.
- **cameraunlock-core** is the shared submodule (DLLs `CameraUnlock.Core.*`), from https://github.com/itsloopyo/cameraunlock-core. `../cameraunlock-core` is canonical - all changes go there; every mod ingests it from GitHub. Use as much of it as benefits us, and feed shared improvements back as mods grow.
- **Never commit:** `.claude/`, `.pixi/`, `bin/`, `obj/`, `libs/`, `release/`, `.vs/`, `*.user`.
- **Never use em-dashes.** Normal hyphens only, everywhere: code, comments, docs, commit messages, chat.
- **New mod from scratch starts at version 0.0.0.**
- **Make changes the user must verify obviously visible.** Instead of "has the marker repositioned 0.5m" use "is the marker moving left-to-right by 5m in an animated loop". Never ask for superhuman precision ("turn your head 30 degrees and hold for 3s").

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

**Sample rate is not fixed.** The interpolator EMA-estimates the incoming sample interval (`IntervalBlend = 0.3f`, clamped 0.001–0.2s). Any tracker rate works (30/60/90/120Hz or irregular); velocity extrapolation (`MaxExtrapolationFraction = 0.5f`) bridges to frame rate and eliminates flat spots on high-refresh displays. The old "30Hz" constant is only the seed estimate until real samples arrive.

### Core Library Assemblies (C#)

| Assembly | Purpose | Unity |
|----------|---------|-------|
| `CameraUnlock.Core` | Framework-agnostic: types, protocol, processing, math, config | No |
| `CameraUnlock.Core.Unity` | ViewMatrixModifier, SelfHealingModBase, UI, canvas compensation | Yes |
| `CameraUnlock.Core.Unity.BepInEx` | BepInEx config binding, hotkey base class | Yes + BepInEx |
| `CameraUnlock.Core.Unity.Harmony` | Harmony transpiler patterns | Yes + Harmony |

Multi-targets net35/net40/netstandard2.0/net472/net48, Unity 2017 (Obra Dinn) through modern (Subnautica).

### C++ Core (cameraunlock-core/cpp/)

Headers mirror the C# types: `math/smoothing_utils.h`, processing/interpolator primitives, REFramework utilities (`cameraunlock_reframework`), GUI marker compensation helpers.

### Key Entry Points

- **`StaticHeadTrackingCore`** - static singleton. `Initialize()`, `Update()`, `GetProcessedPose()`.
- **`SelfHealingModBase`** - MonoBehaviour base that survives scene changes via `DontDestroyOnLoad` + auto-recreate.
- **`ViewMatrixModifier`** - `ApplyHeadRotation(cam, yaw, pitch, roll)`; `ApplyHeadRotationDecomposed()` for world-space yaw.
- **`ViewMatrixTrackingController`** - full per-frame controller (pipeline, transitions, render hooks). `TryGetAimScreenOffset()` is the reticle projection to use with it; it mirrors the controller's own rotation composition per yaw mode.
- **`AimDecoupler`** - `ComputeAimDirectionLocal()` inverts tracking rotation for a stable aim vector.
- **`ScreenOffsetCalculator`** - FOV-based tangent projection for reticle/UI compensation. Only valid for camera hooks whose composition matches it (yaw-then-pitch, roll outermost) - see Reticle Compensation.

---

## Camera System

### The Fundamental Rule

**Head tracking only modifies what the player sees. It never modifies what the game thinks the camera is doing.**

### View Matrix Modification (preferred, Unity)

Used by: Subnautica, Green Hell, Gone Home, Firewatch.

1. Always call `cam.ResetWorldToCameraMatrix()` before reading `cam.worldToCameraMatrix` (else it accumulates prior frames' modifications).
2. Save the original view matrix.
3. `cam.worldToCameraMatrix = headRotMatrix * gameViewMatrix`
4. Roll is inverted in the Euler variant: `Quaternion.Euler(pitch, yaw, -roll)`.
5. For world-space yaw (prevents leaning artifacts at extreme angles), use `ApplyHeadRotationDecomposed`.

`camera.transform.forward` stays the aim direction - aim decoupling falls out naturally.

### Transform Save/Restore (alternative, Unity)

Used by: Outer Wilds, Firewatch (screen-space effects layer). Use when screen-space effects (lens flares, motion blur) read `camera.transform` instead of the view matrix.

1. `Camera.onPreCull`: save `camera.transform.rotation`, apply tracked rotation.
2. Rendering happens with tracked rotation (screen-space effects see it correctly).
3. `Camera.onPostRender`: restore.

### Harmony Prefix/Postfix (Unity, per game method)

Used by: Green Hell, Outer Wilds, Obra Dinn. For game methods that read camera direction:

```
Prefix:  remove tracking (restore base rotation / reset matrix)
Postfix: reapply tracking
```

### C++ Pre/Post Hook (RE Engine, REDengine, similar)

Used by: dying-light-2, resident-evil-requiem, witcher-3.

1. **Pre-hook (before game's camera update):** restore clean camera state.
2. **Game update runs** - weapon aim, projectile spawning, AI vision, physics all read the clean rotation.
3. **Post-hook:** save the game's intended rotation.
4. **Render-phase hook** (e.g. `OnPostBeginRendering`): inject head rotation into the view matrix, keeping the head-tracked position.

The render-phase injection is what the player sees; the pre/post sandwich ensures the game never observes tracked state. See RE:Requiem `camera_hook.cpp`.

### Camera Timing

- Apply in `Camera.onPreCull` (after game camera positioning, before rendering), not `LateUpdate`. Register via `Camera.onPreCull += OnCameraPreCull`.
- Filter UI cameras via `cam.name` or `cam.cullingMask` if needed.
- Harmony variant: patch the game's camera-controller `LateUpdate` with prefix (remove) + postfix (apply).

### Rotation Composition

Standard YPR order across all mods:

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
3. Apply as translation in **original view space** (before head rotation) so the offset follows body orientation, not the head-rotated view.
4. Asymmetric Z limits: more range forward (`LimitZ`, default 0.40m) than back (`LimitZBack`, default 0.10m) to prevent clipping through the player model. X and Y use single symmetric limits (`LimitX`, `LimitY`).
5. Horizon-locked basis (flat forward vector) for roll independence.

View-matrix math:
```csharp
Matrix4x4 H = cam.worldToCameraMatrix * originalViewMatrix.inverse;
cam.worldToCameraMatrix = H * Matrix4x4.Translate(-offset) * originalViewMatrix;
```

Transform approach: apply offset to `localPosition` in postfix, remove in next prefix.

---

## Aim Decoupling and Projectile Landing

Head moves the view; mouse/controller controls aim. Projectiles must land where the **reticle** is drawn, not where the view faces.

### Principle (engine-agnostic)

The game's aim/projectile/raycast code reads **clean camera rotation** (where the player is actually aiming). The player sees the **head-tracked** camera. The reticle is drawn at the screen position where the clean aim direction projects into the head-tracked view. So: **projectiles fly straight along clean aim, and we move the reticle to match - never the other way around.**

- **Unity:** view-matrix modification gives this for free - `camera.transform.forward` (clean) = aim, `camera.worldToCameraMatrix` (tracked) = render. Game aim/raycast reads `transform.forward` and gets the mouse-controlled direction.
- **C++ (RE Engine and similar):** the pre/post hook sandwich restores clean rotation before game logic and re-injects tracking in the render phase. Weapons/projectiles see clean aim; the player sees the tracked view.

### Reticle Compensation (all engines)

1. Raycast along clean aim (`transform.forward` or clean camera matrix) to find hit distance.
2. Smooth the hit distance (~15Hz exponential) to prevent jitter.
3. Project the aim world-point through the head-tracked view/projection matrix to get screen coordinates.
4. Move the reticle UI element to that screen position.
5. If the aim point is behind the camera (extreme head turn), hide or clamp to screen edge.

**Per-framework:**
- BepInEx: find the game's reticle via reflection, modify `RectTransform.anchoredPosition`.
- No-reticle games: draw custom via `IMGUIReticle` (Core.Unity).
- DL2/RE:Requiem: ImGui overlay with D3D hook, project using live FOV (read per-frame, smoothed).

**The projection is not a free choice - it is dictated by the camera composition.** Derive the reticle formula from the exact rotation composition the camera modification applies: same operations, order, signs. A formula from different assumptions agrees at small single-axis angles then drifts on combined poses. Never pair a generic projection with a camera path without verifying they encode the same composition. Two recurring failure modes (both shipped and fixed in the field):

- **Roll handling depends on where roll sits in the composition.** If the camera applies roll innermost - Unity's `Quaternion.Euler(pitch, yaw, -roll)` (`ViewMatrixModifier.ApplyHeadRotation`, matching OpenTrack's yaw-pitch-roll Euler convention) - the clean aim point is invariant under roll: the projection must NOT rotate the offset by roll, and pitch+roll moves the reticle purely vertically. Rotating the offset by roll anyway produces a spurious horizontal drift of ~tan(pitch)*sin(roll) (yapyap, 2026-06-03). Only when the camera applies roll outermost (about the final view axis, typical for C++ engine hooks that write Euler angles into engine rotation state) does the projected offset rotate with roll.

- **World-space yaw must be conjugated through the camera's base rotation.** In decomposed/world-yaw mode (`ApplyHeadRotationDecomposed`), head yaw rotates about world up, so its screen-space effect depends on where the game camera points: at the horizon it acts as local yaw; looking straight down it is a pure spin about the view axis and the reticle must stay fixed at centre. A projection that treats head yaw as camera-local sweeps the reticle in a U-shaped arc when the player yaws while looking down (yapyap, 2026-06-03).

**Unity view-matrix mods: do not hand-roll the projection.** Use `ViewMatrixTrackingController.TryGetAimScreenOffset()` (Core.Unity): it projects clean aim through the same composition the controller applies (`ViewMatrixModifier.ComputeAimDirectionInTrackedView` / `...Decomposed`), switches automatically with WorldSpaceYaw mode, and returns false when the aim point is behind the tracked view. A mod that can't use the controller should still compute the offset by pushing the clean-aim view-space direction (0, 0, -1) through the same head-rotation matrix it left-multiplies onto the view matrix - not by re-deriving Euler formulas.

**For C++ / non-controller engines, do NOT project with per-axis yaw/pitch tangents.** The naive formula `ndc_x = -tan(yaw) / tan(fov_h/2)`, `ndc_y = tan(pitch) / tan(fov_v/2)` is roll-unaware and drifts horizontally the moment roll is combined with pitch (once the head tilts, the pitch axis stops being screen-vertical). For camera hooks that compose yaw-then-pitch with roll outermost (the common C++ hook shape), use spherical decomposition into the aim direction, apply roll in direction space, *then* perspective-divide:

```
ax = -sin(yaw)
ay =  sin(pitch) * cos(yaw)   // `cos(yaw)` prevents orbiting on combined yaw+pitch
az =  cos(pitch) * cos(yaw)

// rotate (ax, ay) by roll in DIRECTION space, not screen space -
// screen-space roll rotation introduces FOV/aspect distortion.
rx = ax * cos(roll) - ay * sin(roll)
ry = ax * sin(roll) + ay * cos(roll)

ndc_x =  rx / az / tan(fov_h/2)
ndc_y = -ry / az / tan(fov_v/2)   // flip if NDC-y is positive-up
```

Reference implementations: `cameraunlock-core/csharp/.../Aim/ScreenOffsetCalculator.cs` and the C++ `cameraunlock-core/cpp/.../crosshair_projection.h`. Both encode the composition above - confirm your camera hook actually composes that way before reusing them (ViewMatrixModifier does not; see the roll-innermost mode).

**The roll sign must match between the camera modification and the reticle projection.** If your camera hook writes `-roll` into the engine's rotation state (e.g. BioShock Remastered: UE2.5 `FRotator.Roll` is CW-positive while OpenTrack is CCW-positive, so `engine_hook` negates at the boundary), the reticle projection must use the same-sign roll the game actually renders with. Raw OpenTrack roll in both places puts them 180° out of phase, so the reticle drifts horizontally on roll+pitch. Concretely: if you apply `-roll` to the game, apply `+roll` (the inverse) when rotating `(ax, ay)` in the projection.

**Reticle litmus tests - run all before calling compensation done:**

1. Pure roll, pitch = 0: reticle stays at screen centre.
2. Pure pitch, roll = 0: reticle moves purely vertically.
3. Pitch + roll combined: no horizontal wander as roll changes. (Vertical-only when the camera composes roll innermost; offset rotating around centre when roll is outermost. Either way it must match what the camera renders - aim point and reticle stay glued together.)
4. World-yaw mode (if the mod has one): look straight down, yaw the head - the world spins, the reticle stays fixed at centre.

### UI Compensation

- HUD markers/pings (Subnautica): reposition in `Canvas.willRenderCanvases`.
- Interaction text (Gone Home): move label to follow crosshair.
- Player mask/helmet (Subnautica): apply inverse H to keep screen-fixed.
- Map markers (Outer Wilds): temporarily apply/remove tracking in HUD update prefix/postfix.
- World-anchored GUI markers (RE:Requiem): roll-aware reprojection - when head roll exceeds ~0.1°, apply inverse roll to anchor points before offsetting.

### Crosshair Suppression

When drawing our own reticle, hide the game's built-in crosshair:
- Reflection (e.g. `NGUI_HUD.ReticuleSprite`, `HUDCrosshair`), disable GameObject or set alpha 0. Restore on mod disable.
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

- **Baseline floor 0.15** (`SmoothingUtils.BaselineSmoothing` / `kBaselineSmoothing`). `GetEffectiveSmoothing()` enforces this minimum on every connection. Below it, high-refresh displays show jitter, especially on wireless/WiFi trackers. Do not remove this floor.
- **Frame-rate independent:** the exponential formula converges on identical visual latency regardless of frame rate. At 60Hz with smoothing=0.15, per-frame factor ≈ 0.4, settling ~100–150ms. At 144Hz the per-frame factor is smaller but cumulative settling time is unchanged.
- **User SmoothingFactor:** 0.0 = minimum (floor of 0.15 applied); 1.0 = heavy (~5s settling).
- **PoseInterpolator:** sits between receiver and processor. Active whenever tracking smoothing ≥ 0.001. EMA-estimates the tracker's true sample interval so any rate works; velocity extrapolation up to half a sample period past the latest known position eliminates flat spots at high refresh rates.

### Auto-Recenter

- Recenter on first connection (no-data → fresh-data transition).
- Wait `StabilizationFrames` (default 10) before recentering after a resume, so phone trackers settle. `TrackingLossHandler.StabilizationFrames` in `Core.Unity`.
- `Recenter()` sets the current smoothed pose as the center offset via quaternion inverse.

### Tracking Loss

- **Hold by default:** display last known pose (no snap to center).
- **Freshness:** `IsDataFresh(maxAgeMs = 500)` on `TrackingPose` and `OpenTrackReceiver`.
- **Resume:** smoothing blends back naturally - never snap.
- **Optional fade + auto-recenter** via `TrackingLossHandler` (`Core.Unity`): hold for `FadeDelaySeconds` (0.5s), exponential fade to identity at `FadeSpeed` (2.0), auto-recenter after `RecenterThresholdFrames` (60) frames of no data. Outer Wilds is the canonical user. Mods that don't instantiate it just hold.

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

### Chord Alternatives (keyboards without a nav cluster)

Every mod must also register `Ctrl+Shift+<letter>` chords from the **T/Y/U/G/H/J** cluster (a 2x3 block mid-keyboard, easy to find by touch). `Ctrl+Shift+<letter>` is universally avoided by games (Ctrl is crouch/interact, Shift is sprint/weapon-wheel; both together sits outside any in-game bind set), so it works reliably across the catalogue.

| Action | Chord | Position in cluster |
|--------|-------|---------------------|
| Recenter | `Ctrl+Shift+T` | top-left |
| Toggle tracking | `Ctrl+Shift+Y` | top-middle |
| Toggle position | `Ctrl+Shift+G` | bottom-left |
| (4th toggle, if needed) | `Ctrl+Shift+H` | bottom-middle |
| (5th toggle, if needed) | `Ctrl+Shift+U` | top-right |
| (6th toggle, if needed) | `Ctrl+Shift+J` | bottom-right |

Assign in the order above so the same action lands on the same chord across every mod. Fewer toggles - drop the unused ones, never reshuffle. If a game does bind one of these (very rare), document the conflict in the README and pick the next letter from the same cluster - don't switch modifiers or scatter letters.

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
| Position Sensitivity X / Y / Z | 1.0 | 0.0–5.0 | Override only when physically necessary (e.g. RE:Requiem uses 2.0 because native head-bob range is small) |
| Position Limit X | 0.30 m | 0.01–0.5 | Symmetric (`LimitX`, applied as `±LimitX`) |
| Position Limit Y | 0.20 m | 0.01–0.5 | Symmetric (`LimitY`, applied as `±LimitY`) |
| Position Limit Z (forward) | 0.40 m | 0.01–0.5 | `LimitZ` |
| Position Limit Z (back) | 0.10 m | 0.01–0.5 | `LimitZBack`. Asymmetric, prevents clipping through player |
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

**BepInEx 5:** install.cmd auto-downloads; config at `BepInEx/config/<GUID>.cfg`; DLLs flat in `plugins/` (except Valheim: subfolder). `.headtracking-state.json` tracks whether we installed BepInEx so uninstall only removes it if we put it there. Architecture x64 (x86 for Obra Dinn / Unity 2017).

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

### Release ZIPs

**Installer (GitHub Release):** `<ModName>-v<version>-installer.zip` - install.cmd, uninstall.cmd, plugins/ (or mod/), vendor/<loader-slug>/ (loader zip + LICENSE + README.md), README/LICENSE/CHANGELOG/THIRD-PARTY-NOTICES.

**Nexus (extract to game folder):** `<ModName>-v<version>-nexus.zip` - only the deploy-path subtree containing the DLLs. No vendored loader (Nexus users manage their own).

**MUST NOT be in release ZIPs:** `pixi.toml`, `modules/`, `bin/`, `obj/`, `libs/`, `.claude/`, `.pixi/`, any `.ps1`/`.bat`. Vendor dirs ship the loader zip + LICENSE + README.md only - no scripts.

### Shared Packager

`cameraunlock-core/scripts/package-bepinex-mod.ps1`: params `-ModName`, `-CsprojPath`, `-BuildOutputDir`, `-ModDlls`, `-ProjectRoot`, `-CreateNexusZip`. Output: `release/`.

### install.cmd / uninstall.cmd - Unified Launcher Contract

**Every mod in scope** (bioshock-remastered, dying-light-2, gone-home, green-hell, obra-dinn, peak, resident-evil-requiem, subnautica - and every new BepInEx/MelonLoader/ASI/REFramework/Cecil/shim mod) **ships install.cmd and uninstall.cmd with identical CLI semantics.** The CameraUnlock launcher drives them programmatically; the contract below is what the launcher relies on and what a human running the `.cmd` by hand also gets. **Outer Wilds is the one exception** - it ships through OWML's installer ecosystem and does not participate.

#### CLI surface

```
install.cmd   [GAME_PATH] [/y]
uninstall.cmd [GAME_PATH] [/y] [/force]
```

- **GAME_PATH** (optional, positional): explicit game install root, wins over all auto-detection. Must be an existing directory or the script errors out.
- **`/y`** (aliases `-y`, `--yes`, `/Y`): non-interactive - skip every `pause`, prompt, and "type install to continue" gate. Error-exit paths still print the diagnostic, just without pausing. The launcher always passes `/y`; manual users rarely do.
- **`/force`** (uninstall only; aliases `--force`, `/Force`): remove the loader/framework even if the state file says `installed_by_us: false`. Never touches anything outside the loader's own directories.

**Parsing is order-independent and case-insensitive.** The first positional arg resolving to an existing directory is `GAME_PATH`; everything else must be a recognised flag. Unknown flags are a hard error (exit 2) - fail fast, don't silently ignore. (The legacy positional `UNATTENDED` arg-2 pattern is removed; `/y` replaces it.)

#### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | User-fixable error (game not found, game running, vendored loader missing, extraction failed, loader init gate declined) |
| 2 | Unknown or malformed argument |

The launcher surfaces the last ~20 lines of stderr on any non-zero code.

#### Install flow (canonical)

1. Parse args.
2. Resolve `GAME_PATH`: explicit arg wins; otherwise call `find-game.ps1` shim reading `cameraunlock-core/data/games.json` by `GAME_ID`.
3. Check game isn't running. Exit 1 if it is.
4. Check loader presence via the canonical marker file (see Loader inventory). If absent:
   - Extract `vendor/<loader-slug>/<loader>.zip` (committed copy) directly to the correct location. install.cmd never hits the network (see Vendoring).
   - If the vendored zip is missing, exit 1 with `"installer ZIP is corrupt, re-download"`.
   - Set `framework.installed_by_us = true`.
5. If loader was already present: log `"Existing <Loader> detected, skipping loader install, deploying plugin only."` and set `installed_by_us = false` - **but preserve `true` if the existing state file already says `true`** (updating a mod we installed; don't demote the flag).
6. Loader init gate:
   - `/y` mode: print `"Loader installed. It will initialize on first game launch."` and continue (BepInEx/MelonLoader/REFramework all self-init on first launch).
   - Interactive, loader was absent: show the "run the game once, then type install to continue" gate.
   - Interactive, loader already present: no gate.
7. Deploy mod files to the deploy path (framework-dependent - see inventory).
8. Write updated `.headtracking-state.json`.
9. Final `pause` only if `/y` not set.

#### Uninstall flow (canonical)

1. Parse args.
2. Resolve `GAME_PATH`.
3. Check game isn't running.
4. Remove mod files in `MOD_DLLS` + any `LEGACY_DLLS` carried for backwards-compat cleanup.
5. Decide loader removal:
   - `/force` → remove unconditionally.
   - Else read `.headtracking-state.json`: `installed_by_us: true` → remove loader; `false` or missing → leave alone, log `"<Loader> was not installed by this mod - leaving intact. Use /force to remove anyway."`
6. Remove the state file.
7. Final `pause` only if `/y` not set.

#### State file: `.headtracking-state.json`

Lives at the **game install root** (the resolved `GAME_PATH`, not a subfolder). One file per game, shared across all CameraUnlock mods for that game - in practice one per game, so no cross-mod collisions today (if that changes, add a `mods: []` array). **It is the only attribution marker** - no sidecar files in loader directories, no registry keys. Uninstall's framework-removal decision is made solely from this file (+ `/force`).

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
- `framework.type`: `"BepInEx"`, `"MelonLoader"`, `"MonoCecil"`, `"ASILoader"`, `"REFramework"`, or `"None"` (shim-only mods like BioShock Remastered).
- `framework.installed_by_us`: `true` only if this install.cmd extracted the loader. Never regress `true → false` across re-installs.
- `framework.version`: the loader version we shipped (informational; omit when we didn't install it).
- `mod.id`: slug matching `games.json` (`subnautica`, `green-hell`, `resident-evil-requiem`, …).
- `mod.name`: the `AssemblyName` / `RootNamespace` (`SubnauticaHeadTracking`).
- `mod.version`: SemVer of the installed mod.
- `mod.installed_at`: UTC ISO-8601 timestamp.

#### Loader inventory

Each `install.cmd` dispatches to exactly one `:install_<loader>` subroutine. The marker file is what presence-check tests look for; missing = install needed.

| Framework | Mods | Vendor slug | Marker file | Loader files to remove on uninstall |
|-----------|------|-------------|-------------|-------------------------------------|
| BepInEx 5 x64 | subnautica | `bepinex` | `BepInEx/core/BepInEx.dll` | `BepInEx/`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` |
| BepInEx 5 x86 | obra-dinn | `bepinex` | `BepInEx/core/BepInEx.dll` | Same as x64 |
| BepInExPack (Thunderstore-wrapped) | peak | `bepinex` | `BepInEx/core/BepInEx.dll` | Same as x64; vendor zip extracted through a `BepInExPack_PEAK/` subfolder that must be flattened on install |
| MelonLoader | green-hell | `melonloader` | `MelonLoader/net35/MelonLoader.dll` (or `net6/`) | `MelonLoader/`, `version.dll`, `dobby.dll`, `NOTICE.txt`; `Mods/`, `UserLibs/`, `UserData/` only if empty after mod files come out |
| Mono.Cecil patcher | gone-home | `mono-cecil` | `<Managed>/Assembly-CSharp.dll.original` | Restore `.original` over `Assembly-CSharp.dll`, delete `.original`, delete `Mono.Cecil.dll` |
| Ultimate ASI Loader | dying-light-2 | `ultimate-asi-loader` | `<exe-dir>/winmm.dll` (renamed from `dinput8.dll`) | `winmm.dll` (or `dinput8.dll`), any `scripts/` stub created by the loader |
| REFramework | resident-evil-requiem | `reframework` | `dinput8.dll` + `reframework/` at game root | `dinput8.dll`, `reframework/` |
| None (shim-only) | bioshock-remastered | - | N/A (the mod DLL *is* the shim - `xinput1_3.dll`) | Just the mod DLL |

**Shim-only mods** (bioshock-remastered and any future xinput/dxgi shim): `framework.type: "None"`, `framework.installed_by_us: false`. `/force` is a no-op for framework removal - the mod DLL always comes out.

#### Template layout in cameraunlock-core

Source of truth at `cameraunlock-core/scripts/templates/`:

```
install.cmd             # BepInEx variant (default - most mods)
install-melonloader.cmd
install-cecil.cmd
install-asi.cmd
install-reframework.cmd
install-shim.cmd        # No-loader / xinput / dxgi shim mods
uninstall.cmd           # Shared across all loaders; dispatches loader-removal by reading state file
```

Per-mod `<mod>/scripts/install.cmd` and `uninstall.cmd`:
- Copy the matching template verbatim.
- Edit **only the CONFIG BLOCK** (`GAME_ID`, `MOD_DISPLAY_NAME`, `MOD_DLLS`, `MOD_INTERNAL_NAME`, `MOD_VERSION`, `MOD_CONTROLS`, plus loader-specific vars like `BEPINEX_ARCH`, `MANAGED_SUBFOLDER`, `BEPINEX_SUBFOLDER`).
- Never modify logic outside the CONFIG BLOCK. If you must, fix the template and re-sync all mods.
- Run `unix2dos` after editing (see Hard Rules - the #1 regression source).

#### Canonical arg-parser block

Every template begins with this immediately after the CONFIG BLOCK, before `call :main`:

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

`install.cmd` ignores `FORCE_FLAG`; `uninstall.cmd` uses it. Both honour `YES_FLAG`. Wrap every interactive stop (`if not defined YES_FLAG pause`) and the loader-init gate:

```cmd
if defined YES_FLAG (
    echo Loader installed. It will initialize on first game launch.
) else (
    call :prompt_loader_init
)
```

**Never call `pause` unconditionally.**

#### Launcher expectations

The launcher invokes:

```
install.cmd   "<detected-game-path>" /y
uninstall.cmd "<detected-game-path>" /y
uninstall.cmd "<detected-game-path>" /y /force     # deep uninstall
```

and relies on:
- Zero stdin reads (no prompts, no `set /p`, no `pause`).
- Exit 0 = success; anything else = surface stderr tail.
- State file present after install, absent after uninstall.
- No dialog boxes, no `color` flashes beyond plain console output.
- Idempotent re-runs: installing twice is a no-op on framework (only redeploys DLLs); uninstalling twice is a no-op after the first.

Any deviation is a launcher-breaking bug; fix the template, not the launcher.

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

Each mod has its own `.github/workflows/build.yml` and `release.yml` in its own repo. **GitHub Actions resolves workflows from each repo's `.github/workflows/` path, so these YAMLs cannot be symlinked from `cameraunlock-core`.** One reusable workflow exists for new BepInEx mods - `itsloopyo/cameraunlock-core/.github/workflows/release-bepinex-mod.yml` (used by obra-dinn, peak) - but `build.yml` is always inline.

**`build.yml` triggers:**
- `push` on **any branch** (`branches: ['**']`) so feature branches produce downloadable artifacts for pre-release testing.
- `pull_request` targeting `main`.
- `paths-ignore` drops pure docs/LICENSE changes.
- `if: ${{ !startsWith(github.event.head_commit.message, 'Release v') }}` on the job to skip double-building when `release.ps1` lands its version-bump commit (`release.yml` handles that).
- Tag pushes (`v*.*.*`) are handled exclusively by `release.yml`. Do not widen `build.yml`'s `push:` to include tags.
- Do not add `schedule:` or `workflow_dispatch:`. Per-push artifacts with 14-day retention are the agreed cadence; converting to cron-nightlies or manual dispatch is a separate decision.

**`build.yml` must produce a usable install artifact.** After "Verify build outputs", run the packaging script and upload the installer ZIP. **A `build.yml` that lints, builds, and verifies but does not upload an artifact is incomplete.** Before committing any `build.yml` change (or authoring one), grep for `upload-artifact` and confirm it's present - the most common drift.

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

- name: Compute artifact name from ref
  shell: pwsh
  run: |
    $branch = if ($env:GITHUB_HEAD_REF) { $env:GITHUB_HEAD_REF } else { $env:GITHUB_REF_NAME }
    $sanitized = $branch -replace '[^A-Za-z0-9._-]', '-'
    "ARTIFACT_NAME=<ModName>HeadTracking-$sanitized" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8

- name: Upload installer artifact
  uses: actions/upload-artifact@v7
  with:
    name: ${{ env.ARTIFACT_NAME }}
    path: release/artifact-contents/
    retention-days: 14
    if-no-files-found: error
```

For pixi-driven mods (currently bioshock-remastered), replace the Package step with `run: pixi run package`. The rest is identical.

Conventions:
- Pin Node-24-native majors on JS actions: `actions/checkout@v6`, `actions/upload-artifact@v7`, `microsoft/setup-msbuild@v3`. The earlier v4/v4/v2 trio was Node-20-based (Node 20 forced off June 2026, removed Sept 2026). Bump majors when newer ones ship.
- **Artifact name is `<ModName>HeadTracking-<sanitized-branch>`**, not a flat `-installer` suffix. The "Compute artifact name from ref" step takes `GITHUB_HEAD_REF` (PRs) or `GITHUB_REF_NAME` (push), replaces any char outside `[A-Za-z0-9._-]` with `-`, exports `ARTIFACT_NAME`. So `main` yields `<ModName>HeadTracking-main`; `fix/seamoth` yields `<ModName>HeadTracking-fix-seamoth`. Lets multiple in-flight branches publish without clobbering each other.
- The upload `path` is `release/artifact-contents/` (a staging dir from the preceding "Stage installer contents for artifact upload" step that extracts `release/*-installer.zip`), so downloaders get the installer tree directly, not a zip-inside-a-zip.
- `retention-days: 14` is plenty for test-branch iteration.
- `if-no-files-found: error` catches packaging failures that don't exit nonzero.

End-to-end: branch off `main` → push → workflow completes → download the `<ModName>HeadTracking-<branch>` artifact from the Actions run page → hand the unpacked tree to the user to run `install.cmd`. Only after they confirm, cut a real release via `release.ps1`.

**lopari catalog coupling.** When a mod is added to `lopari/catalog/mods.json`, the `nightly.artifact` field must match the artifact name the workflow uploads. For the default branch that is `<ModName>HeadTracking-main`. Older catalog entries (subnautica, DL2, peak, RE4, skyrim-special-edition, painscreek, RE9, bioshock-remastered) still carry the legacy `-installer` suffix from before per-branch naming; don't copy those. Cross-check against a recent entry (cyberpunk-2077, easy-delivery-co, fallout-new-vegas, black-and-white) which uses the current `-main` form.

**`release.yml` triggers:** push of `v*.*.*` tags only. Does its own submodule-recursive checkout, full Release build, `scripts/package-release.ps1`, release-notes via `generate-release-notes.ps1`, and `gh release create` with installer + nexus ZIPs attached.

**CI packaging is offline.** `scripts/package-release.ps1` consumes whatever is committed under `vendor/`; it never hits the network. Bumping vendored loaders is a manual `pixi run update-deps` + commit step done locally before tagging (see Vendoring).

### Dev build channel (rolling GitHub pre-release)

Separate from versioned Releases, every mod's `pixi run release` accepts `nightly` (`pixi run release nightly`), publishing a dev build as a **rolling GitHub pre-release tagged `dev`** on the mod's own repo. It sits alongside `release major | minor | patch | X.Y.Z` - same task, one more subcommand. This is the distribution channel for pre-release/dev builds, surfaced to users (including via lopari one-click install) as bleeding-edge "in-progress" builds. A mod with stable versioned releases can also publish a `dev` build off its latest commit.

Dev builds are free and open to everyone - one-click install is a convenience, not gated access; anyone can grab the same asset from the repo's Releases page. No R2 bucket, no Patreon broker, no presigned URLs, no separate storage. (Earlier doctrine described a private-R2 + Patreon-broker model and a `patreon-nightly` catalog type; both are gone, replaced by what's described here.)

**Doctrine: dev publishing is a deliberate author action, not a CI side-effect.** `release nightly` runs from the author's machine only. Per-push CI builds (`build.yml`) keep producing GitHub Actions artifacts, but those are for the author and for handing test builds to specific users - they are NOT the `dev` pre-release.

**`pixi.toml` wiring:** no new task. The existing `release` task takes a positional arg; `nightly` becomes one more accepted value. `scripts/release.ps1` short-circuits to `release-nightly.ps1` on the `nightly` argument (mandatory in every mod's `release.ps1`):

```powershell
if ($Version -eq 'nightly') {
    & (Join-Path $PSScriptRoot 'release-nightly.ps1')
    exit $LASTEXITCODE
}
```

Update the usage line too: `Usage: pixi run release <major|minor|patch|nightly|X.Y.Z>`.

**Required at `scripts/release-nightly.ps1` (thin shim, mod-specific bits only):**

```powershell
[CmdletBinding()]
param([switch]$AllowDirty)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Import-Module (Join-Path $ProjectRoot 'cameraunlock-core\powershell\NightlyRelease.psm1') -Force

# Extract $version - source depends on the mod:
#   C++ mods: Select-String on CMakeLists.txt / src\core\constants.h for the version.
#   C# mods:  Select-String on the .csproj for <Version>...</Version>.

Publish-NightlyBuild `
    -ModId '<slug-matching-lopari-catalog-id>' `
    -ModName '<PascalCase-AssemblyName>' `
    -Version $version `
    -ProjectRoot $ProjectRoot `
    -BuildCommand 'pixi run build' `      # omit to default to 'pixi run build-release'
    -AllowDirty:$AllowDirty
```

All build/package/hash/publish logic lives in `cameraunlock-core/powershell/NightlyRelease.psm1`. Never duplicate per-mod - if a mod must deviate, extend the module's parameters and update every shim. Override knobs: `-BuildCommand` / `-PackageCommand`, `-InstallerZipPath`; `-DevTag` defaults to `dev`.

**Auth:** requires GitHub CLI (`gh`) on PATH, authenticated to create releases on the repo (locally `gh auth login`; in CI a `GITHUB_TOKEN` with `contents: write`). No cloud credentials, no `~/.lopari/r2.ps1`, no AWS CLI.

**What `release-nightly` does:**

1. Aborts on a dirty tree unless `-AllowDirty`.
2. Verifies HEAD is on a remote branch (the pre-release tags this exact commit) - fails fast with "push your commit first" if not.
3. Runs the build command (default `pixi run build-release`, override `-BuildCommand`) then the package command (default `pixi run package`).
4. Stamps a dev version string `<version>-nightly.<utc-date>.<git-short-sha>` for the release title/notes.
5. Copies the packager's `<ModName>-v<version>-installer.zip` to the fixed asset name `<ModName>-dev-installer.zip` (stable download URL) and SHA-256s it.
6. Deletes the existing `dev` pre-release and tag (best-effort), then recreates the `dev` pre-release at HEAD with the fresh asset.

Result: a single rolling `dev` pre-release per repo, always pointing at the newest commit's build, with stable URLs:

```
https://github.com/<owner>/<repo>/releases/tag/dev
https://github.com/<owner>/<repo>/releases/download/dev/<ModName>-dev-installer.zip
```

**Catalog wiring (`lopari/catalog/mods.json`):** a mod whose dev builds come from the rolling `dev` pre-release gets a `distribution` block:

```json
"distribution": {
  "type": "dev-release"
}
```

`type` is `dev-release` (the obsolete `patreon-nightly` is gone). The launcher resolves the install from the mod's `dev` GitHub pre-release, keyed off the catalog `repo`. An optional `pinned` sub-object (version / built_at / zip_filename / download_url) is stamped later by lopari.app's update-metadata task so subscribers skip the live GitHub releases API call - the author does not hand-write it, and any pinned `download_url` must be on `github.com`.

This is **independent of** the `nightly.artifact` field, which points lopari at per-push GitHub Actions artifacts (14-day retention, served via nightly.link) rather than the permanent pre-release. A mod may carry both; the launcher prefers `distribution` for the install action. Use `distribution: dev-release` for anything published via `release nightly`.

**`release nightly` is NOT for:**
- Production releases with stable version numbers - those go through `release.ps1` and `gh release create`, attached to a versioned public Release.
- One-off test builds for a specific user - hand them the `build.yml` GitHub Actions artifact instead. The `dev` pre-release is the public bleeding-edge build; replacing it reaches everyone tracking dev.

---

## Vendoring Third-Party Dependencies

**Doctrine: vendored is the install-time source of truth; install.cmd never reaches the network for a mod loader.** A breaking upstream release (asset renamed/removed, repo moved, rate-limited) cannot break our installer because the installer doesn't talk to upstream. The committed `vendor/<loader-slug>/` tree is what ships in the release ZIP and what `install.cmd` extracts.

This is a deliberate flip from the earlier "fetch latest, fall back to vendored" pattern, which broke a real install when a REFramework nightly stopped publishing `RE9.zip`. The dev decides when to bump, via `pixi run update-deps` (manual) + commit. The narrow exception is loaders with licenses too restrictive to vendor (none today): keep an upstream-fetch path in install.cmd and document it in the README.

### The pattern

1. **`pixi run update-deps`** (dev, manual): walks `vendor/<loader-slug>/` and rewrites each from the latest upstream release within a pinned version range. Writes `<loader>.zip`, `LICENSE`, `README.md`. Dev reviews the diff (`git diff --stat`, README tag/SHA) and commits.
2. **`pixi run build` / `package` / `release`**: never touch the network; consume whatever is committed under `vendor/`. Builds and CI are deterministic - the release ZIP carries exactly the vendored bytes on disk at commit time.
3. **`install.cmd`** at user runtime: extracts `vendor/<loader-slug>/<loader>.zip` directly. Hard-errors `"The installer ZIP is corrupt. Re-download the release."` if missing. No upstream fetch, no fallback chain.
4. **Existence short-circuit stays:** `if not exist "%GAME_PATH%\BepInEx\core\BepInEx.dll"` (and equivalents) wins before any extraction. User-installed loaders are left alone; state file records `installed_by_us: false`.

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

`fetch-latest.ps1` inside `vendor/<loader>/` is gone (only the old install-time fetch path needed it). Don't re-add it.

### Required pixi.toml wiring

```toml
update-deps = "powershell -ExecutionPolicy Bypass -File scripts/update-deps.ps1"
```

`build` does **not** depend on `update-deps` - bumping vendored copies is a deliberate dev action with a commit, not a per-build side-effect. CI never refreshes. Vendored zips are committed to git (not LFS - small). They ship inside the GitHub installer ZIP alongside `install.cmd`, NOT in the Nexus ZIP (Nexus users manage their own loader).

### Version-range pinning (mandatory)

Every loader has a hardcoded version-range prefix passed to `Refresh-VendoredLoader` from `update-deps.ps1`. This bounds surprise: a breaking `v6.0.0` upstream tag cannot silently upgrade users via a routine `update-deps`.

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

Bumping a major (say BepInEx 5.4 → 6) is a conscious per-mod change: update the prefix in `update-deps.ps1`, re-run, re-test, commit.

### Shared helpers (`cameraunlock-core/powershell/ModLoaderSetup.psm1`)

- **`Refresh-VendoredLoader`**: the only function `update-deps.ps1` should call. GitHub API query, filter by tag prefix + prerelease flag, download matching asset, write LICENSE + README.md + zip into `vendor/<name>/`. Direct-URL mode for non-GitHub sources (Thunderstore).
- **`Invoke-FetchLatestLoader`**: lower-level building block called by `Refresh-VendoredLoader`. Don't call directly from mods.

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

No upstream fetch, no `USED_UPSTREAM` flag, no `LOADER_SOURCE` indirection. When the outer `if not exist "<loader marker>"` short-circuits (user already has the loader), still log:

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

Use `-OutputFileName` to pin the on-disk filename so install.cmd can hardcode it; without it the upstream asset filename leaks through and renames break installs.

### Ethics and license constraints

Only permissively-licensed loaders may be vendored:
- **MIT / Apache-2.0 / BSD / ISC**: attribution only. Ship upstream LICENSE; done.
- **LGPL-2.1** (BepInEx): attribution + source availability. Keep upstream LICENSE in the vendor dir and link to their repo in THIRD-PARTY-NOTICES so users can obtain source per LGPL §6. Don't modify the binary. Dynamic linking (what Harmony does) does not relicense our code.
- **GPL-3 / AGPL / no-license**: not vendorable. install.cmd keeps an upstream-fetch path; document the exception in the README and accept the install-time fragility.

Every mod's `THIRD-PARTY-NOTICES.md` must list each vendored component with name, version, SPDX license identifier, upstream URL, and the phrase `"bundled in the release ZIP and used as the install-time source."`

### Out of scope

- Nexus ZIP path: Nexus users manage their own loader. Vendoring applies only to the GitHub installer ZIP.
- Game-side binary deps statically linked into our DLLs (HarmonyX, Newtonsoft.Json): already attributed via THIRD-PARTY-NOTICES; no `vendor/` entry needed.

---

## Documentation Standards

- **README.md** sections in order: title + one-liner · features · requirements · installation · manual installation (optional) · OpenTrack setup · phone app setup · controls (nav cluster + chord alternatives) · configuration · troubleshooting · updating/uninstalling · building from source · license · credits (game studio, frameworks, OpenTrack).
- **CHANGELOG.md** auto-generated by `release.ps1` from commits. `## [X.Y.Z] - YYYY-MM-DD`, categories Added/Changed/Fixed/Removed. Conventional prefixes (`feat:`, `fix:`, `perf:`, `chore:`) auto-categorize. First release hand-written.
- **LICENSE** MIT, copyright `itsloopyo / CameraUnlock`.
- **THIRD-PARTY-NOTICES.md** (when applicable): BepInEx (LGPL-2.1), HarmonyX/Lib.Harmony (MIT), MelonLoader (Apache-2.0), OpenTrack (ISC), Mono.Cecil (MIT), ImGui (MIT), MinHook (BSD-2-Clause). Only list what's actually bundled or loaded at install time.

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

Existing mods have inconsistent GUIDs (`com.headtracking.obradinn`, `com.<game>.headtracking`). New mods use the standard above. Don't rename existing GUIDs - it breaks user configs.

---

## Reflection Best Practices

- **Cache everything.** Look up `Type` / `FieldInfo` / `PropertyInfo` / `MethodInfo` once into static fields.
- **`GameTypeResolver` pattern** - lazy-initialized cached lookups per game type.
- **Find via `AppDomain.CurrentDomain.GetAssemblies()` + `asm.GetType(name)`.** Don't hardcode assembly names.
- **Graceful degradation** - if a type isn't found, log once and disable the feature; don't crash.
- **Null checks (Mono compat):** `ReferenceEquals(x, null)` for plain .NET; `x == null` for Unity objects (catches destroyed-but-not-GC'd). Never `is null` pattern on Unity objects.
- **Harmony `FastFieldRef`** for hot-path field access.

---

## Performance

- **Cache `Camera.main`** - it calls `FindGameObjectWithTag` internally. Cache per-frame or per-30-frames.
- **Rate-limit game state detection** to 0.1s or 30 frames.
- **No allocations in hot paths** (OnPreCull/LateUpdate/Update).
- **Multi-camera dedup** - games call `Camera.onPreCull` multiple times per frame (shadows, reflections, secondary cams). Use `PerFrameCache` (`Core.Unity/Utilities`) which keys on `Time.frameCount` so first-call-per-frame wins; later calls reuse the cached result.
- **Lock-free receiver** - `OpenTrackReceiver` uses `volatile` reads on `_rotationPitch/_rotationYaw/_rotationRoll/_isRemoteConnection` on the hot path.

---

## Test Checklist (before release)

1. Head rotation moves view; mouse still aims.
2. Crosshair/reticle stays on the aim point; weapons fire where the reticle points. Run the reticle litmus tests (Reticle Compensation) - single-axis checks pass even when combined poses (pitch+roll, world-yaw while looking down) drift.
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

For finding the camera in a new C++ engine, see the `port-camera-to-cpp-engine` skill.

---

## UE5 (Unreal Engine 5) C++ Hook Notes

### FVector / FRotator are doubles, not floats

UE 5.0+ ships with Large World Coordinates (LWC) on by default. The canonical `FVector` is `FVector3d` (3 doubles, 24 bytes) and `FRotator` is `FRotator3d` (3 doubles, 24 bytes). The float versions (`FVector3f`, `FRotator3f`) only appear where engine code explicitly opts in.

For any UE5 C++ mod hooking a function that takes `FVector*` / `FRotator*` out-params (or any struct containing them, e.g. `FMinimalViewInfo`), declare the structs as doubles:

```cpp
struct FVector  { double X, Y, Z; };       // 24 bytes
struct FRotator { double Pitch, Yaw, Roll; }; // 24 bytes
```

**Why:** declaring them as floats (12 bytes) causes two problems on every call:

1. The engine writes 24 bytes into the 12-byte buffer, overflowing 12 bytes of adjacent stack. Silent memory corruption.
2. The bytes that *do* land get read as the wrong fields. One field (typically Yaw) lands on the high half of an adjacent double and decodes as a plausible-looking small float; the others decode to ~1e25-1e27 garbage. Add tracker delta and the camera spins wildly when those values mod 360.

**Symptom:** rotation/position values in the 1e10-1e27 range, or values that change shape per-call by orders of magnitude. If only one of three axes looks sane and the others are huge, it's almost always this.

Confirmed in Subnautica 2 (UE 5.6.1). Applies to every future UE5 C++ mod unless the build disabled LWC (rare). Verify by dumping `loc_post` from a known position (e.g. spawn) - if Y and Z look sensible in meters and X is ~1e12, the struct is wrong.

### `GetPlayerViewPoint` is the wrong hook target

Even with correct structs: `APlayerController::GetPlayerViewPoint` is read by weapons, AI sight checks, projectile spawning, raycasts. Modifying it violates "zero impact on game logic". Use the pre/post + render-phase pattern ("C++ Pre/Post Hook") and inject into the view-matrix path, not the game-query path.


<!-- agent: Maintain compatibility across new patches -->
# Maintain compatibility across new patches

Game patches break RVAs. Users update on their own schedule. Our mods must
keep working for everyone regardless of which patched build they are running.

## The principle: append-only build profiles

Every mod that pins behavior to RVAs ships a *registry* of build profiles.
Each profile is a tuple `(name, PE fingerprint, offsets)` where the
fingerprint (TimeDateStamp + SizeOfImage + CheckSum) uniquely identifies a
specific shipped build. At load time, the mod fingerprints the running EXE
and looks up the matching profile; no match leaves the mod dormant via the
existing fingerprint failsafe.

When a patch lands and breaks the current build, the response is to **add**
a new profile, not edit the existing one. The user on the un-patched build
keeps matching their old profile by fingerprint; the user who has updated
matches the new profile. Both work simultaneously from the same mod
binary.

## What never to do

- **Never edit an existing profile's RVAs in place.** That breaks the mod
  for every user who has not yet taken the patch. They are stranded with no
  fix: their installed mod version stops working, the newer mod version
  does not know about their build either.
- **Never delete old profiles when adding new ones.** They cost about 1 KB
  each. Keep them forever.
- **Never reorder existing profiles in a way that loses information.** The
  registry is append-mostly; the only re-ordering that makes sense is
  putting the newest profile first for diagnostic-primary purposes (see
  below).

## What to do

When a patch breaks RVAs:

1. Derive the new build's RVAs (Ghidra discovery, runtime caller-capture,
   whatever the mod's discovery workflow requires).
2. Add a new `extern const BuildProfile kStoreProfile_YYYYMMDD = { ... };`
   in the relevant `<store>_offsets.cpp` file. Date suffix is the build's
   release date or close approximation - the PE fingerprint is the
   authoritative routing key, the date is for human readability only.
3. Add the new profile to the **top** of the `kKnownProfiles` array in the
   build registry. The top-of-array entry is the "diagnostic primary": when
   no profile matches at all, the user-facing log line ("game is newer than
   any known build") compares the running EXE against this primary's
   fingerprint to label whether the user is on a newer or older build. It
   should always be the most recent build the mod knows about.
4. Leave every prior profile in place, in its original position below the
   new entry.
5. CHANGELOG entry lists what changed in the new patched build (a one-line
   note is enough; the diff itself is in the new profile's RVAs).

## Naming convention

- File names: one file per store, never per-build. e.g. `steam_offsets.cpp`
  holds every Steam profile, `gdk_offsets.cpp` holds every GDK profile.
- Profile constants: `kStoreProfile_YYYYMMDD` (e.g. `kSteamProfile_20260522`,
  `kGdkProfile_20260524`). Append-only inside each file.
- Profile `.Name` field: same `store-platform-YYYYMMDD` form
  (e.g. `"steam-win64-20260522"`). Surfaces in the user-facing log line so
  the user (and we, when triaging) can see exactly which profile activated.

## Why fingerprint, not version string

The mod has to identify the build from inside the running process. Steam
version strings, Xbox package versions, and store metadata are not visible
from process memory in a reliable, sandbox-respecting way. The PE header
of the running EXE is. TimeDateStamp + SizeOfImage + CheckSum together are
unique per built EXE (collisions would require a relink that produced an
identical binary, which never happens in practice). Three independent
fields means a tampered/repacked EXE fails the match instead of silently
mis-routing.

## When the user's profile is unrecognised

The diagnostic primary (top entry of `kKnownProfiles`) labels the mismatch
direction:
- Running TS > primary TS → "game is newer than this mod knows about; check
  the releases page for an updated mod".
- Running TS < primary TS → "game is older; let the store finish updating".
- Same TS, different size/checksum → "tampered/repacked EXE; this mod won't
  engage on a modified binary".

All three cases leave the mod fully dormant (no hooks installed, no
process modification). The game runs vanilla. The failsafe is mandatory:
hooking against stale RVAs crashes the user's game seconds in.

## Costs

- ~1 KB per profile in the binary.
- One extra fingerprint comparison per known profile at process start. With
  a few dozen profiles that's still microseconds.
- The registry file grows over the lifetime of the mod. That is intended.

## Out of scope for this doc

How each profile's RVAs are *derived* is per-mod and per-engine (UE5
Ghidra workflow vs Unity's IL2CPP dumper vs Mono Cecil reflection
vs ...). What this doc enforces is that the *registry shape* and the
*append-only policy* are the same across every mod we ship.
