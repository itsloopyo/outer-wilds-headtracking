using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches ReferenceFrameTracker to use BASE rotation (reticle direction) for targeting raycasts.
    /// Without this patch, LOOK markers flicker because the raycast uses HEAD rotation,
    /// causing targets to be found/lost as the player moves their head.
    /// </summary>
    public static class ReferenceFrameTrackerPatch
    {
        private static readonly RotationPatchHelper _helper = new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);

        public static void ApplyPatches(Harmony harmony)
        {
            var trackerType = AccessTools.TypeByName("ReferenceFrameTracker");
            if (trackerType == null)
                throw new InvalidOperationException("Could not find ReferenceFrameTracker type!");

            var findInLineOfSightMethod = AccessTools.Method(trackerType, "FindReferenceFrameInLineOfSight");
            if (findInLineOfSightMethod != null)
            {
                harmony.Patch(findInLineOfSightMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(Prefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(Postfix))));
            }

            var findInMapViewMethod = AccessTools.Method(trackerType, "FindReferenceFrameInMapView");
            if (findInMapViewMethod != null)
            {
                harmony.Patch(findInMapViewMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(Prefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(Postfix))));
            }
        }

        public static void Prefix() => _helper.BeginPatch();
        public static void Postfix() => _helper.EndPatch();
    }
}
