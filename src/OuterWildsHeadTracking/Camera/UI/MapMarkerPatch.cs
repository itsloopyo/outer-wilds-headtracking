extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using OuterWildsHeadTracking.Camera.Effects;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Applies sub-patches for marker-adjacent systems (signalscope, flashlight, etc.)
    /// and patches OWExtensions.WorldToCanvasPosition.
    /// Rotation is auto-compensated by canvas parenting. Position is handled via
    /// view matrix in SimpleCameraPatch (no canvas movement = no marker drift).
    /// </summary>
    public static class MapMarkerPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            NomaiTranslatorPatches.ApplyPatches(harmony);
            SignalscopePatches.ApplyPatches(harmony);
            QuantumVisibilityPatch.ApplyPatches(harmony);
            FlashlightPatch.ApplyPatches(harmony);
            ReferenceFrameTrackerPatch.ApplyPatches(harmony);
            CanvasMarkerPatch.ApplyPatches(harmony);
        }
    }
}
