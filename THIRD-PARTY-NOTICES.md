# Third-Party Notices

This project depends on the following third-party software.

---

## OWML (Outer Wilds Mod Loader)

- **License:** MIT
- **Source:** https://github.com/amazingalek/owml
- **Usage:** Mod loader for Outer Wilds. OWML is installed and managed by the Outer Wilds Mod Manager (https://github.com/Bwc9876/OuterWildsModManager); this mod does not bundle, download, or modify OWML. Users obtain OWML by installing any mod via the Outer Wilds Mod Manager UI. Not modified.

## HarmonyX / Lib.Harmony

- **License:** MIT
- **Author:** Andreas Pardeike / BepInEx contributors
- **Source:** https://github.com/BepInEx/HarmonyX
- **Usage:** Runtime method patching, shipped inside OWML.

## OpenTrack

- **License:** ISC
- **Source:** https://github.com/opentrack/opentrack
- **Usage:** UDP tracking protocol only. No OpenTrack code is bundled.

---

## Note on the vendoring pattern

Unlike other mods in the CameraUnlock monorepo, this mod does not bundle its mod loader in the release ZIP. Outer Wilds users install mods through the Outer Wilds Mod Manager, which installs OWML as a prerequisite on their behalf. Our release ZIP is an extract-to-OWML-Mods-folder payload with only this mod's DLLs, consistent with the OWML ecosystem's conventions.

---

## Outer Wilds

Outer Wilds is the property of Mobius Digital / Annapurna Interactive. This mod is a fan project and is not affiliated with or endorsed by them. Purchase Outer Wilds at https://store.steampowered.com/app/753640/Outer_Wilds/.
