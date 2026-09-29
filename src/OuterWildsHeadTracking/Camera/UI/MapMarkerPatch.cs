extern alias UnityCoreModule;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Effects;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Applies the manual patches for types that aren't directly accessible at compile time.
    /// Canvas markers need no patch: head tracking is on the camera transform when they
    /// project through it.
    /// </summary>
    public static class MapMarkerPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            NomaiTranslatorPatches.ApplyPatches(harmony);
            SignalscopePatches.ApplyPatches(harmony);
            FlashlightPatch.ApplyPatches(harmony);
            ReferenceFrameTrackerPatch.ApplyPatches(harmony);
        }
    }
}
